using System.Text.Json.Serialization;
using GameEngine.Core.Models;

namespace UNO.Models;

/// <summary>
/// Represents the direction of play.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayDirection
{
    /// <summary>Clockwise (increasing player index).</summary>
    Clockwise = 1,

    /// <summary>Counter-clockwise (decreasing player index).</summary>
    CounterClockwise = -1
}

/// <summary>
/// Represents the current state of a UNO game.
/// This is serialized into GameState.Data.
/// </summary>
public class UnoGameState
{
    /// <summary>
    /// The draw pile.
    /// </summary>
    public Deck DrawPile { get; set; } = new();

    /// <summary>
    /// The discard pile (top card is last element).
    /// </summary>
    public List<Card> DiscardPile { get; set; } = new();

    /// <summary>
    /// Each player's hand, indexed by player position.
    /// </summary>
    public List<PlayerHand> PlayerHands { get; set; } = new();

    /// <summary>
    /// The color currently in effect (set by Wild/Wild Draw Four).
    /// Null means the card's natural color is in effect.
    /// </summary>
    public CardColor? CurrentColor { get; set; }

    /// <summary>
    /// Current direction of play.
    /// </summary>
    public PlayDirection Direction { get; set; } = PlayDirection.Clockwise;

    /// <summary>
    /// Index of the player whose turn it is.
    /// </summary>
    public int CurrentPlayerIndex { get; set; } = 0;

    /// <summary>
    /// Number of cards the next player must draw due to Draw Two / Wild Draw Four stacking.
    /// </summary>
    public int PendingDrawCount { get; set; } = 0;

    /// <summary>
    /// When a Wild Draw Four challenge succeeds, the player position who must draw
    /// the penalty (the offender who played the card) instead of the challenger.
    /// </summary>
    public int? PendingDrawOffenderIndex { get; set; } = null;

    /// <summary>
    /// Whether the next player is skipped.
    /// </summary>
    public bool NextPlayerSkipped { get; set; } = false;

    /// <summary>
    /// Whether the current player has called UNO (when they have 1 card).
    /// </summary>
    public bool UnoCalled { get; set; } = false;

    /// <summary>
    /// The player index who must call UNO (set when they play down to 1 card).
    /// </summary>
    public int? UnoPendingPlayerIndex { get; set; } = null;

    /// <summary>
    /// Game event log for UI.
    /// </summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>
    /// Turn timer configuration for this session (derived from game settings).
    /// </summary>
    public UnoTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>
    /// Per-player timer accounting, indexed by player position.
    /// </summary>
    public List<UnoPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>
    /// UTC moment the current turn started. Used to compute elapsed time.
    /// </summary>
    public DateTime TurnStartUtc { get; set; }

    /// <summary>
    /// UTC moment at which the game must be force-finished. Mirrored onto
    /// <see cref="GameEngine.Core.Models.GameState.GameEndsAtUtc"/> so the Game
    /// Service can poll a game-agnostic deadline.
    /// </summary>
    public DateTime? GameEndsAtUtc { get; set; }

    /// <summary>
    /// UTC deadline for the current player's action. Mirrored onto
    /// <see cref="GameState.NextActionDeadlineUtc"/> so the Game Service can
    /// poll a game-agnostic deadline.
    /// </summary>
    public DateTime? NextActionDeadlineUtc { get; set; }

    /// <summary>
    /// Whether the current player already took their voluntary draw this turn
    /// (one draw per turn). Reset whenever the turn moves to the next player.
    /// </summary>
    public bool DrawnThisTurn { get; set; }

    /// <summary>
    /// Player positions removed from the game (e.g. AFK timeouts).
    /// </summary>
    public List<int> EliminatedPlayerIndexes { get; set; } = new();

    /// <summary>
    /// Player information for display (not serialized).
    /// Maps UserId to DisplayName.
    /// </summary>
    [JsonIgnore]
    public Dictionary<Guid, string>? PlayerNames { get; set; }

    /// <summary>
    /// Player IDs in seat order (not serialized).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<PlayerId>? PlayerIds { get; set; }

    /// <summary>
    /// Serializes the game state to JSON for storage in GameState.Data.
    /// </summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserializes from JSON.
    /// </summary>
    public static UnoGameState FromJson(string json)
        => System.Text.Json.JsonSerializer.Deserialize<UnoGameState>(json) ?? new UnoGameState();

