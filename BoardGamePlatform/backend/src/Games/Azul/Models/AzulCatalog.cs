namespace Azul.Models;

/// <summary>
/// Static base-game facts: the five tile colors, the 20-each/100-total tile
/// economy, per-player-count factory counts, and the end-game bonus values.
/// Azul tiles are identical within a color (no per-tile identity, unlike
/// Splendor cards), so the engine models every tile simply as its color index.
/// Transcribed from docs/games/azul.md §3-§4.
/// </summary>
public static class AzulCatalog
{
    /// <summary>Number of tile colors.</summary>
    public const int ColorCount = 5;

    /// <summary>Copies of each color in the box (100 tiles total).</summary>
    public const int TilesPerColor = 20;

    /// <summary>Total tiles in the game (conservation invariant I-1 baseline).</summary>
    public const int TotalTiles = ColorCount * TilesPerColor;

    /// <summary>Tiles drawn onto each factory display at round start.</summary>
    public const int TilesPerFactory = 4;

    /// <summary>Tile spaces on a floor line (the marker has its own space).</summary>
    public const int FloorCapacity = 7;

    /// <summary>Wall size (5 rows = 5 pattern lines, 5 columns).</summary>
    public const int WallSize = 5;

    /// <summary>Pattern lines per player board.</summary>
    public const int LineCount = 5;

    // End-of-game bonuses (spec §12.3 / §14). Horizontal is 2 per completed
    // row per the two licensed rule summaries (OD-8: PDF verification at
    // implementation time may amend this to per-tile).
    /// <summary>Points per completed horizontal wall row.</summary>
    public const int HorizontalBonus = 2;

    /// <summary>Points per completed vertical wall column.</summary>
    public const int VerticalBonus = 7;

    /// <summary>Points per color with all 5 tiles on the wall.</summary>
    public const int ColorSetBonus = 10;

    /// <summary>Number of active factory displays for a given player count.</summary>
    public static int FactoryCountFor(int players) => players switch
    {
        2 => 5,
        3 => 7,
        _ => 9
    };

    /// <summary>Canonical color name (0 Blue, 1 Red, 2 Yellow, 3 Black, 4 White).</summary>
    public static string ColorName(int color) => color switch
    {
        0 => "Blue",
        1 => "Red",
        2 => "Yellow",
        3 => "Black",
        4 => "White",
        _ => color.ToString()
    };

    /// <summary>Parses a color token name (or a digit) into its index; false if unknown.</summary>
    public static bool TryParseColor(string? raw, out int color)
    {
        color = -1;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "b" or "blue": color = 0; return true;
            case "r" or "red": color = 1; return true;
            case "y" or "yellow": color = 2; return true;
            case "k" or "black": color = 3; return true;
            case "w" or "white": color = 4; return true;
            default:
                if (int.TryParse(raw, out var parsed) && parsed >= 0 && parsed < ColorCount)
                {
                    color = parsed;
                    return true;
                }

                return false;
        }
    }
}
