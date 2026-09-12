using System.Text.Json.Serialization;

namespace Silver.Models;

/// <summary>
/// The sub-phase of the current turn. A turn begins in <see cref="TurnStart"/>;
/// drawing from the deck opens a follow-up decision (discard vs replace,
/// optionally a Trickster choice first); taking the discard pile top or a
/// face-up Squire card opens the mandatory replacement; discarding a 5-12 card
/// from the deck opens its one-shot ability window.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TurnPhase
{
    /// <summary>Nothing drawn yet: draw, take the discard top, take a Squire card, call a census, or use an available ability.</summary>
    TurnStart,

    /// <summary>A Trickster draw picked up multiple cards; the player must choose which one to keep.</summary>
    TricksterChoice,

    /// <summary>A deck card is drawn and pending: discard it or replace village cards with it.</summary>
    DrawnDecision,

    /// <summary>The discard-pile top or a Squire-displayed card was taken: replacing village cards with it is mandatory.</summary>
    ExchangeDecision,

    /// <summary>A 5-12 card was discarded straight from the deck; its ability may be used once or declined.</summary>
    AbilityPending
}

/// <summary>Where the pending card came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrawSource
{
    /// <summary>The pending card was drawn from the deck.</summary>
    Deck,

    /// <summary>The pending card was taken from the discard pile top (no ability window).</summary>
    Discard,

    /// <summary>The pending card was taken from the Squire display area (no ability window).</summary>
    Display
}

/// <summary>
/// Per-game timer configuration for Silver sessions: turn clock and total game
/// duration. Same shape and keys as the UNO configuration so room settings can
/// treat games uniformly.
/// </summary>
public class SilverTurnTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 90;

    /// <summary>
    /// Maximum total turn allowance (base + banked time), in seconds. The bank's
    /// effective capacity is this value minus <see cref="BaseTurnSeconds"/>.
    /// </summary>
    public double MaxBankSeconds { get; set; } = 180;

    /// <summary>Maximum allowed overshoot before the next turn is shortened, in seconds.</summary>
    public double MaxOverrunSeconds { get; set; } = 15;

    /// <summary>Skipped turns after which a player is treated as AFK and eliminated.</summary>
    public int MaxAfkTurns { get; set; } = 3;

    /// <summary>Total allowed game duration before the game is force-finished, in minutes.</summary>
    public double TotalGameTimeMinutes { get; set; } = 60;
}

/// <summary>Per-player turn timer accounting (mirrors the UNO model).</summary>
public class SilverPlayerTimer
{
    /// <summary>Time banked from fast turns; a turn's total allowance is capped at <see cref="SilverTurnTimerConfig.MaxBankSeconds"/>.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after a played turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}

/// <summary>
/// The authoritative Silver state. Serialized as a JSON string into
/// <c>GameState.Data["SilverState"]</c>; contains secrets (face-down cards,
/// per-player knowledge, deck order, removed cards) that must reach clients
/// only through the player-view projection, never directly.
/// </summary>
public class SilverState
{
    /// <summary>The current round, 1-4.</summary>
    public int Round { get; set; } = 1;

    /// <summary>The seat that started the current round (used by the amulet tie-break).</summary>
    public int RoundStartPlayerIndex { get; set; }

    /// <summary>Index of the player whose turn it is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>The current turn sub-phase.</summary>
    public TurnPhase Phase { get; set; } = TurnPhase.TurnStart;

    /// <summary>Each player's village (ordered row of cards), indexed by seat.</summary>
    public List<List<SilverCard>> Villages { get; set; } = new();

    /// <summary>The draw deck; the top of the deck is the LAST element.</summary>
    public List<SilverCard> Deck { get; set; } = new();

    /// <summary>The discard pile; the top card is the LAST element.</summary>
    public List<SilverCard> Discard { get; set; } = new();

    /// <summary>
    /// Cards revealed face up beside the deck by face-up Squires. A separate
    /// area from the discard pile: takeable instead of drawing from the deck,
    /// but never a discard-pile card and never deck order.
    /// </summary>
    public List<SilverCard> Display { get; set; } = new();

    /// <summary>Cards removed from the game this round by the player-count rule (never playable).</summary>
    public List<SilverCard> Removed { get; set; } = new();

    /// <summary>Cards drawn this turn and not yet resolved (1, or several while a Trickster choice is pending).</summary>
    public List<SilverCard> PendingDraw { get; set; } = new();

    /// <summary>Where the pending card came from.</summary>
    public DrawSource PendingSource { get; set; } = DrawSource.Deck;

    /// <summary>The discarded card whose 5-12 ability window is open, when <see cref="Phase"/> is AbilityPending.</summary>
    public Guid? DiscardedAbilityCardId { get; set; }

