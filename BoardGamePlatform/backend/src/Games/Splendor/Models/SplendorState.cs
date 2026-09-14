using System.Text.Json.Serialization;

namespace Splendor.Models;

/// <summary>How a reserved card entered a player's hand.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SplendorReservationSource
{
    /// <summary>Taken face-up from the market (its identity is public).</summary>
    Market,

    /// <summary>Drawn blind from a tier deck (its identity is secret to everyone until purchased).</summary>
    Deck
}

/// <summary>A card held in a player's reservation area (max three per player).</summary>
public sealed class SplendorReservation
{
    /// <summary>The catalogue/instance id of the reserved card.</summary>
    public string CardId { get; set; } = string.Empty;

    /// <summary>The card's tier (1-3) — public even for blind reservations.</summary>
    public int Tier { get; set; }

    /// <summary>Whether the reservation was taken from the market or drawn blind from a deck.</summary>
    public SplendorReservationSource Source { get; set; }
}

/// <summary>One seat's private-and-public objects: tokens, reservations, tableau, nobles.</summary>
public sealed class SplendorSeatState
{
    /// <summary>Tokens held by this player (gold included in TotalWithGold).</summary>
    public SplendorGems Tokens { get; set; } = SplendorGems.Empty();

    /// <summary>Reserved cards (max <see cref="SplendorCatalogue.MaxReservations"/>; never discarded).</summary>
    public List<SplendorReservation> Reserved { get; set; } = new();

    /// <summary>Purchased card ids — permanent, public, source of bonuses and points.</summary>
    public List<string> Purchased { get; set; } = new();

    /// <summary>Claimed noble tile ids — permanent, public, each worth 3 points.</summary>
    public List<string> NoblesOwned { get; set; } = new();

    /// <summary>Permanent bonus count for a gem color (from purchased cards only).</summary>
    public int Bonus(int colorIndex)
    {
        var bonus = 0;
        foreach (var id in Purchased)
        {
            var card = SplendorCatalogue.FindCard(id);
            if (card is not null && (int)card.Bonus == colorIndex)
            {
                bonus++;
            }
        }

        return bonus;
    }

    /// <summary>Total prestige points: purchased card points + 3 per owned noble.</summary>
    public int VictoryPoints
    {
        get
        {
            var vp = NoblesOwned.Count * SplendorCatalogue.NoblePoints;
            foreach (var id in Purchased)
            {
                vp += SplendorCatalogue.FindCard(id)?.Points ?? 0;
            }

            return vp;
        }
    }
}

/// <summary>
/// Per-game timer configuration for Splendor sessions. Same shape and keys as
/// the UNO/Silver configuration so room settings treat games uniformly. The
/// defaults are UX configuration, not rules (spec §20).
/// </summary>
public class SplendorTurnTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 60;

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

/// <summary>Per-player turn timer accounting (mirrors the UNO/Silver model).</summary>
public class SplendorPlayerTimer
{
    /// <summary>Time banked from fast turns; a turn's total allowance is capped at <see cref="SplendorTurnTimerConfig.MaxBankSeconds"/>.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after an acted turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}

/// <summary>
/// The authoritative Splendor state. Serialized as a JSON string into
/// <c>GameState.Data["SplendorState"]</c>; contains secrets (the three deck
/// orders and the ids of blind reservations) that must reach clients only
/// through the viewer-independent projection, never directly (spec §12).
/// </summary>
public class SplendorState
{
    /// <summary>Index of the player whose turn it currently is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>1-based count of completed-plus-current turns (public; display and test aid).</summary>
    public int TurnNumber { get; set; } = 1;

    /// <summary>The central token supply, shared by all players.</summary>
    public SplendorGems Supply { get; set; } = SplendorGems.Empty();

    /// <summary>
    /// Three draw decks indexed by tier minus one; the FRONT (index 0) is the
    /// next card drawn. Deck order is secret to everyone and must be persisted.
    /// </summary>
    public List<List<string>> Decks { get; set; } = new();

    /// <summary>
    /// Twelve market slots: index (tier-1)*4 + position. Null marks a slot whose
    /// tier deck ran out (permanently empty, spec §7).
    /// </summary>
    public List<string?> Market { get; set; } = new();

