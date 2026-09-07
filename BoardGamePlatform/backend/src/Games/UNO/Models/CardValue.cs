namespace UNO.Models;

/// <summary>
/// Represents the value/action of a UNO card.
/// </summary>
public enum CardValue
{
    /// <summary>Number cards 0-9.</summary>
    Zero = 0,
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,

    /// <summary>Action cards.</summary>
    Skip = 10,
    Reverse = 11,
    DrawTwo = 12,

    /// <summary>Wild cards.</summary>
    Wild = 13,
    WildDrawFour = 14
}