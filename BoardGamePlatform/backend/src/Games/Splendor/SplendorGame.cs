using System.Text.Json;
using GameEngine.Core;
using GameEngine.Core.Models;
using Splendor.Models;

namespace Splendor;

/// <summary>
/// Splendor (Marc André, Space Cowboys 2014 — base game) implementation of the
/// platform Game Engine contract. Contains only Splendor rules: no HTTP,
/// SignalR, EF Core, or database dependencies. Registered in DI as an
/// <see cref="IGame"/>; also implements <see cref="IPlayerViewGame"/> so the
/// Game Service can strip the secrets every client is forbidden to see
/// (deck order, blind reservation identities) before delivery — the projection
/// is viewer-independent (spec §12).
/// </summary>
public sealed partial class SplendorGame : IGame, IPlayerViewGame
{
    /// <summary>The game type identifier used across the platform to select this game.</summary>
    public string GameType => "Splendor";

    /// <inheritdoc />
    public int MinPlayers => 2;

    /// <inheritdoc />
    public int MaxPlayers => 4;

    /// <inheritdoc />
    public GameState CreateGame(GameOptions options)
    {
        if (options.Players.Count < MinPlayers || options.Players.Count > MaxPlayers)
        {
            throw new ArgumentException("Splendor requires 2-4 players");
        }

        var playerNames = new Dictionary<Guid, string>();
        var timerConfig = new SplendorTurnTimerConfig();
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
        var splendor = new SplendorState
        {
            Decks = new List<List<string>>
            {
                TierDeck(1),
                TierDeck(2),
                TierDeck(3)
            },
            Market = new List<string?>(new string?[12]),
            Supply = new SplendorGems
            {
                Diamond = GemsPerColor(count),
                Sapphire = GemsPerColor(count),
                Emerald = GemsPerColor(count),
                Ruby = GemsPerColor(count),
                Onyx = GemsPerColor(count),
                Gold = SplendorCatalogue.GoldCount
            },
            Seats = Enumerable.Range(0, count).Select(_ => new SplendorSeatState()).ToList(),
            Seed = seed,
            TimerConfig = timerConfig,
            PlayerNames = playerNames,
            PlayerIds = options.Players
        };

        // Shuffle each tier deck separately, reveal its first four cards, and
        // keep the rest as the face-down draw deck (front = next drawn).
        for (var tier = 1; tier <= 3; tier++)
        {
            var deck = splendor.Decks[tier - 1];
            Shuffle(deck, CreateRng(splendor, tier));
            for (var slot = 0; slot < 4; slot++)
            {
                splendor.Market[(tier - 1) * 4 + slot] = DrawFromDeck(splendor, tier);
            }
        }

        // Shuffle the ten distinct nobles and reveal players + 1; the rest are
        // removed from the game entirely (spec §4).
        var noblePool = SplendorCatalogue.Nobles.Select(n => n.Id).ToList();
        Shuffle(noblePool, CreateRng(splendor, 10));
        splendor.NoblesInMarket = noblePool.Take(count + 1).ToList();

        // Physical "youngest player begins" mapped to a random start seat
        // (OD-4 default, platform precedent from Silver).
        splendor.CurrentPlayerIndex = CreateRng(splendor, 20).Next(count);
        splendor.SetTurnClock();

        if (timerConfig.TotalGameTimeMinutes > 0)
        {
            splendor.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(timerConfig.TotalGameTimeMinutes);
        }

        splendor.EventLog.Add(SplendorEvent.Build("splendor.gameStarted", new
        {
            players = Enumerable.Range(0, count).Select(seat => NameOf(splendor, seat)).ToList()
        }));

        return WrapState(splendor, sessionId: Guid.NewGuid(), version: 1, isOver: false, winner: null);
    }