    /// <summary>
    /// Gets the top card of the discard pile.
    /// </summary>
    public Card? TopCard => DiscardPile.Count > 0 ? DiscardPile[^1] : null;

    /// <summary>
    /// Gets the next player index based on current direction.
    /// </summary>
    public int GetNextPlayerIndex(int playerCount)
    {
        var next = CurrentPlayerIndex + (int)Direction;
        if (next >= playerCount) next = 0;
        if (next < 0) next = playerCount - 1;
        return next;
    }

    /// <summary>
    /// Advances to the next player, handling skips and direction.
    /// Eliminated players are transparently skipped.
    /// </summary>
    public void AdvancePlayer(int playerCount)
    {
        DrawnThisTurn = false;
        if (NextPlayerSkipped)
        {
            NextPlayerSkipped = false;
            CurrentPlayerIndex = GetNextPlayerIndex(playerCount);
        }
        CurrentPlayerIndex = AdvanceToNextEligible(playerCount);
    }

    /// <summary>
    /// Advances <see cref="CurrentPlayerIndex"/> to the next non-eliminated player.
    /// </summary>
    public int AdvanceToNextEligible(int playerCount)
    {
        var guard = 0;
        do
        {
            CurrentPlayerIndex = GetNextPlayerIndex(playerCount);
            guard++;
        } while (EliminatedPlayerIndexes.Contains(CurrentPlayerIndex) && guard < playerCount);
        return CurrentPlayerIndex;
    }

    /// <summary>
    /// Gets (creating if needed) the timer entry for a player position.
    /// </summary>
    public UnoPlayerTimer EnsureTimer(int playerIndex)
    {
        while (PlayerTimers.Count <= playerIndex)
        {
            PlayerTimers.Add(new UnoPlayerTimer());
        }
        return PlayerTimers[playerIndex];
    }

    /// <summary>
    /// The seconds the given player is allotted for their current turn.
    /// The total allowance (base + bank - penalty) can never exceed
    /// <see cref="UnoTurnTimerConfig.MaxBankSeconds"/>, so the bank cannot grow a
    /// single turn past the configured ceiling.
    /// </summary>
    public double MaxTurnSeconds(int playerIndex, UnoTurnTimerConfig config)
    {
        var timer = EnsureTimer(playerIndex);
        var raw = config.BaseTurnSeconds + timer.BankSeconds - timer.DeferredPenaltySeconds;
        var floor = Math.Max(0, config.BaseTurnSeconds - config.MaxOverrunSeconds);
        return Math.Max(floor, Math.Min(config.MaxBankSeconds, raw));
    }

    /// <summary>
    /// Starts the clock for the current player and sets the action deadline.
    /// </summary>
    public void SetTurnClock()
    {
        var config = TimerConfig ?? new UnoTurnTimerConfig();
        TurnStartUtc = DateTime.UtcNow;
        NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(MaxTurnSeconds(CurrentPlayerIndex, config));
    }

    /// <summary>
    /// Applies turn-timer accounting after a valid turn action: banks unused time
    /// (capped) or charges an overrun (bank first, then deferred penalty), then
    /// starts a fresh clock for the next player when the turn has ended.
    /// </summary>
    public void ApplyTurnTimeAccounting(int actorIndex, bool turnEnded)
    {
        var config = TimerConfig ?? new UnoTurnTimerConfig();
        var timer = EnsureTimer(actorIndex);

        if (TurnStartUtc != default)
        {
            var elapsed = DateTime.UtcNow - TurnStartUtc;
            var allotted = MaxTurnSeconds(actorIndex, config);

            if (elapsed.TotalSeconds <= allotted)
            {
                var saved = Math.Max(0, allotted - elapsed.TotalSeconds);
                // The bank can never push a future turn past MaxBankSeconds total
                // (base + bank), so its effective capacity is the difference.
                var bankCapacity = Math.Max(0, config.MaxBankSeconds - config.BaseTurnSeconds);
                timer.BankSeconds = Math.Min(bankCapacity, timer.BankSeconds + saved);
            }
            else
            {
                var overrun = elapsed.TotalSeconds - allotted;
                var bankCover = Math.Min(timer.BankSeconds, overrun);
                timer.BankSeconds -= bankCover;
                timer.DeferredPenaltySeconds += Math.Min(config.MaxOverrunSeconds, overrun - bankCover);
            }

            timer.ConsecutiveTimeouts = 0;
        }

        if (turnEnded)
        {
            SetTurnClock();
        }
    }
}