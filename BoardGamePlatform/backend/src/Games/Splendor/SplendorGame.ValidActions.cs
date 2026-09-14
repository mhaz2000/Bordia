using GameEngine.Core.Models;
using Splendor.Models;

namespace Splendor;

/// <summary>
/// Enumeration of the concrete legal choices for a seat (spec §16): exact
/// color combinations, target cards, and affordability-filtered purchases.
/// Engine-level contract consumed by the harness (not an HTTP endpoint);
/// payment/return breakdowns carry one canonical valid split — other legal
/// splits of the same target are accepted by <c>ProcessAction</c> as well.
/// </summary>
public sealed partial class SplendorGame
{
    /// <inheritdoc />
    public IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId)
    {
        var actions = new List<GameAction>();
        if (state.IsOver)
        {
            return actions;
        }

        if (!state.TryGetString("SplendorState", out var json) || json is null)
        {
            return actions;
        }

        var splendor = SplendorState.FromJson(json);
        Rehydrate(splendor, state);

        var seat = IndexOf(state, playerId);
        if (seat < 0 || seat >= splendor.PlayerCount || splendor.EliminatedSeats.Contains(seat))
        {
            return actions;
        }

        if (seat != splendor.CurrentPlayerIndex)
        {
            return actions;
        }

        var seatState = splendor.Seats[seat];
        var availableColors = Enumerable.Range(0, 5).Where(c => splendor.Supply[c] > 0).ToList();
        var reserved = seatState.Reserved.Count;

        // Take 3 different (or the OD-1 forced smaller take): one action per
        // exact color combination of size min(3, available). Each entry carries
        // a canonical token-return plan so it is genuinely legal as-is.
        var carriedClaim = FirstMultiNobleClaim(splendor, seat, extraBonus: -1);
        if (availableColors.Count > 0)
        {
            var size = Math.Min(3, availableColors.Count);
            foreach (var combination in Combinations(availableColors, size))
            {
                actions.Add(CreateAction(
                    SplendorActionType.TakeThreeGems,
                    playerId,
                    new TakeThreeGemsPayload
                    {
                        Colors = combination.Select(ColorName).ToList(),
                        Return = CanonicalReturns(splendor, seat, size),
                        ClaimNoble = carriedClaim
                    }));
            }
        }

        // Take 2 identical: one per color with 4+ in the supply.
        foreach (var color in availableColors.Where(c => splendor.Supply[c] >= 4))
        {
            actions.Add(CreateAction(
                SplendorActionType.TakeTwoGems,
                playerId,
                new TakeTwoGemsPayload
                {
                    Color = ColorName(color),
                    Return = CanonicalReturns(splendor, seat, 2),
                    ClaimNoble = carriedClaim
                }));
        }

        if (reserved < SplendorCatalogue.MaxReservations)
        {
            // Reserve any market card (the gold gain may force a return).
            foreach (var marketCardId in splendor.Market.Where(id => id is not null))
            {
                actions.Add(CreateAction(
                    SplendorActionType.ReserveMarketCard,
                    playerId,
                    new ReserveMarketCardPayload
                    {
                        CardId = marketCardId,
                        Return = CanonicalReturns(splendor, seat, splendor.Supply.Gold > 0 ? 1 : 0),
                        ClaimNoble = carriedClaim
                    }));
            }

            // Reserve blindly from any non-empty tier deck.
            for (var tier = 1; tier <= 3; tier++)
            {
                if (splendor.Decks[tier - 1].Count > 0)
                {
                    actions.Add(CreateAction(
                        SplendorActionType.ReserveDeckCard,
                        playerId,
                        new ReserveDeckCardPayload
                        {
                            Tier = tier,
                            Return = CanonicalReturns(splendor, seat, splendor.Supply.Gold > 0 ? 1 : 0),
                            ClaimNoble = carriedClaim
                        }));
                }
            }
        }

        // Buyable market cards (affordability from bonuses + held tokens + gold).
        foreach (var marketCardId in splendor.Market.Where(id => id is not null))
        {
            var card = SplendorCatalogue.FindCard(marketCardId!);
            if (card is not null && TryCanonicalPayment(splendor, seat, card, out var payment))
            {
                actions.Add(CreateAction(
                    SplendorActionType.PurchaseMarketCard,
                    playerId,
                    new PurchaseMarketCardPayload
                    {
                        CardId = card.Id,
                        Payment = payment,
                        ClaimNoble = FirstMultiNobleClaim(splendor, seat, (int)card.Bonus)
                    }));
            }
        }

        // Buyable own reservations.
        for (var index = 0; index < seatState.Reserved.Count; index++)
        {
            var card = SplendorCatalogue.FindCard(seatState.Reserved[index].CardId);
            if (card is not null && TryCanonicalPayment(splendor, seat, card, out var payment))
            {
                actions.Add(CreateAction(
                    SplendorActionType.PurchaseReservedCard,
                    playerId,
                    new PurchaseReservedCardPayload
                    {
                        ReservationIndex = index,
                        Payment = payment,
                        ClaimNoble = FirstMultiNobleClaim(splendor, seat, (int)card.Bonus)
                    }));
            }
        }

        return actions;
    }

    /// <summary>
    /// When the end of this hypothetical turn would leave the seat eligible for
    /// two or more available nobles, the payload must carry a claim: return the
    /// first eligible noble in display order (the same default the OD-5 timeout
    /// path uses); null when at most one is eligible (auto-award, no claim).
    /// </summary>
    private static string? FirstMultiNobleClaim(SplendorState splendor, int seat, int extraBonus)
    {
        var eligible = splendor.NoblesInMarket
            .Where(id => SplendorCatalogue.Nobles.First(n => n.Id == id).Requirement
                .Select((need, color) => splendor.Seats[seat].Bonus(color) + (color == extraBonus ? 1 : 0) >= need)
                .All(ok => ok))
            .ToList();
        return eligible.Count >= 2 ? eligible[0] : null;
    }

    /// <summary>
    /// Builds a canonical token-return plan (if any) for an action that would
    /// add <paramref name="gain"/> tokens: returns the player's holdings in
    /// fixed color order until the post-action total is at most 10. Null when
    /// no overflow occurs.
    /// </summary>
    private static List<GemReturnPayload>? CanonicalReturns(SplendorState splendor, int seat, int gain)
    {
        var tokens = splendor.Seats[seat].Tokens;
        var excess = tokens.TotalWithGold + gain - SplendorCatalogue.MaxTokens;
        if (excess <= 0)
        {
            return null;
        }

        var plan = new List<GemReturnPayload>();
        for (var kind = 0; kind < 6 && excess > 0; kind++)
        {
            var held = kind < 5 ? tokens[kind] : tokens.Gold;
            var take = Math.Min(held, excess);
            if (take > 0)
            {
                plan.Add(new GemReturnPayload { Color = ColorName(kind), Count = take });
                excess -= take;
            }
        }

        return plan;
    }

    /// <summary>
    /// Builds one canonical payment for the card (pay colored tokens up to the
    /// effective requirement, gold covers the shortfall), or false when even
    /// that minimal plan is unaffordable.
    /// </summary>
    private static bool TryCanonicalPayment(SplendorState splendor, int seat, SplendorCard card, out SplendorPaymentPayload payment)
    {
        payment = new SplendorPaymentPayload();
        var tokens = splendor.Seats[seat].Tokens;
        var pays = new int[5];
        var shortfall = 0;
        for (var i = 0; i < 5; i++)
        {
            var required = Math.Max(0, card.Cost[i] - splendor.Seats[seat].Bonus(i));
            pays[i] = Math.Min(required, tokens[i]);
            shortfall += required - pays[i];
        }

        if (shortfall > tokens.Gold)
        {
            return false;
        }

        payment.Diamond = pays[0];
        payment.Sapphire = pays[1];
        payment.Emerald = pays[2];
        payment.Ruby = pays[3];
        payment.Onyx = pays[4];
        payment.Gold = shortfall;
        return true;
    }

    private static IEnumerable<List<int>> Combinations(List<int> source, int size)
    {
        if (size == 0)
        {
            yield return new List<int>();
            yield break;
        }

        for (var i = 0; i <= source.Count - size; i++)
        {
            foreach (var rest in Combinations(source.Skip(i + 1).ToList(), size - 1))
            {
                var combo = new List<int> { source[i] };
                combo.AddRange(rest);
                yield return combo;
            }
        }
    }
}
