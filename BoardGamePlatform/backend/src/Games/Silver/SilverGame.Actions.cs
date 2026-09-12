using Silver.Models;

namespace Silver;

/// <summary>
/// Turn actions for Silver: peeks, draws (with Trickster extras), the drawn-card
/// decision, taking the discard top or a Squire-displayed card, replacement
/// mechanics (single and multi with mismatch handling), Guard placement,
/// calling a census, and amulet placement.
/// </summary>
public sealed partial class SilverGame
{
    private SilverActionResult ProcessPeekVillageCard(SilverState silver, int seat, PeekVillageCardPayload? payload)
    {
        if (payload is null) return FailResult(Errors.InvalidPayload);
        if (silver.PeeksUsed[seat] >= 2) return FailResult(Errors.PeekLimitReached);
        if (!TryGetOwnCard(silver, seat, payload.SlotIndex, requireFaceDown: true, blockGuarded: false, out var error, out var card))
        {
            return FailResult(error);
        }

        AddKnowledge(silver, seat, card);
        silver.PeeksUsed[seat]++;
        return Ok();
    }

    private SilverActionResult ProcessDrawFromDeck(SilverState silver, DrawFromDeckPayload? payload)
    {
        if (silver.Phase != TurnPhase.TurnStart || silver.PendingDraw.Count > 0)
        {
            return FailResult(Errors.WrongPhase);
        }
        if (silver.Deck.Count == 0) return FailResult(Errors.DeckEmpty);

        var seat = silver.CurrentPlayerIndex;
        var extra = payload?.TricksterExtra ?? 0;
        if (extra < 0) return FailResult(Errors.InvalidPayload);

        if (extra > 0)
        {
            var tricksters = silver.Villages[seat].Count(c => c.FaceUp && c.Value == 4);
            if (extra > tricksters) return FailResult(Errors.AbilityNotAvailable);
            extra = Math.Min(extra, silver.Deck.Count - 1);
        }

        var drawn = new List<SilverCard> { silver.DrawTop()! };
        for (var i = 0; i < extra; i++)
        {
            drawn.Add(silver.DrawTop()!);
        }
        silver.PendingDraw = drawn;
        silver.PendingSource = DrawSource.Deck;
        silver.Phase = drawn.Count > 1 ? TurnPhase.TricksterChoice : TurnPhase.DrawnDecision;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.cardDrawn", new
        {
            player = NameOf(silver, seat),
            count = drawn.Count
        }));
        return result;
    }

    private SilverActionResult ProcessTakeDiscard(SilverState silver)
    {
        if (silver.Phase != TurnPhase.TurnStart || silver.PendingDraw.Count > 0)
        {
            return FailResult(Errors.WrongPhase);
        }
        if (silver.Discard.Count == 0) return FailResult(Errors.DiscardEmpty);
        if (!HasOwnerExchangeable(silver, silver.CurrentPlayerIndex))
        {
            // Taking from the discard commits to a replacement; without any
            // replaceable village card it is not allowed.
            return FailResult(Errors.ExchangeNotAllowed);
        }

        var seat = silver.CurrentPlayerIndex;
        var card = silver.Discard[^1];
        silver.Discard.RemoveAt(silver.Discard.Count - 1);
        silver.PendingDraw.Add(card);
        silver.PendingSource = DrawSource.Discard;
        silver.Phase = TurnPhase.ExchangeDecision;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.discardTaken", new
        {
            player = NameOf(silver, seat),
            value = card.Value
        }));
        return result;
    }

    private SilverActionResult ProcessTakeSquireCard(SilverState silver, TakeSquireCardPayload? payload)
    {
        if (silver.Phase != TurnPhase.TurnStart || silver.PendingDraw.Count > 0)
        {
            return FailResult(Errors.WrongPhase);
        }
        if (payload is null || payload.DisplayIndex < 0 || payload.DisplayIndex >= silver.Display.Count)
        {
            return FailResult(Errors.InvalidSlot);
        }
        if (!HasOwnerExchangeable(silver, silver.CurrentPlayerIndex))
        {
            return FailResult(Errors.ExchangeNotAllowed);
        }

        var seat = silver.CurrentPlayerIndex;
        var card = silver.Display[payload.DisplayIndex];
        silver.Display.RemoveAt(payload.DisplayIndex);
        silver.PendingDraw.Add(card);
        silver.PendingSource = DrawSource.Display;
        silver.Phase = TurnPhase.ExchangeDecision;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.squireCardTaken", new
        {
            player = NameOf(silver, seat),
            value = card.Value
        }));
        return result;
    }

    private SilverActionResult ProcessChooseDrawnCard(SilverState silver, ChooseDrawnCardPayload? payload)
    {
        if (silver.Phase != TurnPhase.TricksterChoice) return FailResult(Errors.WrongPhase);
        if (payload is null) return FailResult(Errors.InvalidPayload);

        var chosen = silver.PendingDraw.FirstOrDefault(c => c.Id == payload.CardId);
        if (chosen is null) return FailResult(Errors.InvalidPayload);

        // The unchosen extra cards go back to the TOP of the deck in the same
        // order they were drawn (the first-drawn ends up on top again), without
        // the player gaining further information from them.
        var returned = silver.PendingDraw.Where(c => c.Id != payload.CardId).ToList();
        returned.Reverse();
        foreach (var other in returned)
        {
            silver.AddToDeckTop(other);
        }
        silver.PendingDraw = new List<SilverCard> { chosen };
        silver.Phase = TurnPhase.DrawnDecision;
        return Ok();
    }

    private SilverActionResult ProcessDiscardDrawnCard(SilverState silver)
    {
        if (silver.Phase != TurnPhase.DrawnDecision || silver.PendingDraw.Count == 0)
        {
            return FailResult(Errors.WrongPhase);
        }

        var seat = silver.CurrentPlayerIndex;
        var card = silver.PendingDraw[0];
        silver.PendingDraw.Clear();
        card.FaceUp = true;
        silver.Discard.Add(card);

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.cardDiscarded", new
        {
            player = NameOf(silver, seat),
            value = card.Value
        }));

        if (card.Value is >= 5 and <= 12)
        {
            // A 5-12 card discarded straight from the deck opens its one-shot
            // ability window before the turn ends.
            silver.Phase = TurnPhase.AbilityPending;
            silver.DiscardedAbilityCardId = card.Id;
        }
        else
        {
            result.TurnEnded = true;
            result.CheckRoundEnd = true;
            result.IsTurnAction = true;
        }
        return result;
    }

    private SilverActionResult ProcessSkipAbility(SilverState silver)
    {
        if (silver.Phase != TurnPhase.AbilityPending) return FailResult(Errors.WrongPhase);
        // A Witch peek without an exchange leaves the looked-at card on top of
        // the deck; the knowledge the Witch player gained is kept.
        silver.DiscardedAbilityCardId = null;
        silver.WitchPeekedCardId = null;
        silver.RevealerChooserSeat = null;
        silver.Phase = TurnPhase.TurnStart;

        return new SilverActionResult
        {
            IsValid = true,
            TurnEnded = true,
            CheckRoundEnd = true,
            IsTurnAction = true
        };
    }

    private SilverActionResult ProcessExchange(SilverState silver, bool fromDeck, ExchangePayload? payload)
    {
        var expectedPhase = fromDeck ? TurnPhase.DrawnDecision : TurnPhase.ExchangeDecision;
        var sourceOk = fromDeck
            ? silver.PendingSource == DrawSource.Deck
            : silver.PendingSource is DrawSource.Discard or DrawSource.Display;
        if (silver.Phase != expectedPhase || !sourceOk || silver.PendingDraw.Count == 0)
        {
            return FailResult(Errors.WrongPhase);
        }
        if (payload is null || payload.Slots.Count == 0) return FailResult(Errors.InvalidPayload);

        var seat = silver.CurrentPlayerIndex;
        var incoming = silver.PendingDraw[0];
        var result = ApplyReplacement(silver, seat, payload.Slots, payload.PlacementSlot,
            payload.NewAtEnd, payload.PenaltyAtEnd, incoming, incomingFaceUp: !fromDeck);
        if (!result.IsValid) return result;

        if (fromDeck) AddKnowledge(silver, seat, incoming);

        silver.PendingDraw.Clear();
        silver.Phase = TurnPhase.TurnStart;
        result.TurnEnded = true;
        result.CheckRoundEnd = true;
        result.IsTurnAction = true;
        return result;
    }

    /// <summary>
    /// The atomic replacement core shared by deck/discard/Squire exchanges and
    /// the Master and Witch own-village exchanges: validates the chosen slots
    /// (a card bound by the Silver Amulet may never move; the owner may still
    /// exchange a card a Guard covers), performs the single or matching multi
    /// replacement, or resolves the exact failed-match behavior (cards return
    /// to their original positions and orientations with their values learned
    /// by everyone, the incoming card joins the village, and a 3+ set draws a
    /// face-down penalty card). The incoming card's orientation depends on its
    /// source: discard- and Squire-taken cards were public and enter face up;
    /// deck-drawn cards enter face down, known only to the drawing player.
    /// </summary>
    private SilverActionResult ApplyReplacement(
        SilverState silver, int seat, List<int> rawSlots, int placementSlot,
        bool newAtEnd, bool penaltyAtEnd, SilverCard incoming, bool incomingFaceUp)
    {
        var village = silver.Villages[seat];
        var slots = rawSlots.Distinct().OrderBy(i => i).ToList();
        if (slots.Count == 0) return FailResult(Errors.InvalidPayload);

        foreach (var slot in slots)
        {
            if (slot < 0 || slot >= village.Count) return FailResult(Errors.InvalidSlot);
            if (silver.IsAmuletProtected(village[slot])) return FailResult(Errors.CardProtected);
        }
        if (slots.Count > 1 && !rawSlots.Contains(placementSlot))
        {
            return FailResult(Errors.InvalidPayload);
        }

        var result = Ok();

        if (slots.Count == 1)
        {
            // Single replacement: the old card goes face up on the discard
            // pile; the new card takes the same slot with its source
            // orientation.
            var slot = slots[0];
            var old = village[slot];
            village[slot] = incoming;
            old.FaceUp = true;
            silver.Discard.Add(old);
            incoming.FaceUp = incomingFaceUp;

            result.Events.Add(SilverEvent.Build("silver.exchanged", new
            {
                player = NameOf(silver, seat),
                count = 1
            }));
        }
        else if (SetMatches(slots.Select(i => village[i])))
        {
            // Multi replacement: the set is flipped face up (proving the
            // match), discarded, and the new card goes into any one of the
            // freed spots; remaining cards close the gaps.
            var insertAt = placementSlot - slots.Count(i => i < placementSlot);
            var setCards = slots.Select(i => village[i]).ToList();

            foreach (var card in setCards)
            {
                card.FaceUp = true;
                silver.Discard.Add(card);
            }
            foreach (var slot in slots.OrderByDescending(i => i))
            {
                village.RemoveAt(slot);
            }
            incoming.FaceUp = incomingFaceUp;
            village.Insert(Math.Min(insertAt, village.Count), incoming);

            result.Events.Add(SilverEvent.Build("silver.exchanged", new
            {
                player = NameOf(silver, seat),
                count = slots.Count
            }));
        }
        else
        {
            // Failed match: every slid card returns to its ORIGINAL position
            // and orientation (everyone saw the flip-to-prove and learns the
            // values), the incoming card is added at either end with its
            // source orientation, and a set of 3+ additionally draws a
            // face-down penalty card without looking at it.
            var setCards = slots.Select(i => village[i]).ToList();
            var originalOrientation = setCards.Select(c => (card: c, wasFaceUp: c.FaceUp)).ToList();
            foreach (var (card, _) in originalOrientation)
            {
                card.FaceUp = true;
                for (var s = 0; s < PlayerCount(silver); s++)
                {
                    AddKnowledge(silver, s, card);
                }
            }
            foreach (var (card, wasFaceUp) in originalOrientation)
            {
                card.FaceUp = wasFaceUp;
            }

            incoming.FaceUp = incomingFaceUp;
            if (newAtEnd) village.Add(incoming); else village.Insert(0, incoming);

            if (slots.Count >= 3 && silver.Deck.Count > 0)
            {
                var penalty = silver.DrawTop()!;
                penalty.FaceUp = false;
                if (penaltyAtEnd) village.Add(penalty); else village.Insert(0, penalty);
                result.Events.Add(SilverEvent.Build("silver.penaltyCard", new { player = NameOf(silver, seat) }));
            }

            result.Events.Add(SilverEvent.Build("silver.exchangeFailed", new
            {
                player = NameOf(silver, seat),
                count = slots.Count
            }));
        }

        return result;
    }

    private SilverActionResult ProcessMoveGuard(SilverState silver, int seat, MoveGuardPayload? payload)
    {
        if (!CanUseOwnTurnAbility(silver)) return FailResult(Errors.WrongPhase);
        if (payload is null) return FailResult(Errors.InvalidPayload);

        var village = silver.Villages[seat];
        if (payload.GuardSlot < 0 || payload.GuardSlot >= village.Count
            || payload.TargetSlot < 0 || payload.TargetSlot >= village.Count
            || payload.GuardSlot == payload.TargetSlot)
        {
            return FailResult(Errors.InvalidSlot);
        }

        var guard = village[payload.GuardSlot];
        if (!guard.FaceUp || guard.Value != 3) return FailResult(Errors.AbilityNotAvailable);
        if (silver.IsAmuletProtected(guard)) return FailResult(Errors.CardProtected);
        if (silver.AbilitiesUsedThisTurn.Contains($"Guard:{guard.Id}"))
        {
            return FailResult(Errors.AbilityAlreadyUsed);
        }

        var target = village[payload.TargetSlot];
        if (silver.IsProtected(target)) return FailResult(Errors.CardProtected);

        // Attaching to a new card also moves it away from any previous one.
        silver.Guards[guard.Id] = target.Id;
        MarkAbilityUsed(silver, $"Guard:{guard.Id}");
        silver.ActedThisTurn = true;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = "Guard"
        }));
        return result;
    }

    private SilverActionResult ProcessRemoveGuard(SilverState silver, int seat, RemoveGuardPayload? payload)
    {
        if (!CanUseOwnTurnAbility(silver)) return FailResult(Errors.WrongPhase);
        if (payload is null) return FailResult(Errors.InvalidPayload);

        var village = silver.Villages[seat];
        if (payload.GuardSlot < 0 || payload.GuardSlot >= village.Count)
        {
            return FailResult(Errors.InvalidSlot);
        }

        var guard = village[payload.GuardSlot];
        if (!guard.FaceUp || guard.Value != 3) return FailResult(Errors.AbilityNotAvailable);
        if (silver.IsAmuletProtected(guard)) return FailResult(Errors.CardProtected);
        if (!silver.Guards.ContainsKey(guard.Id)) return FailResult(Errors.GuardNotAttached);
        if (silver.AbilitiesUsedThisTurn.Contains($"Guard:{guard.Id}"))
        {
            return FailResult(Errors.AbilityAlreadyUsed);
        }

        silver.Guards.Remove(guard.Id);
        MarkAbilityUsed(silver, $"Guard:{guard.Id}");
        silver.ActedThisTurn = true;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = "Guard"
        }));
        return result;
    }

    private SilverActionResult ProcessCallCensus(SilverState silver, int seat)
    {
        if (silver.Phase != TurnPhase.TurnStart) return FailResult(Errors.WrongPhase);
        if (silver.CensusCallerIndex is not null) return FailResult(Errors.VoteNotAllowed);
        if (silver.ActedThisTurn) return FailResult(Errors.VoteNotAllowed);
        if (silver.Villages[seat].Count > 4) return FailResult(Errors.VoteNotAllowed);

        silver.CensusCallerIndex = seat;
        silver.RemainingCensusTurns = PlayerCount(silver) - 1 - silver.EliminatedPlayerIndexes.Count;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.voteCalled", new { player = NameOf(silver, seat) }));
        result.TurnEnded = true;
        result.CheckRoundEnd = true;
        result.IsTurnAction = true;
        if (silver.RemainingCensusTurns <= 0)
        {
            // No other active player: the round ends immediately.
            result.RoundEnded = true;
        }
        return result;
    }

    private SilverActionResult ProcessPlaceAmulet(SilverState silver, int seat, PlaceAmuletPayload? payload)
    {
        if (!CanUseOwnTurnAbility(silver)) return FailResult(Errors.WrongPhase);
        if (silver.AmuletHolderIndex != seat) return FailResult(Errors.AmuletNotAvailable);
        if (!silver.AmuletPlaceable) return FailResult(Errors.AmuletNotAvailable);
        if (silver.AmuletPlacedCardId is not null) return FailResult(Errors.AmuletNotAvailable);
        if (payload is null) return FailResult(Errors.InvalidPayload);

        var village = silver.Villages[seat];
        if (payload.SlotIndex < 0 || payload.SlotIndex >= village.Count) return FailResult(Errors.InvalidSlot);
        var card = village[payload.SlotIndex];
        if (silver.IsProtected(card)) return FailResult(Errors.CardProtected);

        silver.AmuletPlacedCardId = card.Id;
        silver.ActedThisTurn = true;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.amuletPlaced", new { player = NameOf(silver, seat) }));
        return result;
    }

    /// <summary>
    /// Fetches one of the actor's own village cards. <paramref name="blockGuarded"/>
    /// distinguishes outsider semantics (a Guard-covered card is untouchable)
    /// from owner semantics (the owner may look at / move / burn the card a
    /// Guard covers, but never the Amulet-bound card).
    /// </summary>
    private bool TryGetOwnCard(SilverState silver, int seat, int slot, bool requireFaceDown, bool blockGuarded, out string error, out SilverCard card)
    {
        var village = silver.Villages[seat];
        if (slot < 0 || slot >= village.Count)
        {
            error = Errors.InvalidSlot;
            card = null!;
            return false;
        }
        card = village[slot];
        if (requireFaceDown && card.FaceUp)
        {
            error = Errors.InvalidSlot;
            card = null!;
            return false;
        }
        if (blockGuarded ? silver.IsProtected(card) : silver.IsAmuletProtected(card))
        {
            error = Errors.CardProtected;
            card = null!;
            return false;
        }
        error = string.Empty;
        return true;
    }
}
