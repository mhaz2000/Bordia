using Silver.Models;

namespace Silver;

/// <summary>
/// Card abilities for Silver and the system actions (turn timeout, game time
/// expiry). Ability activation timings follow the rulebook's icon taxonomy:
/// 0-1 are persistent while face up in any village (Villager ending, Squire
/// reveals), 2-4 activate on the owner's turn while face up in their village
/// (Enchanter peek, Guard protection, Trickster extra draws in the draw
/// action), 5-12 only when the card is discarded immediately after being drawn
/// from the deck, 13 applies to replacement matching (one wildcard).
/// </summary>
public sealed partial class SilverGame
{
    private SilverActionResult ProcessUseAbility(SilverState silver, int seat, UseAbilityPayload? payload)
    {
        if (payload is null) return FailResult(Errors.InvalidPayload);

        return payload.Ability switch
        {
            SilverAbility.Enchanter => ProcessEnchanter(silver, seat, payload),
            SilverAbility.Exposer => ProcessExposer(silver, seat, payload),
            SilverAbility.Revealer => ProcessRevealer(silver, seat, payload),
            SilverAbility.ApprenticeSeer => ProcessApprenticeSeer(silver, seat, payload),
            SilverAbility.Seer => ProcessSeer(silver, seat, payload),
            SilverAbility.Beholder => ProcessBeholder(silver, seat, payload),
            SilverAbility.Master => ProcessMaster(silver, seat, payload),
            SilverAbility.Witch => ProcessWitch(silver, seat, payload),
            SilverAbility.Robber => ProcessRobber(silver, seat, payload),
            _ => FailResult(Errors.UnknownAbility)
        };
    }

    /// <summary>
    /// The window in which persistent own-village abilities (Enchanter) and
    /// free actions (Guard moves, amulet placement) may be used: any sub-phase
    /// of the actor's turn except the Trickster's pending card choice, where
    /// the draw decision must be completed first.
    /// </summary>
    private static bool CanUseOwnTurnAbility(SilverState silver)
    {
        return silver.Phase is TurnPhase.TurnStart or TurnPhase.DrawnDecision
            or TurnPhase.ExchangeDecision or TurnPhase.AbilityPending;
    }

    // ----- 2-4 tier: face up in your own village, on your turn -----

    /// <summary>
    /// Enchanter (2): for every face-up Enchanter in your village, look at one
    /// of your own face-down cards. Per-instance rights are tracked as plain
    /// use entries counted against the face-up Enchanter total. Cards covered
    /// by your own Guard remain viewable by you (only the Amulet binds the
    /// owner).
    /// </summary>
    private SilverActionResult ProcessEnchanter(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!CanUseOwnTurnAbility(silver)) return FailResult(Errors.WrongPhase);
        var enchanters = silver.Villages[seat].Count(c => c.FaceUp && c.Value == 2);
        if (enchanters == 0) return FailResult(Errors.AbilityNotAvailable);
        if (AbilityUsesThisTurn(silver, "Enchanter") >= enchanters)
        {
            return FailResult(Errors.AbilityAlreadyUsed);
        }
        if (!TryGetOwnCard(silver, seat, payload.OwnSlot ?? -1, requireFaceDown: true, blockGuarded: false, out var error, out var card))
        {
            return FailResult(error);
        }

