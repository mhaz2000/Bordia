using System.Collections.Generic;

namespace UNO.Models;

/// <summary>
/// Represents a player's hand of cards.
/// </summary>
public class PlayerHand
{
    /// <summary>
    /// The cards in the hand. Public settable collection so the hand
    /// round-trips through JSON serialization.
    /// </summary>
    public List<Card> Cards { get; set; } = new();

    /// <summary>
    /// Number of cards in hand.
    /// </summary>
    public int Count => Cards.Count;

    /// <summary>
    /// Adds a card to the hand.
    /// </summary>
    public void Add(Card card) => Cards.Add(card);

    /// <summary>
    /// Adds multiple cards to the hand.
    /// </summary>
    public void AddRange(IEnumerable<Card> cards) => Cards.AddRange(cards);

    /// <summary>
    /// Removes and returns a specific card from the hand.
    /// </summary>
    public bool Remove(Card card) => Cards.Remove(card);

    /// <summary>
    /// Removes and returns a card at the specified index.
    /// </summary>
    public Card RemoveAt(int index)
    {
        var card = Cards[index];
        Cards.RemoveAt(index);
        return card;
    }

    /// <summary>
    /// Checks if the hand contains a specific card.
    /// </summary>
    public bool Contains(Card card) => Cards.Contains(card);

    /// <summary>
    /// Checks if the hand has only one card left (UNO!).
    /// </summary>
    public bool HasUno => Cards.Count == 1;

    /// <summary>
    /// Checks if the hand is empty (player won).
    /// </summary>
    public bool IsEmpty => Cards.Count == 0;

    /// <summary>
    /// Gets all cards that can be played on the given top card.
    /// </summary>
    public List<Card> GetPlayableCards(Card topCard, CardColor? currentColor = null)
    {
        return Cards.Where(c => c.CanPlayOn(topCard, currentColor)).ToList();
    }

    /// <summary>
    /// Clears the hand.
    /// </summary>
    public void Clear() => Cards.Clear();
}