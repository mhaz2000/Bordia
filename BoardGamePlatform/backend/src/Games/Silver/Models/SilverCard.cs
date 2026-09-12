namespace Silver.Models;

/// <summary>
/// A single Silver card instance. The <see cref="Id"/> is stable for the whole
/// game so per-player knowledge and guard relationships can be tracked per
/// instance (they survive the slot shifting of replacements and follow the
/// card when an ability moves it).
/// </summary>
public class SilverCard
{
    /// <summary>Creates a card instance with a fresh identity.</summary>
    public SilverCard(int value)
    {
        Id = Guid.NewGuid();
        Value = value;
    }

    /// <summary>Parameterless constructor for JSON round-trips.</summary>
    public SilverCard()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>The stable instance identity used by knowledge/guard tracking.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The number of werewolves the resident attracts (0-13). Value 13
    /// (Doppelgänger) acts as one wildcard when matching replacement sets.
    /// Never serialize a masked value: player views are separate projections.
    /// </summary>
    public int Value { get; set; }

    /// <summary>Whether the card is currently face up (public knowledge).</summary>
    public bool FaceUp { get; set; }
}
