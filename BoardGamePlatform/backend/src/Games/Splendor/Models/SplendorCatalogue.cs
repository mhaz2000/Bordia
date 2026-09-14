namespace Splendor.Models;

/// <summary>
/// A base-game development card definition: stable catalogue id, tier
/// (1-3), permanent bonus color, prestige points and gem cost. The cost is
/// indexed in color order Diamond, Sapphire, Emerald, Ruby, Onyx. All 90
/// cards are physically unique, so the catalogue id doubles as the instance
/// id — this table is both the card and its only copy (spec §3.3).
/// </summary>
/// <param name="Id">Stable catalogue/instance id (e.g. "L3S02").</param>
/// <param name="Tier">Development level 1-3.</param>
/// <param name="Bonus">Permanent discount color once purchased.</param>
/// <param name="Points">Prestige points (0-5).</param>
/// <param name="Cost">Gem costs in color order (Diamond, Sapphire, Emerald, Ruby, Onyx).</param>
public sealed record SplendorCard(string Id, int Tier, SplendorColor Bonus, int Points, int[] Cost);

/// <summary>
/// A noble tile: stable id (also the display order used by the OD-5
/// deterministic noble-choice default) and its bonus requirement in color
/// order. Every noble is worth <see cref="SplendorCatalogue.NoblePoints"/>.
/// </summary>
/// <param name="Id">Stable noble id (e.g. "N-DSE").</param>
/// <param name="Requirement">Required permanent bonuses in color order.</param>
public sealed record SplendorNoble(string Id, int[] Requirement);

/// <summary>
/// Static base-game catalogue: the exact 40 Level 1, 30 Level 2, 20 Level 3
/// development cards and 10 noble tiles, transcribed from
/// docs/games/splendor.md §3.3 (photo-verified card extraction).
/// </summary>
public static class SplendorCatalogue
{
    /// <summary>Prestige points awarded by every noble tile.</summary>
    public const int NoblePoints = 3;

    /// <summary>Prestige points that trigger the end of the game.</summary>
    public const int TriggerPoints = 15;

    /// <summary>Maximum tokens (gold included) a player may hold at end of turn.</summary>
    public const int MaxTokens = 10;

    /// <summary>Maximum cards a player may keep reserved.</summary>
    public const int MaxReservations = 3;

    /// <summary>Total colored gem tokens per color (a 4-player supply; reduced at 2-3 players).</summary>
    public const int FullGemsPerColor = 7;

    /// <summary>Total gold tokens (never reduced by player count).</summary>
    public const int GoldCount = 5;

    /// <summary>The five gem colors in canonical catalogue order.</summary>
    public static readonly SplendorColor[] GemColors =
    [
        SplendorColor.Diamond,
        SplendorColor.Sapphire,
        SplendorColor.Emerald,
        SplendorColor.Ruby,
        SplendorColor.Onyx
    ];

