using System.Text.Json;
using GameEngine.Core.Models;
using Silver.Models;

namespace Silver;

/// <summary>
/// Player-specific state projection. Implements the optional
/// <see cref="IPlayerViewGame"/> capability: the returned state contains public
/// information plus the viewer's own private information and knowledge, and
/// never the values of other players' hidden cards, the deck order, the
/// removed cards, or other players' knowledge.
/// </summary>
public sealed partial class SilverGame
{
    /// <inheritdoc />
    public GameState GetPlayerView(GameState authoritativeState, PlayerId viewer)
    {
        if (!authoritativeState.TryGetString("SilverState", out var json) || json is null)
        {
            return authoritativeState;
        }

        var silver = SilverState.FromJson(json);
        Rehydrate(silver, authoritativeState);
        var viewerSeat = IndexOf(authoritativeState, viewer);
        var view = BuildView(silver, viewerSeat);

        return new GameState
        {
            SessionId = authoritativeState.SessionId,
            GameType = authoritativeState.GameType,
            Players = authoritativeState.Players,
            CurrentPlayerIndex = silver.CurrentPlayerIndex,
            IsOver = authoritativeState.IsOver,
            Winner = authoritativeState.Winner,
            Version = authoritativeState.Version,
            NextActionDeadlineUtc = silver.NextActionDeadlineUtc,
            GameEndsAtUtc = silver.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SilverState"] = JsonSerializer.Serialize(view),
                ["PlayerNames"] = authoritativeState.TryGetString("PlayerNames", out var names) && names is not null
                    ? names
                    : JsonSerializer.Serialize(new Dictionary<Guid, string>())
            }
        };
    }

    private static SilverView BuildView(SilverState silver, int viewerSeat)
    {
        var knowledge = viewerSeat >= 0 && viewerSeat < silver.Knowledge.Count
            ? silver.Knowledge[viewerSeat]
            : new Dictionary<Guid, int>();
        var viewerIsCurrent = viewerSeat == silver.CurrentPlayerIndex;

        SilverViewCard Project(SilverCard card)
        {
            var visible = card.FaceUp || knowledge.ContainsKey(card.Id);
            return new SilverViewCard
            {
                Id = card.Id,
                Value = visible ? card.Value : null,
                FaceUp = card.FaceUp,
                Protected = silver.IsProtected(card),
                AmuletProtected = silver.IsAmuletProtected(card),
                GuardedByCardId = silver.GuardCovering(card)
            };
        }

        SilverViewCard ProjectPending(SilverCard card)
        {
            // A deck-drawn card is known only to the player who drew it (the
            // current player); discard- and Squire-taken cards are face up
            // (public).
            var visible = card.FaceUp || knowledge.ContainsKey(card.Id) || viewerIsCurrent;
            return new SilverViewCard
            {
                Id = card.Id,
                Value = visible ? card.Value : null,
                FaceUp = card.FaceUp,
                Protected = silver.IsProtected(card),
                AmuletProtected = silver.IsAmuletProtected(card),
                GuardedByCardId = silver.GuardCovering(card)
            };
        }

        return new SilverView
        {
            Round = silver.Round,
            CurrentPlayerIndex = silver.CurrentPlayerIndex,
            Phase = silver.Phase,
            Villages = silver.Villages.Select(v => v.Select(Project).ToList()).ToList(),
            DeckSize = silver.Deck.Count,
            RemovedCount = silver.Removed.Count,
            DiscardTop = silver.DiscardTop is { } top ? Project(top) : null,
            DiscardSize = Math.Max(0, silver.Discard.Count - 1),
            DiscardPile = silver.Discard.Select(Project).ToList(),
            Display = silver.Display.Select(Project).ToList(),
            PendingDraw = silver.PendingDraw.Select(ProjectPending).ToList(),
            PendingSource = silver.PendingSource,
            WitchPeekPending = silver.WitchPeekedCardId is not null,
            WitchPeekedValue = silver.WitchPeekedCardId is { } peekedId && knowledge.TryGetValue(peekedId, out var peekedValue)
                ? peekedValue
                : null,
            RevealerChooserSeat = silver.RevealerChooserSeat,
            CensusCallerIndex = silver.CensusCallerIndex,
            RemainingCensusTurns = silver.RemainingCensusTurns,
            AmuletHolderIndex = silver.AmuletHolderIndex,
            AmuletPlacedCardId = silver.AmuletPlacedCardId,
            AmuletPlaceable = silver.AmuletPlaceable,
            CumulativeScores = silver.CumulativeScores.ToList(),
            LastRoundScores = silver.LastRoundScores?.ToList(),
            LastRoundCensusCallerIndex = silver.LastRoundCensusCallerIndex,
            EliminatedPlayerIndexes = silver.EliminatedPlayerIndexes.ToList(),
            ViewerPeeksUsed = viewerSeat >= 0 && viewerSeat < silver.PeeksUsed.Count ? silver.PeeksUsed[viewerSeat] : 0,
            ViewerIsCurrentPlayer = viewerIsCurrent,
            AbilitiesUsedThisTurn = silver.AbilitiesUsedThisTurn.ToList(),
            ActedThisTurn = silver.ActedThisTurn,
            EventLog = silver.EventLog.ToList(),
            TimerConfig = silver.TimerConfig is { } config ? new SilverTurnTimerConfig
            {
                BaseTurnSeconds = config.BaseTurnSeconds,
                MaxBankSeconds = config.MaxBankSeconds,
                MaxOverrunSeconds = config.MaxOverrunSeconds,
                MaxAfkTurns = config.MaxAfkTurns,
                TotalGameTimeMinutes = config.TotalGameTimeMinutes
            } : null,
            PlayerTimers = silver.PlayerTimers.Select(t => new SilverPlayerTimer
            {
                BankSeconds = t.BankSeconds,
                DeferredPenaltySeconds = t.DeferredPenaltySeconds,
                ConsecutiveTimeouts = t.ConsecutiveTimeouts
            }).ToList(),
            TurnStartUtc = silver.TurnStartUtc
        };
    }
}
