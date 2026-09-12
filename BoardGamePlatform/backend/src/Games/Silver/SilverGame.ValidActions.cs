using System.Text.Json;
using GameEngine.Core.Models;
using Silver.Models;

namespace Silver;

/// <summary>
/// Engine-level contract returning only the actions that are currently legal
/// for the requesting player. Used by tests and tooling, not exposed over HTTP.
/// </summary>
public sealed partial class SilverGame
{
    /// <inheritdoc />
    public IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId)
    {
        var actions = new List<GameAction>();
        if (state.IsOver) return actions;
        if (!state.TryGetString("SilverState", out var json) || json is null) return actions;

        var silver = SilverState.FromJson(json);
        silver.PlayerIds = state.Players;
        var seat = IndexOf(state, playerId);
        if (seat < 0 || seat >= silver.Villages.Count) return actions;
        if (silver.EliminatedPlayerIndexes.Contains(seat)) return actions;

        // Initial peeks: any active player, until the round ends, max two.
        if (silver.PeeksUsed[seat] < 2)
        {
            foreach (var slot in OwnViewableSlots(silver, seat))
            {
                actions.Add(CreateAction(SilverActionType.PeekVillageCard, new PeekVillageCardPayload { SlotIndex = slot }));
            }
        }

        // A pending Revealer prompt is answered by the TARGET player, not the actor.
        if (silver.RevealerChooserSeat == seat)
        {
            foreach (var slot in HiddenViewableSlots(silver, seat))
            {
                actions.Add(CreateAction(SilverActionType.ChooseRevealCard, new ChooseRevealCardPayload { SlotIndex = slot }));
            }
            return actions;
        }

        if (seat != silver.CurrentPlayerIndex) return actions;

        switch (silver.Phase)
        {
            case TurnPhase.TurnStart:
                if (silver.Deck.Count > 0)
                {
                    var tricksters = silver.Villages[seat].Count(c => c.FaceUp && c.Value == 4);
                    var maxExtra = Math.Min(tricksters, Math.Max(0, silver.Deck.Count - 1));
                    for (var extra = 0; extra <= maxExtra; extra++)
                    {
                        actions.Add(CreateAction(SilverActionType.DrawFromDeck, new DrawFromDeckPayload { TricksterExtra = extra }));
                    }
                }
                if (silver.Discard.Count > 0 && HasOwnerExchangeable(silver, seat))
                {
                    actions.Add(CreateAction(SilverActionType.TakeDiscard, new { }));
                }
                if (silver.Display.Count > 0 && HasOwnerExchangeable(silver, seat))
                {
                    for (var i = 0; i < silver.Display.Count; i++)
                    {
                        actions.Add(CreateAction(SilverActionType.TakeSquireCard, new TakeSquireCardPayload { DisplayIndex = i }));
                    }
                }
                AddFreeActions(silver, seat, actions);
                if (CanCallCensus(silver, seat))
                {
                    actions.Add(CreateAction(SilverActionType.CallCensus, new { }));
                }
                break;

            case TurnPhase.TricksterChoice:
                foreach (var card in silver.PendingDraw)
                {
                    actions.Add(CreateAction(SilverActionType.ChooseDrawnCard, new ChooseDrawnCardPayload { CardId = card.Id }));
                }
                break;

            case TurnPhase.DrawnDecision:
                actions.Add(CreateAction(SilverActionType.DiscardDrawnCard, new { }));
                AddReplacementActions(silver, seat, SilverActionType.ExchangeWithDrawn, actions);
                AddFreeActions(silver, seat, actions);
                break;

            case TurnPhase.ExchangeDecision:
                AddReplacementActions(silver, seat, SilverActionType.ExchangeWithDiscard, actions);
                AddFreeActions(silver, seat, actions);
                break;

            case TurnPhase.AbilityPending:
                AddPendingAbilityActions(silver, seat, actions);
                actions.Add(CreateAction(SilverActionType.SkipAbility, new { }));
                AddFreeActions(silver, seat, actions);
                break;
        }

        return actions;
    }

    private static bool CanCallCensus(SilverState silver, int seat)
    {
        return silver.Phase == TurnPhase.TurnStart
            && silver.CensusCallerIndex is null
            && !silver.ActedThisTurn
            && silver.Villages[seat].Count <= 4;
    }

    private static bool CanPlaceAmulet(SilverState silver, int seat)
    {
        return silver.AmuletHolderIndex == seat
            && silver.AmuletPlaceable
            && silver.AmuletPlacedCardId is null
            && CanUseOwnTurnAbility(silver);
    }

    /// <summary>
    /// Free actions usable at any point during the actor's turn (except the
    /// Trickster choice): Enchanter peeks, Guard moves/removals, amulet
    /// placement. None of them end the turn.
    /// </summary>
    private static void AddFreeActions(SilverState silver, int seat, List<GameAction> actions)
    {
        if (!CanUseOwnTurnAbility(silver)) return;

        // Enchanter (2): one peek per face-up Enchanter per turn.
        var enchanters = silver.Villages[seat].Count(c => c.FaceUp && c.Value == 2);
        if (enchanters > AbilityUsesThisTurn(silver, "Enchanter"))
        {
            foreach (var slot in OwnViewableSlots(silver, seat))
            {
                actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                {
                    Ability = SilverAbility.Enchanter,
                    OwnSlot = slot
                }));
            }
        }

        // Guard (3): each face-up Guard may attach/move/remove once per turn.
        var village = silver.Villages[seat];
        for (var g = 0; g < village.Count; g++)
        {
            var guard = village[g];
            if (!guard.FaceUp || guard.Value != 3 || silver.IsAmuletProtected(guard)) continue;
            if (silver.AbilitiesUsedThisTurn.Contains($"Guard:{guard.Id}")) continue;

            if (silver.Guards.ContainsKey(guard.Id))
            {
                actions.Add(CreateAction(SilverActionType.RemoveGuard, new RemoveGuardPayload { GuardSlot = g }));
            }
            for (var t = 0; t < village.Count; t++)
            {
                if (t == g || silver.IsProtected(village[t])) continue;
                actions.Add(CreateAction(SilverActionType.MoveGuard, new MoveGuardPayload { GuardSlot = g, TargetSlot = t }));
            }
        }

        // Silver Amulet placement.
        if (CanPlaceAmulet(silver, seat))
        {
            for (var i = 0; i < village.Count; i++)
            {
                if (silver.IsProtected(village[i])) continue;
                actions.Add(CreateAction(SilverActionType.PlaceAmulet, new PlaceAmuletPayload { SlotIndex = i }));
            }
        }
    }

    private static void AddReplacementActions(SilverState silver, int seat, SilverActionType type, List<GameAction> actions)
    {
        var village = silver.Villages[seat];
        var slots = new List<int>();
        for (var i = 0; i < village.Count; i++)
        {
            if (!silver.IsAmuletProtected(village[i])) slots.Add(i);
        }

        // Single replacements.
        foreach (var slot in slots)
        {
            actions.Add(CreateAction(type, new ExchangePayload { Slots = new List<int> { slot }, PlacementSlot = slot }));
        }

        // Multi replacements: every subset of owner-movable cards of size >= 2
        // whose values match (a single Doppelgänger acts as the wildcard; two
        // match only each other), with each freed slot as the placement.
        foreach (var subset in MatchingSubsets(village, slots))
        {
            foreach (var placement in subset)
            {
                actions.Add(CreateAction(type, new ExchangePayload
                {
                    Slots = subset.ToList(),
                    PlacementSlot = placement
                }));
            }
        }
    }

    private static IEnumerable<List<int>> MatchingSubsets(List<SilverCard> village, List<int> slots)
    {
        var remaining = slots.ToList();
        var results = new List<List<int>>();
        void Backtrack(List<int> current, int start)
        {
            if (current.Count >= 2 && SetMatches(current.Select(i => village[i])))
            {
                results.Add(current.ToList());
            }
            for (var i = start; i < remaining.Count; i++)
            {
                current.Add(remaining[i]);
                Backtrack(current, i + 1);
                current.RemoveAt(current.Count - 1);
            }
        }
        Backtrack(new List<int>(), 0);
        return results;
    }

    private static void AddPendingAbilityActions(SilverState silver, int seat, List<GameAction> actions)
    {
        if (silver.DiscardedAbilityCardId is not { } id) return;
        var card = silver.Discard.LastOrDefault(c => c.Id == id);
        if (card is null) return;
        var village = silver.Villages[seat];

        switch (card.Value)
        {
            case 5: // Exposer: reveal one of your own face-down cards.
                foreach (var slot in OwnViewableSlots(silver, seat))
                {
                    actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                    {
                        Ability = SilverAbility.Exposer,
                        OwnSlot = slot
                    }));
                }
                break;

            case 6: // Revealer: choose an opponent with a hideable card; they pick it.
                for (var target = 0; target < PlayerCount(silver); target++)
                {
                    if (target == seat) continue;
                    if (HiddenViewableSlots(silver, target).Count == 0) continue;
                    actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                    {
                        Ability = SilverAbility.Revealer,
                        TargetPlayerIndex = target
                    }));
                }
                break;

            case 7: // Apprentice Seer: peek one or two of your own face-down cards.
            {
                var slots = OwnViewableSlots(silver, seat);
                foreach (var slot in slots)
                {
                    actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                    {
                        Ability = SilverAbility.ApprenticeSeer,
                        PeekSlots = new List<int> { slot }
                    }));
                }
                for (var i = 0; i < slots.Count; i++)
                {
                    for (var j = i + 1; j < slots.Count; j++)
                    {
                        actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                        {
                            Ability = SilverAbility.ApprenticeSeer,
                            PeekSlots = new List<int> { slots[i], slots[j] }
                        }));
                    }
                }
                break;
            }

            case 8: // Seer: peek one face-down card in an opponent's village.
                for (var target = 0; target < PlayerCount(silver); target++)
                {
                    if (target == seat) continue;
                    foreach (var slot in HiddenViewableSlots(silver, target))
                    {
                        actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                        {
                            Ability = SilverAbility.Seer,
                            TargetPlayerIndex = target,
                            TargetSlot = slot
                        }));
                    }
                }
                break;

            case 9: // Beholder: peek one face-down card in any village.
                for (var target = 0; target < PlayerCount(silver); target++)
                {
                    foreach (var slot in target == seat ? OwnViewableSlots(silver, seat) : HiddenViewableSlots(silver, target))
                    {
                        actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                        {
                            Ability = SilverAbility.Beholder,
                            TargetPlayerIndex = target,
                            TargetSlot = slot
                        }));
                    }
                }
                break;

            case 10: // Master: any discard card (except the Master itself) replacing
                      // one card or a matching set; declining leaves it discarded.
                actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                {
                    Ability = SilverAbility.Master
                }));
                for (var discardIndex = 0; discardIndex < silver.Discard.Count; discardIndex++)
                {
                    if (silver.Discard[discardIndex].Id == id) continue;
                    AddMasterReplacementOptions(silver, seat, actions, discardIndex);
                }
                break;

            case 11: // Witch: peek alone, or exchange the deck top into your
                     // village (single/matching set) or an opponent's (single).
                if (silver.Deck.Count > 0)
                {
                    if (silver.WitchPeekedCardId is null)
                    {
                        actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                        {
                            Ability = SilverAbility.Witch
                        }));
                    }
                    AddWitchOwnOptions(silver, seat, actions);
                    for (var target = 0; target < PlayerCount(silver); target++)
                    {
                        if (target == seat) continue;
                        var targetVillage = silver.Villages[target];
                        for (var slot = 0; slot < targetVillage.Count; slot++)
                        {
                            if (silver.IsProtected(targetVillage[slot])) continue;
                            actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                            {
                                Ability = SilverAbility.Witch,
                                TargetPlayerIndex = target,
                                TargetSlot = slot
                            }));
                        }
                    }
                }
                break;

            case 12: // Robber: steal any uncovered opponent card (any orientation)
                     // and give one owner-movable village card in its place.
                for (var ownSlot = 0; ownSlot < village.Count; ownSlot++)
                {
                    if (silver.IsAmuletProtected(village[ownSlot])) continue;
                    for (var target = 0; target < PlayerCount(silver); target++)
                    {
                        if (target == seat) continue;
                        var targetVillage = silver.Villages[target];
                        for (var slot = 0; slot < targetVillage.Count; slot++)
                        {
                            if (silver.IsProtected(targetVillage[slot])) continue;
                            actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                            {
                                Ability = SilverAbility.Robber,
                                OwnSlot = ownSlot,
                                TargetPlayerIndex = target,
                                TargetSlot = slot
                            }));
                        }
                    }
                }
                break;
        }
    }

    private static void AddMasterReplacementOptions(SilverState silver, int seat, List<GameAction> actions, int discardIndex)
    {
        var village = silver.Villages[seat];
        var slots = new List<int>();
        for (var i = 0; i < village.Count; i++)
        {
            if (!silver.IsAmuletProtected(village[i])) slots.Add(i);
        }
        foreach (var slot in slots)
        {
            actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
            {
                Ability = SilverAbility.Master,
                OwnSlots = new List<int> { slot },
                DiscardIndex = discardIndex
            }));
        }
        foreach (var subset in MatchingSubsets(village, slots))
        {
            foreach (var placement in subset)
            {
                actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                {
                    Ability = SilverAbility.Master,
                    OwnSlots = subset.ToList(),
                    PlacementSlot = placement,
                    DiscardIndex = discardIndex
                }));
            }
        }
    }

    private static void AddWitchOwnOptions(SilverState silver, int seat, List<GameAction> actions)
    {
        var village = silver.Villages[seat];
        var slots = new List<int>();
        for (var i = 0; i < village.Count; i++)
        {
            if (!silver.IsAmuletProtected(village[i])) slots.Add(i);
        }
        foreach (var slot in slots)
        {
            actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
            {
                Ability = SilverAbility.Witch,
                OwnSlots = new List<int> { slot }
            }));
        }
        foreach (var subset in MatchingSubsets(village, slots))
        {
            foreach (var placement in subset)
            {
                actions.Add(CreateAction(SilverActionType.UseAbility, new UseAbilityPayload
                {
                    Ability = SilverAbility.Witch,
                    OwnSlots = subset.ToList(),
                    PlacementSlot = placement
                }));
            }
        }
    }
}
