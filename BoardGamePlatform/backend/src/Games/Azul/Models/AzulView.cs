namespace Azul.Models;

/// <summary>One player's board as delivered to clients (all public, spec §16).</summary>
public sealed class AzulSeatView
{
    /// <summary>5 rows x 5 columns wall; -1 empty, else the tile color.</summary>
    public List<List<int>> Wall { get; set; } = new();

    /// <summary>The five pattern lines (colors), incomplete ones persist across rounds.</summary>
    public List<List<int>> PatternLines { get; set; } = new();

    /// <summary>Floor tiles (max 7).</summary>
    public List<int> Floor { get; set; } = new();

    /// <summary>Current score.</summary>
    public int Score { get; set; }
}

/// <summary>
/// The client-visible projection of an Azul game: the full public board with
/// the bag's draw ORDER stripped (only <see cref="BagCount"/> survives), since
/// that is the single hidden piece of information (spec §16). The projection is
/// viewer-independent (like Splendor). A client must never receive a state
/// carrying a raw <c>Bag</c> array — the frontend rejects such authoritative-
/// shaped payloads (platform 2026-09-12 leak-guard precedent).
/// </summary>
public sealed class AzulView
{
    /// <summary>1-based round counter.</summary>
    public int RoundNumber { get; set; }

    /// <summary>Index of the player whose turn it currently is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>Active factory displays (5 | 7 | 9).</summary>
    public int FactoryCount { get; set; }

    /// <summary>Number of tiles left in the bag (contents/order are never sent).</summary>
    public int BagCount { get; set; }

    /// <summary>The factory displays (contents per color).</summary>
    public List<List<int>> Factories { get; set; } = new();

    /// <summary>The central leftover pool.</summary>
    public List<int> Center { get; set; } = new();

    /// <summary>Count of tiles in the discard pool (contents need not be public).</summary>
    public int DiscardCount { get; set; }

    /// <summary>-1 = marker in the center; else the seat holding it this round.</summary>
    public int MarkerSeat { get; set; } = -1;

    /// <summary>The seat that starts the current round.</summary>
    public int FirstPlayerSeat { get; set; }

    /// <summary>Per-seat boards, in seat order.</summary>
    public List<AzulSeatView> Seats { get; set; } = new();

    /// <summary>Seats removed by AFK timeouts.</summary>
    public List<int> EliminatedSeats { get; set; } = new();

    /// <summary>Public-safe event log.</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>Seat index of the requesting viewer (convenience; -1 when unknown).</summary>
    public int ViewerSeat { get; set; } = -1;

    /// <summary>Whether the viewer is the current player (convenience flag).</summary>
    public bool ViewerIsCurrentPlayer { get; set; }

    /// <summary>Turn timer configuration (public; clients derive the soft deadline).</summary>
    public AzulTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting (public).</summary>
    public List<AzulPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started.</summary>
    public DateTime TurnStartUtc { get; set; }
}
