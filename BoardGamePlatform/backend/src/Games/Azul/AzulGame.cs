using System.Text.Json;
using Azul.Models;
using GameEngine.Core;
using GameEngine.Core.Models;

namespace Azul;

/// <summary>
/// Azul (Michael Kiesling, Plan B / Next Move 2017 — base game) implementation of
/// the platform Game Engine contract. Contains only Azul rules: no HTTP, SignalR,
/// EF Core, or database dependencies. Registered in DI as an <see cref="IGame"/>;
/// also implements <see cref="IPlayerViewGame"/> so the Game Service can strip the
/// bag draw order (the game's only hidden information) before delivery — the
/// projection is viewer-independent (spec §16).
/// </summary>
public sealed partial class AzulGame : IGame, IPlayerViewGame
{
    /// <summary>The game type identifier used across the platform to select this game.</summary>
    public string GameType => "Azul";

    /// <inheritdoc />
    public int MinPlayers => 2;

    /// <inheritdoc />
    public int MaxPlayers => 4;

    /// <inheritdoc />
    public GameState CreateGame(GameOptions options)
    {
        if (options.Players.Count < MinPlayers || options.Players.Count > MaxPlayers)
        {
            throw new ArgumentException("Azul requires 2-4 players");
        }

        var playerNames = new Dictionary<Guid, string>();
        var timerConfig = new AzulTurnTimerConfig();
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
        var azul = new AzulState
        {
            FactoryCount = AzulCatalog.FactoryCountFor(count),
            Bag = Enumerable.Range(0, AzulCatalog.ColorCount)
                .SelectMany(color => Enumerable.Repeat(color, AzulCatalog.TilesPerColor))
                .ToList(),
            Factories = Enumerable.Range(0, AzulCatalog.FactoryCountFor(count))
                .Select(_ => new List<int>())
                .ToList(),
            Seats = Enumerable.Range(0, count).Select(_ => new AzulSeatState()).ToList(),
            Seed = seed,
            TimerConfig = timerConfig,
            PlayerNames = playerNames,
            PlayerIds = options.Players
        };

        // Shuffle the bag, deal four tiles onto each factory (front-of-bag order).
        AzulState.Shuffle(azul.Bag, CreateRng(azul, 1));
        for (var f = 0; f < azul.FactoryCount; f++)
        {
            for (var t = 0; t < AzulCatalog.TilesPerFactory; t++)
            {
                azul.Factories[f].Add(azul.DrawFromBag());
            }
        }

        // Round-1 first player: uniform-random seat (OD-2, physical "whoever
        // most recently visited Portugal"); marker starts in the center.
        azul.FirstPlayerSeat = CreateRng(azul, 2).Next(count);
        azul.CurrentPlayerIndex = azul.FirstPlayerSeat;
        azul.MarkerSeat = -1;
        azul.SetTurnClock();

        if (timerConfig.TotalGameTimeMinutes > 0)
        {
            azul.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(timerConfig.TotalGameTimeMinutes);
        }

        azul.EventLog.Add(AzulEvent.Build("azul.gameStarted", new
        {
            players = Enumerable.Range(0, count).Select(seat => NameOf(azul, seat)).ToList()
        }));

        return WrapState(azul, sessionId: Guid.NewGuid(), version: 1, isOver: false, winner: null);
    }

