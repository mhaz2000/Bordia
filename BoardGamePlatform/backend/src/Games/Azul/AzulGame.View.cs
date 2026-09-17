using System.Text.Json;
using Azul.Models;
using GameEngine.Core.Models;

namespace Azul;

/// <summary>
/// Player-view projection implementing <see cref="IPlayerViewGame"/>. Azul's
/// board is entirely public, but the bag's draw ORDER is the one secret that
/// must never reach a client (it leaks future drafting distribution), so the
/// projection replaces the bag with its count. Viewer-independent (spec §16).
/// </summary>
public sealed partial class AzulGame
{
    /// <inheritdoc />
    public GameState GetPlayerView(GameState authoritativeState, PlayerId viewer)
    {
        if (!authoritativeState.TryGetString("AzulState", out var json) || json is null)
        {
            return authoritativeState;
        }

        var azul = AzulState.FromJson(json);
        Rehydrate(azul, authoritativeState);
        var viewerSeat = IndexOf(authoritativeState, viewer);
        var view = BuildView(azul, viewerSeat);

        return new GameState
        {
            SessionId = authoritativeState.SessionId,
            GameType = authoritativeState.GameType,
            Players = authoritativeState.Players,
            CurrentPlayerIndex = azul.CurrentPlayerIndex,
            IsOver = authoritativeState.IsOver,
            Winner = authoritativeState.Winner,
            Version = authoritativeState.Version,
            NextActionDeadlineUtc = azul.NextActionDeadlineUtc,
            GameEndsAtUtc = azul.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["AzulState"] = JsonSerializer.Serialize(view),
                ["PlayerNames"] = authoritativeState.TryGetString("PlayerNames", out var names) && names is not null
                    ? names
                    : JsonSerializer.Serialize(new Dictionary<Guid, string>())
            }
        };
    }

    private static AzulView BuildView(AzulState azul, int viewerSeat)
    {
        return new AzulView
        {
            RoundNumber = azul.RoundNumber,
            CurrentPlayerIndex = azul.CurrentPlayerIndex,
            FactoryCount = azul.FactoryCount,
            BagCount = azul.Bag.Count,
            Factories = azul.Factories.Select(f => f.ToList()).ToList(),
            Center = azul.Center.ToList(),
            DiscardCount = azul.Discard.Count,
            MarkerSeat = azul.MarkerSeat,
            FirstPlayerSeat = azul.FirstPlayerSeat,
            Seats = azul.Seats.Select(seat => new AzulSeatView
            {
                Wall = seat.Wall.Select(row => row.ToList()).ToList(),
                PatternLines = seat.PatternLines.Select(line => line.ToList()).ToList(),
                Floor = seat.Floor.ToList(),
                Score = seat.Score
            }).ToList(),
            EliminatedSeats = azul.EliminatedSeats.ToList(),
            EventLog = azul.EventLog.ToList(),
            ViewerSeat = viewerSeat,
            ViewerIsCurrentPlayer = viewerSeat == azul.CurrentPlayerIndex,
            TimerConfig = azul.TimerConfig is { } config ? new AzulTurnTimerConfig
            {
                BaseTurnSeconds = config.BaseTurnSeconds,
                MaxBankSeconds = config.MaxBankSeconds,
                MaxOverrunSeconds = config.MaxOverrunSeconds,
                MaxAfkTurns = config.MaxAfkTurns,
                TotalGameTimeMinutes = config.TotalGameTimeMinutes
            } : null,
            PlayerTimers = azul.PlayerTimers.Select(t => new AzulPlayerTimer
            {
                BankSeconds = t.BankSeconds,
                DeferredPenaltySeconds = t.DeferredPenaltySeconds,
                ConsecutiveTimeouts = t.ConsecutiveTimeouts
            }).ToList(),
            TurnStartUtc = azul.TurnStartUtc
        };
    }
}
