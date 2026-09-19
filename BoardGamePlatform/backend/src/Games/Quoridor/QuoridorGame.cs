using System.Text.Json;
using GameEngine.Core;
using GameEngine.Core.Models;
using Quoridor.Models;

namespace Quoridor;

/// <summary>
/// Quoridor (Mirko Marchesi, Gigamic 1997 — base game) implementation of
/// the platform Game Engine contract. Contains only Quoridor rules: no HTTP, SignalR,
/// EF Core, or database dependencies. Registered in DI as an <see cref="IGame"/>.
/// </summary>
/// <remarks>
/// Quoridor has zero hidden information — all pawn positions and placed walls are
/// public. The engine does NOT implement IPlayerViewGame; the full authoritative
/// state is broadcast and served to every client (spec §24).
/// </remarks>
public sealed partial class QuoridorGame : IGame
{
    /// <summary>The game type identifier used across the platform to select this game.</summary>
    public string GameType => "Quoridor";

    /// <inheritdoc />
    public int MinPlayers => 2;

    /// <inheritdoc />
    public int MaxPlayers => 4;

    /// <inheritdoc />
    public GameState CreateGame(GameOptions options)
    {
        // Validate player count (only 2 or 4 per spec)
        if (options.Players.Count != 2 && options.Players.Count != 4)
        {
            throw new ArgumentException(Errors.PlayerCount);
        }

        var playerNames = new Dictionary<Guid, string>();
        var timerConfig = new QuoridorTimerConfig();
        int? seed = null;

        if (!string.IsNullOrWhiteSpace(options.Settings))
        {
            try
            {
                using var doc = JsonDocument.Parse(options.Settings);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("PlayerNames", out var namesElement)
                        && namesElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in namesElement.EnumerateObject())
                        {
                            if (Guid.TryParse(prop.Name, out var userId)
                                && prop.Value.ValueKind == JsonValueKind.String)
                            {
                                playerNames[userId] = prop.Value.GetString()!;
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("Seed", out var seedElement)
                        && seedElement.ValueKind == JsonValueKind.Number)
                    {
                        seed = seedElement.GetInt32();
                    }

                    if (doc.RootElement.TryGetProperty("Timer", out var timerElement)
                        && timerElement.ValueKind == JsonValueKind.Object)
                    {
                        if (timerElement.TryGetProperty("BaseTurnSeconds", out var b)) timerConfig.BaseTurnSeconds = b.GetDouble();
                        if (timerElement.TryGetProperty("MaxBankSeconds", out var m)) timerConfig.MaxBankSeconds = m.GetDouble();
                        if (timerElement.TryGetProperty("MaxOverrunSeconds", out var o)) timerConfig.MaxOverrunSeconds = o.GetDouble();
                        if (timerElement.TryGetProperty("MaxAfkTurns", out var a)) timerConfig.MaxAfkTurns = a.GetInt32();
                        if (timerElement.TryGetProperty("TotalGameTimeMinutes", out var t)) timerConfig.TotalGameTimeMinutes = t.GetDouble();
                    }
                }
            }
            catch
            {
                // Ignore parsing errors, fall back to empty names and defaults.
            }
        }

        var count = options.Players.Count;
        var wallsPerPlayer = QuoridorCatalog.WallsPerPlayer(count);

        var quoridor = new QuoridorState
        {
            SeatCount = count,
            Pawns = Enumerable.Range(0, count)
                .Select(s =>
                {
                    var pos = QuoridorCatalog.StartPosition(s, count);
                    return new QuoridorPawn { Row = pos.row, Col = pos.col };
                })
                .ToList(),
            Walls = new List<QuoridorWall>(),
            WallsRemaining = Enumerable.Repeat(wallsPerPlayer, count).ToList(),
            EliminatedSeats = new List<int>(),
            Seed = seed,
            TimerConfig = timerConfig,
            PlayerNames = playerNames,
            PlayerIds = options.Players
        };

        // Initialize player timers
        quoridor.PlayerTimers = Enumerable.Range(0, count)
            .Select(_ => new QuoridorPlayerTimer())
            .ToList();

