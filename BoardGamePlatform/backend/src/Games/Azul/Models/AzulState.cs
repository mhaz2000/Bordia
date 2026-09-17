using System.Text.Json.Serialization;
using GameEngine.Core.Models;

namespace Azul.Models;

/// <summary>
/// One player's Azul board: the 5x5 wall (row = pattern line), the five
/// staircase pattern lines (length 1..5), the 7-space floor line, and their
/// running score. All of it is public information (spec §16).
/// </summary>
public sealed class AzulSeatState
{
    public AzulSeatState()
    {
        Wall = Enumerable.Range(0, AzulCatalog.WallSize)
            .Select(_ => Enumerable.Range(0, AzulCatalog.WallSize).Select(_ => -1).ToList())
            .ToList();
        PatternLines = Enumerable.Range(0, AzulCatalog.LineCount)
            .Select(_ => new List<int>())
            .ToList();
        Floor = new List<int>();
    }

    /// <summary>5 rows x 5 columns; -1 = empty, else the tile color. Row index == pattern line index.</summary>
    public List<List<int>> Wall { get; set; }

    /// <summary>The five pattern lines; line l holds at most l+1 tiles, all one color; incomplete lines persist across rounds.</summary>
    public List<List<int>> PatternLines { get; set; }

    /// <summary>Floor tiles (max <see cref="AzulCatalog.FloorCapacity"/>); the first-player marker occupies its own separate space.</summary>
    public List<int> Floor { get; set; }

    /// <summary>Current score (clamped at 0 on floor penalties, spec §11 OD-7).</summary>
    public int Score { get; set; }

    /// <summary>The single color filling a line, or -1 when empty.</summary>
    public int LineColor(int line) => PatternLines[line].Count > 0 ? PatternLines[line][0] : -1;

    /// <summary>Capacity of a pattern line (its length), 1..5.</summary>
    public static int LineCapacity(int line) => line + 1;

    /// <summary>Whether the line is filled to capacity (completes only at the tiling phase).</summary>
    public bool IsLineComplete(int line) => PatternLines[line].Count == LineCapacity(line);

    /// <summary>Whether the color is already on the wall row fed by this pattern line (spec §9.4).</summary>
    public bool WallRowHasColor(int line, int color) => Wall[line].Contains(color);

    /// <summary>A line can receive the color now: not full, empty-or-same-color, and the wall row lacks the color.</summary>
    public bool CanPlace(int line, int color)
        => line >= 0 && line < AzulCatalog.LineCount
           && PatternLines[line].Count < LineCapacity(line)
           && (PatternLines[line].Count == 0 || LineColor(line) == color)
           && !WallRowHasColor(line, color);

