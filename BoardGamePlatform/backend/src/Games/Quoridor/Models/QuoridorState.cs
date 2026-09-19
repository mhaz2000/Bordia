using System.Text.Json.Serialization;
using GameEngine.Core.Models;

namespace Quoridor.Models;

/// <summary>
/// Represents a pawn position on the board.
/// </summary>
public sealed class QuoridorPawn
{
    public int Row { get; set; }
    public int Col { get; set; }

    /// <summary>Allows tuple-like deconstruction: var (r, c) = pawn;</summary>
    public void Deconstruct(out int row, out int col) => (row, col) = (Row, Col);
}

/// <summary>
/// Per-game timer configuration for Quoridor sessions.
/// Same shape and keys as UNO/Silver/Azul/Splendor so room settings treat games uniformly.
/// The defaults are UX configuration, not rules (spec §18).
/// </summary>
public class QuoridorTimerConfig
{
    /// <summary>Base allowance granted every turn, in seconds.</summary>
    public double BaseTurnSeconds { get; set; } = 90;

    /// <summary>Maximum total turn allowance (base + banked time), in seconds.</summary>
    public double MaxBankSeconds { get; set; } = 180;

    /// <summary>Maximum allowed overshoot before the next turn is shortened, in seconds.</summary>
    public double MaxOverrunSeconds { get; set; } = 15;

    /// <summary>Skipped turns after which a player is treated as AFK and eliminated.</summary>
    public int MaxAfkTurns { get; set; } = 3;

    /// <summary>Total allowed game duration before the game is force-finished, in minutes.</summary>
    public double TotalGameTimeMinutes { get; set; } = 60;
}

/// <summary>
/// Per-player turn timer accounting (mirrors the UNO/Silver/Azul/Splendor model).
/// </summary>
public class QuoridorPlayerTimer
{
    /// <summary>Time banked from fast turns; a turn's total allowance is capped at MaxBankSeconds.</summary>
    public double BankSeconds { get; set; }

    /// <summary>Seconds owed from overruns that the bank could not cover.</summary>
    public double DeferredPenaltySeconds { get; set; }

    /// <summary>Consecutive turns skipped by timeout; resets after an acted turn.</summary>
    public int ConsecutiveTimeouts { get; set; }
}

/// <summary>
/// A single placed wall on the board.
/// </summary>
public sealed class QuoridorWall
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Orientation { get; set; } = string.Empty; // "H" or "V"
}

/// <summary>
/// The authoritative Quoridor state, serialized as a JSON string into
/// GameState.Data["QuoridorState"]. The entire state is public information;
/// there is no hidden information (spec §24), so no IPlayerViewGame is needed.
/// </summary>
public class QuoridorState
{
    /// <summary>Number of players (2 or 4).</summary>
    public int SeatCount { get; set; }

    /// <summary>Current player index (0..SeatCount-1).</summary>
    public int CurrentPlayerIndex { get; set; }

    /// <summary>
    /// Pawn positions per seat.
    /// </summary>
    public List<QuoridorPawn> Pawns { get; set; } = new();

    /// <summary>All placed walls.</summary>
    public List<QuoridorWall> Walls { get; set; } = new();

    /// <summary>Walls remaining per seat.</summary>
    public List<int> WallsRemaining { get; set; } = new();

    /// <summary>Seats removed by AFK timeouts (spec §18).</summary>
    public List<int> EliminatedSeats { get; set; } = new();

    /// <summary>Persistent public-safe game event log.</summary>
    public List<string> EventLog { get; set; } = new();

    /// <summary>Optional RNG seed from game settings (deterministic testing). Null = fresh shuffles.</summary>
    public int? Seed { get; set; }

    /// <summary>Turn timer configuration for this session.</summary>
    public QuoridorTimerConfig? TimerConfig { get; set; }

    /// <summary>Per-player timer accounting, indexed by seat.</summary>
    public List<QuoridorPlayerTimer> PlayerTimers { get; set; } = new();

    /// <summary>UTC moment the current turn started.</summary>
    public DateTime TurnStartUtc { get; set; }

    /// <summary>UTC deadline for the current player's action. Mirrored onto GameState.NextActionDeadlineUtc.</summary>
    public DateTime? NextActionDeadlineUtc { get; set; }

    /// <summary>UTC moment at which the game is force-finished. Mirrored onto GameState.GameEndsAtUtc.</summary>
    public DateTime? GameEndsAtUtc { get; set; }

    /// <summary>Winner seat index (for convenience, mirrors GameState.Winner). -1 if none/draw.</summary>
    public int WinnerSeat { get; set; } = -1;

    /// <summary>True if the game ended in a draw (GameTimeExpired).</summary>
    public bool Draw { get; set; }

