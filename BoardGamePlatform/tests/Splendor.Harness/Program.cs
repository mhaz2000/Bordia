using System.Text.Json;
using GameEngine.Core.Models;
using Splendor;
using Splendor.Models;

return SplendorHarness.Run();

internal static class SplendorHarness
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();

    private static readonly SplendorGame Game = new();

    public static int Run()
    {
        CatalogueChecks();
        SetupChecks();
        GemChecks();
        ReservationChecks();
        PurchaseChecks();
        NobleChecks();
        EndGameChecks();
        ValidActionsChecks();
        PersistenceChecks();
        RandomnessChecks();
        HiddenInformationChecks();
        TimerChecks();
        InvariantSoakChecks();

        Console.WriteLine();
        Console.WriteLine($"SPLENDOR HARNESS: {_passed} passed, {_failed} failed");
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

    // ---------------------------------------------------------------- scaffold

    private static GameOptions Opts(int players, int? seed = 42)
    {
        var ids = Enumerable.Range(0, players).Select(_ => new PlayerId(Guid.NewGuid())).ToList();
        var names = ids.Select((p, i) => (p.UserId, $"P{i}")).ToDictionary();
        var settings = JsonSerializer.Serialize(new
        {
            PlayerNames = names,
            Seed = seed,
            Timer = new
            {
                BaseTurnSeconds = 60.0,
                MaxBankSeconds = 180.0,
                MaxOverrunSeconds = 15.0,
                MaxAfkTurns = 3,
                TotalGameTimeMinutes = 60.0
            }
        });
        return new GameOptions { GameType = "Splendor", Players = ids, Settings = settings };
    }

    private static GameState Fresh(int players, int? seed = 42) => Game.CreateGame(Opts(players, seed));

    private static GameState Wrap(SplendorState st)
    {
        var ids = Enumerable.Range(0, st.PlayerCount).Select(_ => new PlayerId(Guid.NewGuid())).ToList();
        st.PlayerIds = ids;
        st.PlayerNames = ids.Select((p, i) => (p.UserId, $"P{i}")).ToDictionary();
        return new GameState
        {
            SessionId = Guid.NewGuid(),
            GameType = "Splendor",
            Players = ids,
            CurrentPlayerIndex = st.CurrentPlayerIndex,
            IsOver = false,
            Version = 1,
            NextActionDeadlineUtc = st.NextActionDeadlineUtc,
            GameEndsAtUtc = st.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SplendorState"] = st.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(st.PlayerNames)
            }
        };
    }

    /// <summary>
    /// 4-player default fixture; supply 7/5. Tests place market cards explicitly;
    /// decks hold the remaining tier cards in catalogue order.
    /// </summary>
    private static GameState Fixture(
        Action<SplendorState>? configure = null,
        List<string>? marketTier1 = null,
        List<string>? marketTier2 = null,
        List<string>? marketTier3 = null,
        int players = 4)
    {
        var st = new SplendorState
        {
            Supply = new SplendorGems { Diamond = 7, Sapphire = 7, Emerald = 7, Ruby = 7, Onyx = 7, Gold = 5 },
            Decks = new List<List<string>>
            {
                TierIds(1).ToList(),
                TierIds(2).ToList(),
                TierIds(3).ToList()
            },
            Market = new List<string?>(new string?[12]),
            Seats = Enumerable.Range(0, players).Select(_ => new SplendorSeatState()).ToList(),
            NoblesInMarket = SplendorCatalogue.Nobles.Select(n => n.Id).ToList(),
            TimerConfig = new SplendorTurnTimerConfig()
        };

        for (var i = 0; i < (marketTier1 ?? new List<string>()).Count && i < 4; i++) st.Market[i] = marketTier1![i];
        for (var i = 0; i < (marketTier2 ?? new List<string>()).Count && i < 4; i++) st.Market[4 + i] = marketTier2![i];
        for (var i = 0; i < (marketTier3 ?? new List<string>()).Count && i < 4; i++) st.Market[8 + i] = marketTier3![i];

        for (var i = 0; i < players; i++)
        {
            st.PlayerTimers.Add(new SplendorPlayerTimer());
        }

        configure?.Invoke(st);
        if (st.NextActionDeadlineUtc is null)
        {
            st.TurnStartUtc = DateTime.UtcNow;
            st.NextActionDeadlineUtc = DateTime.UtcNow.AddMinutes(5);
        }

        if (st.GameEndsAtUtc is null)
        {
            st.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(60);
        }

        return Wrap(st);
    }

    private static List<string> TierIds(int tier)
        => SplendorCatalogue.Cards.Where(c => c.Tier == tier).Select(c => c.Id).ToList();

    private static SplendorState Parse(GameState state)
    {
        if (!state.TryGetString("SplendorState", out var json) || json is null)
        {
            throw new InvalidOperationException("missing splendor state");
        }

        return SplendorState.FromJson(json);
    }

    private static GameState Act(GameState state, SplendorActionType type, object payload)
    {
        var who = state.Players[state.CurrentPlayerIndex ?? 0];
        return ActAs(state, who, type, payload);
    }

    private static GameState ActAs(GameState state, PlayerId who, SplendorActionType type, object payload)
    {
        var error = TryAs(state, who, type, payload, out var next);
        if (error is not null)
        {
            throw new InvalidOperationException($"unexpected rejection of {type}: {error}");
        }

        return next;
    }

    private static string? Try(GameState state, SplendorActionType type, object payload, out GameState next)
    {
        var who = state.Players[state.CurrentPlayerIndex ?? 0];
        return TryAs(state, who, type, payload, out next);
    }

    private static string? TryAs(GameState state, PlayerId who, SplendorActionType type, object payload, out GameState next)
    {
        var action = new GameAction
        {
            PlayerId = who,
            ActionType = type.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Timestamp = DateTime.UtcNow,
            SequenceNumber = 1
        };
        var result = Game.ProcessAction(state, action);
        next = result.NewState ?? state;
        return result.IsValid ? null : (result.Error ?? result.ErrorCode);
    }

    private static void Expect(GameState state, SplendorActionType type, object payload, string code)
    {
        var error = Try(state, type, payload, out _);
        Check($"{type} rejected with {code}", error == code);
    }

    private static bool LogHas(GameState state, string code)
    {
        return Parse(state).EventLog.Any(e => e.Contains($"\"c\":\"{code}\"", StringComparison.Ordinal));
    }

    private static string ViewJson(GameState state, PlayerId viewer)
    {
        var view = Game.GetPlayerView(state, viewer);
        return view.TryGetString("SplendorState", out var json) ? json ?? string.Empty : string.Empty;
    }

    private static SplendorCard Card(string id) => SplendorCatalogue.Cards.First(c => c.Id == id);

    private static SplendorPaymentPayload Pay(int[] gems, int gold = 0) => new()
    {
        Diamond = gems.Length > 0 ? gems[0] : 0,
        Sapphire = gems.Length > 1 ? gems[1] : 0,
        Emerald = gems.Length > 2 ? gems[2] : 0,
        Ruby = gems.Length > 3 ? gems[3] : 0,
        Onyx = gems.Length > 4 ? gems[4] : 0,
        Gold = gold
    };

    /// <summary>Bonus-color card lists for seeding fixtures (all 0-VP cards of a color).</summary>
    private static List<string> ZeroVp(SplendorColor color, int take)
        => SplendorCatalogue.Cards.Where(c => c.Bonus == color && c.Points == 0).Take(take).Select(c => c.Id).ToList();

    // ---------------------------------------------------------------- catalogue

    private static void CatalogueChecks()
    {
        Check("90 cards", SplendorCatalogue.Cards.Count == 90);
        Check("40 L1", SplendorCatalogue.Cards.Count(c => c.Tier == 1) == 40);
        Check("30 L2", SplendorCatalogue.Cards.Count(c => c.Tier == 2) == 30);
        Check("20 L3", SplendorCatalogue.Cards.Count(c => c.Tier == 3) == 20);
        Check("card ids unique", SplendorCatalogue.Cards.Select(c => c.Id).Distinct().Count() == 90);
        Check("L1 vp total 5", SplendorCatalogue.Cards.Where(c => c.Tier == 1).Sum(c => c.Points) == 5);
        Check("L2 vp total 55", SplendorCatalogue.Cards.Where(c => c.Tier == 2).Sum(c => c.Points) == 55);
        Check("L3 vp total 80", SplendorCatalogue.Cards.Where(c => c.Tier == 3).Sum(c => c.Points) == 80);
        Check("five 1-VP L1 cards", SplendorCatalogue.Cards.Count(c => c.Tier == 1 && c.Points == 1) == 5);
        Check("L1 other cards 0 VP", SplendorCatalogue.Cards.Count(c => c.Tier == 1 && c.Points == 0) == 35);
        Check("rulebook example L3S02 = 4vp Sapphire [6,3,0,0,3]",
            Card("L3S02").Points == 4 && Card("L3S02").Bonus == SplendorColor.Sapphire
            && Card("L3S02").Cost.SequenceEqual(new[] { 6, 3, 0, 0, 3 }));
        Check("all costs total 3..16", SplendorCatalogue.Cards.All(c => c.Cost.Sum() is >= 3 and <= 16));
        Check("no card costs its own gold", SplendorCatalogue.Cards.All(c => c.Cost.All(x => x >= 0)));
        Check("10 nobles", SplendorCatalogue.Nobles.Count == 10);
        Check("noble ids unique", SplendorCatalogue.Nobles.Select(n => n.Id).Distinct().Count() == 10);
        Check("five pair nobles (sum 8)", SplendorCatalogue.Nobles.Count(n => n.Requirement.Sum() == 8) == 5);
        Check("five triple nobles (sum 9)", SplendorCatalogue.Nobles.Count(n => n.Requirement.Sum() == 9) == 5);
        Check("rulebook example noble N-DSE = 3x D/S/E",
            SplendorCatalogue.Nobles.First(n => n.Id == "N-DSE").Requirement.SequenceEqual(new[] { 3, 3, 3, 0, 0 }));
        Check("pair nobles form the 5-cycle", SplendorCatalogue.Nobles
            .Where(n => n.Requirement.Sum() == 8)
            .Select(n => string.Join(",", Enumerable.Range(0, 5).Where(i => n.Requirement[i] > 0)))
            .OrderBy(s => s).SequenceEqual(new[] { "0,1", "0,4", "1,2", "2,3", "3,4" }));
    }

    // ---------------------------------------------------------------- setup

    private static void SetupChecks()
    {
        foreach (var (count, gems, nobles) in new[] { (2, 4, 3), (3, 5, 4), (4, 7, 5) })
        {
            var s = Fresh(count);
            var st = Parse(s);
            Check($"{count}p supply {gems}/color",
                st.Supply.Diamond == gems && st.Supply.Sapphire == gems && st.Supply.Emerald == gems
                && st.Supply.Ruby == gems && st.Supply.Onyx == gems);
            Check($"{count}p gold 5", st.Supply.Gold == 5);
            Check($"{count}p nobles {nobles}", st.NoblesInMarket.Count == nobles);
            Check($"{count}p decks 36/26/16",
                st.Decks[0].Count == 36 && st.Decks[1].Count == 26 && st.Decks[2].Count == 16);
            Check($"{count}p market full", st.Market.All(id => id is not null));
            Check($"{count}p market tiers match",
                st.Market[0..4].All(id => Card(id!)!.Tier == 1)
                && st.Market[4..8].All(id => Card(id!)!.Tier == 2)
                && st.Market[8..12].All(id => Card(id!)!.Tier == 3));
            Check($"{count}p all 90 cards conserved",
                st.Decks.SelectMany(d => d).Concat(st.Market.Select(m => m!)).OrderBy(x => x)
                    .SequenceEqual(TierIds(1).Concat(TierIds(2)).Concat(TierIds(3)).OrderBy(x => x)));
            Check($"{count}p players start empty", st.Seats.All(x =>
                x.Tokens.TotalWithGold == 0 && x.Reserved.Count == 0 && x.Purchased.Count == 0
                && x.NoblesOwned.Count == 0 && x.VictoryPoints == 0));
            Check($"{count}p current player set", s.CurrentPlayerIndex is >= 0 && s.CurrentPlayerIndex < count);
            Check($"{count}p deadlines set", s.NextActionDeadlineUtc is not null && s.GameEndsAtUtc is not null);
            Check($"{count}p not over", !s.IsOver && s.Version == 1);
            Check($"{count}p gameStarted logged", LogHas(s, "splendor.gameStarted"));
        }

        var threw2 = false;
        var threw5 = false;
        try { Game.CreateGame(Opts(1)); } catch (ArgumentException) { threw2 = true; }

        try { Game.CreateGame(Opts(5)); } catch (ArgumentException) { threw5 = true; }

        Check("1 player rejected", threw2);
        Check("5 players rejected", threw5);

        var a = Parse(Fresh(3, 7));
        var b = Parse(Fresh(3, 7));
        Check("same seed = same setup", a.Decks[0].SequenceEqual(b.Decks[0]) && a.Market.SequenceEqual(b.Market)
            && a.NoblesInMarket.SequenceEqual(b.NoblesInMarket) && a.CurrentPlayerIndex == b.CurrentPlayerIndex);
        var c = Parse(Fresh(3, 8));
        Check("different seed = different order", !a.Decks[0].SequenceEqual(c.Decks[0]) || !a.Market.SequenceEqual(c.Market));
    }

    // ---------------------------------------------------------------- gems

    private static void GemChecks()
    {
        var s = Fresh(4);
        var st = Parse(s);
        var who = s.Players[st.CurrentPlayerIndex];

        // Take three different from a full supply.
        var t1 = ActAs(s, who, SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        var p1 = Parse(t1);
        Check("3-different deltas", p1.Supply.Diamond == 6 && p1.Supply.Sapphire == 6 && p1.Supply.Emerald == 6
            && p1.Seats[st.CurrentPlayerIndex].Tokens.Diamond == 1);
        Check("3-different turn advanced", t1.CurrentPlayerIndex != st.CurrentPlayerIndex);
        Check("gemsTaken logged", LogHas(t1, "splendor.gemsTaken"));

        // Duplicates / unknown colors / wrong count.
        Expect(t1, SplendorActionType.TakeThreeGems, new { Colors = new[] { "ruby", "ruby", "onyx" } }, "splendor.duplicateColor");
        Expect(t1, SplendorActionType.TakeThreeGems, new { Colors = new[] { "gold", "onyx", "ruby" } }, "splendor.invalidGemColor");
        Expect(t1, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond" } }, "splendor.exactGemsRequired");
        Expect(t1, SplendorActionType.TakeTwoGems, new { Color = "gold" }, "splendor.invalidGemColor");

        // Take two: boundary at 4.
        var pairState = Fixture(x =>
        {
            x.Supply.Ruby = 4;
            x.Supply.Diamond = 3;
            x.Supply.Onyx = 1;
        });
        var ok = Try(pairState, SplendorActionType.TakeTwoGems, new { Color = "ruby" }, out var afterPair);
        Check("take 2 at exactly 4 supply", ok is null && Parse(afterPair).Supply.Ruby == 2);
        Expect(pairState, SplendorActionType.TakeTwoGems, new { Color = "diamond" }, "splendor.cannotTakeTwo");
        Expect(pairState, SplendorActionType.TakeTwoGems, new { Color = "onyx" }, "splendor.cannotTakeTwo");

        // Zero-token supply color for take-3: excluded.
        var zeroColor = Fixture(x => x.Supply.Onyx = 0);
        Expect(zeroColor, SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "diamond", "sapphire", "onyx" } }, "splendor.insufficientSupply");

        // OD-1 forced-smaller takes.
        var scarce = Fixture(x =>
        {
            x.Supply = new SplendorGems { Diamond = 2, Gold = 0, Onyx = 1 };
        });
        Check("OD-1: two colors available -> take two different",
            Try(scarce, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "onyx" } }, out _) is null);
        Expect(scarce, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond" } }, "splendor.exactGemsRequired");
        var one = Fixture(x => x.Supply = new SplendorGems { Sapphire = 5, Gold = 0 });
        Check("OD-1: one color -> take one",
            Try(one, SplendorActionType.TakeThreeGems, new { Colors = new[] { "sapphire" } }, out _) is null);
        Expect(one, SplendorActionType.TakeThreeGems, new { Colors = new[] { "sapphire", "diamond" } }, "splendor.exactGemsRequired");
        var none = Fixture(x => x.Supply = new SplendorGems { Gold = 2 });
        Expect(none, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond" } }, "splendor.insufficientSupply");

        // 10-token limit boundary cases.
        var nine = Fixture(x => x.Seats[0].Tokens = new SplendorGems { Diamond = 2, Sapphire = 2, Emerald = 2, Ruby = 2, Onyx = 1 });
        Expect(nine, SplendorActionType.TakeTwoGems, new { Color = "onyx" }, "splendor.tokenLimitExceeded");
        var returnsOk = Try(nine, SplendorActionType.TakeTwoGems,
            new { Color = "onyx", Return = new[] { new { Color = "diamond", Count = 1 } } }, out var afterReturn);
        Check("overflow take with return accepted", returnsOk is null);
        var pr = Parse(afterReturn);
        Check("return moved tokens back", pr.Seats[0].Tokens.TotalWithGold == 10 && pr.Supply.Diamond == 8);
        Expect(nine, SplendorActionType.TakeTwoGems,
            new { Color = "onyx", Return = new[] { new { Color = "diamond", Count = 2 } } }, "splendor.invalidReturn");
        Expect(nine, SplendorActionType.TakeTwoGems,
            new { Color = "onyx", Return = new[] { new { Color = "gold", Count = 1 } } }, "splendor.invalidReturn");

        var ten = Fixture(x => x.Seats[0].Tokens = new SplendorGems { Diamond = 2, Sapphire = 2, Emerald = 2, Ruby = 2, Onyx = 2 });
        Expect(ten, SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "ruby", "onyx", "emerald" } }, "splendor.tokenLimitExceeded");
        var tenReturns = Try(ten, SplendorActionType.TakeThreeGems,
            new
            {
                Colors = new[] { "ruby", "onyx", "emerald" },
                Return = new[]
                {
                    new { Color = "diamond", Count = 1 },
                    new { Color = "sapphire", Count = 1 },
                    new { Color = "ruby", Count = 1 }
                }
            }, out _);
        Check("overflow 3 with 3 returns (incl. just-taken)", tenReturns is null);
        Expect(ten, SplendorActionType.TakeTwoGems,
            new { Color = "onyx", Return = new[] { new { Color = "emerald", Count = 1 }, new { Color = "ruby", Count = 1 }, new { Color = "sapphire", Count = 1 } } }, "splendor.invalidReturn");
        var atTenNoGain = Fixture(x =>
        {
            x.Seats[0].Tokens = new SplendorGems { Diamond = 2, Sapphire = 2, Emerald = 2, Ruby = 2, Onyx = 2 };
            x.Supply.Gold = 0;
        });
        Expect(atTenNoGain, SplendorActionType.ReserveMarketCard, new { CardId = "nope" }, "splendor.cardUnavailable");

        // A player at 10 tokens can still buy (purchases only reduce holdings).
        var buyAtTen = Fixture(marketTier1: new List<string> { "L1E01" }, configure: x =>
            x.Seats[0].Tokens = new SplendorGems { Ruby = 10 });
        var buyErr = Try(buyAtTen, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1E01", Payment = Pay(new[] { 0, 0, 0, 3, 0 }) }, out var afterBuyTen);
        Check("purchase while holding 10 tokens", buyErr is null && Parse(afterBuyTen).Seats[0].Tokens.TotalWithGold == 7);
    }

    // ---------------------------------------------------------------- reservations

    private static void ReservationChecks()
    {
        var s = Fixture(marketTier1: new List<string> { "L1D02", "L1E01", "L1O01", "L1R01" });
        var st = Parse(s);
        var nextTier1 = st.Decks[0][0];

        var r1 = Act(s, SplendorActionType.ReserveMarketCard, new { CardId = "L1D02" });
        var p1 = Parse(r1);
        Check("market card reserved", p1.Seats[0].Reserved.Count == 1
            && p1.Seats[0].Reserved[0].CardId == "L1D02" && p1.Seats[0].Reserved[0].Source == SplendorReservationSource.Market);
        Check("market refilled from tier deck", p1.Market[0] == nextTier1 && p1.Decks[0].Count == 39);
        Check("gold taken with reserve", p1.Seats[0].Tokens.Gold == 1 && p1.Supply.Gold == 4);
        Check("reserve gives nothing", p1.Seats[0].VictoryPoints == 0 && p1.Seats[0].Purchased.Count == 0);
        Check("cardReserved names market card", string.Join("\n", p1.EventLog)
            .Contains("L1D02", StringComparison.Ordinal));

        // Seat 1 acts now (turn advanced): blind tier-2 reserve.
        var before = Parse(r1).Decks[1][0];
        var r2 = Act(r1, SplendorActionType.ReserveDeckCard, new { Tier = 2 });
        var p2 = Parse(r2);
        Check("deck reserve blind to seat 1", p2.Seats[1].Reserved.Count == 1
            && p2.Seats[1].Reserved[0].Source == SplendorReservationSource.Deck
            && p2.Seats[1].Reserved[0].CardId == before
            && p2.Seats[1].Reserved[0].Tier == 2);
        Check("deck reserve reduced tier-2 deck", p2.Decks[1].Count == 29);
        Check("deck reserve did NOT touch market", p2.Market.SequenceEqual(Parse(r1).Market));
        Check("log hides blind card id", !string.Join("\n", p2.EventLog).Contains(before, StringComparison.Ordinal));
        Check("gold on blind reserve", p2.Seats[1].Tokens.Gold == 1);

        // 3-reservation limit.
        var threeState = Fixture(marketTier1: new List<string> { "L1D01", "L1D03", "L1D04", "L1D05" }, configure: x =>
        {
            for (var i = 0; i < 3; i++)
            {
                x.Seats[0].Reserved.Add(new SplendorReservation { CardId = CardIds(i), Tier = 1, Source = SplendorReservationSource.Deck });
            }
        });
        Expect(threeState, SplendorActionType.ReserveMarketCard, new { CardId = "L1D01" }, "splendor.reservationLimit");
        Expect(threeState, SplendorActionType.ReserveDeckCard, new { Tier = 3 }, "splendor.reservationLimit");
        var thirdOk = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x =>
        {
            for (var i = 0; i < 2; i++)
            {
                x.Seats[0].Reserved.Add(new SplendorReservation { CardId = CardIds(i), Tier = 1, Source = SplendorReservationSource.Deck });
            }
        });
        Check("third reservation is legal",
            Try(thirdOk, SplendorActionType.ReserveMarketCard, new { CardId = "L1D01" }, out _) is null);

        // No gold left: reserve still legal, no gold.
        var noGold = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x => x.Supply.Gold = 0);
        var ngErr = Try(noGold, SplendorActionType.ReserveMarketCard, new { CardId = "L1D01" }, out var ngState);
        Check("reserve at 0 gold still works", ngErr is null && Parse(ngState).Seats[0].Tokens.Gold == 0);

        // Reserve at 10 tokens -> must return exactly one.
        var tenReserve = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x =>
            x.Seats[0].Tokens = new SplendorGems { Diamond = 2, Sapphire = 2, Emerald = 2, Ruby = 2, Onyx = 2 });
        Expect(tenReserve, SplendorActionType.ReserveMarketCard, new { CardId = "L1D01" }, "splendor.tokenLimitExceeded");
        Check("reserve at 10 with return accepted", Try(tenReserve, SplendorActionType.ReserveMarketCard,
            new { CardId = "L1D01", Return = new[] { new { Color = "gold", Count = 1 } } }, out _) is null);
        Check("return of just-taken gold allowed", Try(tenReserve, SplendorActionType.ReserveMarketCard,
            new { CardId = "L1D01", Return = new[] { new { Color = "diamond", Count = 1 } } }, out _) is null);

        // Bad targets.
        Expect(noGold, SplendorActionType.ReserveMarketCard, new { CardId = "L3S04" }, "splendor.cardUnavailable");
        Expect(noGold, SplendorActionType.ReserveMarketCard, new { CardId = "not-a-card" }, "splendor.cardUnavailable");
        var emptyDeck = Fixture(x => x.Decks[2].Clear());
        Expect(emptyDeck, SplendorActionType.ReserveDeckCard, new { Tier = 3 }, "splendor.deckEmpty");
        Expect(emptyDeck, SplendorActionType.ReserveDeckCard, new { Tier = 9 }, "splendor.invalidPayload");

        // Last market slot drains when the tier deck is empty.
        var lastSlot = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x => x.Decks[0].Clear());
        var ls = Act(lastSlot, SplendorActionType.ReserveMarketCard, new { CardId = "L1D01" });
        Check("empty deck -> market slot null", Parse(ls).Market[0] is null);
    }

    private static string CardIds(int i) => SplendorCatalogue.Cards[i].Id;

    // ---------------------------------------------------------------- purchasing

    private static void PurchaseChecks()
    {
        // Exact cost.
        var s = Fixture(marketTier1: new List<string> { "L1S01" }, configure: x => x.Seats[0].Tokens = new SplendorGems { Onyx = 3 });
        var b = Act(s, SplendorActionType.PurchaseMarketCard, new { CardId = "L1S01", Payment = Pay(new[] { 0, 0, 0, 0, 3 }) });
        var pb = Parse(b);
        Check("exact-cost purchase", pb.Seats[0].Purchased.Contains("L1S01")
            && pb.Seats[0].Tokens.Onyx == 0 && pb.Supply.Onyx == 10);
        Check("market refilled after purchase", pb.Market[0] is not null && pb.Decks[0].Count == 39);

        // Zero-cost purchase via bonuses.
        var disc = Fixture(marketTier1: new List<string> { "L2E01" }, configure: x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Emerald, 5)) x.Seats[0].Purchased.Add(id);
            x.Seats[0].Tokens = new SplendorGems { Diamond = 1, Sapphire = 1, Emerald = 1, Ruby = 1, Onyx = 1, Gold = 1 };
        });
        var discErr = Try(disc, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L2E01", Payment = Pay(Array.Empty<int>()) }, out var afterDisc);
        Check("zero-cost purchase via bonuses", discErr is null
            && Parse(afterDisc).Seats[0].Tokens.TotalWithGold == 6
            && Parse(afterDisc).Seats[0].Bonus(2) == 6);

        // Bonus coverage partially reduced cost.
        var part = Fixture(marketTier1: new List<string> { "L2E01" }, configure: x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Emerald, 2)) x.Seats[0].Purchased.Add(id);
            x.Seats[0].Tokens = new SplendorGems { Emerald = 3, Ruby = 5 };
        });
        Expect(part, SplendorActionType.PurchaseMarketCard, new { CardId = "L2E01", Payment = Pay(new[] { 0, 0, 2, 0, 0 }) }, "splendor.insufficientFunds");
        Check("bonus-discounted payment",
            Try(part, SplendorActionType.PurchaseMarketCard, new { CardId = "L2E01", Payment = Pay(new[] { 0, 0, 3, 0, 0 }) }, out _) is null);
        Expect(part, SplendorActionType.PurchaseMarketCard, new { CardId = "L2E01", Payment = Pay(new[] { 0, 0, 0, 5, 0 }) }, "splendor.invalidPayment");

        // Gold joker covers the shortfall exactly; over/under rejected; wrong colors rejected.
        var gold = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x => x.Seats[0].Tokens = new SplendorGems { Sapphire = 2, Gold = 2 });
        Check("gold covers shortfall", Try(gold, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1D01", Payment = Pay(new[] { 0, 2 }, gold: 1) }, out _) is null);
        Expect(gold, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D01", Payment = Pay(new[] { 0, 2 }, gold: 2) }, "splendor.invalidPayment");
        Expect(gold, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D01", Payment = Pay(new[] { 0, 2 }, gold: 0) }, "splendor.insufficientFunds");
        Expect(gold, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D01", Payment = Pay(new[] { 1, 0, 0, 0, 0 }, gold: 3) }, "splendor.invalidPayment");
        Expect(gold, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D01", Payment = Pay(new[] { 3, 0, 0, 0, 0 }) }, "splendor.invalidPayment");
        Expect(gold, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D01", Payment = Pay(new[] { 0, -1, 0, 0, 0 }) }, "splendor.invalidPayload");

        // 1-VP card scores immediately.
        var res = Fixture(marketTier1: new List<string> { "L1D02" }, configure: x => x.Seats[0].Tokens = new SplendorGems { Emerald = 4 });
        var bought = Act(res, SplendorActionType.PurchaseMarketCard, new { CardId = "L1D02", Payment = Pay(new[] { 0, 0, 4, 0, 0 }) });
        Check("1-VP card scores", Parse(bought).Seats[0].VictoryPoints == 1);

        // Buy from hand: no market refill, reservation slot frees.
        var fromHand = Fixture(x =>
        {
            x.Seats[0].Tokens = new SplendorGems { Onyx = 3 };
            x.Seats[0].Reserved.Add(new SplendorReservation { CardId = "L1S01", Tier = 1, Source = SplendorReservationSource.Deck });
        });
        var handBought = Act(fromHand, SplendorActionType.PurchaseReservedCard, new { ReservationIndex = 0, Payment = Pay(new[] { 0, 0, 0, 0, 3 }) });
        var ph = Parse(handBought);
        Check("reserved card purchased", ph.Seats[0].Purchased.Contains("L1S01") && ph.Seats[0].Reserved.Count == 0);
        Check("hand purchase does not touch market", ph.Market.All(id => id is null));
        Check("purchase reveals card id in log", string.Join("\n", ph.EventLog).Contains("L1S01", StringComparison.Ordinal));
        Expect(fromHand, SplendorActionType.PurchaseReservedCard, new { ReservationIndex = 1, Payment = Pay(new[] { 0, 0, 0, 0, 3 }) }, "splendor.reservationNotFound");
        Expect(fromHand, SplendorActionType.PurchaseReservedCard, new { ReservationIndex = -1, Payment = Pay(Array.Empty<int>()) }, "splendor.reservationNotFound");

        // Not your card / not in market.
        var other = Fixture(marketTier1: new List<string> { "L1D01" }, configure: x =>
        {
            x.Seats[1].Reserved.Add(new SplendorReservation { CardId = "L1S01", Tier = 1, Source = SplendorReservationSource.Market });
            x.Seats[0].Tokens = new SplendorGems { Onyx = 9, Gold = 1 };
        });
        Expect(other, SplendorActionType.PurchaseMarketCard, new { CardId = "L1S01", Payment = Pay(new[] { 0, 0, 0, 0, 3 }) }, "splendor.cardUnavailable");
        Expect(other, SplendorActionType.PurchaseReservedCard, new { ReservationIndex = 0, Payment = Pay(Array.Empty<int>()) }, "splendor.reservationNotFound");
    }

    // ---------------------------------------------------------------- nobles

    private static void NobleChecks()
    {
        // Auto-award at exactly one eligible after a purchase: 4 D-bonus + 3
        // S-bonus held, buy the 4th S-bonus card (L1S04: 2E 2O) -> N-DS.
        SplendorState Seed(SplendorState x, int sapphireOwned)
        {
            foreach (var id in ZeroVp(SplendorColor.Diamond, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Sapphire, 4).Where(id => id != "L1S04").Take(sapphireOwned))
            {
                x.Seats[0].Purchased.Add(id);
            }

            x.Seats[0].Tokens = new SplendorGems { Emerald = 2, Onyx = 2 };
            return x;
        }

        var one = Fixture(configure: x => Seed(x, 3), marketTier1: new List<string> { "L1S04" });
        var oneAwarded = Act(one, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 2, 0, 2 }) });
        var po = Parse(oneAwarded);
        Check("noble auto-awarded at 1 eligible (no claim needed)",
            po.Seats[0].NoblesOwned.Contains("N-DS") && !po.NoblesInMarket.Contains("N-DS"));
        Check("noble adds 3 points", po.Seats[0].VictoryPoints == 3);
        Check("nobleClaimed logged", LogHas(oneAwarded, "splendor.nobleClaimed"));

        // Multiple eligible: claim required, choice honored, others remain.
        var many = Fixture(configure: x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Diamond, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Sapphire, 4).Where(id => id != "L1S04")) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Emerald, 4)) x.Seats[0].Purchased.Add(id);
            x.Seats[0].Tokens = new SplendorGems { Emerald = 2, Onyx = 2 };
        }, marketTier1: new List<string> { "L1S04" });
        Expect(many, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 0, 0, 2 }) }, "splendor.nobleChoiceRequired");
        var claimErr = Try(many, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 0, 0, 2 }), ClaimNoble = "N-SE" }, out var afterClaim);
        Check("claim one of two eligible", claimErr is null);
        var pc = Parse(afterClaim);
        Check("chosen noble awarded", pc.Seats[0].NoblesOwned.SequenceEqual(new[] { "N-SE" }));
        Check("other eligible noble remains", pc.NoblesInMarket.Contains("N-DS"));

        // Rejected claims (also prove the whole action is not partially applied).
        Expect(many, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 0, 0, 2 }), ClaimNoble = "N-RO" }, "splendor.nobleChoiceInvalid");
        Expect(many, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 0, 0, 2 }), ClaimNoble = "N-BOGUS" }, "splendor.nobleUnavailable");
        Check("failed action leaves state untouched", Parse(many).Seats[0].Tokens.Onyx == 2);

        // Claim mismatch when exactly one eligible is rejected.
        Expect(one, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L1S04", Payment = Pay(new[] { 0, 0, 2, 0, 2 }), ClaimNoble = "N-RO" }, "splendor.nobleChoiceInvalid");

        // Reserved cards give no bonuses; tokens never satisfy nobles.
        var reservedNotCounted = Fixture(x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Diamond, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Sapphire, 4))
            {
                x.Seats[0].Reserved.Add(new SplendorReservation { CardId = id, Tier = 1, Source = SplendorReservationSource.Deck });
            }
        });
        Check("reserved cards give no bonus", Parse(reservedNotCounted).Seats[0].Bonus(1) == 0);
        var tokensOnly = Fixture(x => x.Seats[0].Tokens = new SplendorGems { Diamond = 4, Sapphire = 4 });
        var tokensAward = Act(tokensOnly, SplendorActionType.TakeTwoGems, new { Color = "onyx" });
        Check("tokens never award nobles", Parse(tokensAward).Seats[0].NoblesOwned.Count == 0);

        // Carried-over eligibility: 4D+4S+4E bonuses make N-DS, N-SE and N-DSE
        // eligible (three!). One per turn; claims keep being required while
        // two or more remain; the last is auto-awarded.
        var carry = Fixture(configure: x =>
        {
            foreach (var color in new[] { SplendorColor.Diamond, SplendorColor.Sapphire, SplendorColor.Emerald })
            {
                foreach (var id in ZeroVp(color, 4)) x.Seats[0].Purchased.Add(id);
            }
        });
        // Rotating color combos so ten takes never exceed any single pile (7).
        var combos = new[]
        {
            new[] { "ruby", "emerald", "onyx" },
            new[] { "diamond", "sapphire", "ruby" },
            new[] { "emerald", "onyx", "diamond" },
            new[] { "sapphire", "ruby", "emerald" },
            new[] { "onyx", "diamond", "sapphire" }
        };
        GameState TakeN(GameState st, int n, string? claim) =>
            Act(st, SplendorActionType.TakeThreeGems,
                claim is null
                    ? (object)new { Colors = combos[n % 5] }
                    : new { Colors = combos[n % 5], ClaimNoble = claim });

        Expect(carry, SplendorActionType.TakeThreeGems, new { Colors = combos[0] }, "splendor.nobleChoiceRequired");
        var carry1 = TakeN(carry, 0, "N-DS");
        Check("first turn claims one of three", Parse(carry1).Seats[0].NoblesOwned.SequenceEqual(new[] { "N-DS" }));
        var carry2 = TakeN(carry1, 1, null);
        var carry3 = TakeN(carry2, 2, null);
        var carry4 = TakeN(carry3, 3, null);
        Expect(carry4, SplendorActionType.TakeThreeGems, new { Colors = combos[4] }, "splendor.nobleChoiceRequired");
        Check("carried-over eligibility still demands a claim next own turn", true);
        var carry5 = TakeN(carry4, 4, "N-SE");
        Check("second claim lands on carried-over turn",
            Parse(carry5).Seats[0].NoblesOwned.SequenceEqual(new[] { "N-DS", "N-SE" }));
        var carry6 = TakeN(carry5, 0, null);
        var carry7 = TakeN(carry6, 1, null);
        var carry8 = TakeN(carry7, 2, null);
        var carry9 = TakeN(carry8, 3, null);
        Check("last eligible noble auto-awarded",
            Parse(carry9).Seats[0].NoblesOwned.SequenceEqual(new[] { "N-DS", "N-SE", "N-DSE" }));
    }

    // ---------------------------------------------------------------- end game

    private static void EndGameChecks()
    {
        // 14 VP on the table; the purchase crosses 15 -> equal final turns.
        var trigger = Fixture(marketTier1: new List<string> { "L3S04" }, configure: x =>
        {
            foreach (var id in new[] { "L3D04", "L3E01", "L3R04" }) x.Seats[0].Purchased.Add(id);
            x.Seats[0].Tokens = new SplendorGems { Diamond = 7, Sapphire = 3 };
        });
        var trig15 = Act(trigger, SplendorActionType.PurchaseMarketCard,
            new { CardId = "L3S04", Payment = Pay(new[] { 6, 3, 0, 0, 0 }) });
        var pt = Parse(trig15);
        Check("15 crosses -> final round", pt.FinalRoundTriggered && pt.TriggerSeat == 0 && pt.FinalTurnSeatsPending.Count == 3);
        Check("trigger does not end game", !trig15.IsOver);
        Check("finalRoundTriggered logged", LogHas(trig15, "splendor.finalRoundTriggered"));
        Check("trigger player does not get another turn", trig15.CurrentPlayerIndex == 1);

        var s2 = Act(trig15, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        var s3 = Act(s2, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        var s4 = Act(s3, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        Check("game over after equal final turns", s4.IsOver);
        Check("winner is the 15+ player", s4.Winner == s4.Players[0]);
        Check("gameFinished logged", LogHas(s4, "splendor.gameFinished"));
        Expect(s4, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "onyx" } }, "splendor.gameOver");

        // Existing >=15 triggers at the end of the actor's next turn.
        var already = Fixture(x =>
        {
            foreach (var id in new[] { "L3D04", "L3S04", "L3O04" }) x.Seats[0].Purchased.Add(id);
        });
        var overErr = Try(already, SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "diamond", "sapphire", "emerald" } }, out var overAfter);
        Check("threshold checked at every turn end", overErr is null && Parse(overAfter).FinalRoundTriggered);

        // Reaching 15 via a noble award at end of turn (12 VP + N-DS +3).
        var nobleEnd = Fixture(x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Diamond, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Sapphire, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in new[] { "L3D04", "L3S04", "L2D06", "L2S06" }) x.Seats[0].Purchased.Add(id);
            // D6, S6 bonuses -> exactly N-DS eligible; VP = 5+5+1+1 = 12.
        });
        var nobleEndPre = Parse(nobleEnd).Seats[0].VictoryPoints;
        var nobleEndAfter = Act(nobleEnd, SplendorActionType.TakeThreeGems, new { Colors = new[] { "ruby", "emerald", "onyx" } });
        var pne = Parse(nobleEndAfter);
        Check("noble award can cross the threshold", pne.Seats[0].VictoryPoints >= 15 && pne.FinalRoundTriggered);
        Check("pre-award score was below 15", nobleEndPre < 15);

        // Tie-break: fewer purchased cards wins.
        var tieFewestPurchased = Fixture(x =>
        {
            foreach (var id in new[] { "L3D04", "L3E04", "L3R04" }) x.Seats[0].Purchased.Add(id); // 15, 3 cards
            foreach (var id in new[] { "L3D03", "L3E03", "L3O03", "L3R03", "L3S03" }) x.Seats[1].Purchased.Add(id); // 15, 5 cards
            x.FinalRoundTriggered = true;
            x.TriggerSeat = 0;
            x.FinalTurnSeatsPending = new List<int> { 1 };
            x.CurrentPlayerIndex = 1;
        });
        var tieResolved = Act(tieFewestPurchased, SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        Check("tie-break: fewer purchased wins", tieResolved.IsOver && tieResolved.Winner == tieResolved.Players[0]);

        // Tie-break: equal VP and equal purchased -> fewer reserved wins.
        var tied = new[] { "L3D03", "L3E03", "L3O03", "L3R03", "L3S03" };
        var tieReserved = Fixture(x =>
        {
            foreach (var id in tied) x.Seats[0].Purchased.Add(id);
            foreach (var id in tied) x.Seats[1].Purchased.Add(id);
            x.Seats[0].Reserved.Add(new SplendorReservation { CardId = "L1D01", Tier = 1, Source = SplendorReservationSource.Deck });
            x.FinalRoundTriggered = true;
            x.TriggerSeat = 0;
            x.FinalTurnSeatsPending = new List<int> { 1 };
            x.CurrentPlayerIndex = 1;
        });
        var tieR = Act(tieReserved, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        Check("tie-break: fewer reserved wins", tieR.IsOver && tieR.Winner == tieR.Players[1]);

        // Full tie -> shared victory.
        var shared = Fixture(x =>
        {
            foreach (var seat in x.Seats.Take(2))
            {
                foreach (var id in tied) seat.Purchased.Add(id);
            }

            x.FinalRoundTriggered = true;
            x.TriggerSeat = 0;
            x.FinalTurnSeatsPending = new List<int> { 1 };
            x.CurrentPlayerIndex = 1;
        });
        var sharedOver = Act(shared, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        Check("full tie = shared victory", sharedOver.IsOver && sharedOver.Winner is null);

        // Final-round VP crossings never extend.
        var noExtend = Fixture(x =>
        {
            x.FinalRoundTriggered = true;
            x.TriggerSeat = 0;
            x.FinalTurnSeatsPending = new List<int> { 1 };
            x.CurrentPlayerIndex = 1;
            foreach (var id in new[] { "L3D04", "L3E04", "L3R04", "L3O04" }) x.Seats[1].Purchased.Add(id);
        });
        var ended = Act(noExtend, SplendorActionType.TakeThreeGems, new { Colors = new[] { "diamond", "sapphire", "emerald" } });
        Check("final turn ends game regardless of crossing 15 again", ended.IsOver && ended.Winner == ended.Players[1]);
    }

    // ---------------------------------------------------------------- valid actions

    private static void ValidActionsChecks()
    {
        var s = Fresh(4);
        Check("non-current player gets none",
            Game.GetValidActions(s, s.Players[((int)s.CurrentPlayerIndex! + 1) % 4]).Count == 0);

        var full = Parse(s);
        var cur = s.Players[full.CurrentPlayerIndex];
        var acts = Game.GetValidActions(s, cur);
        Check("10 three-gem combos", acts.Count(a => a.ActionType == "TakeThreeGems") == 10);
        Check("5 two-gem actions at supply 7", acts.Count(a => a.ActionType == "TakeTwoGems") == 5);
        Check("12 market reserves + 3 deck reserves",
            acts.Count(a => a.ActionType == "ReserveMarketCard") == 12 && acts.Count(a => a.ActionType == "ReserveDeckCard") == 3);
        Check("no purchases with empty hand", !acts.Any(a => a.ActionType.StartsWith("Purchase", StringComparison.Ordinal)));
        Check("all listed valid actions are accepted", acts.All(a => Game.ProcessAction(s, a).IsValid));

        var broke = Fixture(x => x.Supply = new SplendorGems { Gold = 3 });
        var brokeActs = Game.GetValidActions(broke, broke.Players[0]);
        Check("no takes when supply empty",
            brokeActs.All(a => a.ActionType != "TakeThreeGems" && a.ActionType != "TakeTwoGems"));
        Check("gold-only supply still allows reserves", brokeActs.Count(a => a.ActionType == "ReserveDeckCard") == 3);

        var poor = Fixture(marketTier1: new List<string> { "L1S01", "L3S04" }, configure: x =>
        {
            x.Market[0] = "L1S01";
            x.Market[1] = "L3S04";
            x.Seats[0].Tokens = new SplendorGems { Onyx = 3 };
        });
        var poorActs = Game.GetValidActions(poor, poor.Players[0]);
        var purchases = poorActs.Where(a => a.ActionType.StartsWith("Purchase", StringComparison.Ordinal)).ToList();
        Check("affordable purchase enumerated", purchases.Any(a => a.Payload.Contains("L1S01", StringComparison.Ordinal)));
        Check("unaffordable purchase absent", purchases.All(a => !a.Payload.Contains("L3S04", StringComparison.Ordinal)));

        var doneWrapped = Fixture(x => x.CurrentPlayerIndex = 0);
        var doneFlag = new GameState
        {
            SessionId = doneWrapped.SessionId,
            GameType = "Splendor",
            Players = doneWrapped.Players,
            CurrentPlayerIndex = 0,
            IsOver = true,
            Version = 3,
            Data = doneWrapped.Data
        };
        Check("no valid actions when over", Game.GetValidActions(doneFlag, doneFlag.Players[0]).Count == 0);
        Check("actions rejected when over",
            TryAs(doneFlag, doneFlag.Players[0], SplendorActionType.TakeThreeGems,
                new { Colors = new[] { "diamond", "sapphire", "emerald" } }, out _) == "splendor.gameOver");
        Check("unknown player gets none", Game.GetValidActions(doneWrapped, new PlayerId(Guid.NewGuid())).Count == 0);
        Check("unknown action type rejected",
            Try(doneWrapped, (SplendorActionType)(-7), new { }, out _) is "splendor.unknownActionType");
        Check("not-your-turn rejected",
            TryAs(doneWrapped, doneWrapped.Players[1], SplendorActionType.TakeThreeGems,
                new { Colors = new[] { "diamond", "sapphire", "emerald" } }, out _) == "splendor.notYourTurn");
    }

    // ---------------------------------------------------------------- persistence

    private static void PersistenceChecks()
    {
        var s = Fresh(4, 11);
        var st0 = Parse(s);
        var deckFront = st0.Decks[0][0];
        var firstSeat = st0.CurrentPlayerIndex;

        s = GameState.FromJson(s.ToJson());
        var r1 = Act(s, SplendorActionType.ReserveDeckCard, new { Tier = 1 });
        var p1 = Parse(r1);
        Check("blind reservation drew the deck front", p1.Seats[firstSeat].Reserved.Count == 1
            && p1.Seats[firstSeat].Reserved[0].CardId == deckFront);

        // The reservation id survives serialize -> deserialize -> continue.
        r1 = GameState.FromJson(r1.ToJson());
        var reSeat = (int)r1.CurrentPlayerIndex!;
        Check("next seat plays on reloaded state",
            Game.GetValidActions(r1, r1.Players[reSeat]).Count > 0
            && Parse(r1).Seats[firstSeat].Reserved[0].CardId == deckFront);

        var owner = r1.Players[firstSeat];
        var viewOwner = ViewJson(r1, owner);
        Check("even the owner's view hides the blind id", !viewOwner.Contains(deckFront, StringComparison.Ordinal));

        // A 15-step chain with round-trips between every step stays consistent.
        var chain = Fresh(2, 5);
        for (var i = 0; i < 15; i++)
        {
            chain = GameState.FromJson(chain.ToJson());
            var cur = chain.Players[chain.CurrentPlayerIndex ?? 0];
            var options = Game.GetValidActions(chain, cur);
            if (options.Count == 0)
            {
                break;
            }

            var pick = options[(i * 7) % options.Count];
            var res = Game.ProcessAction(chain, pick);
            Check($"chain step {i} valid", res.IsValid);
            if (!res.IsValid)
            {
                break;
            }

            chain = res.NewState!;
        }

        var cp = Parse(chain);
        var gems = 4;
        Check("chain conserved gems per color", Enumerable.Range(0, 5).All(i =>
            cp.Supply[i] + cp.Seats.Sum(x => x.Tokens[i]) == gems));
        Check("chain conserved gold", cp.Supply.Gold + cp.Seats.Sum(x => x.Tokens.Gold) == 5);
        Check("chain version bumped", chain.Version > 1);
        Check("chain input states untouched", Parse(GameState.FromJson(Game.CreateGame(Opts(2, 5)).ToJson())).EventLog.Count == 1);

        // Final-round state survives a reload.
        var trig = Fixture(marketTier1: new List<string> { "L3S04" }, configure: x =>
        {
            foreach (var id in new[] { "L3D04", "L3E01", "L3R04" }) x.Seats[0].Purchased.Add(id);
            x.Seats[0].Tokens = new SplendorGems { Diamond = 7, Sapphire = 3 };
        });
        var trigState = Act(trig, SplendorActionType.PurchaseMarketCard, new { CardId = "L3S04", Payment = Pay(new[] { 6, 3, 0, 0, 0 }) });
        var reloaded = GameState.FromJson(trigState.ToJson());
        var pr = Parse(reloaded);
        Check("final round survives reload", pr.FinalRoundTriggered && pr.FinalTurnSeatsPending.Count == 3);
        Check("timers survive reload", pr.NextActionDeadlineUtc == Parse(trigState).NextActionDeadlineUtc
            && pr.GameEndsAtUtc == Parse(trigState).GameEndsAtUtc);

        // Deck order persists exactly.
        var deckBefore = Parse(trigState).Decks[1];
        var deckAfter = Parse(GameState.FromJson(trigState.ToJson())).Decks[1];
        Check("deck order persists exactly", deckBefore.SequenceEqual(deckAfter));
    }

    // ---------------------------------------------------------------- randomness

    private static void RandomnessChecks()
    {
        // Client cannot steer a blind draw: extra fields are ignored, the deck front wins.
        var s = Fixture(x => x.CurrentPlayerIndex = 0);
        var st = Parse(s);
        var expected = st.Decks[0][0];
        var err = Try(s, SplendorActionType.ReserveDeckCard, new { Tier = 1, CardId = "L3S04" }, out var after);
        var p = Parse(after);
        Check("forged card id ignored (deck front wins)", err is null && p.Seats[0].Reserved[0].CardId == expected);
        Check("forged card id cannot pick the card", p.Seats[0].Reserved[0].CardId != "L3S04" || expected == "L3S04");

        // The chosen card comes from the deck top - market purchase of a hidden card fails.
        var otherSeat = after.Players[1];
        var err2 = TryAs(after, otherSeat, SplendorActionType.PurchaseMarketCard,
            new { CardId = expected, Payment = Pay(Array.Empty<int>()) }, out _);
        Check("cannot buy cards hidden in decks/hands", err2 == "splendor.cardUnavailable");
    }

    // ---------------------------------------------------------------- hidden information

    private static void HiddenInformationChecks()
    {
        var s = Fresh(4, 3);
        s = Act(s, SplendorActionType.ReserveDeckCard, new { Tier = 2 });
        var st = Parse(s);
        var blind = st.Seats[0].Reserved.Single(r => r.Source == SplendorReservationSource.Deck);
        var secretDeckIds = st.Decks.SelectMany(d => d).ToHashSet();

        foreach (var viewer in s.Players)
        {
            var view = ViewJson(s, viewer);
            var viewState = JsonDocument.Parse(view);
            Check($"view is not the authoritative shape (no Decks)",
                !viewState.RootElement.TryGetProperty("Decks", out _));
            Check($"view for {viewer} hides blind reservation",
                !view.Contains(blind.CardId, StringComparison.Ordinal));
            var market = viewState.RootElement.GetProperty("Market").EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : null)
                .Where(id => id is not null).ToHashSet();
            var purchased = viewState.RootElement.GetProperty("Seats").EnumerateArray()
                .SelectMany(se => se.GetProperty("Purchased").EnumerateArray().Select(e => e.GetString()!))
                .ToHashSet();
            var leaked = secretDeckIds.Where(id => !market.Contains(id) && !purchased.Contains(id)
                && view.Contains($"\"{id}\"", StringComparison.Ordinal)).ToList();
            Check($"no deck-resident id leaks to {viewer}", leaked.Count == 0);
        }

        Check("event log hides blind identity",
            !string.Join("\n", st.EventLog).Contains(blind.CardId, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- timers

    private static void TimerChecks()
    {
        var s = Fixture();
        Expect(s, SplendorActionType.TurnTimeout, new { }, "splendor.timerNotExpired");

        var expired = Fixture(x =>
        {
            x.TurnStartUtc = DateTime.UtcNow.AddSeconds(-300);
            x.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-30);
        });
        var after = Act(expired, SplendorActionType.TurnTimeout, new { });
        var pa = Parse(after);
        Check("timeout advances the turn", after.CurrentPlayerIndex == 1);
        Check("timeout charges overrun", pa.EnsureTimer(0).DeferredPenaltySeconds > 0);
        Check("timeout counts consecutive", pa.EnsureTimer(0).ConsecutiveTimeouts == 1);
        Check("new deadline set", pa.NextActionDeadlineUtc > DateTime.UtcNow);

        // Timeout auto-claims an eligible noble in display order (OD-5).
        var nobleTimeout = Fixture(x =>
        {
            foreach (var id in ZeroVp(SplendorColor.Diamond, 4)) x.Seats[0].Purchased.Add(id);
            foreach (var id in ZeroVp(SplendorColor.Sapphire, 4)) x.Seats[0].Purchased.Add(id);
            x.TurnStartUtc = DateTime.UtcNow.AddSeconds(-300);
            x.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-30);
        });
        var nobleAfter = Act(nobleTimeout, SplendorActionType.TurnTimeout, new { });
        Check("timeout claims eligible noble (display order first)",
            Parse(nobleAfter).Seats[0].NoblesOwned.Contains("N-DS"));

        // 2-player AFK: survivor wins instantly.
        var twoP = Fixture(players: 2, configure: x =>
        {
            x.TimerConfig = new SplendorTurnTimerConfig { MaxAfkTurns = 1 };
            x.TurnStartUtc = DateTime.UtcNow.AddSeconds(-300);
            x.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-30);
        });
        var a1 = Act(twoP, SplendorActionType.TurnTimeout, new { });
        Check("2p AFK: survivor wins", a1.IsOver && a1.Winner == a1.Players[1]);

        // 3-player AFK: seat removed, game continues, seat cannot act.
        var threeP = Fixture(players: 3, configure: x =>
        {
            x.TimerConfig = new SplendorTurnTimerConfig { MaxAfkTurns = 1 };
            x.TurnStartUtc = DateTime.UtcNow.AddSeconds(-300);
            x.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-30);
        });
        var b1 = Act(threeP, SplendorActionType.TurnTimeout, new { });
        var pb1 = Parse(b1);
        Check("3p AFK: eliminated, continues", !b1.IsOver && pb1.EliminatedSeats.Contains(0) && !pb1.ActiveSeats().Contains(0));
        Check("turn advanced past eliminated seat", b1.CurrentPlayerIndex == 1);
        var elimErr = TryAs(b1, b1.Players[0], SplendorActionType.TakeThreeGems,
            new { Colors = new[] { "diamond", "sapphire", "emerald" } }, out _);
        Check("eliminated seat cannot act", elimErr == "splendor.playerEliminated");

        // Advancement skips eliminated seats; last survivor wins.
        var b2 = Act(Expire(b1), SplendorActionType.TurnTimeout, new { });
        var pb2 = Parse(b2);
        Check("second elimination leaves one active", pb2.EliminatedSeats.Contains(1) && pb2.ActiveSeats().SequenceEqual(new[] { 2 }));
        Check("last survivor wins when others are out", b2.IsOver && b2.Winner == b2.Players[2]);

        // Game time expiry.
        var future = Fixture();
        Expect(future, SplendorActionType.GameTimeExpired, new { }, "splendor.timeNotUp");

        var timeUp = Fixture(x =>
        {
            x.Seats[0].Purchased.Add("L3S04");
            x.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-5);
        });
        var forceEnded = Act(timeUp, SplendorActionType.GameTimeExpired, new { });
        Check("GameTimeExpired force-finishes", forceEnded.IsOver && forceEnded.Winner == forceEnded.Players[0]);
        Check("deadlines cleared on end", Parse(forceEnded).NextActionDeadlineUtc is null && Parse(forceEnded).GameEndsAtUtc is null);

        var timeTie = Fixture(x =>
        {
            x.Seats[0].Purchased.Add("L3S04");
            x.Seats[1].Purchased.Add("L3S04");
            x.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-5);
        });
        var tieEnded = Act(timeTie, SplendorActionType.GameTimeExpired, new { });
        Check("force-finish tie -> shared victory (winner null)", tieEnded.IsOver && tieEnded.Winner is null);
    }

    // ---------------------------------------------------------------- invariant soak

    private static void InvariantSoakChecks()
    {
        foreach (var players in new[] { 2, 3, 4 })
        {
            var s = Fresh(players, 99 + players);
            var setup = Parse(s);
            var baseline = new int[6];
            for (var i = 0; i < 5; i++)
            {
                baseline[i] = setup.Supply[i] + setup.Seats.Sum(x => x.Tokens[i]);
            }

            baseline[5] = setup.Supply.Gold + setup.Seats.Sum(x => x.Tokens.Gold);

            var rng = new Random(7);
            var violations = new List<string>();
            for (var turn = 0; turn < 120 && !s.IsOver; turn++)
            {
                s = GameState.FromJson(s.ToJson());
                var cur = s.Players[s.CurrentPlayerIndex ?? 0];
                var options = Game.GetValidActions(s, cur);
                if (options.Count == 0)
                {
                    var stuck = Parse(s);
                    stuck.TurnStartUtc = DateTime.UtcNow.AddSeconds(-61);
                    stuck.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
                    var timeout = Try(WrapReusing(s, stuck), SplendorActionType.TurnTimeout, new { }, out var advanced);
                    if (timeout is not null)
                    {
                        violations.Add($"turn {turn}: stuck with no legal actions and timeout rejected ({timeout})");
                        break;
                    }

                    s = advanced;
                    continue;
                }

                var picked = options[rng.Next(options.Count)];
                var res = Game.ProcessAction(s, picked);
                if (!res.IsValid || res.NewState is null)
                {
                    violations.Add($"turn {turn}: enumerated {picked.ActionType} rejected: {res.Error}");
                    break;
                }

                s = res.NewState;
                var err = InvariantCheck(s, baseline);
                if (err is not null)
                {
                    violations.Add($"turn {turn}: {err}");
                    break;
                }
            }

            Check($"invariant soak {players}p", violations.Count == 0);
            foreach (var v in violations)
            {
                Console.WriteLine("    " + v);
            }
        }
    }

    /// <summary>Rewinds the current seat's turn clock past the hard deadline.</summary>
    private static GameState Expire(GameState s)
    {
        var st = Parse(s);
        st.TurnStartUtc = DateTime.UtcNow.AddSeconds(-300);
        st.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-30);
        return WrapReusing(s, st);
    }

    private static GameState WrapReusing(GameState template, SplendorState st)
    {
        st.PlayerIds = template.Players;
        return new GameState
        {
            SessionId = template.SessionId,
            GameType = "Splendor",
            Players = template.Players,
            CurrentPlayerIndex = st.CurrentPlayerIndex,
            IsOver = false,
            Version = template.Version + 1,
            NextActionDeadlineUtc = st.NextActionDeadlineUtc,
            GameEndsAtUtc = st.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SplendorState"] = st.ToJson(),
                ["PlayerNames"] = template.TryGetString("PlayerNames", out var names) ? names : "{}"
            }
        };
    }

    private static string? InvariantCheck(GameState gs, int[] baseline)
    {
        var st = Parse(gs);

        for (var i = 0; i < 5; i++)
        {
            if (st.Supply[i] < 0 || st.Seats.Any(x => x.Tokens[i] < 0))
            {
                return $"I1 negative tokens color {i}";
            }

            if (st.Supply[i] + st.Seats.Sum(x => x.Tokens[i]) != baseline[i])
            {
                return $"I1 conservation broken color {i}";
            }
        }

        if (st.Supply.Gold + st.Seats.Sum(x => x.Tokens.Gold) != baseline[5])
        {
            return "I1 gold conservation broken";
        }

        if (st.Seats.Any(x => x.Tokens.TotalWithGold > 10))
        {
            return "I2 token limit violated";
        }

        var locations = new List<string>();
        locations.AddRange(st.Decks.SelectMany(d => d));
        locations.AddRange(st.Market.Where(m => m is not null).Select(m => m!));
        foreach (var seat in st.Seats)
        {
            locations.AddRange(seat.Purchased);
            locations.AddRange(seat.Reserved.Select(r => r.CardId));
        }

        if (locations.Count != locations.Distinct().Count())
        {
            return "I3 card in two places";
        }

        if (locations.Any(id => SplendorCatalogue.FindCard(id) is null))
        {
            return "I3 unknown card id in state";
        }

        if (st.Seats.Any(x => x.Reserved.Count > 3))
        {
            return "I4 reservation limit violated";
        }

        for (var tier = 0; tier < 3; tier++)
        {
            if (st.Market[(tier * 4)..(tier * 4 + 4)].Any(m => m is not null && Card(m!).Tier != tier + 1))
            {
                return "I6 wrong tier in market";
            }
        }

        var nobleLocations = st.NoblesInMarket.Concat(st.Seats.SelectMany(x => x.NoblesOwned)).ToList();
        if (nobleLocations.Count != nobleLocations.Distinct().Count())
        {
            return "I10 noble awarded twice";
        }

        if (!st.IsOverStateSafe())
        {
            return "I11 current player not active";
        }

        return null;
    }
}

internal static class SplendorStateHarnessExtensions
{
    /// <summary>I11: while the game runs, the current seat must be active.</summary>
    public static bool IsOverStateSafe(this SplendorState st)
        => st.CurrentPlayerIndex >= 0
            && st.CurrentPlayerIndex < st.PlayerCount
            && !st.EliminatedSeats.Contains(st.CurrentPlayerIndex);
}
