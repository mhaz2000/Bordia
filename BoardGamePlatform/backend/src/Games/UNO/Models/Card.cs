using System.Text.Json.Serialization;

namespace UNO.Models;

/// <summary>
/// Represents a single UNO card.
/// </summary>
public readonly record struct Card
{
    /// <summary>
    /// The color of the card.
    /// </summary>
    public CardColor Color { get; init; }

    /// <summary>
    /// The value/action of the card.
    /// </summary>
    public CardValue Value { get; init; }

    /// <summary>
    /// Creates a new card.
    /// </summary>
    public Card(CardColor color, CardValue value)
    {
        Color = color;
        Value = value;
    }

    /// <summary>
    /// Gets a unique ID for this card (used for tracking individual cards in deck).
    /// </summary>
    [JsonIgnore]
    public int Id => (int)Color * 100 + (int)Value;

    /// <summary>
    /// Determines if this card is a wild card.
    /// </summary>
    [JsonIgnore]
    public bool IsWild => Color == CardColor.Wild;

    /// <summary>
    /// Determines if this card is an action card (Skip, Reverse, DrawTwo, Wild, WildDrawFour).
    /// </summary>
    [JsonIgnore]
    public bool IsAction => Value >= CardValue.Skip;

    /// <summary>
    /// Determines if this card is a number card (0-9).
    /// </summary>
    [JsonIgnore]
    public bool IsNumber => Value <= CardValue.Nine;

    /// <summary>
    /// Returns a short string representation (e.g., "R7", "BSkip", "WDraw4").
    /// </summary>
    public override string ToString()
    {
        if (IsWild)
        {
            return Value == CardValue.WildDrawFour ? "WDraw4" : "Wild";
        }

        var colorChar = Color switch
        {
            CardColor.Red => 'R',
            CardColor.Blue => 'B',
            CardColor.Green => 'G',
            CardColor.Yellow => 'Y',
            _ => '?'
        };

        var valueStr = Value switch
        {
            CardValue.Skip => "Skip",
            CardValue.Reverse => "Rev",
            CardValue.DrawTwo => "Draw2",
            _ => ((int)Value).ToString()
        };

        return $"{colorChar}{valueStr}";
    }

    /// <summary>
    /// Checks if this card can be played on top of another card.
    /// </summary>
    public bool CanPlayOn(Card topCard, CardColor? currentColor = null)
    {
        // Wild cards can always be played
        if (IsWild) return true;

        // If there's a current color chosen (from Wild), match that
        if (currentColor.HasValue && Color == currentColor.Value) return true;

        // Match color or value
        return Color == topCard.Color || Value == topCard.Value;
    }
}