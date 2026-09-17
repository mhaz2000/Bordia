using Azul.Models;

namespace Azul;

/// <summary>
/// The three player action processors and the two system actions, plus the
/// shared placement mechanics (draft → one pattern line → floor → discard).
/// Every player action validates its complete decision set against the
/// authoritative state before mutating anything; the round-end tiling decision
/// and turn advance live in <c>AzulGame.ProcessAction</c> (spec §8, §13, §19).
/// </summary>
public sealed partial class AzulGame
{
    private const int InvalidColor = -1;

    // ----- drafting -----

    private static AzulActionResult ProcessDraftFromFactory(
        AzulState azul, int seat, DraftFromFactoryPayload? payload)
    {
        if (payload is null || !TryReadColor(payload.Color, out var color))
        {
            return FailResult(Errors.InvalidPayload);
        }

        if (payload.FactoryIndex < 0 || payload.FactoryIndex >= azul.FactoryCount)
        {
            return FailResult(Errors.InvalidFactory, new object?[] { payload.FactoryIndex });
        }

        if (payload.LineIndex < AzulFloorSentinel.Value || payload.LineIndex >= AzulCatalog.LineCount)
        {
            return FailResult(Errors.InvalidPayload);
        }

        var factory = azul.Factories[payload.FactoryIndex];
        var count = factory.Count(t => t == color);
        if (count == 0)
        {
            return FailResult(Errors.NoTilesOfColor, new object?[] { AzulCatalog.ColorName(color) });
        }

        var seatState = azul.Seats[seat];
        var lineError = ValidateLine(seatState, color, payload.LineIndex);
        if (lineError is not null)
        {
            return FailResult(lineError.Value.Code, lineError.Value.Args);
        }

        // Draft the color; the factory's remaining (other-color) tiles go to the center.
        var result = Ok();
        factory.RemoveAll(t => t == color);
        var leftovers = factory.Count;
        if (leftovers > 0)
        {
            azul.Center.AddRange(factory);
            factory.Clear();
            result.Events.Add(AzulEvent.Build("azul.tilesMovedToCenter", new
            {
                count = leftovers,
                factory = payload.FactoryIndex
            }));
        }

        result.Events.Add(AzulEvent.Build("azul.tileDrafted", new
        {
            player = NameOf(azul, seat),
            count,
            color = AzulCatalog.ColorName(color),
            factory = payload.FactoryIndex
        }));

        PlaceDraft(azul, seat, color, count, payload.LineIndex, result.Events);

        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    private static AzulActionResult ProcessDraftFromCenter(
        AzulState azul, int seat, DraftFromCenterPayload? payload)
    {
        if (payload is null
            || payload.LineIndex < AzulFloorSentinel.Value || payload.LineIndex >= AzulCatalog.LineCount
            || !TryReadColor(payload.Color, out var color))
        {
            return FailResult(Errors.InvalidPayload);
        }

        if (azul.Center.Count == 0)
        {
            return FailResult(Errors.CenterEmpty);
        }

        var count = azul.Center.Count(t => t == color);
        if (count == 0)
        {
            return FailResult(Errors.NoTilesOfColor, new object?[] { AzulCatalog.ColorName(color) });
        }

        var seatState = azul.Seats[seat];
        var lineError = ValidateLine(seatState, color, payload.LineIndex);
        if (lineError is not null)
        {
            return FailResult(lineError.Value.Code, lineError.Value.Args);
        }

        var result = Ok();
        azul.Center.RemoveAll(t => t == color);

        // First center draft of the round must take the first-player marker (spec §15).
        if (azul.MarkerSeat == -1)
        {
            azul.MarkerSeat = seat;
            result.Events.Add(AzulEvent.Build("azul.firstPlayerTaken", new { player = NameOf(azul, seat) }));
        }

        result.Events.Add(AzulEvent.Build("azul.tileDrafted", new
        {
            player = NameOf(azul, seat),
            count,
            color = AzulCatalog.ColorName(color),
            center = true
        }));

        PlaceDraft(azul, seat, color, count, payload.LineIndex, result.Events);

        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    private static AzulActionResult ProcessTakeFirstPlayer(AzulState azul, int seat)
    {
        if (azul.MarkerSeat != -1)
        {
            return FailResult(Errors.FirstPlayerNotAvailable);
        }

        azul.MarkerSeat = seat;
        var result = Ok();
        result.Events.Add(AzulEvent.Build("azul.firstPlayerTaken", new { player = NameOf(azul, seat) }));
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    // ----- placement mechanics -----

    /// <summary>Parses a color token: an int 0..4 (names accepted via AzulCatalog).</summary>
    private static bool TryReadColor(string? raw, out int color)
    {
        color = InvalidColor;
        return AzulCatalog.TryParseColor(raw, out color);
    }

    /// <summary>
    /// Validates a chosen line for a color (spec §8.3). Returns an error tuple when
    /// the line is illegal, or when the floor sentinel was used though a legal line
    /// exists. Null means the choice is legal.
    /// </summary>
    private static (string Code, object?[] Args)? ValidateLine(AzulSeatState seat, int color, int lineIndex)
    {
        if (lineIndex == AzulFloorSentinel.Value)
        {
            return seat.HasAnyLegalLine(color)
                ? (Errors.FloorNotOptional, Array.Empty<object?>())
                : null;
        }

        var line = seat.PatternLines[lineIndex];
        if (line.Count > 0 && seat.LineColor(lineIndex) != color)
        {
            return (Errors.LineColorMismatch, new object?[] { AzulCatalog.ColorName(color) });
        }

        if (line.Count >= AzulSeatState.LineCapacity(lineIndex))
        {
            return (Errors.LineFull, Array.Empty<object?>());
        }

        if (seat.WallRowHasColor(lineIndex, color))
        {
            return (Errors.WallRowHasColor, new object?[] { AzulCatalog.ColorName(color) });
        }

        return null;
    }

    /// <summary>
    /// Places k tiles of a color into the chosen line (or, for the sentinel, onto the
    /// floor), spilling overflow to the floor and any beyond-floor overflow to the
    /// discard pool, recording the public events (spec §8, §9, §11).
    /// </summary>
    private static void PlaceDraft(AzulState azul, int seat, int color, int count, int lineIndex, List<string> events)
    {
        var state = azul.Seats[seat];

        if (lineIndex != AzulFloorSentinel.Value)
        {
            var free = AzulSeatState.LineCapacity(lineIndex) - state.PatternLines[lineIndex].Count;
            var placed = Math.Min(count, free);
            for (var i = 0; i < placed; i++)
            {
                state.PatternLines[lineIndex].Add(color);
            }

            if (placed > 0)
            {
                events.Add(AzulEvent.Build("azul.tilesPlaced", new
                {
                    player = NameOf(azul, seat),
                    line = lineIndex,
                    count = placed,
                    color = AzulCatalog.ColorName(color)
                }));
            }

            count -= placed;
        }

        if (count > 0)
        {
            var space = AzulCatalog.FloorCapacity - state.Floor.Count;
            var inFloor = Math.Min(count, space);
            for (var i = 0; i < inFloor; i++)
            {
                state.Floor.Add(color);
            }

            var discarded = count - inFloor;
            for (var i = 0; i < discarded; i++)
            {
                azul.Discard.Add(color);
            }

            events.Add(AzulEvent.Build("azul.tilesDroppedFloor", new
            {
                player = NameOf(azul, seat),
                count = inFloor,
                color = AzulCatalog.ColorName(color)
            }));
        }
    }

    // ----- system actions -----

    private static AzulActionResult ProcessTurnTimeout(AzulState azul, int seat)
    {
        if (azul.NextActionDeadlineUtc is not { } deadline || DateTime.UtcNow <= deadline)
        {
            return FailResult(Errors.TimerNotExpired);
        }

        var config = azul.TimerConfig ?? new AzulTurnTimerConfig();
        var timer = azul.EnsureTimer(seat);
        timer.ConsecutiveTimeouts++;
        ChargeOverrun(azul, seat, config);

        var result = Ok();
        result.TurnEnded = true;
        result.IsTurnAction = false;

        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            azul.EliminatedSeats.Add(seat);
            if (azul.MarkerSeat == seat)
            {
                azul.MarkerSeat = -1;
            }

            result.Events.Add(AzulEvent.Build("azul.playerEliminated", new { player = NameOf(azul, seat) }));

            if (azul.ActiveSeats().Count() <= 1)
            {
                FinishGame(azul, result);
            }
        }

        return result;
    }

    private static AzulActionResult ProcessGameTimeExpired(AzulState azul)
    {
        if (azul.GameEndsAtUtc is not { } endsAt || DateTime.UtcNow < endsAt)
        {
            return FailResult(Errors.TimeNotUp);
        }

        var result = Ok();
        result.IsTurnAction = false;
        result.Events.Add(AzulEvent.Build("azul.gameTimeExpired", new { }));
        FinishGame(azul, result);
        return result;
    }

    /// <summary>Charges a timed-out turn's overrun against the bank, then the (capped) deferred penalty (platform timer pattern).</summary>
    private static void ChargeOverrun(AzulState azul, int seat, AzulTurnTimerConfig config)
    {
        var timer = azul.EnsureTimer(seat);
        if (azul.TurnStartUtc == default)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - azul.TurnStartUtc;
        var allotted = azul.MaxTurnSeconds(seat, config);
        var overrun = elapsed.TotalSeconds - allotted;
        if (overrun <= 0)
        {
            return;
        }

        var bankCover = Math.Min(timer.BankSeconds, overrun);
        timer.BankSeconds -= bankCover;
        timer.DeferredPenaltySeconds += Math.Min(config.MaxOverrunSeconds, overrun - bankCover);
    }
}
