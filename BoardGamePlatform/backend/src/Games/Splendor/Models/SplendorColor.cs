using System.Text.Json.Serialization;

namespace Splendor.Models;

/// <summary>
/// The five gem colors of Splendor. The physical box names the white gem
/// "diamond"; the six token piles on the table are these five colors plus
/// gold jokers (gold is not a <see cref="SplendorColor"/> — it is never a
/// card bonus and is never taken by a gem action).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SplendorColor
{
    /// <summary>White gem.</summary>
    Diamond,

    /// <summary>Blue gem.</summary>
    Sapphire,

    /// <summary>Green gem.</summary>
    Emerald,

    /// <summary>Red gem.</summary>
    Ruby,

    /// <summary>Black gem.</summary>
    Onyx
}