    /// <summary>
    /// The Witch's pending peek: the id of the deck-top card the Witch player
    /// has looked at but not yet exchanged (the card stays on top of the deck
    /// until the exchange or the decline).
    /// </summary>
    public Guid? WitchPeekedCardId { get; set; }

    /// <summary>Seat whose card the Revealer target must choose, while that choice is pending.</summary>
    public int? RevealerChooserSeat { get; set; }

    /// <summary>
    /// Explicit guard-protection relationships: guard card id → protected
    /// card id. Both cards stay in the guard owner's village. A protected
    /// card may not be viewed or moved by anyone except its owner.
    /// </summary>
    public Dictionary<Guid, Guid> Guards { get; set; } = new();

    /// <summary>Seat of the player who called a census, when the round is in its final turns; null otherwise.</summary>
    public int? CensusCallerIndex { get; set; }

    /// <summary>How many final turns remain after a census call (one per other non-eliminated player).</summary>
    public int RemainingCensusTurns { get; set; }

    /// <summary>Seat of the Silver Amulet holder; assigned to the starting player in round 1.</summary>
    public int? AmuletHolderIndex { get; set; }

    /// <summary>The card instance the amulet is placed on this round, when placed.</summary>
    public Guid? AmuletPlacedCardId { get; set; }

    /// <summary>Whether the amulet holder earned the placement right (they successfully called the previous round's census).</summary>
    public bool AmuletPlaceable { get; set; }

    /// <summary>Cumulative scores per seat across completed rounds.</summary>
    public List<int> CumulativeScores { get; set; } = new();

    /// <summary>The per-seat scores of the most recently completed round, for display.</summary>
    public List<int>? LastRoundScores { get; set; }

    /// <summary>The seat that called the census in the most recently completed round, for display.</summary>
    public int? LastRoundCensusCallerIndex { get; set; }

    /// <summary>
    /// Per-seat knowledge: seat → (card instance id → known value). A player
    /// knows a card they peeked, exchanged in from the deck, viewed as the
    /// Witch, or received as the Robber; the map follows card instances.
    /// Cleared between rounds (each round is a fresh deal).
    /// </summary>
    public List<Dictionary<Guid, int>> Knowledge { get; set; } = new();

    /// <summary>How many of the two per-round initial peeks each seat has used, indexed by seat.</summary>
    public List<int> PeeksUsed { get; set; } = new();

    /// <summary>
    /// Per-instance ability uses already spent this turn. Plain names for
    /// singleton semantics, "Ability:{cardId}" for per-card-instance rights
    /// (one use per face-up Enchanter / per face-up Guard per turn).
    /// </summary>
    public List<string> AbilitiesUsedThisTurn { get; set; } = new();

    /// <summary>Whether an ability was used or the amulet was placed this turn; blocks calling a census.</summary>
    public bool ActedThisTurn { get; set; }

    /// <summary>Player seats removed from the game (AFK timeouts). Their villages are still scored.</summary>
    public List<int> EliminatedPlayerIndexes { get; set; } = new();

    /// <summary>Persistent game event log for UI (public-safe entries only).</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>
    /// Optional RNG seed from game settings (deterministic testing per the
    /// rules spec). Null means non-deterministic shuffles.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>Turn timer configuration for this session (derived from game settings).</summary>
    public SilverTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting, indexed by seat.</summary>
    public List<SilverPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started. Used to compute elapsed time.</summary>
    public DateTime TurnStartUtc { get; set; }

    /// <summary>UTC deadline for the current player's action. Mirrored onto GameState.NextActionDeadlineUtc.</summary>
    public DateTime? NextActionDeadlineUtc { get; set; }

    /// <summary>UTC moment at which the game must be force-finished. Mirrored onto GameState.GameEndsAtUtc.</summary>
    public DateTime? GameEndsAtUtc { get; set; }

    /// <summary>Player information for display (not serialized).</summary>
    [JsonIgnore]
    public Dictionary<Guid, string>? PlayerNames { get; set; }

    /// <summary>Player IDs in seat order (not serialized).</summary>
    [JsonIgnore]
    public IReadOnlyList<GameEngine.Core.Models.PlayerId>? PlayerIds { get; set; }

    /// <summary>Serializes the game state to JSON for storage in GameState.Data.</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);

    /// <summary>Deserializes from JSON.</summary>
    public static SilverState FromJson(string json)
        => System.Text.Json.JsonSerializer.Deserialize<SilverState>(json) ?? new SilverState();

    /// <summary>Gets the top card of the discard pile, or null when empty.</summary>
    public SilverCard? DiscardTop => Discard.Count > 0 ? Discard[^1] : null;