    /// <summary>Player display names for event text (not serialized).</summary>
    [JsonIgnore]
    public Dictionary<Guid, string>? PlayerNames { get; set; }

    /// <summary>Player ids in seat order (not serialized).</summary>
    [JsonIgnore]
    public IReadOnlyList<PlayerId>? PlayerIds { get; set; }

    /// <summary>Serializes the game state to JSON for storage in GameState.Data.</summary>
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);

    /// <summary>Deserializes from JSON.</summary>
    public static QuoridorState FromJson(string json)
        => System.Text.Json.JsonSerializer.Deserialize<QuoridorState>(json) ?? new QuoridorState();

    /// <summary>Whether the seat is still active (not eliminated).</summary>
    public bool IsActive(int seat) => !EliminatedSeats.Contains(seat);

    /// <summary>Seats that still take turns, in seat order.</summary>
    public IEnumerable<int> ActiveSeats()
    {
        for (var i = 0; i < SeatCount; i++)
        {
            if (IsActive(i)) yield return i;
        }
    }

    /// <summary>Gets (creating if needed) the timer entry for a seat.</summary>
    public QuoridorPlayerTimer EnsureTimer(int seat)
    {
        while (PlayerTimers.Count <= seat) PlayerTimers.Add(new QuoridorPlayerTimer());
        return PlayerTimers[seat];
    }

    /// <summary>Maximum seconds the given player is allotted for the current turn (base + bank - penalty, capped).</summary>
    public double MaxTurnSeconds(int seat, QuoridorTimerConfig config)
    {
        var timer = EnsureTimer(seat);
        var raw = config.BaseTurnSeconds + timer.BankSeconds - timer.DeferredPenaltySeconds;
        var floor = Math.Max(0, config.BaseTurnSeconds - config.MaxOverrunSeconds);
        return Math.Max(floor, Math.Min(config.MaxBankSeconds, raw));
    }

    /// <summary>Starts the clock for the current player (allowance + grace hard deadline).</summary>
    public void SetTurnClock()
    {
        var config = TimerConfig ?? new QuoridorTimerConfig();
        TurnStartUtc = DateTime.UtcNow;
        NextActionDeadlineUtc = DateTime.UtcNow
            .AddSeconds(MaxTurnSeconds(CurrentPlayerIndex, config))
            .AddSeconds(config.MaxOverrunSeconds);
    }

    /// <summary>Applies turn-timer accounting after a valid player action, then starts a fresh clock.</summary>
    public void ApplyTurnTimeAccounting(int seat, bool turnEnded)
    {
        var config = TimerConfig ?? new QuoridorTimerConfig();
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

        if (turnEnded) SetTurnClock();
    }

    // ==================== Path-preservation BFS (spec §9) ====================

    /// <summary>
    /// Checks if the given seat has any path to its goal squares.
    /// Pawns are passable; only walls block. Returns true if at least one path exists.
    /// </summary>
    public bool HasPathToGoal(int seat)
    {
        var start = Pawns[seat];
        var startRow = start.Row;
        var startCol = start.Col;
        var goals = QuoridorCatalog.GoalSquares(seat, SeatCount);

        // If already on goal, trivially reachable
        if (goals.Any(g => g.row == startRow && g.col == startCol)) return true;

        var visited = new bool[QuoridorCatalog.BoardSize, QuoridorCatalog.BoardSize];
        var queue = new Queue<(int r, int c)>();
        queue.Enqueue((startRow, startCol));
        visited[startRow, startCol] = true;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();

            // Check if current cell is a goal
            // Check if current cell is a goal
            if (goals.Any(g => g.row == r && g.col == c)) return true;

            // Explore 4 orthogonal neighbors
            foreach (var (dr, dc) in QuoridorCatalog.Directions)
            {
                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nr >= QuoridorCatalog.BoardSize || nc < 0 || nc >= QuoridorCatalog.BoardSize) continue;
                if (visited[nr, nc]) continue;

                // Check if there's a wall blocking this edge
                if (IsWallBetween(r, c, nr, nc)) continue;

                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if a wall blocks the edge between cell (r1,c1) and (r2,c2).
    /// Assumes the cells are orthogonal neighbors.
    /// </summary>
    public bool IsWallBetween(int r1, int c1, int r2, int c2)
    {
        if (r1 == r2) // horizontal move (left/right) -> check vertical wall
        {
            // The vertical groove between columns min(c1,c2) and min(c1,c2)+1 (spec §3).
            // A V wall V(r, cw) covers unit edges (r, cw) and (r+1, cw), so the edge at
            // row r1 is covered by a wall whose first unit edge is r1 OR r1-1 (spec §8).
            int cw = Math.Min(c1, c2);
            int r = r1;
            return Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationVertical
                && w.Col == cw && (w.Row == r || w.Row == r - 1));
        }
        else // vertical move (up/down) -> check horizontal wall
        {
            // The horizontal groove between rows min(r1,r2) and min(r1,r2)+1 (spec §3).
            // An H wall H(rw, c) covers unit edges (rw, c) and (rw, c+1), so the edge at
            // column c1 is covered by a wall whose first unit edge is c1 OR c1-1 (spec §8).
            int rw = Math.Min(r1, r2);
            int c = c1;
            return Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationHorizontal
                && w.Row == rw && (w.Col == c || w.Col == c - 1));
        }
    }

    /// <summary>
    /// Checks if a wall slot is occupied (overlap test, spec §8).
    /// Returns true if the candidate wall shares any unit edge with existing walls,
    /// or if a perpendicular wall would cross through its middle (the "+" junction —
    /// identical (Row, Col) slots). Corner touches and end-to-side "T" contacts are
    /// legal touching, not crossings.
    /// </summary>
    public bool WallOverlaps(QuoridorWall candidate)
    {
        if (candidate.Orientation == QuoridorCatalog.OrientationHorizontal)
        {
            // H(rw, c) covers edges (rw, c) and (rw, c+1); two H walls in the same row
            // overlap iff their first unit edges differ by at most 1 (spec §8).
            if (Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationHorizontal
                && w.Row == candidate.Row
                && Math.Abs(w.Col - candidate.Col) <= 1)) return true;

            // Cross-orientation: a V wall must not cross through this H wall's middle
            // (identical slot = the "+" junction; end/side touches are legal).
            return Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationVertical
                && WallsCross(candidate, w));
        }
        else // Vertical
        {
            // V(r, cw) covers edges (r, cw) and (r+1, cw); two V walls in the same column
            // overlap iff their first unit edges differ by at most 1 (spec §8).
            if (Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationVertical
                && w.Col == candidate.Col
                && Math.Abs(w.Row - candidate.Row) <= 1)) return true;

            // Cross-orientation: an H wall must not cross through this V wall's middle
            // (identical slot = the "+" junction; end/side touches are legal).
            return Walls.Any(w => w.Orientation == QuoridorCatalog.OrientationHorizontal
                && WallsCross(candidate, w));
        }
    }

    /// <summary>
    /// Returns true only when an H wall and a V wall truly CROSS — each passing
    /// through the other's middle (the "+" junction), which happens exactly when the
    /// two slots share the same (Row, Col). Every other perpendicular contact — a wall
    /// end meeting the other wall's side ("T"), or two ends meeting corner-to-corner —
    /// is legal touching, not crossing (spec §8, OD-6).
    /// </summary>
    private static bool WallsCross(QuoridorWall a, QuoridorWall b)
    {
        QuoridorWall h, v;
        if (a.Orientation == QuoridorCatalog.OrientationHorizontal) { h = a; v = b; }
        else { h = b; v = a; }

        return h.Row == v.Row && h.Col == v.Col;
    }

    /// <summary>
    /// Validates a wall slot is within board bounds (spec §8).
    /// Walls are 2 cells long and may sit flush with the rim:
    /// H Col in [0,7] (covers columns c and c+1), V Row in [0,7] (covers rows r and r+1).
    /// </summary>
    public static bool IsWallSlotInBounds(QuoridorWall wall)
    {
        if (wall.Orientation == QuoridorCatalog.OrientationHorizontal)
        {
            return wall.Row >= 0 && wall.Row <= 7 && wall.Col >= 0 && wall.Col <= 7;
        }
        else // Vertical
        {
            return wall.Row >= 0 && wall.Row <= 7 && wall.Col >= 0 && wall.Col <= 7;
        }
    }

    // ==================== Movement legality (spec §6-7) ====================

    /// <summary>
    /// Checks if a move from (fromRow,fromCol) to (toRow,toCol) is legal.
    /// Returns (isLegal, isJump, jumpKind). jumpKind is "straight" or "aside" or null.
    /// </summary>
    public (bool legal, bool isJump, string? jumpKind) CheckMove(int seat, int fromRow, int fromCol, int toRow, int toCol)
    {
        // Bounds check
        if (toRow < 0 || toRow >= QuoridorCatalog.BoardSize || toCol < 0 || toCol >= QuoridorCatalog.BoardSize)
            return (false, false, null);

        // Occupied by any pawn?
        if (Pawns.Any(p => p.Row == toRow && p.Col == toCol))
            return (false, false, null);

        int dr = toRow - fromRow;
        int dc = toCol - fromCol;
        int absDr = Math.Abs(dr), absDc = Math.Abs(dc);

        // Single orthogonal step
        if ((absDr == 1 && absDc == 0) || (absDr == 0 && absDc == 1))
        {
            if (IsWallBetween(fromRow, fromCol, toRow, toCol)) return (false, false, null);
            return (true, false, null);
        }

        // Jump: two cells orthogonally away (straight jump behind adjacent pawn)
        if ((absDr == 2 && absDc == 0) || (absDr == 0 && absDc == 2))
        {
            // There must be an adjacent pawn in between
            int midRow = fromRow + dr / 2;
            int midCol = fromCol + dc / 2;

            // Check there's a pawn at the middle cell
            bool pawnBetween = Pawns.Any(p => p.Row == midRow && p.Col == midCol);
            if (!pawnBetween) return (false, false, null);

            // No wall between from->mid or mid->to
            if (IsWallBetween(fromRow, fromCol, midRow, midCol)) return (false, false, null);
            if (IsWallBetween(midRow, midCol, toRow, toCol)) return (false, false, null);

            return (true, true, "straight");
        }

        // Aside jump: diagonal (1,1) when straight jump is blocked
        if (absDr == 1 && absDc == 1)
        {
            // The straight-behind cell must be blocked (occupied, off-board, or walled)
            int straightRow = fromRow + 2 * Math.Sign(dr);
            int straightCol = fromCol + 2 * Math.Sign(dc);

            // Check the adjacent pawn exists (the one we're jumping beside)
            int midRow = fromRow + Math.Sign(dr);
            int midCol = fromCol + Math.Sign(dc);
            bool pawnBeside = Pawns.Any(p => p.Row == midRow && p.Col == midCol);
            if (!pawnBeside) return (false, false, null);

            // Check if straight jump is possible
            bool straightPossible = true;
            if (straightRow < 0 || straightRow >= QuoridorCatalog.BoardSize ||
                straightCol < 0 || straightCol >= QuoridorCatalog.BoardSize)
            {
                straightPossible = false;
            }
            else if (Pawns.Any(p => p.Row == straightRow && p.Col == straightCol))
            {
                straightPossible = false;
            }
            else if (IsWallBetween(midRow, midCol, straightRow, straightCol))
            {
                straightPossible = false;
            }

            if (straightPossible) return (false, false, null); // Aside not offered when straight legal (OD-5)

            // Now validate the aside jump: no wall on the two edges we cross
            // Path: (fromRow,fromCol) -> (midRow,midCol) -> (toRow,toCol)
            // The two unit edges: from->midRow/midCol and midRow/midCol->to
            // Actually for diagonal jump we cross two edges: from->corner and corner->to
            // The corner is at (toRow, fromCol) or (fromRow, toCol) depending on direction
            // For Quoridor, the rule is: both orthogonal edges must be wall-free
            // Edge 1: from (fromRow, fromCol) to (midRow, fromCol) [if moving N/S first] or (fromRow, midCol) [if E/W first]
            // Edge 2: from that corner to (toRow, toCol)
            // More precisely: the move goes from (r,c) to (r±1,c±1) jumping over pawn at (r±1,c) or (r,c±1)
            // The edges crossed: (fromRow, fromCol) -> (toRow, fromCol) and (toRow, fromCol) -> (toRow, toCol)
            // Or: (fromRow, fromCol) -> (fromRow, toCol) and (fromRow, toCol) -> (toRow, toCol)

int cornerRow, cornerCol;
            if (dr != 0 && dc != 0)
            {
                // For diagonal jump, we need to check both possible L-shaped paths
                // The pawn is at (midRow, midCol), we're at (fromRow, fromCol), landing at (toRow, toCol)
                // The two unit edges we cross are:
                // Path A: (fromRow, fromCol) -> (midRow, fromCol) [vertical/horizontal toward pawn]
                //         (midRow, fromCol) -> (toRow, toCol) [diagonal to landing]
                // But actually the standard rule: you can't jump through a wall "laterally"
                // So we check if there's a wall on either of the two orthogonal edges between from and to
                // The two edges are:
                // 1. Between (fromRow, fromCol) and (fromRow + Math.Sign(dr), fromCol) [vertical toward pawn's row]
                // 2. Between (fromRow + Math.Sign(dr), fromCol) and (toRow, toCol) [horizontal to landing]
                // OR the symmetric path
                // Simplified: check the two unit edges forming the L around the corner
                cornerRow = toRow;
                cornerCol = fromCol;

                if (IsWallBetween(fromRow, fromCol, cornerRow, cornerCol)) return (false, false, null);
                if (IsWallBetween(cornerRow, cornerCol, toRow, toCol)) return (false, false, null);

                // Also check the symmetric path (corner at fromRow, toCol)
                cornerRow = fromRow;
                cornerCol = toCol;
                if (IsWallBetween(fromRow, fromCol, cornerRow, cornerCol)) return (false, false, null);
                if (IsWallBetween(cornerRow, cornerCol, toRow, toCol)) return (false, false, null);
            }

            return (true, true, "aside");
        }

        return (false, false, null);
    }
}