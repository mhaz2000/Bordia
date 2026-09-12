using System.Text.Json;
using GameEngine.Core.Models;
using Silver;
using Silver.Models;

return SilverHarness.Run();

internal static class SilverHarness
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();

    public static int Run()
    {
        SetupChecks();
        SquireChecks();
        TricksterChecks();
        VillagerChecks();
        EnchanterGuardChecks();
        TurnFlowChecks();
        PendingAbilityChecks();
        MasterWitchRobberChecks();
        DoppelgangerChecks();
        HiddenInformationChecks();
        CensusRoundEndingChecks();
        ScoringChecks();
        PersistenceChecks();
        GameCompletionChecks();
        ValidActionsChecks();

        Console.WriteLine();
        Console.WriteLine($"SILVER HARNESS: {_passed} passed, {_failed} failed");
        foreach (var failure in _failures)
        {
            Console.WriteLine("  FAIL: " + failure);
        }
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
        }
        else
        {
            _failed++;
            _failures.Add(name);
            Console.WriteLine($"  FAIL {name}");
        }
    }

    private static GameOptions Opts(int players, int? seed = null)
    {
        var ids = Enumerable.Range(0, players)
            .Select(i => new PlayerId(Guid.NewGuid()))
            .ToList();
        var names = ids.ToDictionary(p => p.UserId, p => $"P{ids.IndexOf(p)}");
        var settings = JsonSerializer.Serialize(new
        {
            PlayerNames = names,
            Seed = seed,
            Timer = new { BaseTurnSeconds = 45.0, MaxBankSeconds = 120.0, MaxOverrunSeconds = 15.0, MaxAfkTurns = 3, TotalGameTimeMinutes = 60.0 }
        });
        return new GameOptions { GameType = "Silver", Players = ids, Settings = settings };
    }

    private static SilverState Parse(GameState state)
    {
        state.TryGetString("SilverState", out var json);
        return SilverState.FromJson(json!);
    }

    private static Guid IdOfValue(List<SilverCard> village, int value)
        => village.First(c => c.Value == value).Id;

    private sealed class H
    {
        public SilverGame Game = new();
        public GameState State;
        public SilverState S;
        public bool Pad = true;

        public H(int players, int? seed = null)
        {
            State = Game.CreateGame(Opts(players, seed));
            S = Parse(State);
        }

        public PlayerId Uid(int seat) => State.Players[seat];

        public void Save() => State.Data["SilverState"] = S.ToJson();

        public GameResult Act(int seat, string actionType, object? payload = null)
        {
            PadTo52();
            Save();
            var action = new GameAction
            {
                PlayerId = Uid(seat),
                ActionType = actionType,
                Payload = payload is null ? "{}" : JsonSerializer.Serialize(payload)
            };
            var result = Game.ProcessAction(State, action);
            if (result.IsValid && result.NewState is not null) State = result.NewState;
            S = Parse(State);
            return result;
        }

        /// <summary>Keeps the 52-card invariant after crafting so round redeals always deal.</summary>
        public void PadTo52()
        {
            if (!Pad) return;
            var total = S.Villages.Sum(v => v.Count) + S.Deck.Count + S.Discard.Count
                + S.Display.Count + S.PendingDraw.Count + S.Removed.Count;
            while (total < 52)
            {
                S.Deck.Insert(0, new SilverCard(6));
                total++;
            }
        }

        /// <summary>Draws and discards (or declines the ability) as a plain turn.</summary>
        public void TakeSimpleTurn(int seat)
        {
            Act(seat, "DrawFromDeck", new DrawFromDeckPayload());
            Act(seat, "DiscardDrawnCard");
            if (S.Phase == TurnPhase.AbilityPending)
            {
                Act(seat, "SkipAbility");
            }
        }

        public SilverView View(int seat)
            => JsonSerializer.Deserialize<SilverView>(
                   (string)Game.GetPlayerView(State, Uid(seat)).Data["SilverState"]!)!;

        public List<GameAction> Valid(int seat)
        {
            Save();
            return Game.GetValidActions(State, Uid(seat)).ToList();
        }

        public void SetVillage(int seat, params SilverCard[] cards) => S.Villages[seat] = cards.ToList();
        public void SetDeck(params SilverCard[] cards) => S.Deck = cards.ToList();
        public void SetDiscard(params SilverCard[] cards) => S.Discard = cards.ToList();
        public void SetDisplay(params SilverCard[] cards) => S.Display = cards.ToList();

        /// <summary>Directly attaches a Guard card (in any seat's village) over a target card.</summary>
        public void AttachGuard(int seat, int guardSlot, int targetSlot)
            => S.Guards[S.Villages[seat][guardSlot].Id] = S.Villages[seat][targetSlot].Id;

        public SilverCard C(int value, bool faceUp = false) => new()
        {
            Value = value,
            FaceUp = faceUp
        };
    }

    // ---------------------------------------------------------------- setup

    private static void SetupChecks()
    {
        Console.WriteLine("Setup");
        foreach (var n in new[] { 2, 3, 4 })
        {
            var h = new H(n);
            Check($"setup.{n}p: five face-down cards per village",
                h.S.Villages.All(v => v.Count == 5 && v.All(c => !c.FaceUp)));
            Check($"setup.{n}p: deck+discard = 32 (deck {31})",
                h.S.Deck.Count == 31 && h.S.Discard.Count == 1 && h.S.Discard[0].FaceUp);
            Check($"setup.{n}p: removed-from-game count", h.S.Removed.Count == 5 * (4 - n));
            Check($"setup.{n}p: deadlines set",
                h.S.NextActionDeadlineUtc is not null && h.S.GameEndsAtUtc is not null);

            var all = h.S.Villages.SelectMany(v => v).Concat(h.S.Deck).Concat(h.S.Discard)
                .Concat(h.S.Removed).ToList();
            Check($"setup.{n}p: 52 cards total", all.Count == 52);
            Check($"setup.{n}p: composition (0 x2, 1-12 x4, 13 x2)",
                all.GroupBy(c => c.Value).All(g => g.Count() == (g.Key == 0 || g.Key == 13 ? 2 : 4))
                && all.GroupBy(c => c.Value).Count() == 14);

            Check($"setup.{n}p: amulet initially assigned to the starting player",
                h.S.AmuletHolderIndex == h.S.RoundStartPlayerIndex && h.S.AmuletHolderIndex == h.S.CurrentPlayerIndex);
            Check($"setup.{n}p: empty display/guards, no census",
                h.S.Display.Count == 0 && h.S.Guards.Count == 0 && h.S.CensusCallerIndex is null);
            Check($"setup.{n}p: fresh knowledge and peek counters",
                h.S.Knowledge.All(k => k.Count == 0) && h.S.PeeksUsed.All(p => p == 0));
        }

        foreach (var bad in new[] { 1, 5 })
        {
            var threw = false;
            try { _ = new H(bad); }
            catch (ArgumentException) { threw = true; }
            Check($"setup rejects {bad} players", threw);
        }

        var hp = new H(2);
        var cp = hp.S.CurrentPlayerIndex;
        Check("setup: first peek ok",
            hp.Act(cp, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 0 }).IsValid);
        Check("setup: second peek ok",
            hp.Act(cp, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 1 }).IsValid);
        Check("setup: third peek rejected",
            hp.Act(cp, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 2 }).ErrorCode == "silver.peekLimitReached");
        Check("setup: peeks do not consume the turn", hp.S.CurrentPlayerIndex == cp);

        var s1 = new H(3, seed: 42);
        var s2 = new H(3, seed: 42);
        Check("setup: same seed reproduces the deal",
            s1.S.Deck.Select(c => c.Value).SequenceEqual(s2.S.Deck.Select(c => c.Value))
            && s1.S.Villages.Select(v => v.Sum(c => c.Value)).SequenceEqual(s2.S.Villages.Select(v => v.Sum(c => c.Value))));
        var s3 = new H(3, seed: 123);
        Check("setup: different seed changes the deal",
            !s1.S.Deck.Select(c => c.Value).SequenceEqual(s3.S.Deck.Select(c => c.Value)));
    }

    // --------------------------------------------------------------- squire

    private static void SquireChecks()
    {
        Console.WriteLine("Squire");
        // Passive: a face-up Squire (in ANY village) reveals one deck card
        // after every turn.
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;
        h.SetVillage(other, h.C(1, faceUp: true), h.C(2));
        h.SetDeck(h.C(9), h.C(3));
        h.TakeSimpleTurn(cp);
        Check("squire: card revealed after the turn",
            h.S.Display.Count == 1 && h.S.Display[0].FaceUp && h.S.Display[0].Value == 9);
        Check("squire: turn passed to the squire owner", h.S.CurrentPlayerIndex == other);

        // Take a Squire card instead of drawing; it enters face up.
        h.SetVillage(other, h.C(1, faceUp: true), h.C(2), h.C(5));
        h.Act(other, "TakeSquireCard", new TakeSquireCardPayload { DisplayIndex = 0 });
        Check("squire: take enters the mandatory replacement",
            h.S.Phase == TurnPhase.ExchangeDecision && h.S.PendingSource == DrawSource.Display);
        h.Act(other, "ExchangeWithDiscard", new ExchangePayload { Slots = new List<int> { 2 }, PlacementSlot = 2 });
        Check("squire: taken card entered face up",
            h.S.Villages[other][2].Value == 9 && h.S.Villages[other][2].FaceUp);
        Check("squire: taking a display card does not touch the deck",
            h.S.PendingDraw.Count == 0);

        // Refill respects the CURRENT squire count; revealed cards stay.
        h.TakeSimpleTurn(cp);
        Check("squire: refilled to the squire still on the table",
            h.S.Display.Count == 1 && h.S.Display[0].FaceUp);
        h.SetVillage(other, h.C(2), h.C(5));
        h.TakeSimpleTurn(other);
        var stayed = h.S.Display.Count;
        h.TakeSimpleTurn(cp);
        Check("squire: no further reveals once the Squire left", h.S.Display.Count == stayed);

        // Two face-up Squires reveal two cards after a turn.
        var h2 = new H(2);
        cp = h2.S.CurrentPlayerIndex;
        h2.SetVillage(cp, h2.C(1, faceUp: true), h2.C(1, faceUp: true), h2.C(2));
        h2.SetDeck(h2.C(9), h2.C(4), h2.C(3), h2.C(2), h2.C(2), h2.C(2));
        h2.Pad = false;
        h2.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h2.Act(cp, "DiscardDrawnCard");
        Check("squire: two face-up Squires reveal two cards",
            h2.S.Display.Count == 2 && h2.S.Round == 1);

        // A deck that cannot supply the reveals empties and ends the round.
        var h3 = new H(2) { Pad = false };
        cp = h3.S.CurrentPlayerIndex;
        other = 1 - cp;
        h3.SetVillage(cp, h3.C(1, faceUp: true), h3.C(1, faceUp: true), h3.C(2), h3.C(2), h3.C(2));
        h3.SetVillage(other, h3.C(2), h3.C(2), h3.C(2), h3.C(2), h3.C(2));
        h3.SetDeck(h3.C(4));
        h3.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h3.Act(cp, "DiscardDrawnCard");
        Check("squire: an exhausted deck cannot refill the display", h3.S.Display.Count == 0);
        Check("squire: the emptied deck ends the round", h3.S.Round == 2);

        // Invalid take indices.
        var h4 = new H(2);
        cp = h4.S.CurrentPlayerIndex;
        Check("squire: take from an empty display rejected",
            h4.Act(cp, "TakeSquireCard", new TakeSquireCardPayload { DisplayIndex = 0 }).ErrorCode == "silver.invalidSlot");

        // A timeout while a taken Squire card awaits replacement returns the
        // card to the display - it must never land in the discard pile.
        var h5 = new H(2);
        cp = h5.S.CurrentPlayerIndex;
        h5.SetVillage(cp, h5.C(1, faceUp: true), h5.C(2), h5.C(2), h5.C(2), h5.C(2));
        h5.SetDisplay(h5.C(5));
        var discardTopBefore = h5.S.Discard[^1].Id;
        var takenDisplayId = h5.S.Display[0].Id;
        h5.Act(cp, "TakeSquireCard", new TakeSquireCardPayload { DisplayIndex = 0 });
        h5.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        h5.Save();
        h5.Act(cp, "TurnTimeout");
        Check("squire: timeout returns the taken card to the display, not the discard",
            h5.S.Display.Any(c => c.Id == takenDisplayId)
            && h5.S.Discard[^1].Id == discardTopBefore
            && h5.S.Discard.All(c => c.Id != takenDisplayId));
    }

    // ------------------------------------------------------------- trickster

    private static void TricksterChecks()
    {
        Console.WriteLine("Trickster");
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(4, faceUp: true), h.C(4, faceUp: true), h.C(1));
        h.SetDeck(h.C(9), h.C(3), h.C(2), h.C(1));
        h.Pad = false;
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 2 });
        Check("trickster: two extras draw three cards",
            h.S.Phase == TurnPhase.TricksterChoice && h.S.PendingDraw.Select(c => c.Value).SequenceEqual(new[] { 1, 2, 3 }));

        var ids = h.S.PendingDraw.Select(c => c.Id).ToList();
        h.Act(cp, "ChooseDrawnCard", new ChooseDrawnCardPayload { CardId = ids[1] });
        Check("trickster: chosen card kept",
            h.S.Phase == TurnPhase.DrawnDecision && h.S.PendingDraw.Single().Value == 2);
        Check("trickster: unchosen cards return to the TOP in drawn order (first-drawn on top)",
            h.S.Deck[^1].Id == ids[0] && h.S.Deck[^2].Id == ids[2] && h.S.Deck[^3].Value == 9);
        Check("trickster: returned cards stay face down and give the player no knowledge",
            !h.S.Deck[^1].FaceUp && !h.S.Knowledge[cp].ContainsKey(ids[0]) && !h.S.Knowledge[cp].ContainsKey(ids[2]));
        Check("trickster: chosen card can then be discarded",
            h.Act(cp, "DiscardDrawnCard").IsValid && h.S.Discard[^1].Value == 2);

        var h2 = new H(2);
        cp = h2.S.CurrentPlayerIndex;
        Check("trickster: extra draw without a face-up Trickster rejected",
            h2.Act(cp, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 1 }).ErrorCode == "silver.abilityNotAvailable");

        h2 = new H(2);
        cp = h2.S.CurrentPlayerIndex;
        h2.SetVillage(cp, h2.C(4, faceUp: true), h2.C(1));
        h2.SetDeck(h2.C(9), h2.C(3));
        Check("trickster: one extra with one face-up Trickster",
            h2.Act(cp, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 1 }).IsValid
            && h2.S.PendingDraw.Count == 2);
        h2 = new H(2);
        cp = h2.S.CurrentPlayerIndex;
        h2.SetVillage(cp, h2.C(4, faceUp: true), h2.C(1));
        h2.SetDeck(h2.C(4, faceUp: true), h2.C(3), h2.C(2), h2.C(1));
        Check("trickster: asking for more extras than face-up Tricksters rejected",
            h2.Act(cp, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 2 }).ErrorCode == "silver.abilityNotAvailable");
    }

    private static void VillagerChecks()
    {
        Console.WriteLine("Villager");
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(0, faceUp: true), h.C(0, faceUp: true), h.C(1));
        h.TakeSimpleTurn(cp);
        Check("villager: two face-up in one village end the round", h.S.Round == 2);

        // A Villager face up in the discard pile does NOT end the round.
        var hd = new H(2) { Pad = false };
        cp = hd.S.CurrentPlayerIndex;
        hd.SetVillage(cp, hd.C(0, faceUp: true), hd.C(1));
        hd.SetDiscard(hd.C(3), hd.C(0, faceUp: true));
        hd.SetDeck(hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(0));
        hd.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hd.Act(cp, "DiscardDrawnCard");
        if (hd.S.Phase == TurnPhase.AbilityPending) hd.Act(cp, "SkipAbility");
        Check("villager: face-up villagers in the discard pile do not end the round",
            hd.S.Discard.Count(c => c.Value == 0 && c.FaceUp) >= 2 && hd.S.Round == 1);

        // A Villager revealed beside the deck by a Squire does NOT end the round.
        var hs = new H(2) { Pad = false };
        cp = hs.S.CurrentPlayerIndex;
        var otherS = 1 - cp;
        hs.SetVillage(cp, hs.C(0, faceUp: true), hs.C(1), hs.C(1));
        hs.SetVillage(otherS, hs.C(1, faceUp: true), hs.C(1), hs.C(1));
        hs.SetDeck(hs.C(9), hs.C(4), hs.C(3), hs.C(2), hs.C(0), hs.C(4));
        hs.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hs.Act(cp, "DiscardDrawnCard");
        Check("villager: a villager in the Squire display does not end the round",
            hs.S.Display.Count == 1 && hs.S.Display[0].Value == 0 && hs.S.Round == 1);
    }

    private static void EnchanterGuardChecks()
    {
        Console.WriteLine("Enchanter & Guard");
        // Enchanter (2): one peek per face-up Enchanter.
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;
        h.SetVillage(cp, h.C(2, faceUp: true), h.C(8), h.C(3));
        var peeked = h.S.Villages[cp][1].Id;
        Check("enchanter: peek own card",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 1 }).IsValid);
        Check("enchanter: knowledge recorded", h.S.Knowledge[cp].ContainsKey(peeked));
        Check("enchanter: once per face-up Enchanter",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 2 }).ErrorCode == "silver.abilityAlreadyUsed");
        Check("enchanter: does not end the turn", h.S.CurrentPlayerIndex == cp);

        h.SetVillage(cp, h.C(2, faceUp: true), h.C(2, faceUp: true), h.C(8), h.C(3), h.C(4));
        h.TakeSimpleTurn(cp);
        h.TakeSimpleTurn(other);
        Check("enchanter: two face-up Enchanters allow two peeks",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 2 }).IsValid
            && h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 3 }).IsValid
            && h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 4 }).ErrorCode == "silver.abilityAlreadyUsed");

        var h2 = new H(2);
        h2.SetVillage(h2.S.CurrentPlayerIndex, h2.C(8));
        Check("enchanter: requires a face-up Enchanter",
            h2.Act(h2.S.CurrentPlayerIndex, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 0 }).ErrorCode == "silver.abilityNotAvailable");

        // Guard (3): explicit protection relationship.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(3, faceUp: true), h.C(8), h.C(7, faceUp: true));
        var guardId = h.S.Villages[cp][0].Id;
        var targetId = h.S.Villages[cp][1].Id;
        Check("guard: attach",
            h.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 1 }).IsValid
            && h.S.Guards[guardId] == targetId);
        Check("guard: once per Guard per turn",
            h.Act(cp, "RemoveGuard", new RemoveGuardPayload { GuardSlot = 0 }).ErrorCode == "silver.abilityAlreadyUsed");
        Check("guard: does not end the turn", h.S.CurrentPlayerIndex == cp);
        Check("guard: the owner may still peek the guarded card",
            h.Act(cp, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 1 }).IsValid);

        // Move/remove across turns and outsider blocking.
        h.TakeSimpleTurn(cp);
        h.TakeSimpleTurn(other);
        Check("guard: move to another card",
            h.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 2 }).IsValid
            && h.S.Guards[guardId] == h.S.Villages[cp][2].Id);
        h.TakeSimpleTurn(cp);
        h.TakeSimpleTurn(other);
        Check("guard: remove", h.Act(cp, "RemoveGuard", new RemoveGuardPayload { GuardSlot = 0 }).IsValid
            && h.S.Guards.Count == 0);
        Check("guard: removing an unattached Guard rejected",
            h.Act(cp, "RemoveGuard", new RemoveGuardPayload { GuardSlot = 0 }).ErrorCode == "silver.guardNotAttached");
        Check("guard: attaching a non-Guard rejected",
            h.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 1, TargetSlot = 2 }).ErrorCode == "silver.abilityNotAvailable");

        // Outsiders cannot target guarded cards; the owner can exchange them away.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(3, faceUp: true), h.C(8));
        h.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 1 });
        var guardedId = h.S.Villages[cp][1].Id;
        h.SetDeck(h.C(9), h.C(2));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        h.SetVillage(other, h.C(8, faceUp: true), h.C(1));
        h.SetDeck(h.C(8));
        h.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(other, "DiscardDrawnCard");
        Check("guard: an opposing Seer cannot peek the guarded card",
            h.Act(other, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Seer, TargetPlayerIndex = cp, TargetSlot = 1
            }).ErrorCode == "silver.cardProtected");
        Check("guard: the Seer cannot know the guarded card",
            !h.S.Knowledge[other].ContainsKey(guardedId));
        h.Act(other, "SkipAbility");
        h.Act(cp, "TakeDiscard");
        Check("guard: the owner may exchange the guarded card away",
            h.Act(cp, "ExchangeWithDiscard", new ExchangePayload { Slots = new List<int> { 1 }, PlacementSlot = 1 }).IsValid);
        Check("guard: the link is dropped when the card leaves the village",
            h.S.Guards.Count == 0);
    }

    // ----------------------------------------------------------- turn flow

    private static void TurnFlowChecks()
    {
        Console.WriteLine("Turn flow");
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;

        h.SetDeck(h.C(9), h.C(3));
        h.PadTo52();
        var deckCount = h.S.Deck.Count;
        Check("flow: draw from deck ok",
            h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload()).IsValid);
        Check("flow: drawn-card decision phase", h.S.Phase == TurnPhase.DrawnDecision);
        Check("flow: deck shrank by one", h.S.Deck.Count == deckCount - 1);
        Check("flow: engine drew the top card", h.S.PendingDraw.Single().Value == 3);
        Check("flow: other player cannot act",
            h.Act(other, "DrawFromDeck", new DrawFromDeckPayload()).ErrorCode == "silver.notYourTurn");
        Check("flow: take-discard rejected mid-decision",
            h.Act(cp, "TakeDiscard").ErrorCode == "silver.wrongPhase");

        Check("flow: discard drawn card ok", h.Act(cp, "DiscardDrawnCard").IsValid);
        Check("flow: discarded card is public discard top",
            h.S.Discard[^1].Value == 3 && h.S.Discard[^1].FaceUp);
        Check("flow: no ability window for a 3", h.S.Phase == TurnPhase.TurnStart);
        Check("flow: turn advanced clockwise", h.S.CurrentPlayerIndex == other);

        // Single exchange from the deck: per the official rules the drawn
        // card enters the village FACE DOWN, known only to its owner.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(5), h.C(7));
        h.SetDeck(h.C(9), h.C(6));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        var exchangedSlotCard = h.S.Villages[cp][1];
        Check("flow: single exchange ok",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 1 }, PlacementSlot = 1 }).IsValid);
        Check("flow: new card in same slot, face down",
            h.S.Villages[cp][1].Value == 6 && !h.S.Villages[cp][1].FaceUp);
        Check("flow: opponent's view cannot see the face-down replacement",
            h.View(other).Villages[cp][1].Value is null);
        Check("flow: old card face up on discard", h.S.Discard[^1].Id == exchangedSlotCard.Id && h.S.Discard[^1].FaceUp);
        Check("flow: exchanger knows the card taken from the deck",
            h.S.Knowledge[cp].ContainsKey(h.S.Villages[cp][1].Id));

        // Take-discard: exchange is mandatory.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetDiscard(h.C(4));
        h.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(other, "DiscardDrawnCard");
        Check("flow: take discard ok", h.Act(cp, "TakeDiscard").IsValid);
        Check("flow: take-discard enters exchange decision", h.S.Phase == TurnPhase.ExchangeDecision);
        Check("flow: discard not allowed after take-discard",
            h.Act(cp, "DiscardDrawnCard").ErrorCode == "silver.wrongPhase");
        h.SetVillage(cp, h.C(2), h.C(2));
        Check("flow: mandatory exchange ok",
            h.Act(cp, "ExchangeWithDiscard", new ExchangePayload { Slots = new List<int> { 0 }, PlacementSlot = 0 }).IsValid);
        Check("flow: discard-taken card enters face up",
            h.S.Villages[cp][0].Value == 4 && h.S.Villages[cp][0].FaceUp);
        Check("flow: exchange ends the turn", h.S.CurrentPlayerIndex == other);

        // Multi exchange, matching set.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(5), h.C(5), h.C(1));
        h.SetDeck(h.C(9));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("flow: multi exchange of matching set ok",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 0, 1 }, PlacementSlot = 0 }).IsValid);
        Check("flow: village shrank to the new card + rest",
            h.S.Villages[cp].Count == 2 && h.S.Villages[cp][0].Value == 9 && !h.S.Villages[cp][0].FaceUp
            && h.S.Villages[cp][1].Value == 1);

        // Mismatch: cards return to their ORIGINAL orientations (§11),
        // everyone learns the values, and the incoming card joins the village.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(5, faceUp: true), h.C(6), h.C(1));
        h.SetDeck(h.C(9), h.C(2));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        var five = h.S.Villages[cp][0].Id;
        var six = h.S.Villages[cp][1].Id;
        Check("flow: mismatched exchange is a legal (penalized) action",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload
            {
                Slots = new List<int> { 0, 1 },
                PlacementSlot = 0,
                NewAtEnd = true
            }).IsValid);
        Check("flow: mismatch restores original orientations",
            h.S.Villages[cp].Count == 4
            && h.S.Villages[cp].First(c => c.Id == five).FaceUp
            && !h.S.Villages[cp].First(c => c.Id == six).FaceUp);
        Check("flow: mismatch reveals values to everyone",
            h.S.Knowledge[cp].ContainsKey(five) && h.S.Knowledge[other].ContainsKey(five)
            && h.S.Knowledge[cp].ContainsKey(six) && h.S.Knowledge[other].ContainsKey(six));
        Check("flow: mismatch adds the new card face down at the requested end",
            h.S.Villages[cp][^1].Value == 2 && !h.S.Villages[cp][^1].FaceUp);

        // Mismatch of 3+ draws a face-down penalty card.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(5), h.C(6), h.C(7), h.C(1));
        h.SetDeck(h.C(9), h.C(2), h.C(2));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("flow: 3+ mismatch draws a penalty card",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload
            {
                Slots = new List<int> { 0, 1, 2 },
                PlacementSlot = 0,
                NewAtEnd = true,
                PenaltyAtEnd = true
            }).IsValid);
        Check("flow: village grew and the penalty card is face down & unknown",
            h.S.Villages[cp].Count == 6 && !h.S.Villages[cp][^1].FaceUp
            && !h.S.Knowledge[cp].ContainsKey(h.S.Villages[cp][^1].Id));

        // Only 2-card mismatch sets escape the penalty.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(5), h.C(6), h.C(1), h.C(1), h.C(1));
        h.SetDeck(h.C(9), h.C(2));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        var before = h.S.Deck.Count;
        h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 0, 1 }, PlacementSlot = 0, NewAtEnd = true });
        Check("flow: 2-card mismatch draws no penalty card", h.S.Deck.Count == before - 0);

        // The client can never choose the drawn card.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetDeck(h.C(9), h.C(13));
        Check("flow: draw ignores forged payload fields",
            h.Act(cp, "DrawFromDeck", new { cardIndex = 7, forcedValue = 9, tricksterExtra = 5 }).IsValid
            && h.S.PendingDraw.Single().Value == 13);

        // An amulet-bound card cannot be exchanged away by its owner.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.S.AmuletHolderIndex = cp;
        h.S.AmuletPlaceable = true;
        h.Act(cp, "PlaceAmulet", new PlaceAmuletPayload { SlotIndex = 0 });
        h.SetDeck(h.C(4));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("flow: amulet-bound card cannot be replaced even by the owner",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 0 }, PlacementSlot = 0 }).ErrorCode == "silver.cardProtected");
    }

    // -------------------------------------------------- pending 5-12 abilities

    private static void PendingAbilityChecks()
    {
        Console.WriteLine("Pending abilities (5-9)");

        // Exposer (5): window opens only on deck-draw discard.
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetDeck(h.C(5));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("exposer: window opens on discard", h.S.Phase == TurnPhase.AbilityPending);
        Check("exposer: turn own card face up",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Exposer, OwnSlot = 0 }).IsValid
            && h.S.Villages[cp][0].FaceUp);
        Check("exposer: ability use ends the turn", h.S.CurrentPlayerIndex == other);

        // Revealer (6): the opponent chooses which card to reveal.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(other, h.C(7), h.C(1));
        h.SetDeck(h.C(6));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("revealer: own village rejected",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Revealer, TargetPlayerIndex = cp }).ErrorCode == "silver.invalidTargetPlayer");
        h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Revealer, TargetPlayerIndex = other });
        Check("revealer: prompt pending on the opponent, window still open",
            h.S.RevealerChooserSeat == other && h.S.Phase == TurnPhase.AbilityPending && h.S.CurrentPlayerIndex == cp);
        Check("revealer: the actor cannot answer their own prompt",
            h.Act(cp, "ChooseRevealCard", new ChooseRevealCardPayload { SlotIndex = 0 }).ErrorCode == "silver.notYourTurn");
        Check("revealer: opponent picks their card",
            h.Act(other, "ChooseRevealCard", new ChooseRevealCardPayload { SlotIndex = 0 }).IsValid
            && h.S.Villages[other][0].FaceUp && h.S.Villages[other][0].Value == 7);
        Check("revealer: the actor's turn ended", h.S.CurrentPlayerIndex == other);

        // Apprentice Seer (7): up to two of your own cards.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(8), h.C(3), h.C(1));
        h.SetDeck(h.C(7));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        var a1 = h.S.Villages[cp][0].Id;
        var a2 = h.S.Villages[cp][1].Id;
        Check("apprenticeSeer: peek two own cards",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.ApprenticeSeer,
                PeekSlots = new List<int> { 0, 1 }
            }).IsValid && h.S.Knowledge[cp].ContainsKey(a1) && h.S.Knowledge[cp].ContainsKey(a2));
        Check("apprenticeSeer: peek stays secret",
            !h.S.Knowledge[other].ContainsKey(a1));
        h.SetDeck(h.C(2));
        h.TakeSimpleTurn(other);
        h.SetDeck(h.C(7));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("apprenticeSeer: only own-village slots resolve (own village is the only target)",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.ApprenticeSeer,
                PeekSlots = new List<int> { 5 }
            }).ErrorCode == "silver.invalidSlot");

        // Seer (8): one face-down card in an opponent's village.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(other, h.C(8), h.C(1));
        h.SetVillage(cp, h.C(4), h.C(1));
        h.SetDeck(h.C(8));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("seer: own village rejected",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Seer, TargetPlayerIndex = cp, TargetSlot = 0
            }).ErrorCode == "silver.invalidTargetPlayer");
        var oppCard = h.S.Villages[other][0].Id;
        Check("seer: peek opponent",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Seer, TargetPlayerIndex = other, TargetSlot = 0
            }).IsValid && h.S.Knowledge[cp].ContainsKey(oppCard));
        Check("seer: peek does not flip the card", !h.S.Villages[other][0].FaceUp);

        // Beholder (9): one face-down card anywhere.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetDeck(h.C(9));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        var ownCard = h.S.Villages[cp][1].Id;
        Check("beholder: peek own card",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Beholder, TargetPlayerIndex = cp, TargetSlot = 1
            }).IsValid && h.S.Knowledge[cp].ContainsKey(ownCard));

        // 5-12 abilities never trigger from the discard pile.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(2), h.C(2));
        h.SetDiscard(h.C(9));
        h.Act(cp, "TakeDiscard");
        Check("timing: discard-taken 9 must be exchanged",
            h.Act(cp, "ExchangeWithDiscard", new ExchangePayload { Slots = new List<int> { 0 }, PlacementSlot = 0 }).IsValid);
        Check("timing: no ability window after take-discard",
            h.S.Phase == TurnPhase.TurnStart);

        // A 13 burned from the deck has no window.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetDeck(h.C(13));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("timing: a burned Doppelganger opens no ability window",
            h.S.Phase == TurnPhase.TurnStart && h.S.CurrentPlayerIndex != cp);

        // No census after using an ability this turn.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(2, faceUp: true), h.C(1), h.C(1));
        h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Enchanter, OwnSlot = 1 });
        Check("timing: cannot call census after using an ability",
            h.Act(cp, "CallCensus").ErrorCode == "silver.voteNotAllowed");
    }

    // ------------------------------------------------ Master, Witch, Robber

    private static void MasterWitchRobberChecks()
    {
        Console.WriteLine("Master, Witch, Robber");
        // Master (10): reach any discard card; displaced cards land on top of it.
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetDiscard(h.C(4), h.C(11));
        h.SetDeck(h.C(10));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        var masterId = h.S.Discard[^1].Id;
        Check("master: take any discard card",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Master, OwnSlots = new List<int> { 0 }, DiscardIndex = 0
            }).IsValid && h.S.Villages[cp][0].Value == 4 && h.S.Villages[cp][0].FaceUp);
        Check("master: displaced card on top, above the Master in the pile",
            h.S.Discard[^1].Value == 8 && h.S.Discard[^2].Id == masterId && h.S.Discard[^3].Value == 11);

        // The Master may not be taken by its own ability.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetDeck(h.C(10));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("master: cannot take the Master itself",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Master, OwnSlots = new List<int> { 0 }, DiscardIndex = 1
            }).ErrorCode == "silver.invalidPayload");
        Check("master: declining leaves it discarded and ends the turn",
            h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Master }).IsValid
            && h.S.Discard[^1].Value == 10 && h.S.CurrentPlayerIndex != cp);

        // Master multi-card replacement.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(5), h.C(5), h.C(1));
        h.SetDiscard(h.C(2), h.C(11));
        h.SetDeck(h.C(10));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("master: multi replacement of a matching set",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Master, OwnSlots = new List<int> { 0, 1 }, PlacementSlot = 1, DiscardIndex = 0
            }).IsValid && h.S.Villages[cp].Count == 2 && h.S.Villages[cp][0].Value == 2 && h.S.Villages[cp][0].FaceUp);

        // Witch (11): peek step, then exchange or decline.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(other, h.C(8), h.C(1));
        h.SetDeck(h.C(12), h.C(11));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Witch });
        var topId = h.S.Deck[^1].Id;
        var deckBeforePeek = h.S.Deck.Count;
        Check("witch: peek records knowledge without touching the deck",
            h.S.WitchPeekedCardId == topId && h.S.Knowledge[cp].ContainsKey(topId) && h.S.Deck.Count == deckBeforePeek);
        Check("witch: peek does not end the turn", h.S.CurrentPlayerIndex == cp);
        var victim = h.S.Villages[other][0].Id;
        Check("witch: exchange the looked-at deck card into an opponent village",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Witch, TargetPlayerIndex = other, TargetSlot = 0
            }).IsValid);
        Check("witch: deck top moved into the village face down, known only to the witch",
            h.S.Deck.Count == deckBeforePeek - 1 && h.S.Villages[other][0].Id != victim && !h.S.Villages[other][0].FaceUp
            && h.S.Villages[other][0].Value == 12 && h.S.Deck[^1].Id != topId);
        Check("witch: the victim does not learn the inserted card",
            !h.S.Knowledge[other].ContainsKey(h.S.Villages[other][0].Id));
        Check("witch: displaced card face up on discard",
            h.S.Discard[^1].Id == victim && h.S.Discard[^1].FaceUp);
        Check("witch: ability use ends the turn", h.S.CurrentPlayerIndex == other);

        // Peek + decline leaves the deck untouched.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetDeck(h.C(12), h.C(11));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Witch });
        topId = h.S.Deck[^1].Id;
        Check("witch: declining after the peek leaves the card on the deck",
            h.Act(cp, "SkipAbility").IsValid && h.S.Deck[^1].Id == topId);

        // Witch own-village multi replacement (single-step with auto peek).
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(5), h.C(5), h.C(1));
        h.SetDeck(h.C(3), h.C(11));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("witch: own multi-card exchange with the peeked deck card",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Witch, OwnSlots = new List<int> { 0, 1 }, PlacementSlot = 0
            }).IsValid && h.S.Villages[cp].Count == 2 && h.S.Villages[cp][0].Value == 3 && !h.S.Villages[cp][0].FaceUp
            && h.S.Knowledge[cp].ContainsKey(h.S.Villages[cp][0].Id));

        // Robber (12): any card may be stolen, orientation preserved.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetVillage(other, h.C(6, faceUp: true), h.C(3));
        h.SetDeck(h.C(12));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("robber: steal a FACE-UP opponent card",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Robber, OwnSlot = 0, TargetPlayerIndex = other, TargetSlot = 0
            }).IsValid && h.S.Villages[cp][0].Value == 6 && h.S.Villages[cp][0].FaceUp);
        Check("robber: own card kept its face-down orientation in the victim's village",
            h.S.Villages[other][0].Value == 8 && !h.S.Villages[other][0].FaceUp);
        Check("robber: the victim does not learn the incoming card",
            !h.S.Knowledge[other].ContainsKey(h.S.Villages[other][0].Id));
        Check("robber: robber knows the stolen card",
            h.S.Knowledge[cp].ContainsKey(h.S.Villages[cp][0].Id));

        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetVillage(other, h.C(6), h.C(3));
        h.SetDeck(h.C(12));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("robber: stolen face-down card stays face down in the robber's village",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Robber, OwnSlot = 1, TargetPlayerIndex = other, TargetSlot = 1
            }).IsValid && h.S.Villages[cp][1].Value == 3 && !h.S.Villages[cp][1].FaceUp);
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(8), h.C(1));
        h.SetDeck(h.C(12));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        Check("robber: own village cannot be the steal target",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Robber, OwnSlot = 0, TargetPlayerIndex = cp, TargetSlot = 1
            }).ErrorCode == "silver.invalidTargetPlayer");

        // A card taken from the discard pile never opens its ability window.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(2), h.C(2));
        h.SetDiscard(h.C(12));
        h.Act(cp, "TakeDiscard");
        Check("timing: discard-taken Robber cannot steal",
            h.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Robber, OwnSlot = 0, TargetPlayerIndex = 1 - cp, TargetSlot = 0
            }).ErrorCode == "silver.wrongPhase");
    }

    // ------------------------------------------------------- Doppelgänger

    private static void DoppelgangerChecks()
    {
        Console.WriteLine("Doppelgänger");
        // One Doppelgänger matches any value; two match only each other.
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(13), h.C(7), h.C(7), h.C(1));
        h.SetDeck(h.C(9));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("doppel: {7,13} matches",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 0, 1 }, PlacementSlot = 0 }).IsValid
            && h.S.Villages[cp].Count == 3);

        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(13), h.C(13), h.C(1));
        h.SetDeck(h.C(9));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("doppel: {13,13} matches",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload { Slots = new List<int> { 0, 1 }, PlacementSlot = 0 }).IsValid
            && h.S.Villages[cp].Count == 2 && h.S.Villages[cp][0].Value == 9);

        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(13), h.C(13), h.C(7), h.C(1));
        h.SetDeck(h.C(9), h.C(2));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        var before = h.S.Deck.Count;
        Check("doppel: {13,13,7} fails (two wildcards in one group)",
            h.Act(cp, "ExchangeWithDrawn", new ExchangePayload
            {
                Slots = new List<int> { 0, 1, 2 }, PlacementSlot = 0, NewAtEnd = true, PenaltyAtEnd = true
            }).IsValid && h.S.Villages[cp].Count == 6);
        Check("doppel: failed 3-set drew the penalty card", h.S.Deck.Count == before - 1);

        // Two Doppelgängers at round end score 13 for the round.
        var hw = new H(2);
        cp = hw.S.CurrentPlayerIndex;
        var other = 1 - cp;
        hw.SetVillage(cp, hw.C(1));
        hw.SetVillage(other, hw.C(13), hw.C(13), hw.C(5));
        hw.SetDeck(hw.C(2), hw.C(2));
        hw.Act(cp, "CallCensus");
        hw.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        hw.Act(other, "DiscardDrawnCard");
        Check("doppel: exactly two Doppelgängers score 13",
            hw.S.LastRoundScores![other] == 13 && hw.S.LastRoundScores[cp] == 0);
    }

    // --------------------------------------------------- hidden information

    private static void HiddenInformationChecks()
    {
        Console.WriteLine("Hidden information");
        var h = new H(3);
        var a = h.S.CurrentPlayerIndex;
        var b = (a + 1) % 3;

        var viewA = h.View(a);
        var viewB = h.View(b);
        Check("hidden: initial view hides all face-down values",
            viewA.Villages.SelectMany(v => v).Where(c => !c.FaceUp).All(c => c.Value is null)
            && viewB.Villages.SelectMany(v => v).Where(c => !c.FaceUp).All(c => c.Value is null));
        Check("hidden: discard top is public", viewA.DiscardTop is { } top && top.FaceUp && top.Value is not null);
        Check("hidden: view carries no deck order", viewA.DeckSize == h.S.Deck.Count);
        Check("hidden: removed cards are not exposed",
            viewA.RemovedCount == h.S.Removed.Count && viewA.GetType().GetProperty("RemovedValues") is null);

        h.Act(a, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 0 });
        viewA = h.View(a);
        viewB = h.View(b);
        Check("hidden: peeker sees their own card", viewA.Villages[a][0].Value is not null);
        Check("hidden: others do not see the peeked card", viewB.Villages[a][0].Value is null);

        // A guarded face-down card stays hidden from outsiders.
        var hg = new H(2);
        var gseat = hg.S.CurrentPlayerIndex;
        var gother = 1 - gseat;
        hg.SetVillage(gseat, hg.C(3, faceUp: true), hg.C(8));
        hg.Act(gseat, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 1 });
        var gv = hg.View(gother);
        Check("hidden: guarded card exposes no value to outsiders",
            gv.Villages[gseat][1].Value is null && gv.Villages[gseat][1].Protected);
        Check("hidden: the guard card id is public",
            gv.Villages[gseat][1].GuardedByCardId == hg.S.Villages[gseat][0].Id);

        // Squire peek into B's village via the ability window.
        var ha = new H(2);
        var cp = ha.S.CurrentPlayerIndex;
        var other = 1 - cp;
        ha.SetVillage(cp, ha.C(8, faceUp: true), ha.C(1));
        ha.SetDeck(ha.C(2), ha.C(8));
        ha.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        ha.Act(cp, "DiscardDrawnCard");
        var secretId = ha.S.Villages[other][0].Id;
        Check("hidden: seer act valid",
            ha.Act(cp, "UseAbility", new UseAbilityPayload
            {
                Ability = SilverAbility.Seer, TargetPlayerIndex = other, TargetSlot = 0
            }).IsValid);
        var va = ha.View(cp);
        var vb = ha.View(other);
        Check("hidden: seer's view shows the peeked opponent card",
            va.Villages[other].First(c => c.Id == secretId).Value is not null);
        Check("hidden: opponent's view still hides it",
            vb.Villages[other].First(c => c.Id == secretId).Value is null);
        var seerSeat = cp;
        var seerOpponent = other;

        // Pending deck draw is visible only to the drawer.
        var hp = new H(2);
        cp = hp.S.CurrentPlayerIndex;
        other = 1 - cp;
        hp.SetDeck(hp.C(13));
        hp.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        Check("hidden: drawer sees the pending card value",
            hp.View(cp).PendingDraw.Single().Value == 13);
        Check("hidden: others see no pending value",
            hp.View(other).PendingDraw.Single().Value is null);

        // The Witch peek never leaks the card to others through the deck (the
        // deck order is never serialized).
        var hc = new H(2);
        cp = hc.S.CurrentPlayerIndex;
        other = 1 - cp;
        hc.SetVillage(cp, hc.C(1), hc.C(1));
        hc.SetDeck(hc.C(9), hc.C(11));
        hc.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hc.Act(cp, "DiscardDrawnCard");
        hc.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Witch });
        Check("hidden: witch peek is not exposed in the opponent's view",
            hc.View(other).DeckSize == hc.S.Deck.Count && hc.View(other).WitchPeekPending);

        // Shared events never contain hidden values.
        var drawEvents = hp.S.EventLog
            .Where(e => e.Contains("silver.cardDrawn"))
            .Select(e => JsonSerializer.Deserialize<Dictionary<string, object?>>(e)!)
            .ToList();
        Check("hidden: draw events carry no card value",
            drawEvents.All(e => !((JsonElement)e["d"]!).TryGetProperty("value", out _)));
        Check("hidden: no event leaks a peeked/witched card value",
            !hc.S.EventLog.Any(e => e.Contains("silver.abilityUsed") && e.Contains("\"value\"")));

        // Reconnect: the same view is produced again, knowledge intact, no leak.
        var json = ha.State.ToJson();
        ha.State = GameState.FromJson(json);
        ha.S = Parse(ha.State);
        var reconnected = ha.View(seerSeat);
        var otherView = ha.View(seerOpponent);
        Check("hidden: reconnect preserves own knowledge without leaking others'",
            reconnected.Villages[seerOpponent].First(c => c.Id == secretId).Value is not null
            && otherView.Villages[seerOpponent].First(c => c.Id == secretId).Value is null);
    }

    // ------------------------------------------------ census and round end

    private static void CensusRoundEndingChecks()
    {
        Console.WriteLine("Census & round ending");
        var h = new H(3);
        var cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(1), h.C(2), h.C(3));
        Check("census: call ok at three cards", h.Act(cp, "CallCensus").IsValid);
        Check("census: caller recorded", h.S.CensusCallerIndex == cp);
        Check("census: two final turns for the others", h.S.RemainingCensusTurns == 2);

        var next = (cp + 1) % 3;
        Check("census: non-caller cannot call during final turns",
            h.Act(next, "CallCensus").ErrorCode == "silver.voteNotAllowed");

        h.SetDeck(h.C(2), h.C(2));
        h.Act(next, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(next, "DiscardDrawnCard");
        Check("census: one final turn consumed", h.S.RemainingCensusTurns == 1 && h.S.Round == 1);

        var last = (cp + 2) % 3;
        h.SetDeck(h.C(2), h.C(2));
        h.Act(last, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(last, "DiscardDrawnCard");
        Check("census: round ended after all final turns", h.S.Round == 2);
        Check("log: roundStarted is appended after the finished round's scoring",
            h.S.EventLog.FindLastIndex(e => e.Contains("silver.roundStarted"))
            > h.S.EventLog.FindIndex(e => e.Contains("silver.roundScored")));
        Check("log: the last final-turn draw precedes the scoring events",
            h.S.EventLog.FindLastIndex(e => e.Contains("silver.cardDrawn"))
            < h.S.EventLog.FindIndex(e => e.Contains("silver.roundScored")));
        Check("census: scores recorded", h.S.LastRoundScores is { } scores && scores.Count == 3);
        Check("round: fresh villages dealt face down",
            h.S.Villages.All(v => v.Count == 5 && v.All(c => !c.FaceUp)));
        Check("round: knowledge cleared between rounds",
            h.S.Knowledge.All(k => k.Count == 0));
        Check("round: fresh peeks", h.S.PeeksUsed.All(p => p == 0));
        Check("round: display/guards/census cleared",
            h.S.Display.Count == 0 && h.S.Guards.Count == 0 && h.S.CensusCallerIndex is null);
        Check("round: deck+discard back to 32", h.S.Deck.Count == 31 && h.S.Discard.Count == 1);
        Check("round: amulet holder starts round 2", h.S.CurrentPlayerIndex == h.S.AmuletHolderIndex);

        // The caller answering the last-turn player's Revealer prompt still
        // ends the round: the census countdown tracks WHOSE TURN ended, not
        // who submitted the action.
        var hcv = new H(2);
        var cvCp = hcv.S.CurrentPlayerIndex;
        var cvOther = 1 - cvCp;
        hcv.SetVillage(cvCp, hcv.C(1), hcv.C(1), hcv.C(1));
        hcv.Act(cvCp, "CallCensus");
        hcv.SetDeck(hcv.C(2), hcv.C(6));
        hcv.Act(cvOther, "DrawFromDeck", new DrawFromDeckPayload());
        hcv.Act(cvOther, "DiscardDrawnCard");
        hcv.Act(cvOther, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Revealer, TargetPlayerIndex = cvCp });
        Check("census: caller answering the final-turn Revealer ends the round",
            hcv.Act(cvCp, "ChooseRevealCard", new ChooseRevealCardPayload { SlotIndex = 0 }).IsValid
            && hcv.S.Round == 2);

        // Call rejected at five cards.
        var h5 = new H(2);
        var cp5 = h5.S.CurrentPlayerIndex;
        Check("census: call rejected at five cards",
            h5.Act(cp5, "CallCensus").ErrorCode == "silver.voteNotAllowed");

        // The caller of the finished round is remembered for display.
        Check("census: caller recorded last round", h.S.LastRoundCensusCallerIndex == cp);

        // Two face-up Villagers end the round immediately.
        var hv = new H(2);
        cp = hv.S.CurrentPlayerIndex;
        hv.SetVillage(cp, hv.C(0, faceUp: true), hv.C(0, faceUp: true), hv.C(1));
        hv.SetDeck(hv.C(2), hv.C(2));
        hv.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hv.Act(cp, "DiscardDrawnCard");
        Check("round: double face-up villager ends the round", hv.S.Round == 2);

        // Deck depletion ends the round.
        var hd = new H(2) { Pad = false };
        cp = hd.S.CurrentPlayerIndex;
        hd.SetVillage(cp, hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2));
        hd.SetVillage(1 - cp, hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2));
        hd.SetDiscard(hd.C(3));
        hd.SetDeck(hd.C(4), hd.C(3), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2), hd.C(2));
        for (var i = 0; i < 12 && hd.S.Round == 1; i++)
        {
            var cur = hd.S.CurrentPlayerIndex;
            hd.Act(cur, "DrawFromDeck", new DrawFromDeckPayload());
            hd.Act(cur, "DiscardDrawnCard");
            if (hd.S.Phase == TurnPhase.AbilityPending) hd.Act(cur, "SkipAbility");
        }
        Check("round: depleted deck ends the round", hd.S.Round == 2);

        // A call followed by deck depletion still scores the caller as caller.
        var hcd = new H(2) { Pad = false };
        cp = hcd.S.CurrentPlayerIndex;
        var other = 1 - cp;
        hcd.SetVillage(cp, hcd.C(1));
        hcd.SetVillage(other, hcd.C(9), hcd.C(9));
        hcd.SetDiscard(hcd.C(3));
        hcd.SetDeck(hcd.C(5), hcd.C(4), hcd.C(2), hcd.C(2), hcd.C(2), hcd.C(2), hcd.C(2), hcd.C(2));
        hcd.Act(cp, "CallCensus");
        hcd.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        hcd.Act(other, "DiscardDrawnCard");
        Check("round: final turns ending with deck depletion still scores the caller",
            hcd.S.Round == 2 && hcd.S.LastRoundScores![cp] == 0 && hcd.S.LastRoundScores[other] == 18);
    }

    // -------------------------------------------------------------- scoring

    private static void ScoringChecks()
    {
        Console.WriteLine("Scoring");
        // Caller with the lowest sum scores 0; amulet + placement right.
        var hw = new H(2);
        var cp = hw.S.CurrentPlayerIndex;
        var other = 1 - cp;
        hw.SetVillage(cp, hw.C(1));
        hw.SetVillage(other, hw.C(9), hw.C(9), hw.C(9));
        hw.SetDeck(hw.C(2), hw.C(2));
        hw.Act(cp, "CallCensus");
        hw.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        hw.Act(other, "DiscardDrawnCard");
        Check("scoring: successful caller scores 0", hw.S.LastRoundScores![cp] == 0);
        Check("scoring: other players score their sum", hw.S.LastRoundScores![other] == 27);
        Check("scoring: amulet to the lowest scorer", hw.S.AmuletHolderIndex == cp);
        Check("scoring: successful caller may place the amulet", hw.S.AmuletPlaceable);
        Check("scoring: amulet holder starts the next round", hw.S.CurrentPlayerIndex == cp);

        // Failed call: sum + 10.
        var hf = new H(2);
        cp = hf.S.CurrentPlayerIndex;
        other = 1 - cp;
        hf.SetVillage(cp, hf.C(5));
        hf.SetVillage(other, hf.C(1), hf.C(2));
        hf.SetDeck(hf.C(2), hf.C(2));
        hf.Act(cp, "CallCensus");
        hf.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        hf.Act(other, "DiscardDrawnCard");
        Check("scoring: failed caller scores sum + 10", hf.S.LastRoundScores![cp] == 15);
        Check("scoring: lowest scorer takes the amulet", hf.S.AmuletHolderIndex == other);
        Check("scoring: failed caller cannot place the amulet", !hf.S.AmuletPlaceable);

        // Amulet placement: blocks census that turn, protects the card.
        var ham = new H(2);
        var holder = ham.S.CurrentPlayerIndex;
        var hother = 1 - holder;
        ham.SetVillage(holder, ham.C(8), ham.C(1), ham.C(1));
        ham.S.AmuletPlaceable = true;
        Check("amulet: placement ok",
            ham.Act(holder, "PlaceAmulet", new PlaceAmuletPayload { SlotIndex = 0 }).IsValid);
        Check("amulet: placement blocks the census call",
            ham.Act(holder, "CallCensus").ErrorCode == "silver.voteNotAllowed");
        Check("amulet: card bound for the round", ham.S.AmuletPlacedCardId == ham.S.Villages[holder][0].Id);
        Check("amulet: even the owner cannot peek it",
            ham.Act(holder, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 0 }).ErrorCode == "silver.cardProtected");
        Check("amulet: second placement rejected",
            ham.Act(holder, "PlaceAmulet", new PlaceAmuletPayload { SlotIndex = 1 }).ErrorCode == "silver.amuletNotAvailable");

        // Non-holder placement rejected.
        var hn = new H(2);
        Check("amulet: non-holder rejected",
            hn.Act(hn.S.CurrentPlayerIndex, "PlaceAmulet", new PlaceAmuletPayload { SlotIndex = 0 }).ErrorCode == "silver.amuletNotAvailable");

        // Census caller tied for the lowest total scores 0.
        var ht2 = new H(2);
        cp = ht2.S.CurrentPlayerIndex;
        other = 1 - cp;
        ht2.SetVillage(cp, ht2.C(3));
        ht2.SetVillage(other, ht2.C(1), ht2.C(2));
        ht2.SetDeck(ht2.C(2), ht2.C(2));
        ht2.Act(cp, "CallCensus");
        ht2.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
        ht2.Act(other, "DiscardDrawnCard");
        Check("scoring: caller tied for lowest still scores 0", ht2.S.LastRoundScores![cp] == 0);

        // Round-lowest tie: the current holder keeps the amulet.
        var hk = new H(3);
        cp = hk.S.CurrentPlayerIndex;
        var third = (cp + 2) % 3;
        hk.S.AmuletHolderIndex = third;
        hk.SetVillage(cp, hk.C(5));
        hk.SetVillage((cp + 1) % 3, hk.C(1));
        hk.SetVillage(third, hk.C(1));
        hk.SetDeck(hk.C(2), hk.C(2));
        hk.Act(cp, "CallCensus");
        for (var step = 1; step <= 2; step++)
        {
            hk.TakeSimpleTurn((cp + step) % 3);
        }
        Check("scoring: tie including the holder keeps it", hk.S.AmuletHolderIndex == third);

        // Round-lowest tie without the holder: seat closest clockwise after
        // this round's start player.
        var ht = new H(3);
        cp = ht.S.CurrentPlayerIndex;
        var startSeat = ht.S.RoundStartPlayerIndex;
        ht.SetVillage(cp, ht.C(5));
        ht.SetVillage((cp + 1) % 3, ht.C(1), ht.C(2));
        ht.SetVillage((cp + 2) % 3, ht.C(1), ht.C(2));
        ht.Act(cp, "CallCensus");
        for (var step = 1; step <= 2; step++)
        {
            ht.TakeSimpleTurn((cp + step) % 3);
        }
        Check("scoring: two non-callers tie for the lowest round score",
            ht.S.LastRoundScores is { } tieScores && tieScores.Count(v => v == tieScores.Min()) == 2);
        Check("scoring: tie without holder goes to the seat after the start player",
            ht.S.AmuletHolderIndex == (startSeat + 1) % 3);

        // Protected cards still count toward the score.
        var hps = new H(2);
        cp = hps.S.CurrentPlayerIndex;
        other = 1 - cp;
        hps.SetVillage(cp, hps.C(6));
        hps.S.AmuletPlaceable = true;
        hps.SetDeck(hps.C(4));
        hps.Act(cp, "PlaceAmulet", new PlaceAmuletPayload { SlotIndex = 0 });
        hps.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hps.Act(cp, "DiscardDrawnCard");
        hps.SetVillage(other, hps.C(1));
        hps.Act(other, "CallCensus");
        hps.SetDeck(hps.C(2));
        hps.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hps.Act(cp, "DiscardDrawnCard");
        Check("scoring: the amulet-protected card still scores", hps.S.LastRoundScores![cp] == 6);
    }

    // ---------------------------------------------------------- persistence

    private static void PersistenceChecks()
    {
        Console.WriteLine("Persistence");
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(3, faceUp: true), h.C(8));
        h.SetDeck(h.C(9), h.C(3));
        h.PadTo52();
        h.Act(cp, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 1 });
        var peeked = h.S.Villages[cp][1].Id;
        h.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 1 });
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        var deckCountAfterDraw = h.S.Deck.Count;

        GameState current = h.State;
        for (var roundTrip = 0; roundTrip < 3; roundTrip++)
        {
            var json = current.ToJson();
            current = GameState.FromJson(json);
            Check($"persistence: round-trip {roundTrip + 1} keeps the pending draw",
                Parse(current).PendingDraw.Single().Value == 3);
        }

        h.State = current;
        h.S = Parse(current);
        Check("persistence: guard link survives round-trips", h.S.Guards.Count == 1);
        Check("persistence: action continues after round-trips",
            h.Act(cp, "DiscardDrawnCard").IsValid);
        Check("persistence: knowledge survives round-trips",
            h.S.Knowledge[cp].ContainsKey(peeked));
        Check("persistence: deck order survives round-trips",
            h.S.Deck.Count == deckCountAfterDraw && h.S.Deck[^1].Value == 9);
    }

    // -------------------------------------------------------- game completion

    private static void GameCompletionChecks()
    {
        Console.WriteLine("Game completion");

        // Four rounds; fewest cumulative points wins.
        var h = new H(2);
        var loser = -1;
        for (var round = 1; round <= 4; round++)
        {
            var cp = h.S.CurrentPlayerIndex;
            var other = 1 - cp;
            h.SetVillage(cp, h.C(round));
            h.SetVillage(other, h.C(9), h.C(9), h.C(9));
            loser = other;
            h.SetDeck(h.C(2), h.C(2));
            h.Act(cp, "CallCensus");
            h.Act(other, "DrawFromDeck", new DrawFromDeckPayload());
            h.Act(other, "DiscardDrawnCard");
        }
        Check("completion: game over after four rounds", h.State.IsOver);
        Check("completion: fewest cumulative points wins",
            h.State.Winner is { } w && w.UserId == h.Uid(loser == 0 ? 1 : 0).UserId);
        Check("completion: deadlines cleared on game over",
            h.State.NextActionDeadlineUtc is null && h.State.GameEndsAtUtc is null);
        Check("completion: game-finished event emitted",
            h.S.EventLog.Any(e => e.Contains("silver.gameFinished")));

        // Tie resolved by the amulet holder (initially assigned) via force-finish.
        var ht = new H(2);
        ht.S.CumulativeScores = new List<int> { 12, 12 };
        ht.S.AmuletHolderIndex = 1;
        ht.S.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Check("completion: force-finish on tied standings",
            ht.Act(ht.S.CurrentPlayerIndex, "GameTimeExpired").IsValid && ht.State.IsOver);
        Check("completion: amulet holder wins the tie",
            ht.State.Winner is { } tieWinner && tieWinner.UserId == ht.Uid(1).UserId);

        // GameTimeExpired force-finish.
        var hg = new H(2);
        var cpG = hg.S.CurrentPlayerIndex;
        hg.S.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(5);
        Check("completion: game-time expiry rejected before the limit",
            hg.Act(cpG, "GameTimeExpired").ErrorCode == "silver.timeNotUp");
        hg.S.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-1);
        hg.S.CumulativeScores = new List<int> { 3, 9 };
        Check("completion: force-finish applies the standings",
            hg.Act(cpG, "GameTimeExpired").IsValid && hg.State.IsOver
            && hg.State.Winner is { } timedWinner && timedWinner.UserId == hg.Uid(0).UserId);

        // AFK elimination: 2 players → instant win for the survivor.
        var ha = new H(2);
        var afk = ha.S.CurrentPlayerIndex;
        var active = 1 - afk;
        for (var i = 0; i < 2; i++)
        {
            ha.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
            ha.Act(afk, "TurnTimeout");
            ha.TakeSimpleTurn(active);
        }
        ha.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        ha.Act(afk, "TurnTimeout");
        Check("completion: three AFK timeouts end a 2p game", ha.State.IsOver);
        Check("completion: survivor wins",
            ha.State.Winner is { } survivor && survivor.UserId == ha.Uid(active).UserId);

        // AFK elimination: 3 players → seat removed, game continues, village scored.
        var h3 = new H(3);
        var afk3 = h3.S.CurrentPlayerIndex;
        for (var cycle = 0; cycle < 2; cycle++)
        {
            h3.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
            h3.Act(afk3, "TurnTimeout");
            for (var step = 1; step <= 2; step++)
            {
                h3.TakeSimpleTurn((afk3 + step) % 3);
            }
        }
        h3.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        h3.Act(afk3, "TurnTimeout");
        Check("completion: 3p game continues after elimination", !h3.State.IsOver);
        Check("completion: seat eliminated", h3.S.EliminatedPlayerIndexes.Contains(afk3));
        Check("completion: eliminated player cannot act",
            h3.Act(afk3, "PeekVillageCard", new PeekVillageCardPayload { SlotIndex = 0 }).ErrorCode == "silver.playerEliminated");
        var cpAfterAfk = h3.S.CurrentPlayerIndex;
        h3.SetVillage(afk3, h3.C(9), h3.C(9));
        h3.SetVillage(cpAfterAfk, h3.C(1));
        h3.SetVillage((cpAfterAfk + 1) % 3, h3.C(2));
        h3.SetDeck(h3.C(2), h3.C(2));
        h3.Act(cpAfterAfk, "CallCensus");
        h3.Act((cpAfterAfk + 1) % 3, "DrawFromDeck", new DrawFromDeckPayload());
        h3.Act((cpAfterAfk + 1) % 3, "DiscardDrawnCard");
        Check("completion: eliminated village is still scored at round end",
            h3.S.Round == 2 && h3.S.LastRoundScores![afk3] == 18);

        // Turn timeout resolves a pending Trickster choice by discarding the
        // first card and returning the extras to the TOP of the deck.
        var hto = new H(2);
        var cpTo = hto.S.CurrentPlayerIndex;
        var otherTo = 1 - cpTo;
        hto.SetVillage(cpTo, hto.C(4, faceUp: true), hto.C(1));
        hto.SetDeck(hto.C(8), hto.C(3), hto.C(7));
        hto.Pad = false;
        hto.Act(cpTo, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 1 });
        var first = hto.S.PendingDraw[0].Id;
        var second = hto.S.PendingDraw[1].Id;
        hto.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        Check("completion: timeout resolves the pending trickster draw",
            hto.Act(cpTo, "TurnTimeout").IsValid
            && hto.S.Discard[^1].Id == first
            && hto.S.Deck[^1].Id == second
            && hto.S.CurrentPlayerIndex == otherTo);
        Check("completion: timeout rejected before the deadline",
            hto.Act(otherTo, "TurnTimeout").ErrorCode == "silver.timerNotExpired");

        // Timeout clears a pending Revealer prompt (the game never stalls).
        var hr = new H(2);
        cpTo = hr.S.CurrentPlayerIndex;
        otherTo = 1 - cpTo;
        hr.SetVillage(otherTo, hr.C(5), hr.C(1));
        hr.SetDeck(hr.C(6));
        hr.Act(cpTo, "DrawFromDeck", new DrawFromDeckPayload());
        hr.Act(cpTo, "DiscardDrawnCard");
        hr.Act(cpTo, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Revealer, TargetPlayerIndex = otherTo });
        hr.S.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        Check("completion: timeout clears the pending reveal prompt",
            hr.Act(cpTo, "TurnTimeout").IsValid && hr.S.RevealerChooserSeat is null);
    }

    // -------------------------------------------------------- valid actions

    private static void ValidActionsChecks()
    {
        Console.WriteLine("Valid actions");
        var h = new H(2);
        var cp = h.S.CurrentPlayerIndex;
        var other = 1 - cp;

        var actions = h.Valid(cp);
        Check("validactions: turn start offers draw and take-discard",
            actions.Any(a => a.ActionType == "DrawFromDeck")
            && actions.Any(a => a.ActionType == "TakeDiscard"));
        Check("validactions: call census hidden at five cards",
            actions.All(a => a.ActionType != "CallCensus"));

        h.SetVillage(cp, h.C(1), h.C(1));
        actions = h.Valid(cp);
        Check("validactions: call census offered at two cards",
            actions.Any(a => a.ActionType == "CallCensus"));

        Check("validactions: other player gets only peeks",
            h.Valid(other).All(a => a.ActionType == "PeekVillageCard"));

        // Trickster draw variants are enumerated.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        h.SetVillage(cp, h.C(4, faceUp: true), h.C(1));
        actions = h.Valid(cp);
        Check("validactions: one trickster offers plain and +1 draws",
            actions.Count(a => a.ActionType == "DrawFromDeck") == 2);

        // Squire display cards are takeable at turn start.
        h.SetDisplay(h.C(2));
        actions = h.Valid(cp);
        Check("validactions: display card offered",
            actions.Any(a => a.ActionType == "TakeSquireCard"));

        // The Trickster choice phase only offers the choice.
        h.SetDeck(h.C(9), h.C(3), h.C(7));
        h.Pad = false;
        h.SetVillage(cp, h.C(4, faceUp: true), h.C(4, faceUp: true), h.C(1));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload { TricksterExtra = 2 });
        actions = h.Valid(cp);
        Check("validactions: trickster choice phase offers only the choice (peeks aside)",
            actions.Where(a => a.ActionType != "PeekVillageCard").All(a => a.ActionType == "ChooseDrawnCard")
            && actions.Count(a => a.ActionType == "ChooseDrawnCard") == 3);

        // Pending ability windows.
        h = new H(2);
        cp = h.S.CurrentPlayerIndex;
        other = 1 - cp;
        h.SetDeck(h.C(6));
        h.SetVillage(other, h.C(5), h.C(1));
        h.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        h.Act(cp, "DiscardDrawnCard");
        actions = h.Valid(cp);
        Check("validactions: revealer window offers opponent choices and skip",
            actions.Any(a => a.ActionType == "UseAbility"
                && JsonSerializer.Deserialize<UseAbilityPayload>(a.Payload)!.Ability == SilverAbility.Revealer)
            && actions.Any(a => a.ActionType == "SkipAbility"));
        h.Act(cp, "UseAbility", new UseAbilityPayload { Ability = SilverAbility.Revealer, TargetPlayerIndex = other });
        Check("validactions: the prompted opponent is offered ChooseRevealCard",
            h.Valid(other).Any(a => a.ActionType == "ChooseRevealCard")
            && h.Valid(other).All(a => a.ActionType is "ChooseRevealCard" or "PeekVillageCard"));

        // Robber may target face-up opponent cards.
        var hr = new H(2);
        cp = hr.S.CurrentPlayerIndex;
        other = 1 - cp;
        hr.SetVillage(cp, hr.C(1), hr.C(1));
        hr.SetVillage(other, hr.C(7, faceUp: true));
        hr.SetDeck(hr.C(12));
        hr.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hr.Act(cp, "DiscardDrawnCard");
        Check("validactions: robber offered on a face-up target",
            hr.Valid(cp).Any(a => a.ActionType == "UseAbility"
                && JsonSerializer.Deserialize<UseAbilityPayload>(a.Payload) is { Ability: SilverAbility.Robber }));

        // Master offers the decline action.
        var hm2 = new H(2);
        cp = hm2.S.CurrentPlayerIndex;
        hm2.SetVillage(cp, hm2.C(3), hm2.C(1));
        hm2.SetDeck(hm2.C(10));
        hm2.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        hm2.Act(cp, "DiscardDrawnCard");
        Check("validactions: master decline offered",
            hm2.Valid(cp).Any(a => a.ActionType == "UseAbility"
                && JsonSerializer.Deserialize<UseAbilityPayload>(a.Payload) is { Ability: SilverAbility.Master, OwnSlots: null }));

        // A matching set is offered as a multi replacement.
        var hm = new H(2);
        cp = hm.S.CurrentPlayerIndex;
        hm.SetVillage(cp, hm.C(5), hm.C(5), hm.C(1));
        hm.SetDeck(hm.C(9));
        hm.Act(cp, "DrawFromDeck", new DrawFromDeckPayload());
        actions = hm.Valid(cp);
        Check("validactions: matching pair offered as multi replacement",
            actions.Any(a => a.ActionType == "ExchangeWithDrawn"
                && JsonSerializer.Deserialize<ExchangePayload>(a.Payload)!.Slots.Count == 2));
        Check("validactions: {5,5,13-free} no bogus three-sets with different values",
            !actions.Any(a => a.ActionType == "ExchangeWithDrawn"
                && JsonSerializer.Deserialize<ExchangePayload>(a.Payload)!.Slots.Count == 3));

        // Guard actions offered for a face-up guard.
        var hgv = new H(2);
        cp = hgv.S.CurrentPlayerIndex;
        hgv.SetVillage(cp, hgv.C(3, faceUp: true), hgv.C(8), hgv.C(2));
        actions = hgv.Valid(cp);
        Check("validactions: guard attach offered",
            actions.Any(a => a.ActionType == "MoveGuard")
            && !actions.Any(a => a.ActionType == "RemoveGuard"));
        hgv.Act(cp, "MoveGuard", new MoveGuardPayload { GuardSlot = 0, TargetSlot = 1 });
        hgv.S.AbilitiesUsedThisTurn.Clear();
        actions = hgv.Valid(cp);
        Check("validactions: guard remove + move offered when attached",
            actions.Any(a => a.ActionType == "RemoveGuard")
            && actions.Any(a => a.ActionType == "MoveGuard"));
    }
}
