namespace Splendor.Models;

/// <summary>
/// A bag of gem tokens indexed by color plus a gold-joker count. Used both
/// for the central supply and for each player's holdings. Values are never
/// allowed to go negative (enforced by the engine via <see cref="TrySpend"/>).
/// </summary>
public sealed class SplendorGems
{
    /// <summary>Diamond (white) gem count.</summary>
    public int Diamond { get; set; }

    /// <summary>Sapphire (blue) gem count.</summary>
    public int Sapphire { get; set; }

    /// <summary>Emerald (green) gem count.</summary>
    public int Emerald { get; set; }

    /// <summary>Ruby (red) gem count.</summary>
    public int Ruby { get; set; }

    /// <summary>Onyx (black) gem count.</summary>
    public int Onyx { get; set; }

    /// <summary>Gold joker token count.</summary>
    public int Gold { get; set; }

    /// <summary>Gets the count for a gem <paramref name="color"/> (index form used by costs/bonuses).</summary>
    public int this[int colorIndex]
    {
        get => colorIndex switch
        {
            0 => Diamond,
            1 => Sapphire,
            2 => Emerald,
            3 => Ruby,
            4 => Onyx,
            _ => throw new IndexOutOfRangeException(nameof(colorIndex))
        };
        set
        {
            switch (colorIndex)
            {
                case 0: Diamond = value; break;
                case 1: Sapphire = value; break;
                case 2: Emerald = value; break;
                case 3: Ruby = value; break;
                case 4: Onyx = value; break;
                default: throw new IndexOutOfRangeException(nameof(colorIndex));
            }
        }
    }

    /// <summary>Gets the count of the given gem color.</summary>
    public int Of(SplendorColor color) => this[(int)color];

    /// <summary>Total gems held, excluding gold.</summary>
    public int GemsTotal => Diamond + Sapphire + Emerald + Ruby + Onyx;

    /// <summary>Total tokens held, gold joker included (the 10-token limit is measured on this).</summary>
    public int TotalWithGold => GemsTotal + Gold;

    /// <summary>Adds <paramref name="amount"/> of a gem color (negative to remove).</summary>
    public void Add(SplendorColor color, int amount) => this[(int)color] += amount;

    /// <summary>
    /// Attempts to remove the specified payment (five gem counts + gold). Fails
    /// and leaves the bag unchanged if any component is short.
    /// </summary>
    public bool TrySpend(int[] gemAmounts, int goldAmount)
    {
        for (var i = 0; i < 5; i++)
        {
            if (gemAmounts[i] < 0 || this[i] < gemAmounts[i])
            {
                return false;
            }
        }

        if (goldAmount < 0 || Gold < goldAmount)
        {
            return false;
        }

        for (var i = 0; i < 5; i++)
        {
            this[i] -= gemAmounts[i];
        }

        Gold -= goldAmount;
        return true;
    }

    /// <summary>Returns the specified payment to the supply (inverse of <see cref="TrySpend"/>).</summary>
    public void Receive(int[] gemAmounts, int goldAmount)
    {
        for (var i = 0; i < 5; i++)
        {
            this[i] += gemAmounts[i];
        }

        Gold += goldAmount;
    }

    /// <summary>Zero-filled bag.</summary>
    public static SplendorGems Empty() => new();
}
