namespace Splendor.Models;

/// <summary>
/// A reserved card as it appears to any client: the tier and source are
/// public; a blind (<see cref="SplendorReservationSource.Deck"/>) reservation
/// carries <see cref="CardId"/> = null — nobody, not even its owner, is
/// entitled to the identity before it is purchased (spec §12).
/// </summary>
public sealed class SplendorViewReservation
{
    /// <summary>The card's tier (1-3).</summary>
    public int Tier { get; set; }

    /// <summary>Whether it came from the market or was drawn blind from a deck.</summary>
    public SplendorReservationSource Source { get; set; }

    /// <summary>The card id for market-sourced (public) reservations; null for blind deck reservations.</summary>
    public string? CardId { get; set; }
}

/// <summary>One seat's projected game area (all public information by definition).</summary>
public sealed class SplendorSeatView
{
    /// <summary>Tokens held by the seat (public: chips sit on the table).</summary>
    public SplendorGems Tokens { get; set; } = SplendorGems.Empty();

    /// <summary>Permanent bonuses per color, in color order (public derived value).</summary>
    public List<int> Bonuses { get; set; } = new();

    /// <summary>Total prestige points (public derived value).</summary>
    public int VictoryPoints { get; set; }

    /// <summary>Purchased development card ids (public tableau).</summary>
    public List<string> Purchased { get; set; } = new();

    /// <summary>Claimed noble tile ids (public).</summary>
    public List<string> NoblesOwned { get; set; } = new();

    /// <summary>Reservation slots with blind identities redacted.</summary>
    public List<SplendorViewReservation> Reserved { get; set; } = new();
}

/// <summary>
/// The client-visible projection of a Splendor game: everything public, with
/// the three secrets removed — the deck orders (only counts survive) and the
/// identities of blind reservations. Unlike Silver the projection does not
/// depend on the viewer (spec §12): the hidden information in Splendor is
/// hidden from everyone, including its owner.
/// </summary>
public sealed class SplendorView
{
    /// <summary>Index of the player whose turn it currently is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>1-based turn counter (public).</summary>
    public int TurnNumber { get; set; }

    /// <summary>The central token supply.</summary>
    public SplendorGems Supply { get; set; } = SplendorGems.Empty();

    /// <summary>Remaining card count per tier (index 0 = Level 1). The order never leaves the server.</summary>
    public List<int> DeckCounts { get; set; } = new();

    /// <summary>The twelve market slots ((tier-1)*4 + position); null = permanently spent.</summary>
    public List<string?> Market { get; set; } = new();

    /// <summary>Unclaimed revealed nobles, in display order.</summary>
    public List<string> NoblesInMarket { get; set; } = new();

    /// <summary>Per-seat public game areas, in seat order.</summary>
    public List<SplendorSeatView> Seats { get; set; } = new();

    /// <summary>Whether the 15-point threshold has been crossed (final round in progress).</summary>
    public bool FinalRoundTriggered { get; set; }

    /// <summary>The seat that triggered the final round, or -1.</summary>
    public int TriggerSeat { get; set; } = -1;

    /// <summary>How many seats are still owed their single final turn.</summary>
    public int FinalTurnsRemaining { get; set; }

    /// <summary>Seats removed from the game by AFK timeouts.</summary>
    public List<int> EliminatedSeats { get; set; } = new();

    /// <summary>Public-safe event log.</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>Seat index of the requesting viewer (convenience; -1 when unknown).</summary>
    public int ViewerSeat { get; set; } = -1;

    /// <summary>Whether the viewer is the current player (convenience derived flag).</summary>
    public bool ViewerIsCurrentPlayer { get; set; }

    /// <summary>Turn timer configuration (public; clients derive the soft deadline).</summary>
    public SplendorTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting (public).</summary>
    public List<SplendorPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started.</summary>
    public DateTime TurnStartUtc { get; set; }
}
