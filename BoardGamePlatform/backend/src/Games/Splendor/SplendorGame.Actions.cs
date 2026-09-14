using GameEngine.Core.Models;
using Splendor.Models;

namespace Splendor;

/// <summary>
/// The six player action processors and the two system actions. Every player
/// action is atomic: it validates its complete decision set (targets, token
/// returns, payment composition, noble claim) against the authoritative state
/// before mutating anything, and the shared end-of-turn pipeline in
/// <c>SplendorGame.ProcessAction</c> then awards nobles, checks the 15-point
/// threshold and advances play (spec Â§15).
/// </summary>
public sealed partial class SplendorGame
{
    // ----- gem taking -----

    private static SplendorActionResult ProcessTakeThreeGems(
        SplendorState splendor, int seat, TakeThreeGemsPayload? payload)
    {
        if (payload?.Colors is null || payload.Colors.Count == 0)
        {
            return FailResult(Errors.InvalidPayload);
        }

        var colors = new List<int>(payload.Colors.Count);
        foreach (var raw in payload.Colors)
        {
            if (!TryParseGemColor(raw, out var index))
            {
                return FailResult(Errors.InvalidGemColor, new object?[] { raw });
            }

            if (colors.Contains(index))
            {
                return FailResult(Errors.DuplicateColor);
            }

            colors.Add(index);
        }

        var availableColors = Enumerable.Range(0, 5).Count(c => splendor.Supply[c] > 0);
        if (availableColors == 0)
        {
            return FailResult(Errors.InsufficientSupply);
        }

        // OD-1: the take is forced to exactly min(3, available) distinct
        // colors - voluntary taking-fewer while 3+ exist is illegal.
        var required = Math.Min(3, availableColors);
        if (colors.Count != required)
        {
            return FailResult(Errors.ExactGemsRequired, new object?[] { required });
        }

        foreach (var index in colors)
        {
            if (splendor.Supply[index] < 1)
            {
                return FailResult(Errors.InsufficientSupply, new object?[] { ColorName(index) });
            }
        }

        var result = Ok();
        foreach (var index in colors)
        {
            splendor.Supply[index]--;
            splendor.Seats[seat].Tokens[index]++;
        }

        result.Events.Add(SplendorEvent.Build("splendor.gemsTaken", new
        {
            player = NameOf(splendor, seat),
            colors = colors.Select(ColorName).ToList()
        }));

        var returnError = ApplyTokenLimitReturns(splendor, seat, payload.Return, result.Events);
        if (returnError is not null)
        {
            return FailResult(returnError.Value.Code, returnError.Value.Args);
        }

        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    private static SplendorActionResult ProcessTakeTwoGems(
        SplendorState splendor, int seat, TakeTwoGemsPayload? payload)
    {
        if (payload?.Color is null)
        {
            return FailResult(Errors.InvalidPayload);
        }

        if (!TryParseGemColor(payload.Color, out var index))
        {
            return FailResult(Errors.InvalidGemColor, new object?[] { payload.Color });
        }

        if (splendor.Supply[index] < 4)
        {
            return FailResult(Errors.CannotTakeTwo, new object?[] { ColorName(index) });
        }

        var result = Ok();
        splendor.Supply[index] -= 2;
        splendor.Seats[seat].Tokens[index] += 2;
        result.Events.Add(SplendorEvent.Build("splendor.gemsTaken", new
        {
            player = NameOf(splendor, seat),
            colors = new List<string> { ColorName(index), ColorName(index) }
        }));

        var returnError = ApplyTokenLimitReturns(splendor, seat, payload.Return, result.Events);
        if (returnError is not null)
        {
            return FailResult(returnError.Value.Code, returnError.Value.Args);
        }

        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    // ----- reservations -----

    private static SplendorActionResult ProcessReserveMarketCard(
        SplendorState splendor, int seat, ReserveMarketCardPayload? payload)
    {
        if (payload?.CardId is null)
        {
            return FailResult(Errors.InvalidPayload);
        }

        if (splendor.Seats[seat].Reserved.Count >= SplendorCatalogue.MaxReservations)
        {
            return FailResult(Errors.ReservationLimit);
        }

        var card = SplendorCatalogue.FindCard(payload.CardId);
        var slot = card is null ? -1 : splendor.MarketSlotOf(card.Id);
        if (card is null || slot < 0)
        {
            return FailResult(Errors.CardUnavailable);
        }

        var result = Ok();
        splendor.Market[slot] = DrawFromDeck(splendor, card.Tier);
        splendor.Seats[seat].Reserved.Add(new SplendorReservation
        {
            CardId = card.Id,
            Tier = card.Tier,
            Source = SplendorReservationSource.Market
        });
        TakeGoldIfAvailable(splendor, seat, result.Events);

        // Public information: the market card identity was already visible to
        // everyone, so it may be named in the event (spec Â§19).
        result.Events.Add(SplendorEvent.Build("splendor.cardReserved", new
        {
            player = NameOf(splendor, seat),
            tier = card.Tier,
            source = "Market",
            card = card.Id
        }));

        var returnError = ApplyTokenLimitReturns(splendor, seat, payload.Return, result.Events);
        if (returnError is not null)
        {
            return FailResult(returnError.Value.Code, returnError.Value.Args);
        }

        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    private static SplendorActionResult ProcessReserveDeckCard(
        SplendorState splendor, int seat, ReserveDeckCardPayload? payload)
    {
        if (payload is null || payload.Tier is < 1 or > 3)
        {
            return FailResult(Errors.InvalidPayload);
        }

        if (splendor.Seats[seat].Reserved.Count >= SplendorCatalogue.MaxReservations)
        {
            return FailResult(Errors.ReservationLimit);
        }

        if (splendor.Decks[payload.Tier - 1].Count == 0)
        {
            return FailResult(Errors.DeckEmpty, new object?[] { payload.Tier });
        }

        var drawn = DrawFromDeck(splendor, payload.Tier)!;
        var result = Ok();
        splendor.Seats[seat].Reserved.Add(new SplendorReservation
        {
            CardId = drawn,
            Tier = payload.Tier,
            Source = SplendorReservationSource.Deck
        });
        TakeGoldIfAvailable(splendor, seat, result.Events);

        // Leak rule (spec Â§19): the blind reservation's identity is secret to
        // everyone - the event carries only the tier.
        result.Events.Add(SplendorEvent.Build("splendor.cardReserved", new
        {
            player = NameOf(splendor, seat),
            tier = payload.Tier,
            source = "Deck"
        }));

        var returnError = ApplyTokenLimitReturns(splendor, seat, payload.Return, result.Events);
        if (returnError is not null)
        {
            return FailResult(returnError.Value.Code, returnError.Value.Args);
        }

        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    // ----- purchasing -----

    private static SplendorActionResult ProcessPurchaseMarketCard(
        SplendorState splendor, int seat, PurchaseMarketCardPayload? payload)
    {
        if (payload?.CardId is null || payload.Payment is null)
        {
            return FailResult(Errors.InvalidPayload);
        }

        var card = SplendorCatalogue.FindCard(payload.CardId);
        var slot = card is null ? -1 : splendor.MarketSlotOf(card.Id);
        if (card is null || slot < 0)
        {
            return FailResult(Errors.CardUnavailable);
        }

        var result = Ok();
        var paymentError = ValidateAndSpend(splendor, seat, card, payload.Payment, result);
        if (paymentError is not null)
        {
            return FailResult(paymentError.Value.Code, paymentError.Value.Args);
        }

        splendor.Market[slot] = DrawFromDeck(splendor, card.Tier);
        splendor.Seats[seat].Purchased.Add(card.Id);
        AddPurchaseEvent(splendor, seat, card, payload.Payment, result.Events);
        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    private static SplendorActionResult ProcessPurchaseReservedCard(
        SplendorState splendor, int seat, PurchaseReservedCardPayload? payload)
    {
        if (payload is null || payload.Payment is null)
        {
            return FailResult(Errors.InvalidPayload);
        }

        var reserved = splendor.Seats[seat].Reserved;
        if (payload.ReservationIndex < 0 || payload.ReservationIndex >= reserved.Count)
        {
            return FailResult(Errors.ReservationNotFound);
        }

        var reservation = reserved[payload.ReservationIndex];
        var card = SplendorCatalogue.FindCard(reservation.CardId);
        if (card is null)
        {
            return FailResult(Errors.InvalidState);
        }

        var result = Ok();
        var paymentError = ValidateAndSpend(splendor, seat, card, payload.Payment, result);
        if (paymentError is not null)
        {
            return FailResult(paymentError.Value.Code, paymentError.Value.Args);
        }

        reserved.RemoveAt(payload.ReservationIndex);
        splendor.Seats[seat].Purchased.Add(card.Id);
        AddPurchaseEvent(splendor, seat, card, payload.Payment, result.Events);
        result.ClaimNoble = payload.ClaimNoble;
        result.TurnEnded = true;
        result.IsTurnAction = true;
        return result;
    }

    // ----- system actions -----

    private static SplendorActionResult ProcessTurnTimeout(SplendorState splendor, int seat)
    {
        if (splendor.NextActionDeadlineUtc is not { } deadline || DateTime.UtcNow <= deadline)
        {
            return FailResult(Errors.TimerNotExpired);
        }

        var config = splendor.TimerConfig ?? new SplendorTurnTimerConfig();
        var timer = splendor.EnsureTimer(seat);
        timer.ConsecutiveTimeouts++;
        ChargeOverrun(splendor, seat, config);

        var result = Ok();
        result.TurnEnded = true;
        result.IsTurnAction = false;
        // OD-5: a skipped turn that still owes a multi-eligible noble award
        // resolves deterministically by display order.
        result.AutoNoble = true;

        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            splendor.EliminatedSeats.Add(seat);
            splendor.FinalTurnSeatsPending.Remove(seat);
            result.Events.Add(SplendorEvent.Build("splendor.playerEliminated", new
            {
                player = NameOf(splendor, seat)
            }));

            if (splendor.ActiveSeats().Count() <= 1)
            {
                // Platform AFK pattern (2-player): the survivor wins instantly;
                // with everyone gone there is no winner.
                splendor.FinalRoundTriggered = true;
                splendor.FinalTurnSeatsPending.Clear();
                FinishGame(splendor, result, result.Events);
                return result;
            }
        }

        return result;
    }

    private static SplendorActionResult ProcessGameTimeExpired(SplendorState splendor)
    {
        if (splendor.GameEndsAtUtc is not { } endsAt || DateTime.UtcNow < endsAt)
        {
            return FailResult(Errors.TimeNotUp);
        }

        var result = Ok();
        result.IsTurnAction = false;
        result.Events.Add(SplendorEvent.Build("splendor.gameTimeExpired", new { }));
        FinishGame(splendor, result, result.Events);
        return result;
    }

    // ----- shared mechanics -----

    private static void TakeGoldIfAvailable(SplendorState splendor, int seat, List<string> events)
    {
        // Reservation always comes with a gold joker if one exists; an empty
        // gold supply never blocks the reservation itself (spec Â§5.3).
        if (splendor.Supply.Gold > 0)
        {
            splendor.Supply.Gold--;
            splendor.Seats[seat].Tokens.Gold++;
            events.Add(SplendorEvent.Build("splendor.goldTaken", new { player = NameOf(splendor, seat) }));
        }
    }

    /// <summary>
    /// Enforces the 10-token limit at end of turn (OD-2/OD-3): the player must
    /// return exactly the overflow, choosing from any of their held tokens
    /// (gold included, just-taken tokens included).
    /// </summary>
    private static (string Code, object?[] Args)? ApplyTokenLimitReturns(
        SplendorState splendor,
        int seat,
        List<GemReturnPayload>? returns,
        List<string> events)
    {
        var tokens = splendor.Seats[seat].Tokens;
        var excess = tokens.TotalWithGold - SplendorCatalogue.MaxTokens;

        if (excess <= 0)
        {
            return returns is { Count: > 0 }
                ? (Errors.InvalidReturn, new object?[] { 0 })
                : null;
        }

        if (returns is null || returns.Count == 0)
        {
            return (Errors.TokenLimitExceeded, new object?[] { excess });
        }

        var amounts = new int[6];
        foreach (var entry in returns)
        {
            if (entry.Count < 1)
            {
                return (Errors.InvalidPayload, Array.Empty<object?>());
            }

            if (!TryParseColorOrGold(entry.Color, out var kindIndex))
            {
                return (Errors.InvalidGemColor, new object?[] { entry.Color });
            }

            amounts[kindIndex] += entry.Count;
        }

        var total = amounts.Sum();
        if (total < excess)
        {
            return (Errors.TokenLimitExceeded, new object?[] { excess - total });
        }

        if (total > excess)
        {
            return (Errors.InvalidReturn, new object?[] { total - excess });
        }

        for (var i = 0; i < 6; i++)
        {
            if (amounts[i] == 0)
            {
                continue;
            }

            var held = i < 5 ? tokens[i] : tokens.Gold;
            if (held < amounts[i])
            {
                return (Errors.InvalidReturn, new object?[] { ColorName(i) });
            }
        }

        for (var i = 0; i < 6; i++)
        {
            if (amounts[i] == 0)
            {
                continue;
            }

            if (i < 5)
            {
                tokens[i] -= amounts[i];
                splendor.Supply[i] += amounts[i];
            }
            else
            {
                tokens.Gold -= amounts[i];
                splendor.Supply.Gold += amounts[i];
            }
        }

        events.Add(SplendorEvent.Build("splendor.tokensReturned", new
        {
            player = NameOf(splendor, seat),
            count = total
        }));
        return null;
    }

    /// <summary>
    /// Validates and spends a purchase payment (spec Â§9): bonuses are
    /// automatic discounts, colored payments may never exceed the effective
    /// per-color cost or the player's holdings, and gold must cover exactly
    /// the remaining shortfall.
    /// </summary>
    private static (string Code, object?[] Args)? ValidateAndSpend(
        SplendorState splendor,
        int seat,
        SplendorCard card,
        SplendorPaymentPayload payment,
        SplendorActionResult result)
    {
        var tokens = splendor.Seats[seat].Tokens;
        var pays = new[] { payment.Diamond, payment.Sapphire, payment.Emerald, payment.Ruby, payment.Onyx };
        if (pays.Any(p => p < 0) || payment.Gold < 0)
        {
            return (Errors.InvalidPayload, Array.Empty<object?>());
        }

        var required = new int[5];
        var shortfall = 0;
        for (var i = 0; i < 5; i++)
        {
            required[i] = Math.Max(0, card.Cost[i] - splendor.Seats[seat].Bonus(i));
            if (pays[i] > required[i])
            {
                return (Errors.InvalidPayment, new object?[] { ColorName(i) });
            }

            if (pays[i] > tokens[i])
            {
                return (Errors.InsufficientFunds, new object?[] { ColorName(i) });
            }

            shortfall += required[i] - pays[i];
        }

        if (payment.Gold < shortfall)
        {
            return (Errors.InsufficientFunds, new object?[] { card.Id });
        }

        if (payment.Gold > shortfall)
        {
            return (Errors.InvalidPayment, new object?[] { "Gold" });
        }

        for (var i = 0; i < 5; i++)
        {
            tokens[i] -= pays[i];
            splendor.Supply[i] += pays[i];
        }

        tokens.Gold -= payment.Gold;
        splendor.Supply.Gold += payment.Gold;
        _ = result;
        return null;
    }

    private static void AddPurchaseEvent(
        SplendorState splendor, int seat, SplendorCard card, SplendorPaymentPayload payment, List<string> events)
    {
        // Purchases are always public information (market, own reservation);
        // a formerly-blind card's identity is revealed here for the first time.
        events.Add(SplendorEvent.Build("splendor.cardPurchased", new
        {
            player = NameOf(splendor, seat),
            card = card.Id,
            tier = card.Tier,
            bonus = ColorName((int)card.Bonus),
            points = card.Points,
            payment,
            score = splendor.Seats[seat].VictoryPoints
        }));
    }

    /// <summary>
    /// End-of-turn noble step (spec Â§10/Â§15): the active player is automatically
    /// visited by at most one eligible noble; with 2+ eligible the player's
    /// declared claim decides (display-order default on timeout, OD-5).
    /// </summary>
    private static (string Code, object?[] Args)? AwardNobleAtEndOfTurn(
        SplendorState splendor,
        int seat,
        SplendorActionResult result,
        List<string> events)
    {
        // A seat eliminated by this very timeout leaves the game; its turn does
        // not collect nobles.
        if (splendor.EliminatedSeats.Contains(seat))
        {
            return null;
        }

        var eligible = splendor.NoblesInMarket
            .Where(id => MeetsNobleRequirement(splendor, seat, id))
            .ToList();

        if (result.ClaimNoble is { Length: > 0 } claim)
        {
            if (!SplendorCatalogue.IsNoble(claim))
            {
                return (Errors.NobleUnavailable, new object?[] { claim });
            }

            if (!eligible.Contains(claim, StringComparer.Ordinal))
            {
                return (Errors.NobleChoiceInvalid, new object?[] { claim });
            }
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        string claimed;
        if (eligible.Count == 1)
        {
            claimed = eligible[0];
        }
        else if (result.AutoNoble)
        {
            claimed = eligible[0];
        }
        else if (result.ClaimNoble is not { Length: > 0 })
        {
            return (Errors.NobleChoiceRequired, new object?[] { eligible.Count });
        }
        else
        {
            claimed = result.ClaimNoble!;
        }

        splendor.NoblesInMarket.Remove(claimed);
        splendor.Seats[seat].NoblesOwned.Add(claimed);
        events.Add(SplendorEvent.Build("splendor.nobleClaimed", new
        {
            player = NameOf(splendor, seat),
            noble = claimed,
            points = SplendorCatalogue.NoblePoints,
            score = splendor.Seats[seat].VictoryPoints
        }));
        return null;
    }

    private static bool MeetsNobleRequirement(SplendorState splendor, int seat, string nobleId)
    {
        var noble = SplendorCatalogue.Nobles.FirstOrDefault(n => string.Equals(n.Id, nobleId, StringComparison.Ordinal));
        if (noble is null)
        {
            return false;
        }

        for (var i = 0; i < 5; i++)
        {
            if (splendor.Seats[seat].Bonus(i) < noble.Requirement[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void ChargeOverrun(SplendorState splendor, int seat, SplendorTurnTimerConfig config)
    {
        var timer = splendor.EnsureTimer(seat);
        if (splendor.TurnStartUtc == default) return;

        var elapsed = DateTime.UtcNow - splendor.TurnStartUtc;
        var allotted = splendor.MaxTurnSeconds(seat, config);
        var overrun = Math.Max(0, elapsed.TotalSeconds - allotted);
        var bankCover = Math.Min(timer.BankSeconds, overrun);
        timer.BankSeconds -= bankCover;
        timer.DeferredPenaltySeconds += Math.Min(config.MaxOverrunSeconds, overrun - bankCover);
    }

    // ----- payload parsing -----

    /// <summary>
    /// Parses a gem color token: full name (case-insensitive) or the single
    /// letter D/S/E/R/O. Gold is not a gem color.
    /// </summary>
    private static bool TryParseGemColor(string? raw, out int colorIndex)
    {
        colorIndex = -1;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim().ToLowerInvariant();
        switch (value)
        {
            case "d" or "diamond": colorIndex = 0; return true;
            case "s" or "sapphire": colorIndex = 1; return true;
            case "e" or "emerald": colorIndex = 2; return true;
            case "r" or "ruby": colorIndex = 3; return true;
            case "o" or "onyx": colorIndex = 4; return true;
            default: return false;
        }
    }

    /// <summary>Parses a token-kind for returns: the five gem colors plus gold ("g"/"gold").</summary>
    private static bool TryParseColorOrGold(string? raw, out int kindIndex)
    {
        kindIndex = 5;
        if (raw is not null)
        {
            var value = raw.Trim().ToLowerInvariant();
            if (value is "g" or "gold")
            {
                return true;
            }
        }

        return TryParseGemColor(raw, out kindIndex);
    }

    private static string ColorName(int index) => index switch
    {
        0 => "diamond",
        1 => "sapphire",
        2 => "emerald",
        3 => "ruby",
        4 => "onyx",
        _ => "gold"
    };
}