    /// <summary>Whether any pattern line can legally receive the color this turn (drives the forced-floor sentinel, §8.3).</summary>
    public bool HasAnyLegalLine(int color)
    {
        for (var line = 0; line < AzulCatalog.LineCount; line++)
        {
            if (CanPlace(line, color))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Count of full horizontal wall rows (game-end trigger + tie-break, spec §14).</summary>
    public int CompletedHorizontalRows()
        => Wall.Count(row => row.All(cell => cell >= 0));

    /// <summary>Whether any single wall row is complete (the end trigger).</summary>
    public bool HasCompletedRow() => CompletedHorizontalRows() > 0;

    /// <summary>Count of full vertical wall columns.</summary>
    public int CompletedVerticalColumns()
    {
        var columns = 0;
        for (var col = 0; col < AzulCatalog.WallSize; col++)
        {
            var full = true;
            for (var row = 0; row < AzulCatalog.WallSize; row++)
            {
                if (Wall[row][col] < 0)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                columns++;
            }
        }

        return columns;
    }

    /// <summary>Count of colors with all five copies on the wall.</summary>
    public int CompletedColorSets()
    {
        var sets = 0;
        for (var color = 0; color < AzulCatalog.ColorCount; color++)
        {
            if (CountColorOnWall(color) == AzulCatalog.WallSize)
            {
                sets++;
            }
        }

        return sets;
    }

    /// <summary>How many tiles of a color are on the wall.</summary>
    public int CountColorOnWall(int color)
    {
        var count = 0;
        foreach (var row in Wall)
        {
            count += row.Count(cell => cell == color);
        }

        return count;
    }

    /// <summary>Total tiles occupying wall cells.</summary>
    public int WallTiles() => Wall.Sum(row => row.Count(cell => cell >= 0));

    /// <summary>Total tiles still sitting in the pattern lines (persist across rounds).</summary>
    public int PatternTiles() => PatternLines.Sum(line => line.Count);
}

/// <summary>
/// Per-game timer configuration for Azul sessions. Same shape and keys as the
/// UNO/Silver/Splendor configuration so room settings treat games uniformly.
/// The defaults are UX configuration, not rules (spec §24).
/// </summary>
public class AzulTurnTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum total turn allowance (base + banked time), in seconds.
    /// </summary>
    public double MaxBankSeconds { get; set; } = 180;

    /// <summary>Maximum allowed overshoot before the next turn is shortened, in seconds.</summary>
    public double MaxOverrunSeconds { get; set; } = 15;

    /// <summary>Skipped turns after which a player is treated as AFK and eliminated.</summary>
    public int MaxAfkTurns { get; set; } = 3;

    /// <summary>Total allowed game duration before the game is force-finished, in minutes.</summary>
    public double TotalGameTimeMinutes { get; set; } = 60;
}

/// <summary>Per-player turn timer accounting (mirrors the UNO/Silver/Splendor model).</summary>
public class AzulPlayerTimer
{
    /// <summary>Time banked from fast turns; a turn's total allowance is capped at MaxBankSeconds.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after an acted turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}

/// <summary>
/// The authoritative Azul state, serialized as a JSON string into
/// <c>GameState.Data["AzulState"]</c>. The only secret is the <see cref="Bag"/>
/// draw ORDER (contents/counts are effectively public, but the order is not) —
/// it must never reach clients except through the projection (spec §16), which
/// replaces it with <c>BagCount</c>. Everything else is public board state.
/// </summary>
public class AzulState
{
    /// <summary>1-based round counter.</summary>
    public int RoundNumber { get; set; } = 1;

    /// <summary>Index of the player whose turn it currently is.</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>Active factory displays (5 | 7 | 9), fixed at creation by player count.</summary>
    public int FactoryCount { get; set; }

    /// <summary>
    /// The draw bag: colors in draw order, FRONT (index 0) = next drawn. The
    /// order is the game's only hidden information and is redacted in the view.
    /// </summary>
    public List<int> Bag { get; set; } = new();

    /// <summary>The factory displays, each a list of up to 4 tile colors (empty after being drafted).</summary>
    public List<List<int>> Factories { get; set; } = new();

    /// <summary>The central leftover pool: tiles dumped here from drafted factories; drafted as a group by color.</summary>
    public List<int> Center { get; set; } = new();

    /// <summary>The discard pool: off-floor overflow plus completed-line leftovers; reshuffled into the bag when it runs short (§13).</summary>
    public List<int> Discard { get; set; } = new();

    /// <summary>-1 while the first-player marker sits in the center; otherwise the seat currently holding it this round.</summary>
    public int MarkerSeat { get; set; } = -1;

    /// <summary>The seat that starts the current round (carried from the marker holder at the previous round's end).</summary>
    public int FirstPlayerSeat { get; set; }

    /// <summary>Per-seat boards, in seat order.</summary>
    public List<AzulSeatState> Seats { get; set; } = new();

    /// <summary>Seats removed by AFK timeouts (spec §24).</summary>
    public List<int> EliminatedSeats { get; set; } = new();

    /// <summary>Persistent public-safe game event log.</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>Optional RNG seed from game settings (deterministic testing, §17). Null = fresh shuffles.</summary>
    public int? Seed { get; set; }

    /// <summary>Turn timer configuration for this session.</summary>
    public AzulTurnTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting, indexed by seat.</summary>
    public List<AzulPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started.</summary>
    public DateTime TurnStartUtc { get; set; }

