namespace Quoridor.Models;

/// <summary>
/// Static base-game facts: board dimensions, wall counts, starting positions,
/// and goal definitions. All rules refer to the classic 9x9 Quoridor base game.
/// </summary>
public static class QuoridorCatalog
{
    /// <summary>Board dimension (9x9 cells).</summary>
    public const int BoardSize = 9;

    /// <summary>Total walls in the game (20 walls, each 2 cells long).</summary>
    public const int TotalWalls = 20;

    /// <summary>Walls per player for 2-player game.</summary>
    public const int WallsPerPlayer2p = 10;

    /// <summary>Walls per player for 4-player game.</summary>
    public const int WallsPerPlayer4p = 5;

    /// <summary>Wall length in cells (each wall spans 2 adjacent edges).</summary>
    public const int WallLength = 2;

    /// <summary>Player colors (used for theming/seat identification).</summary>
    public static readonly string[] PlayerColors = ["Amber", "Emerald", "Indigo", "Rose"];

    /// <summary>Wall orientations.</summary>
    public const string OrientationHorizontal = "H";
    public const string OrientationVertical = "V";

    /// <summary>Direction vectors for orthogonal moves: N, S, W, E.</summary>
    public static readonly (int dr, int dc)[] Directions =
    [
        (-1, 0), // North
        (1, 0),  // South
        (0, -1), // West
        (0, 1)   // East
    ];

    /// <summary>Number of walls each player receives based on player count.</summary>
    public static int WallsPerPlayer(int playerCount) => playerCount switch
    {
        2 => WallsPerPlayer2p,
        4 => WallsPerPlayer4p,
        _ => throw new ArgumentException("Quoridor only supports 2 or 4 players", nameof(playerCount))
    };

    /// <summary>Starting positions (row, col) for each seat index.
    /// Seat order is clockwise starting at South (index 0): South, West, North, East.
    /// </summary>
    public static (int row, int col) StartPosition(int seat, int playerCount)
    {
        if (playerCount == 2)
        {
            return seat switch
            {
                0 => (BoardSize - 1, BoardSize / 2), // South (8, 4)
                1 => (0, BoardSize / 2),             // North (0, 4)
                _ => throw new ArgumentException($"Invalid seat for 2p: {seat}")
            };
        }
        else // 4 players
        {
            return seat switch
            {
                0 => (BoardSize - 1, BoardSize / 2), // South (8, 4)
                1 => (BoardSize / 2, 0),             // West  (4, 0)
                2 => (0, BoardSize / 2),             // North (0, 4)
                3 => (BoardSize / 2, BoardSize - 1), // East  (4, 8)
                _ => throw new ArgumentException($"Invalid seat for 4p: {seat}")
            };
        }
    }

    /// <summary>Goal squares for a given seat (the opposite edge of the board).
    /// Returns all 9 squares on the goal edge.
    /// </summary>
    public static List<(int row, int col)> GoalSquares(int seat, int playerCount)
    {
        var goals = new List<(int row, int col)>();

        if (playerCount == 2)
        {
            if (seat == 0) // South -> North edge (row 0)
            {
                for (int c = 0; c < BoardSize; c++) goals.Add((0, c));
            }
            else // North -> South edge (row 8)
            {
                for (int c = 0; c < BoardSize; c++) goals.Add((BoardSize - 1, c));
            }
        }
        else // 4 players
        {
            switch (seat)
            {
                case 0: // South -> North edge (row 0)
                    for (int c = 0; c < BoardSize; c++) goals.Add((0, c));
                    break;
                case 1: // West -> East edge (col 8)
                    for (int r = 0; r < BoardSize; r++) goals.Add((r, BoardSize - 1));
                    break;
                case 2: // North -> South edge (row 8)
                    for (int c = 0; c < BoardSize; c++) goals.Add((BoardSize - 1, c));
                    break;
                case 3: // East -> West edge (col 0)
                    for (int r = 0; r < BoardSize; r++) goals.Add((r, 0));
                    break;
            }
        }

        return goals;
    }

    /// <summary>Checks if a position is on the goal edge for the given seat.</summary>
    public static bool IsGoalSquare(int seat, int playerCount, int row, int col)
    {
        if (playerCount == 2)
        {
            if (seat == 0) return row == 0;
            else return row == BoardSize - 1;
        }
        else
        {
            return seat switch
            {
                0 => row == 0,
                1 => col == BoardSize - 1,
                2 => row == BoardSize - 1,
                3 => col == 0,
                _ => false
            };
        }
    }

    /// <summary>Returns the team for a 4-player seat (0=TeamA, 1=TeamB).
    /// In 2-player, each player is their own team.
    /// </summary>
    public static int TeamForSeat(int seat, int playerCount)
    {
        if (playerCount == 2) return seat; // 0 and 1 are separate
        return seat % 2; // Team A: seats 0,2; Team B: seats 1,3
    }
}