    /// <summary>All 90 development cards (unique ids; ids are ordered D,E,O,R,S within each tier).</summary>
    public static readonly IReadOnlyList<SplendorCard> Cards =
    [
        new("L1D01", 1, SplendorColor.Diamond, 0, [0, 3, 0, 0, 0]),
        new("L1D02", 1, SplendorColor.Diamond, 1, [0, 0, 4, 0, 0]),
        new("L1D03", 1, SplendorColor.Diamond, 0, [0, 0, 0, 2, 1]),
        new("L1D04", 1, SplendorColor.Diamond, 0, [0, 2, 0, 0, 2]),
        new("L1D05", 1, SplendorColor.Diamond, 0, [3, 1, 0, 0, 1]),
        new("L1D06", 1, SplendorColor.Diamond, 0, [0, 2, 2, 0, 1]),
        new("L1D07", 1, SplendorColor.Diamond, 0, [0, 1, 1, 1, 1]),
        new("L1D08", 1, SplendorColor.Diamond, 0, [0, 1, 2, 1, 1]),
        new("L1E01", 1, SplendorColor.Emerald, 0, [0, 0, 0, 3, 0]),
        new("L1E02", 1, SplendorColor.Emerald, 1, [0, 0, 0, 0, 4]),
        new("L1E03", 1, SplendorColor.Emerald, 0, [2, 1, 0, 0, 0]),
        new("L1E04", 1, SplendorColor.Emerald, 0, [0, 2, 0, 2, 0]),
        new("L1E05", 1, SplendorColor.Emerald, 0, [1, 3, 1, 0, 0]),
        new("L1E06", 1, SplendorColor.Emerald, 0, [0, 1, 0, 2, 2]),
        new("L1E07", 1, SplendorColor.Emerald, 0, [1, 1, 0, 1, 1]),
        new("L1E08", 1, SplendorColor.Emerald, 0, [1, 1, 0, 1, 2]),
        new("L1O01", 1, SplendorColor.Onyx, 0, [0, 0, 3, 0, 0]),
        new("L1O02", 1, SplendorColor.Onyx, 1, [0, 4, 0, 0, 0]),
        new("L1O03", 1, SplendorColor.Onyx, 0, [0, 0, 2, 1, 0]),
        new("L1O04", 1, SplendorColor.Onyx, 0, [2, 0, 2, 0, 0]),
        new("L1O05", 1, SplendorColor.Onyx, 0, [0, 0, 1, 3, 1]),
        new("L1O06", 1, SplendorColor.Onyx, 0, [2, 2, 0, 1, 0]),
        new("L1O07", 1, SplendorColor.Onyx, 0, [1, 1, 1, 1, 0]),
        new("L1O08", 1, SplendorColor.Onyx, 0, [1, 2, 1, 1, 0]),
        new("L1R01", 1, SplendorColor.Ruby, 0, [3, 0, 0, 0, 0]),
        new("L1R02", 1, SplendorColor.Ruby, 1, [4, 0, 0, 0, 0]),
        new("L1R03", 1, SplendorColor.Ruby, 0, [0, 2, 1, 0, 0]),
        new("L1R04", 1, SplendorColor.Ruby, 0, [2, 0, 0, 2, 0]),
        new("L1R05", 1, SplendorColor.Ruby, 0, [1, 0, 0, 1, 3]),
        new("L1R06", 1, SplendorColor.Ruby, 0, [2, 0, 1, 0, 2]),
        new("L1R07", 1, SplendorColor.Ruby, 0, [1, 1, 1, 0, 1]),
        new("L1R08", 1, SplendorColor.Ruby, 0, [2, 1, 1, 0, 1]),
        new("L1S01", 1, SplendorColor.Sapphire, 0, [0, 0, 0, 0, 3]),
        new("L1S02", 1, SplendorColor.Sapphire, 1, [0, 0, 0, 4, 0]),
        new("L1S03", 1, SplendorColor.Sapphire, 0, [1, 0, 0, 0, 2]),
        new("L1S04", 1, SplendorColor.Sapphire, 0, [0, 0, 2, 0, 2]),
        new("L1S05", 1, SplendorColor.Sapphire, 0, [0, 1, 3, 1, 0]),
        new("L1S06", 1, SplendorColor.Sapphire, 0, [1, 0, 2, 2, 0]),
        new("L1S07", 1, SplendorColor.Sapphire, 0, [1, 0, 1, 1, 1]),
        new("L1S08", 1, SplendorColor.Sapphire, 0, [1, 0, 1, 2, 1]),
        new("L2D01", 2, SplendorColor.Diamond, 2, [0, 0, 0, 5, 0]),
        new("L2D02", 2, SplendorColor.Diamond, 3, [6, 0, 0, 0, 0]),
        new("L2D03", 2, SplendorColor.Diamond, 2, [0, 0, 0, 5, 3]),
        new("L2D04", 2, SplendorColor.Diamond, 2, [0, 0, 1, 4, 2]),
        new("L2D05", 2, SplendorColor.Diamond, 1, [0, 0, 3, 2, 2]),
        new("L2D06", 2, SplendorColor.Diamond, 1, [2, 3, 0, 3, 0]),
        new("L2E01", 2, SplendorColor.Emerald, 2, [0, 0, 5, 0, 0]),
        new("L2E02", 2, SplendorColor.Emerald, 3, [0, 0, 6, 0, 0]),
        new("L2E03", 2, SplendorColor.Emerald, 2, [0, 5, 3, 0, 0]),
        new("L2E04", 2, SplendorColor.Emerald, 2, [4, 2, 0, 0, 1]),
        new("L2E05", 2, SplendorColor.Emerald, 1, [2, 3, 0, 0, 2]),
        new("L2E06", 2, SplendorColor.Emerald, 1, [3, 0, 2, 3, 0]),
        new("L2O01", 2, SplendorColor.Onyx, 2, [5, 0, 0, 0, 0]),
        new("L2O02", 2, SplendorColor.Onyx, 3, [0, 0, 0, 0, 6]),
        new("L2O03", 2, SplendorColor.Onyx, 2, [0, 0, 5, 3, 0]),
        new("L2O04", 2, SplendorColor.Onyx, 2, [0, 1, 4, 2, 0]),
        new("L2O05", 2, SplendorColor.Onyx, 1, [3, 2, 2, 0, 0]),
        new("L2O06", 2, SplendorColor.Onyx, 1, [3, 0, 3, 0, 2]),
        new("L2R01", 2, SplendorColor.Ruby, 2, [0, 0, 0, 0, 5]),
        new("L2R02", 2, SplendorColor.Ruby, 3, [0, 0, 0, 6, 0]),
        new("L2R03", 2, SplendorColor.Ruby, 2, [3, 0, 0, 0, 5]),
        new("L2R04", 2, SplendorColor.Ruby, 2, [1, 4, 2, 0, 0]),
        new("L2R05", 2, SplendorColor.Ruby, 1, [2, 0, 0, 2, 3]),
        new("L2R06", 2, SplendorColor.Ruby, 1, [0, 3, 0, 2, 3]),
        new("L2S01", 2, SplendorColor.Sapphire, 2, [0, 5, 0, 0, 0]),
        new("L2S02", 2, SplendorColor.Sapphire, 3, [0, 6, 0, 0, 0]),
        new("L2S03", 2, SplendorColor.Sapphire, 2, [5, 3, 0, 0, 0]),
        new("L2S04", 2, SplendorColor.Sapphire, 2, [2, 0, 0, 1, 4]),
        new("L2S05", 2, SplendorColor.Sapphire, 1, [0, 2, 2, 3, 0]),
        new("L2S06", 2, SplendorColor.Sapphire, 1, [0, 2, 3, 0, 3]),
        new("L3D01", 3, SplendorColor.Diamond, 4, [0, 0, 0, 0, 7]),
        new("L3D02", 3, SplendorColor.Diamond, 4, [3, 0, 0, 3, 6]),
        new("L3D03", 3, SplendorColor.Diamond, 3, [0, 3, 3, 5, 3]),
        new("L3D04", 3, SplendorColor.Diamond, 5, [3, 0, 0, 0, 7]),
        new("L3E01", 3, SplendorColor.Emerald, 4, [0, 7, 0, 0, 0]),
        new("L3E02", 3, SplendorColor.Emerald, 4, [3, 6, 3, 0, 0]),
        new("L3E03", 3, SplendorColor.Emerald, 3, [5, 3, 0, 3, 3]),
        new("L3E04", 3, SplendorColor.Emerald, 5, [0, 7, 3, 0, 0]),
        new("L3O01", 3, SplendorColor.Onyx, 4, [0, 0, 0, 7, 0]),
        new("L3O02", 3, SplendorColor.Onyx, 4, [0, 0, 3, 6, 3]),
        new("L3O03", 3, SplendorColor.Onyx, 3, [3, 3, 5, 3, 0]),
        new("L3O04", 3, SplendorColor.Onyx, 5, [0, 0, 0, 7, 3]),
        new("L3R01", 3, SplendorColor.Ruby, 4, [0, 0, 7, 0, 0]),
        new("L3R02", 3, SplendorColor.Ruby, 4, [0, 3, 6, 3, 0]),
        new("L3R03", 3, SplendorColor.Ruby, 3, [3, 5, 3, 0, 3]),
        new("L3R04", 3, SplendorColor.Ruby, 5, [0, 0, 7, 3, 0]),
        new("L3S01", 3, SplendorColor.Sapphire, 4, [7, 0, 0, 0, 0]),
        new("L3S02", 3, SplendorColor.Sapphire, 4, [6, 3, 0, 0, 3]),
        new("L3S03", 3, SplendorColor.Sapphire, 3, [3, 0, 3, 3, 5]),
        new("L3S04", 3, SplendorColor.Sapphire, 5, [7, 3, 0, 0, 0])
    ];