    /// <inheritdoc />
    public GameResult ProcessAction(GameState state, GameAction action)
    {
        if (state.IsOver)
        {
            return Fail(state, Errors.GameOver);
        }

        if (!state.TryGetString("AzulState", out var json) || json is null)
        {
            return Fail(state, Errors.InvalidState);
        }

        var azul = AzulState.FromJson(json);
        Rehydrate(azul, state);

        var seat = IndexOf(state, action.PlayerId);
        if (seat < 0 || seat >= azul.PlayerCount)
        {
            return Fail(state, Errors.InvalidPlayer);
        }

        if (azul.EliminatedSeats.Contains(seat))
        {
            return Fail(state, Errors.PlayerEliminated);
        }

        if (!Enum.TryParse<AzulActionType>(action.ActionType, ignoreCase: true, out var type))
        {
            return Fail(state, Errors.UnknownActionType, new object?[] { action.ActionType });
        }

        if (seat != azul.CurrentPlayerIndex)
        {
            return Fail(state, Errors.NotYourTurn);
        }

        var result = type switch
        {
            AzulActionType.DraftFromFactory => ProcessDraftFromFactory(azul, seat, action.PayloadAs<DraftFromFactoryPayload>()),
            AzulActionType.DraftFromCenter => ProcessDraftFromCenter(azul, seat, action.PayloadAs<DraftFromCenterPayload>()),
            AzulActionType.TakeFirstPlayer => ProcessTakeFirstPlayer(azul, seat),
            AzulActionType.TurnTimeout => ProcessTurnTimeout(azul, seat),
            AzulActionType.GameTimeExpired => ProcessGameTimeExpired(azul),
            _ => FailResult(Errors.UnknownActionType, new object?[] { action.ActionType })
        };

        if (!result.IsValid)
        {
            return Fail(state, result.Error!, result.ErrorArgs);
        }

        // Round/turn resolution (spec §13): a player draft may drain the pool and
        // trigger the automatic tiling phase; a system timeout simply skips forward.
        if (result.GameEnded)
        {
            azul.NextActionDeadlineUtc = null;
            azul.GameEndsAtUtc = null;
        }
        else if (result.IsTurnAction)
        {
            if (azul.PoolEmpty())
            {
                EndRound(azul, result);
            }
            else
            {
                AdvancePlayer(azul);
            }

            if (result.GameEnded)
            {
                azul.NextActionDeadlineUtc = null;
                azul.GameEndsAtUtc = null;
            }
            else
            {
                azul.ApplyTurnTimeAccounting(seat, turnEnded: true);
            }
        }
        else
        {
            // System timeout: the overrun was charged by the processor; skip forward.
            AdvancePlayer(azul);
            azul.SetTurnClock();
        }

        azul.EventLog.AddRange(result.Events);

        var winner = result.GameEnded && !result.SharedVictory && result.WinnerSeat >= 0
            ? state.Players[result.WinnerSeat]
            : (PlayerId?)null;
        var newState = WrapState(azul, state.SessionId, state.Version + 1, result.GameEnded, winner);
        return new GameResult
        {
            IsValid = true,
            NewState = newState,
            Events = result.Events.Select(e => new GameEvent { Type = "AzulEvent", Payload = e }).ToList(),
            GameEnded = result.GameEnded
        };
    }

    /// <inheritdoc />
    public bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public PlayerId? GetWinner(GameState state) => state.Winner;

    // ----- shared helpers -----

    private static void AdvancePlayer(AzulState azul)
    {
        var count = azul.PlayerCount;
        var guard = 0;
        do
        {
            azul.CurrentPlayerIndex = (azul.CurrentPlayerIndex + 1) % count;
            guard++;
        }
        while (azul.EliminatedSeats.Contains(azul.CurrentPlayerIndex) && guard <= count);
    }

    private static GameState WrapState(AzulState azul, Guid sessionId, long version, bool isOver, PlayerId? winner)
    {
        return new GameState
        {
            SessionId = sessionId,
            GameType = "Azul",
            Players = azul.PlayerIds ?? new List<PlayerId>(),
            CurrentPlayerIndex = azul.CurrentPlayerIndex,
            IsOver = isOver,
            Winner = winner,
            Version = version,
            NextActionDeadlineUtc = azul.NextActionDeadlineUtc,
            GameEndsAtUtc = azul.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["AzulState"] = azul.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(azul.PlayerNames ?? new Dictionary<Guid, string>())
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

    private static AzulActionResult FailResult(string code, object?[]? args = null)
    {
        return new AzulActionResult
        {
            IsValid = false,
            Error = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static AzulActionResult Ok()
    {
        return new AzulActionResult { IsValid = true };
    }

    private static void Rehydrate(AzulState azul, GameState state)
    {
        azul.PlayerIds = state.Players;
        if (state.TryGetString("PlayerNames", out var namesJson) && namesJson is not null)
        {
            try
            {
                azul.PlayerNames = JsonSerializer.Deserialize<Dictionary<Guid, string>>(namesJson);
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

    private static string NameOf(AzulState azul, int seat)
    {
        if (azul.PlayerIds is { } ids && seat >= 0 && seat < ids.Count
            && azul.PlayerNames is { } names && names.TryGetValue(ids[seat].UserId, out var name))
        {
            return name;
        }

        return $"Player {seat + 1}";
    }

    private static GameAction CreateAction(AzulActionType type, PlayerId playerId, object payload)
    {
        return new GameAction
        {
            PlayerId = playerId,
            ActionType = type.ToString(),
            Payload = JsonSerializer.Serialize(payload)
        };
    }

    /// <summary>
    /// Seeded per-stream when the session settings provided a seed (deterministic
    /// testing), otherwise fresh per call. Nothing random is serialized (spec §17).
    /// </summary>
    private static Random CreateRng(AzulState azul, int stream)
    {
        return azul.Seed is { } seed
            ? new Random(unchecked(seed * 397 + stream * 7))
            : new Random();
    }
}
