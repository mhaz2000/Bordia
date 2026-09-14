using System.Text.Json;
using GameEngine.Core.Models;
using Splendor.Models;

namespace Splendor;

/// <summary>
/// Player-view projection implementing <see cref="IPlayerViewGame"/>. Splendor's
/// hidden information (the three deck orders and the identities of blind
/// reservations) is secret to everyone, including the owner of a blind
/// reservation, so the projection is viewer-independent: it exposes all public
/// state and redacts only what no client may see (spec §12).
/// </summary>
public sealed partial class SplendorGame
{
    /// <inheritdoc />
    public GameState GetPlayerView(GameState authoritativeState, PlayerId viewer)
    {
        if (!authoritativeState.TryGetString("SplendorState", out var json) || json is null)
        {
            return authoritativeState;
        }

        var splendor = SplendorState.FromJson(json);
        Rehydrate(splendor, authoritativeState);
        var viewerSeat = IndexOf(authoritativeState, viewer);
        var view = BuildView(splendor, viewerSeat);

        return new GameState
        {
            SessionId = authoritativeState.SessionId,
            GameType = authoritativeState.GameType,
            Players = authoritativeState.Players,
            CurrentPlayerIndex = splendor.CurrentPlayerIndex,
            IsOver = authoritativeState.IsOver,
            Winner = authoritativeState.Winner,
            Version = authoritativeState.Version,
            NextActionDeadlineUtc = splendor.NextActionDeadlineUtc,
            GameEndsAtUtc = splendor.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SplendorState"] = JsonSerializer.Serialize(view),
                ["PlayerNames"] = authoritativeState.TryGetString("PlayerNames", out var names) && names is not null
                    ? names
                    : JsonSerializer.Serialize(new Dictionary<Guid, string>())
            }
        };
    }

    private static SplendorView BuildView(SplendorState splendor, int viewerSeat)
    {
        return new SplendorView
        {
            CurrentPlayerIndex = splendor.CurrentPlayerIndex,
            TurnNumber = splendor.TurnNumber,
            Supply = new SplendorGems
            {
                Diamond = splendor.Supply.Diamond,
                Sapphire = splendor.Supply.Sapphire,
                Emerald = splendor.Supply.Emerald,
                Ruby = splendor.Supply.Ruby,
                Onyx = splendor.Supply.Onyx,
                Gold = splendor.Supply.Gold
            },
            DeckCounts = splendor.Decks.Select(d => d.Count).ToList(),
            Market = splendor.Market.ToList(),
            NoblesInMarket = splendor.NoblesInMarket.ToList(),
            Seats = splendor.Seats.Select(seat => new SplendorSeatView
            {
                Tokens = new SplendorGems
                {
                    Diamond = seat.Tokens.Diamond,
                    Sapphire = seat.Tokens.Sapphire,
                    Emerald = seat.Tokens.Emerald,
                    Ruby = seat.Tokens.Ruby,
                    Onyx = seat.Tokens.Onyx,
                    Gold = seat.Tokens.Gold
                },
                Bonuses = Enumerable.Range(0, 5).Select(seat.Bonus).ToList(),
                VictoryPoints = seat.VictoryPoints,
                Purchased = seat.Purchased.ToList(),
                NoblesOwned = seat.NoblesOwned.ToList(),
                Reserved = seat.Reserved.Select(r => new SplendorViewReservation
                {
                    Tier = r.Tier,
                    Source = r.Source,
                    // Redact the identity of a still-blind reservation from every
                    // viewer including its owner; market reservations are public.
                    CardId = r.Source == SplendorReservationSource.Deck ? null : r.CardId
                }).ToList()
            }).ToList(),
            FinalRoundTriggered = splendor.FinalRoundTriggered,
            TriggerSeat = splendor.TriggerSeat,
            FinalTurnsRemaining = splendor.FinalTurnSeatsPending.Count,
            EliminatedSeats = splendor.EliminatedSeats.ToList(),
            EventLog = splendor.EventLog.ToList(),
            ViewerSeat = viewerSeat,
            ViewerIsCurrentPlayer = viewerSeat == splendor.CurrentPlayerIndex,
            TimerConfig = splendor.TimerConfig is { } config ? new SplendorTurnTimerConfig
            {
                BaseTurnSeconds = config.BaseTurnSeconds,
                MaxBankSeconds = config.MaxBankSeconds,
                MaxOverrunSeconds = config.MaxOverrunSeconds,
                MaxAfkTurns = config.MaxAfkTurns,
                TotalGameTimeMinutes = config.TotalGameTimeMinutes
            } : null,
            PlayerTimers = splendor.PlayerTimers.Select(t => new SplendorPlayerTimer
            {
                BankSeconds = t.BankSeconds,
                DeferredPenaltySeconds = t.DeferredPenaltySeconds,
                ConsecutiveTimeouts = t.ConsecutiveTimeouts
            }).ToList(),
            TurnStartUtc = splendor.TurnStartUtc
        };
    }
}