        // First player: uniform random seat (OD-2, platform precedent)
        quoridor.CurrentPlayerIndex = CreateRng(quoridor, 1).Next(count);

        // Start the clock
        quoridor.SetTurnClock();

        if (timerConfig.TotalGameTimeMinutes > 0)
        {
            quoridor.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(timerConfig.TotalGameTimeMinutes);
        }

        // Log game start
        quoridor.EventLog.Add(QuoridorEvent.Build(QuoridorEventCode.GameStarted, new
        {
            players = Enumerable.Range(0, count).Select(s => NameOf(quoridor, s)).ToList()
        }));

        return WrapState(quoridor, sessionId: Guid.NewGuid(), version: 1, isOver: false, winner: null);
    }

    /// <inheritdoc />
    public GameResult ProcessAction(GameState state, GameAction action)
    {
        if (state.IsOver)
        {
            return Fail(state, Errors.GameOver);
        }

        if (!state.TryGetString("QuoridorState", out var json) || json is null)
        {
            return Fail(state, Errors.InvalidState);
        }

        var quoridor = QuoridorState.FromJson(json);
        Rehydrate(quoridor, state);

        var seat = IndexOf(state, action.PlayerId);
        if (seat < 0 || seat >= quoridor.SeatCount)
        {
            return Fail(state, Errors.InvalidPlayer);
        }

        if (quoridor.EliminatedSeats.Contains(seat))
        {
            return Fail(state, Errors.PlayerEliminated);
        }

        if (!Enum.TryParse<QuoridorActionType>(action.ActionType, ignoreCase: true, out var type))
        {
            return Fail(state, Errors.UnknownActionType, new object?[] { action.ActionType });
        }

        // TurnTimeout and GameTimeExpired are system actions; they don't require turn ownership
        QuoridorActionResult result;
        if (type == QuoridorActionType.TurnTimeout)
        {
            result = ProcessTurnTimeout(quoridor, seat);
        }
        else if (type == QuoridorActionType.GameTimeExpired)
        {
            result = ProcessGameTimeExpired(quoridor);
        }
        else
        {
            // Player actions require turn ownership
            if (seat != quoridor.CurrentPlayerIndex)
            {
                return Fail(state, Errors.NotYourTurn);
            }

            result = type switch
            {
                QuoridorActionType.MovePawn => ProcessMovePawn(quoridor, seat, action.PayloadAs<MovePawnPayload>()),
                QuoridorActionType.PlaceWall => ProcessPlaceWall(quoridor, seat, action.PayloadAs<PlaceWallPayload>()),
                _ => FailResult(Errors.UnknownActionType, new object?[] { action.ActionType })
            };
        }

        if (!result.IsValid)
        {
            return Fail(state, result.Error!, result.ErrorArgs);
        }

        // Resolution after a valid action
        if (result.GameEnded)
        {
            quoridor.NextActionDeadlineUtc = null;
            quoridor.GameEndsAtUtc = null;
            // Persist the outcome into the state JSON so clients see the winner
            // (GameState.Winner alone is root-level; QuoridorState is the round-trip payload)
            quoridor.WinnerSeat = result.WinnerSeat;
            quoridor.Draw = result.SharedVictory;
        }
        else if (result.IsTurnAction)
        {
            // Turn action: check for win, then advance
            if (result.GameEnded)
            {
                quoridor.NextActionDeadlineUtc = null;
                quoridor.GameEndsAtUtc = null;
            }
            else
            {
                AdvancePlayer(quoridor);
                quoridor.ApplyTurnTimeAccounting(seat, turnEnded: true);
            }
        }
        else
        {
            // System timeout: skip forward (turn already advanced in processor)
            quoridor.SetTurnClock();
        }

        quoridor.EventLog.AddRange(result.Events);

        var winner = result.GameEnded && !result.SharedVictory && result.WinnerSeat >= 0
            ? state.Players[result.WinnerSeat]
            : (PlayerId?)null;

        var newState = WrapState(quoridor, state.SessionId, state.Version + 1, result.GameEnded, winner);
        return new GameResult
        {
            IsValid = true,
            NewState = newState,
            Events = result.Events.Select(e => new GameEvent { Type = "QuoridorEvent", Payload = e }).ToList(),
            GameEnded = result.GameEnded
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId)
    {
        var actions = new List<GameAction>();

        if (state.IsOver) return actions;

        if (!state.TryGetString("QuoridorState", out var json) || json is null)
            return actions;

        var quoridor = QuoridorState.FromJson(json);
        Rehydrate(quoridor, state);

        var seat = IndexOf(state, playerId);
        if (seat < 0 || seat >= quoridor.SeatCount || quoridor.EliminatedSeats.Contains(seat))
            return actions;

        if (seat != quoridor.CurrentPlayerIndex)
            return actions;

        var pawn = quoridor.Pawns[seat];
        var r = pawn.Row;
        var c = pawn.Col;

        // MovePawn: all legal destinations (steps + jumps)
        foreach (var (dr, dc) in QuoridorCatalog.Directions)
        {
            // Single step
            int nr = r + dr, nc = c + dc;
            var check = quoridor.CheckMove(seat, r, c, nr, nc);
            if (check.legal)
            {
                actions.Add(CreateMoveAction(playerId, nr, nc));
            }

            // Straight jump (2 cells)
            int jr = r + 2 * dr, jc = c + 2 * dc;
            check = quoridor.CheckMove(seat, r, c, jr, jc);
            if (check.legal && check.isJump && check.jumpKind == "straight")
            {
                actions.Add(CreateMoveAction(playerId, jr, jc));
            }

            // Aside jumps (diagonal) - check if straight is blocked
            int adjR = r + dr, adjC = c + dc;
            bool pawnAdjacent = quoridor.Pawns.Any(p => p.Row == adjR && p.Col == adjC);
            if (pawnAdjacent)
            {
                // Two diagonal squares beside the adjacent pawn
                int diagR1 = adjR + dc, diagC1 = adjC - dr; // rotate 90° CCW
                int diagR2 = adjR - dc, diagC2 = adjC + dr; // rotate 90° CW

                foreach (var (dR, dC) in new[] { (diagR1, diagC1), (diagR2, diagC2) })
                {
                    check = quoridor.CheckMove(seat, r, c, dR, dC);
                    if (check.legal && check.isJump && check.jumpKind == "aside")
                    {
                        actions.Add(CreateMoveAction(playerId, dR, dC));
                    }
                }
            }
        }

        // PlaceWall: enumerate all legal wall slots
        if (quoridor.WallsRemaining[seat] > 0)
        {
            // Horizontal walls: Row 0..7, Col 0..7 (walls flush to the east rim are legal)
            for (int rw = 0; rw <= 7; rw++)
            {
                for (int col = 0; col <= 7; col++)
                {
                    var wall = new QuoridorWall { Row = rw, Col = col, Orientation = QuoridorCatalog.OrientationHorizontal };
                    if (IsWallLegal(quoridor, seat, wall))
                    {
                        actions.Add(CreateWallAction(playerId, rw, col, QuoridorCatalog.OrientationHorizontal));
                    }
                }
            }

            // Vertical walls: Row 0..7, Col 0..7 (walls flush to the south rim are legal)
            for (int row = 0; row <= 7; row++)
            {
                for (int cw = 0; cw <= 7; cw++)
                {
                    var wall = new QuoridorWall { Row = row, Col = cw, Orientation = QuoridorCatalog.OrientationVertical };
                    if (IsWallLegal(quoridor, seat, wall))
                    {
                        actions.Add(CreateWallAction(playerId, row, cw, QuoridorCatalog.OrientationVertical));
                    }
                }
            }
        }

        return actions;
    }

    /// <inheritdoc />
    public bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public PlayerId? GetWinner(GameState state) => state.Winner;

    // ==================== Helpers ====================

    private static void AdvancePlayer(QuoridorState quoridor)
    {
        var count = quoridor.SeatCount;
        var guard = 0;
        do
        {
            quoridor.CurrentPlayerIndex = (quoridor.CurrentPlayerIndex + 1) % count;
            guard++;
        }
        while (quoridor.EliminatedSeats.Contains(quoridor.CurrentPlayerIndex) && guard <= count);
    }

    private static GameState WrapState(QuoridorState quoridor, Guid sessionId, long version, bool isOver, PlayerId? winner)
    {
        return new GameState
        {
            SessionId = sessionId,
            GameType = "Quoridor",
            Players = quoridor.PlayerIds ?? new List<PlayerId>(),
            CurrentPlayerIndex = quoridor.CurrentPlayerIndex,
            IsOver = isOver,
            Winner = winner,
            Version = version,
            NextActionDeadlineUtc = quoridor.NextActionDeadlineUtc,
            GameEndsAtUtc = quoridor.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["QuoridorState"] = quoridor.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(quoridor.PlayerNames ?? new Dictionary<Guid, string>())
            }
        };
    }

    private static GameResult Fail(GameState state, string code, object?[]? args = null)
    {
        return new GameResult
        {
            IsValid = false,
            NewState = state,
            Error = code,
            ErrorCode = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static QuoridorActionResult FailResult(string code, object?[]? args = null)
    {
        return new QuoridorActionResult
        {
            IsValid = false,
            Error = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static QuoridorActionResult Ok()
    {
        return new QuoridorActionResult { IsValid = true };
    }

    private static void Rehydrate(QuoridorState quoridor, GameState state)
    {
        quoridor.PlayerIds = state.Players;
        if (state.TryGetString("PlayerNames", out var namesJson) && namesJson is not null)
        {
            try
            {
                quoridor.PlayerNames = JsonSerializer.Deserialize<Dictionary<Guid, string>>(namesJson);
            }
            catch { /* ignore */ }
        }
    }

    private static int IndexOf(GameState state, PlayerId playerId)
    {
        for (var i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].UserId == playerId.UserId) return i;
        }
        return -1;
    }

    private static string NameOf(QuoridorState quoridor, int seat)
    {
        if (quoridor.PlayerIds is { } ids && seat >= 0 && seat < ids.Count
            && quoridor.PlayerNames is { } names && names.TryGetValue(ids[seat].UserId, out var name))
        {
            return name;
        }
        return $"Player {seat + 1}";
    }

    private static Random CreateRng(QuoridorState quoridor, int stream)
    {
        return quoridor.Seed is { } seed
            ? new Random(unchecked(seed * 397 + stream * 7))
            : new Random();
    }

    private static GameAction CreateMoveAction(PlayerId playerId, int row, int col)
    {
        return new GameAction
        {
            PlayerId = playerId,
            ActionType = QuoridorActionType.MovePawn.ToString(),
            Payload = JsonSerializer.Serialize(new MovePawnPayload { Row = row, Col = col })
        };
    }

    private static GameAction CreateWallAction(PlayerId playerId, int row, int col, string orientation)
    {
        return new GameAction
        {
            PlayerId = playerId,
            ActionType = QuoridorActionType.PlaceWall.ToString(),
            Payload = JsonSerializer.Serialize(new PlaceWallPayload { Row = row, Col = col, Orientation = orientation })
        };
    }

    // ==================== Wall legality (spec §8-10) ====================

    private static bool IsWallLegal(QuoridorState quoridor, int seat, QuoridorWall wall)
    {
        // Bounds
        if (!QuoridorState.IsWallSlotInBounds(wall)) return false;

        // Overlap
        if (quoridor.WallOverlaps(wall)) return false;

        // Path preservation: simulate placement and check all seats
        quoridor.Walls.Add(wall);
        bool allHavePaths = true;
        for (int s = 0; s < quoridor.SeatCount; s++)
        {
            if (!quoridor.HasPathToGoal(s))
            {
                allHavePaths = false;
                break;
            }
        }
        quoridor.Walls.RemoveAt(quoridor.Walls.Count - 1);

        return allHavePaths;
    }
}