    /// <summary>Draws the top card of the deck, or null when the deck is empty.</summary>
    public SilverCard? DrawTop()
    {
        if (Deck.Count == 0) return null;
        var card = Deck[^1];
        Deck.RemoveAt(Deck.Count - 1);
        return card;
    }

    /// <summary>
    /// Returns a card to the TOP of the deck (Trickster returns preserve the
    /// drawn order; the first card drawn ends up on top again).
    /// </summary>
    public void AddToDeckTop(SilverCard card)
    {
        Deck.Add(card);
    }

    /// <summary>How many face-up cards with the given value sit in any village (public table state).</summary>
    public int FaceUpInVillages(int value)
    {
        return Villages.Sum(v => v.Count(c => c.FaceUp && c.Value == value));
    }

    /// <summary>Whether the given card is currently protected by a Guard or the Silver Amulet.</summary>
    public bool IsProtected(SilverCard card)
    {
        return card.Id == AmuletPlacedCardId || Guards.ContainsValue(card.Id);
    }

    /// <summary>
    /// Whether the given card is protected from everyone INCLUDING its owner
    /// (only the Silver Amulet achieves that; a Guard's owner may still
    /// look at / move / discard the card it protects).
    /// </summary>
    public bool IsAmuletProtected(SilverCard card)
    {
        return card.Id == AmuletPlacedCardId;
    }

    /// <summary>The guard card id protecting the given card, when a Guard covers it.</summary>
    public Guid? GuardCovering(SilverCard card)
    {
        foreach (var link in Guards)
        {
            if (link.Value == card.Id) return link.Key;
        }
        return null;
    }

    /// <summary>
    /// Drops guard links whose guard card or protected card no longer sits in
    /// any village (they were discarded, stolen away, etc).
    /// </summary>
    public void CleanupGuards()
    {
        if (Guards.Count == 0) return;
        var inVillage = new HashSet<Guid>();
        foreach (var village in Villages)
        {
            foreach (var card in village) inVillage.Add(card.Id);
        }
        foreach (var link in Guards.Where(l => !inVillage.Contains(l.Key) || !inVillage.Contains(l.Value)).ToList())
        {
            Guards.Remove(link.Key);
        }
    }

    /// <summary>Gets (creating if needed) the timer entry for a seat.</summary>
    public SilverPlayerTimer EnsureTimer(int seat)
    {
        while (PlayerTimers.Count <= seat)
        {
            PlayerTimers.Add(new SilverPlayerTimer());
        }
        return PlayerTimers[seat];
    }

    /// <summary>
    /// The seconds the given player is allotted for their current turn. The
    /// total allowance (base + bank - penalty) can never exceed
    /// <see cref="SilverTurnTimerConfig.MaxBankSeconds"/>.
    /// </summary>
    public double MaxTurnSeconds(int seat, SilverTurnTimerConfig config)
    {
        var timer = EnsureTimer(seat);
        var raw = config.BaseTurnSeconds + timer.BankSeconds - timer.DeferredPenaltySeconds;
        var floor = Math.Max(0, config.BaseTurnSeconds - config.MaxOverrunSeconds);
        return Math.Max(floor, Math.Min(config.MaxBankSeconds, raw));
    }

    /// <summary>
    /// Starts the clock for the current player and sets the action deadline.
    /// The deadline is the HARD limit: the player's allowance plus the grace
    /// window. Inside the grace window the player may still act (the countdown
    /// shows negative time) and the overshoot is deducted from their next
    /// allowance; the turn is force-skipped only once the hard deadline passes.
    /// </summary>
    public void SetTurnClock()
    {
        var config = TimerConfig ?? new SilverTurnTimerConfig();
        TurnStartUtc = DateTime.UtcNow;
        NextActionDeadlineUtc = DateTime.UtcNow
            .AddSeconds(MaxTurnSeconds(CurrentPlayerIndex, config))
            .AddSeconds(config.MaxOverrunSeconds);
    }

    /// <summary>
    /// Applies turn-timer accounting after a valid turn action: banks unused
    /// time (capped) or charges an overrun (bank first, then deferred penalty),
    /// then starts a fresh clock for the next player when the turn has ended.
    /// </summary>
    public void ApplyTurnTimeAccounting(int seat, bool turnEnded)
    {
        var config = TimerConfig ?? new SilverTurnTimerConfig();
        var timer = EnsureTimer(seat);

        if (TurnStartUtc != default)
        {
            var elapsed = DateTime.UtcNow - TurnStartUtc;
            var allotted = MaxTurnSeconds(seat, config);

            if (elapsed.TotalSeconds <= allotted)
            {
                var saved = Math.Max(0, allotted - elapsed.TotalSeconds);
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