    /// <summary>Revealed noble tiles still unclaimed, in display order (canonical catalogue order).</summary>
    public List<string> NoblesInMarket { get; set; } = new();

    /// <summary>Per-seat game objects, in seat order.</summary>
    public List<SplendorSeatState> Seats { get; set; } = new();

    /// <summary>Whether some player has crossed the 15-point threshold this game.</summary>
    public bool FinalRoundTriggered { get; set; }

    /// <summary>The seat that triggered the final round, or -1.</summary>
    public int TriggerSeat { get; set; } = -1;

    /// <summary>
    /// Seats still owed their single final turn (every active seat except the
    /// trigger, minus eliminations). The game ends when a turn completes and
    /// this list is empty (spec §11).
    /// </summary>
    public List<int> FinalTurnSeatsPending { get; set; } = new();

    /// <summary>Seats removed from the game by AFK timeouts (spec §20).</summary>
    public List<int> EliminatedSeats { get; set; } = new();

    /// <summary>Persistent game event log for UI (public-safe entries only).</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>
    /// Optional RNG seed from game settings (deterministic testing per the spec,
    /// §13). Null means non-deterministic shuffles.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>Turn timer configuration for this session (derived from game settings).</summary>
    public SplendorTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting, indexed by seat.</summary>
    public List<SplendorPlayerTimer> PlayerTimers { get; set; } = new();

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
    public static SplendorState FromJson(string json)
        => System.Text.Json.JsonSerializer.Deserialize<SplendorState>(json) ?? new SplendorState();

    /// <summary>Number of seats in the session.</summary>
    public int PlayerCount => Seats.Count;

    /// <summary>The market slot index holding the card, or -1.</summary>
    public int MarketSlotOf(string cardId) => Market.IndexOf(cardId);

    /// <summary>Whether the given seat is still active (not eliminated).</summary>
    public bool IsActive(int seat) => !EliminatedSeats.Contains(seat);

    /// <summary>Seats that still take turns.</summary>
    public IEnumerable<int> ActiveSeats()
    {
        for (var i = 0; i < Seats.Count; i++)
        {
            if (IsActive(i))
            {
                yield return i;
            }
        }
    }

    /// <summary>Gets (creating if needed) the timer entry for a seat.</summary>
    public SplendorPlayerTimer EnsureTimer(int seat)
    {
        while (PlayerTimers.Count <= seat)
        {
            PlayerTimers.Add(new SplendorPlayerTimer());
        }

        return PlayerTimers[seat];
    }

    /// <summary>
    /// The seconds the given player is allotted for their current turn. The
    /// total allowance (base + bank - penalty) can never exceed
    /// <see cref="SplendorTurnTimerConfig.MaxBankSeconds"/>.
    /// </summary>
    public double MaxTurnSeconds(int seat, SplendorTurnTimerConfig config)
    {
        var timer = EnsureTimer(seat);
        var raw = config.BaseTurnSeconds + timer.BankSeconds - timer.DeferredPenaltySeconds;
        var floor = Math.Max(0, config.BaseTurnSeconds - config.MaxOverrunSeconds);
        return Math.Max(floor, Math.Min(config.MaxBankSeconds, raw));
    }

    /// <summary>
    /// Starts the clock for the current player and sets the action deadline.
    /// The deadline is the HARD limit: the player's allowance plus the grace
    /// window (platform negative-countdown pattern, spec §20).
    /// </summary>
    public void SetTurnClock()
    {
        var config = TimerConfig ?? new SplendorTurnTimerConfig();
        TurnStartUtc = DateTime.UtcNow;
        NextActionDeadlineUtc = DateTime.UtcNow
            .AddSeconds(MaxTurnSeconds(CurrentPlayerIndex, config))
            .AddSeconds(config.MaxOverrunSeconds);
    }

    /// <summary>
    /// Applies turn-timer accounting after a valid player action: banks unused
    /// time (capped) or charges an overrun (bank first, then deferred penalty),
    /// then starts a fresh clock for the next player when the turn ended.
    /// </summary>
    public void ApplyTurnTimeAccounting(int seat, bool turnEnded)
    {
        var config = TimerConfig ?? new SplendorTurnTimerConfig();
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