    /// <summary>All 10 noble tiles in canonical display order (five 4-4 pairs, five 3-3-3 triples).</summary>
    public static readonly IReadOnlyList<SplendorNoble> Nobles =
    [
        new("N-DS", [4, 4, 0, 0, 0]),
        new("N-SE", [0, 4, 4, 0, 0]),
        new("N-ER", [0, 0, 4, 4, 0]),
        new("N-RO", [0, 0, 0, 4, 4]),
        new("N-OD", [4, 0, 0, 0, 4]),
        new("N-ERO", [0, 0, 3, 3, 3]),
        new("N-DSO", [3, 3, 0, 0, 3]),
        new("N-SER", [0, 3, 3, 3, 0]),
        new("N-DRO", [3, 0, 0, 3, 3]),
        new("N-DSE", [3, 3, 3, 0, 0])
    ];

    private static readonly Dictionary<string, SplendorCard> CardIndex =
        Cards.ToDictionary(c => c.Id, StringComparer.Ordinal);

    /// <summary>Looks up a development card definition by catalogue id.</summary>
    public static SplendorCard? FindCard(string? id)
        => id is not null && CardIndex.TryGetValue(id, out var card) ? card : null;

    /// <summary>Whether the id is a known noble tile id.</summary>
    public static bool IsNoble(string? id)
        => id is not null && Nobles.Any(n => string.Equals(n.Id, id, StringComparison.Ordinal));
}