    /// <summary>UTC deadline for the current player's action. Mirrored onto GameState.NextActionDeadlineUtc.</summary>
    public DateTime? NextActionDeadlineUtc { get; set; }

    /// <summary>UTC moment at which the game is force-finished. Mirrored onto GameState.GameEndsAtUtc.</summary>
    public DateTime? GameEndsAtUtc { get; set; }

    /// <summary>Player display names for event text (not serialized).</summary>
    [JsonIgnore]
    public Dictionary<Guid, string>? PlayerNames { get; set; }

    /// <summary>Player ids in seat order (not serialized).</summary>
    [JsonIgnore]
    public IReadOnlyList<PlayerId>? PlayerIds { get; set; }

    /// <summary>Serializes the game state to JSON for storage in GameState.Data.</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);

    /// <summary>Deserializes from JSON.</summary>
    public static AzulState FromJson(string json)
        => System.Text.Json.JsonSerializer.Deserialize<AzulState>(json) ?? new AzulState();

    /// <summary>Number of seats in the session.</summary>
    public int PlayerCount => Seats.Count;

    /// <summary>Whether the seat is still active (not eliminated).</summary>
    public bool IsActive(int seat) => !EliminatedSeats.Contains(seat);

    /// <summary>Seats that still take turns, in seat order.</summary>
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

    /// <summary>Whether no tile remains in any factory or the center (the round's drafting is over, spec §13).</summary>
    public bool PoolEmpty()
        => Center.Count == 0 && Factories.All(f => f.Count == 0);

    /// <summary>Draws the front tile of the bag (next in shuffled order); -1 when exhausted.</summary>
    public int DrawFromBag()
    {
        if (Bag.Count == 0)
        {
            return -1;
        }

        var tile = Bag[0];
        Bag.RemoveAt(0);
        return tile;
    }

    /// <summary>Reshuffles the discard pool back into the bag (append + shuffle) when the bag runs short on refill (§13).</summary>
    public void ReclaimDiscards(Random rng)
    {
        if (Discard.Count == 0)
        {
            return;
        }

        Bag.AddRange(Discard);
        Discard.Clear();
        Shuffle(Bag, rng);
    }

    /// <summary>Gets (creating if needed) the timer entry for a seat.</summary>
    public AzulPlayerTimer EnsureTimer(int seat)
    {
        while (PlayerTimers.Count <= seat)
        {
            PlayerTimers.Add(new AzulPlayerTimer());
        }

        return PlayerTimers[seat];
    }

    /// <summary>The seconds the given player is allotted for the current turn (base + bank - penalty, capped).</summary>
    public double MaxTurnSeconds(int seat, AzulTurnTimerConfig config)
    {
        var timer = EnsureTimer(seat);
        var raw = config.BaseTurnSeconds + timer.BankSeconds - timer.DeferredPenaltySeconds;
        var floor = Math.Max(0, config.BaseTurnSeconds - config.MaxOverrunSeconds);
        return Math.Max(floor, Math.Min(config.MaxBankSeconds, raw));
    }

    /// <summary>Starts the clock for the current player (allowance + grace hard deadline).</summary>
    public void SetTurnClock()
    {
        var config = TimerConfig ?? new AzulTurnTimerConfig();
        TurnStartUtc = DateTime.UtcNow;
        NextActionDeadlineUtc = DateTime.UtcNow
            .AddSeconds(MaxTurnSeconds(CurrentPlayerIndex, config))
            .AddSeconds(config.MaxOverrunSeconds);
    }

    /// <summary>Applies turn-timer accounting after a valid player action, then (optionally) starts a fresh clock.</summary>
    public void ApplyTurnTimeAccounting(int seat, bool turnEnded)
    {
        var config = TimerConfig ?? new AzulTurnTimerConfig();
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

    /// <summary>Fisher-Yates shuffle over a tile list with the supplied RNG.</summary>
    public static void Shuffle(List<int> tiles, Random rng)
    {
        for (var i = tiles.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
    }
}