        AddKnowledge(silver, seat, card);
        MarkAbilityUsed(silver, "Enchanter");
        silver.ActedThisTurn = true;
        // Peeks are secret: no shared event entry.
        return Ok();
    }

    // ----- Revealer opponent-choice (pending from the 5-12 window) -----

    /// <summary>
    /// Revealer (6) prompt answer: the targeted opponent chooses one of their
    /// own face-down, uncovered cards to turn face up. The acting player's turn
    /// then ends (the ability was spent by the actor; the chooser never owes a
    /// turn or a clock).
    /// </summary>
    private SilverActionResult ProcessChooseRevealCard(SilverState silver, int seat, ChooseRevealCardPayload? payload)
    {
        if (silver.Phase != TurnPhase.AbilityPending || silver.RevealerChooserSeat != seat)
        {
            return FailResult(Errors.WrongPhase);
        }
        if (payload is null) return FailResult(Errors.InvalidPayload);

        var village = silver.Villages[seat];
        if (payload.SlotIndex < 0 || payload.SlotIndex >= village.Count) return FailResult(Errors.InvalidSlot);
        var card = village[payload.SlotIndex];
        if (card.FaceUp || silver.IsProtected(card)) return FailResult(Errors.InvalidSlot);

        silver.RevealerChooserSeat = null;
        var result = FinishPendingAbility(silver);
        // The chooser is not the acting player: no timer accounting for them.
        result.IsTurnAction = false;
        card.FaceUp = true;
        result.Events.Add(SilverEvent.Build("silver.cardRevealed", new
        {
            player = NameOf(silver, silver.CurrentPlayerIndex),
            value = card.Value
        }));
        return result;
    }

    // ----- 5-12 tier: the discarded-from-deck card's one-shot window -----

    private SilverActionResult ProcessExposer(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Exposer)) return FailResult(Errors.WrongPhase);
        if (!TryGetOwnCard(silver, seat, payload.OwnSlot ?? -1, requireFaceDown: true, blockGuarded: false, out var error, out var card))
        {
            return FailResult(error);
        }

        var result = FinishPendingAbility(silver);
        card.FaceUp = true;
        result.Events.Add(SilverEvent.Build("silver.cardRevealed", new
        {
            player = NameOf(silver, seat),
            value = card.Value
        }));
        return result;
    }

    private SilverActionResult ProcessRevealer(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Revealer)) return FailResult(Errors.WrongPhase);
        if (payload.TargetPlayerIndex is not { } target || target == seat
            || target < 0 || target >= PlayerCount(silver))
        {
            return FailResult(Errors.InvalidTargetPlayer);
        }
        if (HiddenViewableSlots(silver, target).Count == 0)
        {
            return FailResult(Errors.InvalidTargetPlayer);
        }

        // The opponent chooses which of their face-down cards to reveal.
        silver.RevealerChooserSeat = target;

        var result = Ok();
        result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = nameof(SilverAbility.Revealer)
        }));
        return result;
    }

    /// <summary>
    /// Apprentice Seer (7): secretly look at up to two face-down cards in your
    /// own village. They remain face down; only you learn them.
    /// </summary>
    private SilverActionResult ProcessApprenticeSeer(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.ApprenticeSeer)) return FailResult(Errors.WrongPhase);
        var slots = payload.PeekSlots ?? (payload.OwnSlot is { } own ? new List<int> { own } : new List<int>());
        if (slots.Count is < 1 or > 2) return FailResult(Errors.InvalidPayload);

        var resolved = new List<SilverCard>();
        foreach (var slot in slots.Distinct())
        {
            if (!TryGetOwnCard(silver, seat, slot, requireFaceDown: true, blockGuarded: false, out var error, out var card))
            {
                return FailResult(error);
            }
            resolved.Add(card);
        }

        var result = FinishPendingAbility(silver);
        foreach (var card in resolved)
        {
            AddKnowledge(silver, seat, card);
        }
        return result;
    }

    /// <summary>
    /// Seer (8): secretly look at one face-down card in an opponent's village.
    /// </summary>
    private SilverActionResult ProcessSeer(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Seer)) return FailResult(Errors.WrongPhase);
        if (payload.TargetPlayerIndex is { } target && target == seat)
        {
            return FailResult(Errors.InvalidTargetPlayer);
        }
        if (!TryGetVillageCard(silver, payload.TargetPlayerIndex, payload.TargetSlot,
                requireFaceDown: true, blockGuarded: true, out var error, out var card))
        {
            return FailResult(error);
        }

        var result = FinishPendingAbility(silver);
        AddKnowledge(silver, seat, card);
        return result;
    }

    /// <summary>
    /// Beholder (9): secretly look at one face-down card in your own or an
    /// opponent's village. Guard-covered cards block outsiders; your own
    /// guarded cards remain yours to look at. The Amulet always binds.
    /// </summary>
    private SilverActionResult ProcessBeholder(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Beholder)) return FailResult(Errors.WrongPhase);
        if (payload.TargetPlayerIndex is not { } target || target < 0 || target >= PlayerCount(silver))
        {
            return FailResult(Errors.InvalidTargetPlayer);
        }
        if (!TryGetVillageCard(silver, target, payload.TargetSlot,
                requireFaceDown: true, blockGuarded: target != seat, out var error, out var card))
        {
            return FailResult(error);
        }

        var result = FinishPendingAbility(silver);
        AddKnowledge(silver, seat, card);
        return result;
    }

    /// <summary>
    /// Master (10): take any card from the discard pile and use it to replace
    /// one or more of your village cards (normal replacement rules, entering
    /// face up; displaced cards land on top of the Master in the discard
    /// pile). With no replacement chosen the Master simply stays discarded.
    /// </summary>
    private SilverActionResult ProcessMaster(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Master)) return FailResult(Errors.WrongPhase);

        var slots = (payload.OwnSlots ?? (payload.OwnSlot is { } own ? new List<int> { own } : new List<int>()))
            .Distinct().OrderBy(i => i).ToList();

        // Decline / no legal replacement: the Master goes to the discard pile
        // (where it already is) and the turn ends.
        if (slots.Count == 0)
        {
            var declined = FinishPendingAbility(silver);
            declined.Events.Add(SilverEvent.Build("silver.abilityUsed", new
            {
                player = NameOf(silver, seat),
                ability = nameof(SilverAbility.Master)
            }));
            return declined;
        }

        if (payload.DiscardIndex is not { } discardIndex || discardIndex < 0 || discardIndex >= silver.Discard.Count)
        {
            return FailResult(Errors.InvalidPayload);
        }
        var abilityCard = silver.Discard.LastOrDefault(c => c.Id == silver.DiscardedAbilityCardId);
        if (abilityCard is not null && silver.Discard[discardIndex].Id == abilityCard.Id)
        {
            return FailResult(Errors.InvalidPayload);
        }

        var village = silver.Villages[seat];
        foreach (var slot in slots)
        {
            if (slot < 0 || slot >= village.Count) return FailResult(Errors.InvalidSlot);
            if (silver.IsAmuletProtected(village[slot])) return FailResult(Errors.CardProtected);
        }
        if (slots.Count > 1 && (!payload.PlacementSlot.HasValue || !slots.Contains(payload.PlacementSlot.Value)))
        {
            return FailResult(Errors.InvalidPayload);
        }

        var placement = slots.Count > 1 ? payload.PlacementSlot!.Value : slots[0];

        var result = FinishPendingAbility(silver);
        var taken = silver.Discard[discardIndex];
        silver.Discard.RemoveAt(discardIndex);
        var replacement = ApplyReplacement(silver, seat, slots, placement,
            payload.NewAtEnd, payload.PenaltyAtEnd, taken, incomingFaceUp: true);
        if (!replacement.IsValid) return replacement;
        result.Events.AddRange(replacement.Events);
        result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = nameof(SilverAbility.Master)
        }));
        return result;
    }

    /// <summary>
    /// Witch (11), two steps: first look at the top deck card (it stays on top
    /// until resolved), then either exchange it into a village — own village
    /// single/multi per the normal replacement rules, or a single card in an
    /// opponent's village — or decline (the looked-at card simply stays on top
    /// of the deck; the peek itself is public table behavior).
    /// </summary>
    private SilverActionResult ProcessWitch(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Witch)) return FailResult(Errors.WrongPhase);
        if (silver.Deck.Count == 0) return FailResult(Errors.DeckEmpty);

        var top = silver.Deck[^1];

        // Step 1: pure peek.
        if (payload.OwnSlots is not { Count: > 0 } && payload.OwnSlot is null && payload.TargetPlayerIndex is null)
        {
            if (silver.WitchPeekedCardId is not null) return FailResult(Errors.AbilityAlreadyUsed);
            AddKnowledge(silver, seat, top);
            silver.WitchPeekedCardId = top.Id;
            var peek = Ok();
            peek.Events.Add(SilverEvent.Build("silver.abilityUsed", new
            {
                player = NameOf(silver, seat),
                ability = nameof(SilverAbility.Witch)
            }));
            return peek;
        }

        // Step 2: exchange (auto-peeking first if the player skipped step 1).
        if (silver.WitchPeekedCardId is { } peeked && peeked != top.Id)
        {
            // The deck top changed since the peek (should be unreachable);
            // re-validate against the current top.
            return FailResult(Errors.WrongPhase);
        }
        AddKnowledge(silver, seat, top);

        if (payload.TargetPlayerIndex is { } target)
        {
            // Opponent's village: a single, publicly-legal target card.
            if (target == seat || target < 0 || target >= PlayerCount(silver))
            {
                return FailResult(Errors.InvalidTargetPlayer);
            }
            if (!TryGetVillageCard(silver, target, payload.TargetSlot,
                    requireFaceDown: false, blockGuarded: true, out var error, out var targetCard))
            {
                return FailResult(error);
            }

            var result = FinishPendingAbility(silver);
            var deckCard = silver.DrawTop()!;
            // A deck-sourced card enters face down - known only to the Witch.
            deckCard.FaceUp = false;
            var targetVillage = silver.Villages[target];
            targetVillage[targetVillage.IndexOf(targetCard)] = deckCard;
            targetCard.FaceUp = true;
            silver.Discard.Add(targetCard);
            result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
            {
                player = NameOf(silver, seat),
                ability = nameof(SilverAbility.Witch)
            }));
            return result;
        }

        // Own village: normal replacement rules (single or matching multi).
        var slots = (payload.OwnSlots ?? new List<int> { payload.OwnSlot!.Value })
            .Distinct().OrderBy(i => i).ToList();
        var village = silver.Villages[seat];
        foreach (var slot in slots)
        {
            if (slot < 0 || slot >= village.Count) return FailResult(Errors.InvalidSlot);
            if (silver.IsAmuletProtected(village[slot])) return FailResult(Errors.CardProtected);
        }
        if (slots.Count > 1 && (!payload.PlacementSlot.HasValue || !slots.Contains(payload.PlacementSlot.Value)))
        {
            return FailResult(Errors.InvalidPayload);
        }

        var swap = FinishPendingAbility(silver);
        var incoming = silver.DrawTop()!;
        var replacement = ApplyReplacement(silver, seat, slots,
            slots.Count > 1 ? payload.PlacementSlot!.Value : slots[0],
            payload.NewAtEnd, payload.PenaltyAtEnd, incoming, incomingFaceUp: false);
        if (!replacement.IsValid) return replacement;
        swap.Events.AddRange(replacement.Events);
        swap.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = nameof(SilverAbility.Witch)
        }));
        return swap;
    }

    /// <summary>
    /// Robber (12): steal any one card from an opponent's village (face up or
    /// face down; covered cards are out of reach) and put one of your own
    /// cards into the freed spot. Stolen orientation is preserved; the victim
    /// does not learn the identity of the card they receive; the Robber may
    /// look at the stolen card.
    /// </summary>
    private SilverActionResult ProcessRobber(SilverState silver, int seat, UseAbilityPayload payload)
    {
        if (!IsPendingAbility(silver, SilverAbility.Robber)) return FailResult(Errors.WrongPhase);
        if (!TryGetOwnCard(silver, seat, payload.OwnSlot ?? -1, requireFaceDown: false, blockGuarded: false, out var error, out var ownCard))
        {
            return FailResult(error);
        }
        if (!TryGetVillageCard(silver, payload.TargetPlayerIndex, payload.TargetSlot,
                requireFaceDown: false, blockGuarded: true, out var error2, out var targetCard))
        {
            return FailResult(error2);
        }
        var targetSeat = payload.TargetPlayerIndex!.Value;
        if (targetSeat == seat) return FailResult(Errors.InvalidTargetPlayer);

        var result = FinishPendingAbility(silver);
        var ownVillage = silver.Villages[seat];
        var targetVillage = silver.Villages[targetSeat];
        ownVillage[ownVillage.IndexOf(ownCard)] = targetCard;
        targetVillage[targetVillage.IndexOf(targetCard)] = ownCard;

        AddKnowledge(silver, seat, targetCard);

        result.Events.Add(SilverEvent.Build("silver.abilityUsed", new
        {
            player = NameOf(silver, seat),
            ability = nameof(SilverAbility.Robber)
        }));
        return result;
    }

    // ----- pending-window plumbing -----

    private bool IsPendingAbility(SilverState silver, SilverAbility ability)
    {
        if (silver.Phase != TurnPhase.AbilityPending || silver.DiscardedAbilityCardId is not { } id)
        {
            return false;
        }
        var card = silver.Discard.LastOrDefault(c => c.Id == id);
        return card is not null && card.Value == AbilityValue(ability);
    }

    private static int AbilityValue(SilverAbility ability) => ability switch
    {
        SilverAbility.Exposer => 5,
        SilverAbility.Revealer => 6,
        SilverAbility.ApprenticeSeer => 7,
        SilverAbility.Seer => 8,
        SilverAbility.Beholder => 9,
        SilverAbility.Master => 10,
        SilverAbility.Witch => 11,
        SilverAbility.Robber => 12,
        _ => -1
    };

    private static SilverActionResult FinishPendingAbility(SilverState silver)
    {
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

    /// <summary>
    /// Fetches a card from any village. <paramref name="blockGuarded"/> must be
    /// true for actions taken by someone other than the card's owner.
    /// </summary>
    private bool TryGetVillageCard(SilverState silver, int? targetSeat, int? slot, bool requireFaceDown, bool blockGuarded, out string error, out SilverCard card)
    {
        if (targetSeat is not { } seat || slot is not { } index
            || seat < 0 || seat >= PlayerCount(silver))
        {
            error = Errors.InvalidTargetPlayer;
            card = null!;
            return false;
        }
        var village = silver.Villages[seat];
        if (index < 0 || index >= village.Count)
        {
            error = Errors.InvalidSlot;
            card = null!;
            return false;
        }
        card = village[index];
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

    // ----- system actions -----

    private SilverActionResult ProcessTurnTimeout(SilverState silver, int seat)
    {
        if (silver.NextActionDeadlineUtc is not { } deadline || DateTime.UtcNow <= deadline)
        {
            return FailResult(Errors.TimerNotExpired);
        }

        var config = silver.TimerConfig ?? new SilverTurnTimerConfig();
        var timer = silver.EnsureTimer(seat);
        timer.ConsecutiveTimeouts++;

        // A pending drawn-card decision never stalls the game: the drawn card
        // is discarded without its ability and the turn advances. A pending
        // discard/display-taken card returns to the top of the discard pile.
        if (silver.Phase is TurnPhase.TricksterChoice or TurnPhase.DrawnDecision && silver.PendingDraw.Count > 0)
        {
            var drawn = silver.PendingDraw[0];
            silver.PendingDraw.RemoveAt(0);
            drawn.FaceUp = true;
            silver.Discard.Add(drawn);
            var returned = silver.PendingDraw;
            returned.Reverse();
            foreach (var extra in returned)
            {
                silver.AddToDeckTop(extra);
            }
            silver.PendingDraw.Clear();
        }
        else if (silver.Phase == TurnPhase.ExchangeDecision && silver.PendingDraw.Count > 0)
        {
            // A taken card that was never placed goes back to the area it came
            // from: discard-taken to the discard top, Squire-displayed to the
            // display. It must NOT be dumped into the other pile.
            var taken = silver.PendingDraw[0];
            silver.PendingDraw.Clear();
            taken.FaceUp = true;
            if (silver.PendingSource == DrawSource.Display)
            {
                silver.Display.Add(taken);
            }
            else
            {
                silver.Discard.Add(taken);
            }
        }
        silver.DiscardedAbilityCardId = null;
        silver.WitchPeekedCardId = null;
        silver.RevealerChooserSeat = null;
        silver.Phase = TurnPhase.TurnStart;

        ChargeOverrun(silver, seat, config);

        var result = Ok();
        result.TurnEnded = true;
        result.CheckRoundEnd = true;
        result.IsTurnAction = false;
        result.PlayerAdvanced = true;

        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            if (PlayerCount(silver) == 2)
            {
                // A 2-player game ends immediately with the survivor winning.
                var survivor = (seat + 1) % 2;
                result.GameEnded = true;
                result.WinnerIndex = survivor;
                silver.EliminatedPlayerIndexes.Add(seat);
                result.Events.Add(SilverEvent.Build("silver.playerEliminated", new { player = NameOf(silver, seat) }));
                result.Events.Add(SilverEvent.Build("silver.gameFinished", new { winner = NameOf(silver, survivor) }));
            }
            else
            {
                silver.EliminatedPlayerIndexes.Add(seat);
                result.Events.Add(SilverEvent.Build("silver.playerEliminated", new { player = NameOf(silver, seat) }));
            }
        }

        if (!result.GameEnded)
        {
            AdvancePlayer(silver);
            silver.SetTurnClock();
        }

        return result;
    }

    private SilverActionResult ProcessGameTimeExpired(SilverState silver)
    {
        if (silver.GameEndsAtUtc is not { } endsAt || DateTime.UtcNow < endsAt)
        {
            return FailResult(Errors.TimeNotUp);
        }

        // Force-finish on the current standings: cumulative points of the
        // completed rounds, resolved by the normal amulet/seat tie-break.
        var result = Ok();
        result.GameEnded = true;
        result.WinnerIndex = DetermineWinner(silver);
        result.Events.Add(SilverEvent.Build("silver.gameTimeExpired", new { }));
        result.Events.Add(SilverEvent.Build("silver.gameFinished", new
        {
            winner = result.WinnerIndex is { } w ? NameOf(silver, w) : null
        }));
        return result;
    }

    private static void ChargeOverrun(SilverState silver, int seat, SilverTurnTimerConfig config)
    {
        var timer = silver.EnsureTimer(seat);
        if (silver.TurnStartUtc == default) return;

        var elapsed = DateTime.UtcNow - silver.TurnStartUtc;
        var allotted = silver.MaxTurnSeconds(seat, config);
        var overrun = Math.Max(0, elapsed.TotalSeconds - allotted);
        var bankCover = Math.Min(timer.BankSeconds, overrun);
        timer.BankSeconds -= bankCover;
        timer.DeferredPenaltySeconds += Math.Min(config.MaxOverrunSeconds, overrun - bankCover);
    }
}
