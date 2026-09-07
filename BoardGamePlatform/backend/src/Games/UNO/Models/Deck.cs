using System.Collections.Generic;

namespace UNO.Models;

/// <summary>
/// Represents a deck of UNO cards with draw/discard functionality.
/// </summary>
public class Deck
{
    /// <summary>
    /// The cards in the deck, bottom first. Public settable collection so the deck
    /// round-trips through JSON serialization.
    /// </summary>
    public List<Card> Cards { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    private readonly Random _random = new();

    /// <summary>
    /// Creates a standard 108-card UNO deck.
    /// </summary>
    public static Deck CreateStandard()
    {
        var deck = new Deck();

        // Number cards: 1x 0, 2x 1-9 per color (4 colors)
        foreach (var color in new[] { CardColor.Red, CardColor.Blue, CardColor.Green, CardColor.Yellow })
        {
            deck.Cards.Add(new Card(color, CardValue.Zero));
            for (int i = 1; i <= 9; i++)
            {
                deck.Cards.Add(new Card(color, (CardValue)i));
                deck.Cards.Add(new Card(color, (CardValue)i));
            }

            // Action cards: 2x each per color
            deck.Cards.Add(new Card(color, CardValue.Skip));
            deck.Cards.Add(new Card(color, CardValue.Skip));
            deck.Cards.Add(new Card(color, CardValue.Reverse));
            deck.Cards.Add(new Card(color, CardValue.Reverse));
            deck.Cards.Add(new Card(color, CardValue.DrawTwo));
            deck.Cards.Add(new Card(color, CardValue.DrawTwo));
        }

        // Wild cards: 4x Wild, 4x Wild Draw Four
        for (int i = 0; i < 4; i++)
        {
            deck.Cards.Add(new Card(CardColor.Wild, CardValue.Wild));
            deck.Cards.Add(new Card(CardColor.Wild, CardValue.WildDrawFour));
        }

        deck.Shuffle();
        return deck;
    }

    /// <summary>
    /// Creates an empty deck.
    /// </summary>
    public Deck() { }

    /// <summary>
    /// Number of cards remaining in the deck.
    /// </summary>
    public int Count => Cards.Count;

    /// <summary>
    /// Draws the top card from the deck.
    /// </summary>
    public Card? Draw()
    {
        if (Cards.Count == 0) return null;
        var card = Cards[Cards.Count - 1];
        Cards.RemoveAt(Cards.Count - 1);
        return card;
    }

    /// <summary>
    /// Draws multiple cards from the deck.
    /// </summary>
    public List<Card> Draw(int count)
    {
        var drawn = new List<Card>();
        for (int i = 0; i < count && Cards.Count > 0; i++)
        {
            var card = Draw();
            if (card.HasValue)
                drawn.Add(card.Value);
        }
        return drawn;
    }

    /// <summary>
    /// Adds a card to the bottom of the deck.
    /// </summary>
    public void AddToBottom(Card card)
    {
        Cards.Insert(0, card);
    }

    /// <summary>
    /// Adds multiple cards to the bottom of the deck.
    /// </summary>
    public void AddRangeToBottom(IEnumerable<Card> cards)
    {
        Cards.InsertRange(0, cards);
    }

    /// <summary>
    /// Shuffles the deck using Fisher-Yates algorithm.
    /// </summary>
    public void Shuffle()
    {
        for (int i = Cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (Cards[i], Cards[j]) = (Cards[j], Cards[i]);
        }
    }

    /// <summary>
    /// Reshuffles the discard pile back into the deck (keeping the top card).
    /// </summary>
    public void ReshuffleDiscard(List<Card> discardPile)
    {
        if (discardPile.Count <= 1) return;

        var topCard = discardPile[^1];
        var toReshuffle = discardPile.Take(discardPile.Count - 1).ToList();
        discardPile.Clear();
        discardPile.Add(topCard);

        AddRangeToBottom(toReshuffle);
        Shuffle();
    }

    /// <summary>
    /// Returns all cards for serialization.
    /// </summary>
    public IReadOnlyList<Card> GetAllCards() => Cards.AsReadOnly();
}