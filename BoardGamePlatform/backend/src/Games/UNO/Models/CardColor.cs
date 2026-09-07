namespace UNO.Models;

/// <summary>
/// Represents the color of a UNO card.
/// </summary>
public enum CardColor
{
    /// <summary>Red cards.</summary>
    Red = 0,

    /// <summary>Blue cards.</summary>
    Blue = 1,

    /// <summary>Green cards.</summary>
    Green = 2,

    /// <summary>Yellow cards.</summary>
    Yellow = 3,

    /// <summary>Wild cards have no fixed color until played.</summary>
    Wild = 4
}