    /// <inheritdoc />
    public GameResult ProcessAction(GameState state, GameAction action)
    {
        if (state.IsOver)
        {
            return Fail(state, Errors.GameOver);
        }

        if (!state.TryGetString("SplendorState", out var json) || json is null)
        {
            return Fail(state, Errors.InvalidState);
        }

        var splendor = SplendorState.FromJson(json);
        Rehydrate(splendor, state);

        var seat = IndexOf(state, action.PlayerId);
        if (seat < 0 || seat >= splendor.PlayerCount)
        {
            return Fail(state, Errors.InvalidPlayer);
        }

        if (splendor.EliminatedSeats.Contains(seat))
        {
            return Fail(state, Errors.PlayerEliminated);
        }

        if (!Enum.TryParse<SplendorActionType>(action.ActionType, ignoreCase: true, out var type))
        {
            return Fail(state, Errors.UnknownActionType, new object?[] { action.ActionType });
        }

        // Splendor has no off-turn player rights: every action (including the
        // service-dispatched system actions, which always target the current
        // player) must come from the seat whose turn it is.
        if (seat != splendor.CurrentPlayerIndex)
        {
            return Fail(state, Errors.NotYourTurn);
        }

        var result = type switch
        {
            SplendorActionType.TakeThreeGems => ProcessTakeThreeGems(splendor, seat, action.PayloadAs<TakeThreeGemsPayload>()),
            SplendorActionType.TakeTwoGems => ProcessTakeTwoGems(splendor, seat, action.PayloadAs<TakeTwoGemsPayload>()),
            SplendorActionType.ReserveMarketCard => ProcessReserveMarketCard(splendor, seat, action.PayloadAs<ReserveMarketCardPayload>()),
            SplendorActionType.ReserveDeckCard => ProcessReserveDeckCard(splendor, seat, action.PayloadAs<ReserveDeckCardPayload>()),
            SplendorActionType.PurchaseMarketCard => ProcessPurchaseMarketCard(splendor, seat, action.PayloadAs<PurchaseMarketCardPayload>()),
            SplendorActionType.PurchaseReservedCard => ProcessPurchaseReservedCard(splendor, seat, action.PayloadAs<PurchaseReservedCardPayload>()),
            SplendorActionType.TurnTimeout => ProcessTurnTimeout(splendor, seat),
            SplendorActionType.GameTimeExpired => ProcessGameTimeExpired(splendor),
            _ => FailResult(Errors.UnknownActionType, new object?[] { action.ActionType })
        };

        if (!result.IsValid)
        {
            return Fail(state, result.Error!, result.ErrorArgs);
        }

        // End-of-turn pipeline (spec §15): noble visit → threshold check →
        // final-round countdown or advance. Skipped entirely when the action
        // itself already ended the game (time expiry, 2-player AFK).
        if (result.TurnEnded && !result.GameEnded)
        {
            var nobleError = AwardNobleAtEndOfTurn(splendor, seat, result, result.Events);
            if (nobleError is not null)
            {
                return Fail(state, nobleError.Value.Code, nobleError.Value.Args);
            }

            var endedWithWinner = CompleteTurn(splendor, seat, result, result.Events);
            _ = endedWithWinner;
        }

        if (result.GameEnded)
        {
            splendor.NextActionDeadlineUtc = null;
            splendor.GameEndsAtUtc = null;
        }
        else if (result.IsTurnAction)
        {
            splendor.ApplyTurnTimeAccounting(seat, turnEnded: true);
        }
        else
        {
            // System timeout: the overrun was charged by the processor; only
            // the next player's clock needs starting.
            splendor.SetTurnClock();
        }

        splendor.EventLog.AddRange(result.Events);

        var winner = result.GameEnded && !result.SharedVictory && result.WinnerSeat >= 0
            ? state.Players[result.WinnerSeat]
            : (PlayerId?)null;
        var newState = WrapState(splendor, state.SessionId, state.Version + 1, result.GameEnded, winner);
        return new GameResult
        {
            IsValid = true,
            NewState = newState,
            Events = result.Events.Select(e => new GameEvent { Type = "SplendorEvent", Payload = e }).ToList(),
            GameEnded = result.GameEnded
        };
    }

    /// <inheritdoc />
    public bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public PlayerId? GetWinner(GameState state) => state.Winner;

    // ----- shared turn pipeline -----

    /// <summary>
    /// Advances play after a completed turn: opens the final round when the
    /// actor just crossed the threshold, otherwise finishes the game when the
    /// owed final turns run out. Returns whether the game ended.
    /// </summary>
    private static bool CompleteTurn(SplendorState splendor, int seat, SplendorActionResult result, List<string> events)
    {
        if (!splendor.FinalRoundTriggered)
        {
            if (splendor.Seats[seat].VictoryPoints >= SplendorCatalogue.TriggerPoints)
            {
                splendor.FinalRoundTriggered = true;
                splendor.TriggerSeat = seat;
                splendor.FinalTurnSeatsPending = splendor.ActiveSeats().Where(s => s != seat).ToList();
                events.Add(SplendorEvent.Build("splendor.finalRoundTriggered", new
                {
                    player = NameOf(splendor, seat),
                    remaining = splendor.FinalTurnSeatsPending.Count
                }));
            }
        }
        else
        {
            splendor.FinalTurnSeatsPending.Remove(seat);
            if (splendor.FinalTurnSeatsPending.Count == 0)
            {
                FinishGame(splendor, result, events);
                return true;
            }
        }

        AdvancePlayer(splendor);
        splendor.TurnNumber++;
        return false;
    }

    /// <summary>
    /// Resolves the end of the game: the winner ladder (spec §11) over active
    /// seats — most points, then fewest purchased cards, then fewest reserved
    /// cards, then a shared victory (null winner).
    /// </summary>
    private static void FinishGame(SplendorState splendor, SplendorActionResult result, List<string> events)
    {
        result.GameEnded = true;
        var candidates = splendor.ActiveSeats().ToList();
        if (candidates.Count == 0)
        {
            result.WinnerSeat = -1;
            result.SharedVictory = true;
        }
        else
        {
            var best = candidates
                .OrderByDescending(s => splendor.Seats[s].VictoryPoints)
                .ThenBy(s => splendor.Seats[s].Purchased.Count)
                .ThenBy(s => splendor.Seats[s].Reserved.Count)
                .ToList();
            var top = splendor.Seats[best[0]];
            var tied = best.Where(s =>
                    splendor.Seats[s].VictoryPoints == top.VictoryPoints
                    && splendor.Seats[s].Purchased.Count == top.Purchased.Count
                    && splendor.Seats[s].Reserved.Count == top.Reserved.Count)
                .ToList();
            if (tied.Count == 1)
            {
                result.WinnerSeat = tied[0];
            }
            else
            {
                result.SharedVictory = true;
            }
        }

        events.Add(SplendorEvent.Build("splendor.gameFinished", new
        {
            winner = result.WinnerSeat >= 0 ? NameOf(splendor, result.WinnerSeat) : null,
            scores = splendor.Seats.Select(s => s.VictoryPoints).ToList()
        }));
    }

    private static void AdvancePlayer(SplendorState splendor)
    {
        var count = splendor.PlayerCount;
        var guard = 0;
        do
        {
            splendor.CurrentPlayerIndex = (splendor.CurrentPlayerIndex + 1) % count;
            guard++;
        }
        while (splendor.EliminatedSeats.Contains(splendor.CurrentPlayerIndex) && guard <= count);
    }

    // ----- shared helpers -----

    private static GameState WrapState(SplendorState splendor, Guid sessionId, long version, bool isOver, PlayerId? winner)
    {
        return new GameState
        {
            SessionId = sessionId,
            GameType = "Splendor",
            Players = splendor.PlayerIds ?? new List<PlayerId>(),
            CurrentPlayerIndex = splendor.CurrentPlayerIndex,
            IsOver = isOver,
            Winner = winner,
            Version = version,
            NextActionDeadlineUtc = splendor.NextActionDeadlineUtc,
            GameEndsAtUtc = splendor.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SplendorState"] = splendor.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(splendor.PlayerNames ?? new Dictionary<Guid, string>())
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

    private static SplendorActionResult FailResult(string code, object?[]? args = null)
    {
        return new SplendorActionResult
        {
            IsValid = false,
            Error = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static SplendorActionResult Ok()
    {
        return new SplendorActionResult { IsValid = true };
    }

    private static void Rehydrate(SplendorState splendor, GameState state)
    {
        splendor.PlayerIds = state.Players;
        if (state.TryGetString("PlayerNames", out var namesJson) && namesJson is not null)
        {
            try
            {
                splendor.PlayerNames = JsonSerializer.Deserialize<Dictionary<Guid, string>>(namesJson);
            }
            catch
            {
                // Ignore parsing errors, fall back to no names.
            }
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

    private static string NameOf(SplendorState splendor, int seat)
    {
        if (splendor.PlayerIds is { } ids && seat >= 0 && seat < ids.Count
            && splendor.PlayerNames is { } names && names.TryGetValue(ids[seat].UserId, out var name))
        {
            return name;
        }

        return $"Player {seat + 1}";
    }

    private static GameAction CreateAction(SplendorActionType type, PlayerId playerId, object payload)
    {
        return new GameAction
        {
            PlayerId = playerId,
            ActionType = type.ToString(),
            Payload = JsonSerializer.Serialize(payload)
        };
    }

    private static List<string> TierDeck(int tier)
        => SplendorCatalogue.Cards.Where(c => c.Tier == tier).Select(c => c.Id).ToList();

    private static int GemsPerColor(int playerCount) => playerCount switch
    {
        2 => 4,
        3 => 5,
        _ => SplendorCatalogue.FullGemsPerColor
    };

    /// <summary>Draws the front card of a tier deck (the next-shuffled card); null when exhausted.</summary>
    private static string? DrawFromDeck(SplendorState splendor, int tier)
    {
        var deck = splendor.Decks[tier - 1];
        if (deck.Count == 0) return null;
        var card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    /// <summary>
    /// Seeded per-stream when the session settings provided a seed
    /// (deterministic testing), otherwise fresh per call. Nothing random is
    /// serialized (spec §13).
    /// </summary>
    private static Random CreateRng(SplendorState splendor, int stream)
    {
        return splendor.Seed is { } seed
            ? new Random(unchecked(seed * 397 + stream * 7))
            : new Random();
    }

    private static void Shuffle(List<string> ids, Random rng)
    {
        for (var i = ids.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
    }
}
