using System.Text.Json;
using Azul;
using Azul.Models;
using GameEngine.Core.Models;

return AzulHarness.Run();

internal static class AzulHarness
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();

    private static readonly AzulGame Game = new();

    private static readonly Guid[] Guids =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        Guid.Parse("66666666-6666-6666-6666-666666666666")
    ];

    private static PlayerId Pid(int seat) => new(Guids[seat]);

    public static int Run()
    {
        SetupChecks();
        DraftChecks();
        PatternLineChecks();
        ScoringChecks();
        FloorChecks();
        RoundLifecycleChecks();
        EndGameChecks();
        ValidActionsChecks();
        ConservationChecks();
        PersistenceChecks();
        HiddenInfoChecks();
        TimerChecks();
        InvariantSoakChecks();

        Console.WriteLine();
        Console.WriteLine($"AZUL HARNESS: {_passed} passed, {_failed} failed");
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

    // ---------------------------------------------------------------- fixtures

    private static AzulState Fixture(int players = 2)
    {
        var a = new AzulState { FactoryCount = AzulCatalog.FactoryCountFor(players) };
        for (var f = 0; f < a.FactoryCount; f++)
        {
            a.Factories.Add(new List<int>());
        }

        a.Seats = Enumerable.Range(0, players).Select(_ => new AzulSeatState()).ToList();
        a.PlayerIds = Enumerable.Range(0, players).Select(Pid).ToList();
        a.PlayerNames = Enumerable.Range(0, players).ToDictionary(i => Guids[i], _ => "P");
        a.CurrentPlayerIndex = 0;
        a.FirstPlayerSeat = 0;
        return a;
    }

    private static GameState Wrap(AzulState a, long version = 1)
    {
        return new GameState
        {
            SessionId = Guid.NewGuid(),
            GameType = "Azul",
            Players = a.PlayerIds!,
            CurrentPlayerIndex = a.CurrentPlayerIndex,
            Version = version,
            NextActionDeadlineUtc = a.NextActionDeadlineUtc,
            GameEndsAtUtc = a.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["AzulState"] = a.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(a.PlayerNames ?? new Dictionary<Guid, string>())
            }
        };
    }

    private static AzulState Read(GameState s)
    {
        s.TryGetString("AzulState", out var json);
        var a = AzulState.FromJson(json!);
        a.PlayerIds = s.Players;
        if (s.TryGetString("PlayerNames", out var names) && names is not null)
        {
            a.PlayerNames = JsonSerializer.Deserialize<Dictionary<Guid, string>>(names);
        }

        return a;
    }

    private static GameResult Act(GameState s, int seat, AzulActionType type, object payload)
        => Game.ProcessAction(s, new GameAction
        {
            PlayerId = s.Players[seat],
            ActionType = type.ToString(),
            Payload = JsonSerializer.Serialize(payload)
        });

    private static string Err(GameResult r) => r.ErrorCode ?? r.Error ?? string.Empty;

    private static int Count(AzulState a)
        => a.Bag.Count
           + a.Center.Count
           + a.Discard.Count
           + a.Factories.Sum(f => f.Count)
           + a.Seats.Sum(s => s.PatternTiles() + s.Floor.Count + s.WallTiles());

    /// <summary>
    /// Builds a state whose only pool tile is a lone factory tile of `color` that
    /// completes `line` (cap-1 tiles already sit there). Drafting it drains the
    /// pool and runs the tiling phase — the marker is untouched (factory draft).
    /// </summary>
    private static AzulState LastDraftFixture(AzulState a, int line, int color, (int row, int col, int tile)[] wall)
    {
        foreach (var (row, col, tile) in wall)
        {
            a.Seats[0].Wall[row][col] = tile;
        }

        a.Seats[0].PatternLines[line].Clear();
        for (var i = 0; i < AzulSeatState.LineCapacity(line) - 1; i++)
        {
            a.Seats[0].PatternLines[line].Add(color);
        }

        a.Factories[0].Add(color);
        return a;
    }

    private static GameState PlayLastDraft(AzulState a, int line, int color)
    {
        var r = Act(Wrap(a), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = AzulCatalog.ColorName(color), LineIndex = line });
        if (!r.IsValid)
        {
            throw new InvalidOperationException("fixture draft rejected: " + Err(r));
        }

        return r.NewState!;
    }

    // ------------------------------------------------------------------- setup

    private static void SetupChecks()
    {
        foreach (var (players, factories) in new[] { (2, 5), (3, 7), (4, 9) })
        {
            var state = Game.CreateGame(new GameOptions
            {
                GameType = "Azul",
                Players = Enumerable.Range(0, players).Select(Pid).ToList(),
                Settings = "{\"Seed\":7}"
            });
            var a = Read(state);
            Check($"setup{players}: gameType", state.GameType == "Azul");
            Check($"setup{players}: factoryCount", a.FactoryCount == factories && a.Factories.Count == factories);
            Check($"setup{players}: each factory 4", a.Factories.All(f => f.Count == 4));
            Check($"setup{players}: center empty", a.Center.Count == 0);
            Check($"setup{players}: bag", a.Bag.Count == AzulCatalog.TotalTiles - 4 * factories);
            Check($"setup{players}: marker center", a.MarkerSeat == -1);
            Check($"setup{players}: boards empty", a.Seats.All(s => s.WallTiles() == 0 && s.PatternTiles() == 0 && s.Floor.Count == 0 && s.Score == 0));
            Check($"setup{players}: 20 each color",
                a.Bag.Concat(a.Factories.SelectMany(f => f)).GroupBy(c => c).Count(g => g.Count() == AzulCatalog.TilesPerColor) == 5);
            Check($"setup{players}: deadlines set", a.NextActionDeadlineUtc is not null && a.GameEndsAtUtc is not null);
            Check($"setup{players}: conservation", Count(a) == AzulCatalog.TotalTiles);
        }

        Check("setup: 1p rejected", Throws(() => Game.CreateGame(new GameOptions { Players = [Pid(0)] })));
        Check("setup: 5p rejected", Throws(() => Game.CreateGame(new GameOptions { Players = Enumerable.Range(0, 5).Select(Pid).ToList() })));
    }

    private static bool Throws(Action a)
    {
        try
        {
            a();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // ------------------------------------------------------------------- draft

    private static void DraftChecks()
    {
        var a = Fixture();
        a.Factories[0].AddRange(new[] { 0, 0, 1, 2 });
        var r = Act(Wrap(a), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 1 });
        Check("draft factory valid", r.IsValid);
        var b = Read(r.NewState!);
        Check("draft took color set", b.Seats[0].PatternLines[1].Count == 2 && b.Seats[0].PatternLines[1].All(c => c == 0));
        Check("draft remainder to center", b.Center.Count == 2 && b.Center.Contains(1) && b.Center.Contains(2));
        Check("draft emptied factory", b.Factories[0].Count == 0);
        Check("draft center draft does NOT move other factories", b.Factories.Skip(1).All(f => f.Count == 0));

        var c2 = Fixture();
        c2.Factories[0].AddRange(new[] { 0, 1 });
        Check("draft missing color rejected", Err(Act(Wrap(c2), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "White", LineIndex = 0 })) == "azul.noTilesOfColor");
        Check("draft bad factory index rejected", Err(Act(Wrap(c2), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 99, Color = "Blue", LineIndex = 0 })) == "azul.invalidFactory");
        Check("draft bad line index rejected", Err(Act(Wrap(c2), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 9 })) == "azul.invalidPayload");

        var c3 = Fixture();
        c3.Center.AddRange(new[] { 3, 3, 4 });
        var rr = Act(Wrap(c3), 0, AzulActionType.DraftFromCenter, new DraftFromCenterPayload { Color = "Black", LineIndex = 2 });
        Check("draft center valid", rr.IsValid);
        var c3b = Read(rr.NewState!);
        Check("first center draft claims marker", c3b.MarkerSeat == 0);
        Check("center took only that color", c3b.Seats[0].PatternLines[2].Count == 2 && c3b.Center.SequenceEqual(new[] { 4 }));

        var c4 = Fixture();
        Check("draft empty center rejected", Err(Act(Wrap(c4), 0, AzulActionType.DraftFromCenter,
            new DraftFromCenterPayload { Color = "Blue", LineIndex = 0 })) == "azul.centerEmpty");

        var c5 = Fixture();
        c5.Factories[0].Add(0);
        Check("draft wrong turn rejected", Err(Act(Wrap(c5), 1, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 })) == "azul.notYourTurn");

        var c6 = Fixture();
        c6.MarkerSeat = 1;
        Check("take marker already claimed rejected", Err(Act(Wrap(c6), 0, AzulActionType.TakeFirstPlayer, new { })) == "azul.firstPlayerNotAvailable");
        Check("take marker valid", Act(Wrap(Fixture()), 0, AzulActionType.TakeFirstPlayer, new { }).IsValid);

        // Forced floor: legal only when NO line can take the color.
        var c8 = Fixture();
        c8.Factories[1].Add(1); // keeps the pool alive so the floor is observable
        c8.Center.Add(0);
        for (var l = 0; l < 5; l++)
        {
            c8.Seats[0].PatternLines[l].Add(1); // every line occupied by Red (different color for Blue)
        }

        var fr = Act(Wrap(c8), 0, AzulActionType.DraftFromCenter, new DraftFromCenterPayload { Color = "Blue", LineIndex = AzulFloorSentinel.Value });
        Check("forced floor legal when no line", fr.IsValid);
        var c8b = Read(fr.NewState!);
        Check("forced floor routes to floor", c8b.Seats[0].Floor.Count == 1 && c8b.Seats[0].Floor[0] == 0);

        var c9 = Fixture();
        c9.Center.Add(0);
        Check("floor sentinel illegal when a line exists", Err(Act(Wrap(c9), 0, AzulActionType.DraftFromCenter,
            new DraftFromCenterPayload { Color = "Blue", LineIndex = AzulFloorSentinel.Value })) == "azul.floorNotOptional");

        var c10 = Fixture();
        c10.Factories[0].Add(0);
        c10.Seats[0].PatternLines[0].Add(1);
        Check("line color mismatch rejected", Err(Act(Wrap(c10), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 })) == "azul.lineColorMismatch");

        var c11 = Fixture();
        c11.Factories[0].Add(2);
        c11.Seats[0].PatternLines[0].Add(2); // line0 (cap1) full with Yellow
        Check("line full rejected", Err(Act(Wrap(c11), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Yellow", LineIndex = 0 })) == "azul.lineFull");
    }

    // ----------------------------------------------------------- pattern lines

    private static void PatternLineChecks()
    {
        // Completion is resolved only at the tiling phase, never mid-draft.
        var a = Fixture();
        a.Factories[0].Add(0);
        a.Factories[1].Add(1); // keeps the pool non-empty after the draft
        a.Seats[0].PatternLines[1].Add(0);
        var r = Act(Wrap(a), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 1 });
        var b = Read(r.NewState!);
        Check("completed line persists until tiling", r.IsValid && b.Seats[0].PatternLines[1].Count == 2 && b.Seats[0].Wall[1].All(c => c < 0) && b.RoundNumber == 1);

        // Wall-row color block.
        var c = Fixture();
        c.Factories[0].Add(0);
        c.Seats[0].Wall[2][0] = 0;
        Check("wall-row color blocks placement", Err(Act(Wrap(c), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 2 })) == "azul.wallRowHasColor");

        // A cross-round persisted line color restricts that line.
        var d = Fixture();
        d.Bag.Add(0);
        d.Factories[0].Add(1);
        d.Seats[0].PatternLines[3].Add(1); // Red leftover from a prior round
        var dr = Act(Wrap(d), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Red", LineIndex = 3 });
        Check("persisted line color accepted for same color", dr.IsValid && Read(dr.NewState!).Seats[0].PatternLines[3].Count == 2);
    }

    // ----------------------------------------------------------------- scoring

    private static void ScoringChecks()
    {
        // E1: isolated placement = 1 (row/col otherwise empty).
        var s1 = PlayLastDraft(LastDraftFixture(Fixture(), 0, 0, []), 0, 0);
        Check("E1 isolated = 1", Read(s1).Seats[0].Score == 1);

        // E2: completes a horizontal run of 3 -> 3+1-1 = 3.
        var s2 = PlayLastDraft(LastDraftFixture(Fixture(), 2, 0,
            new[] { (2, 0, 1), (2, 1, 2) }), 2, 0);
        Check("E2 horizontal 3 = 3", Read(s2).Seats[0].Score == 3);

        // E3: vertical run of 3 through the new tile -> 1+3-1 = 3.
        var s3 = PlayLastDraft(LastDraftFixture(Fixture(), 2, 0,
            new[] { (1, 0, 2), (3, 0, 1) }), 2, 0);
        Check("E3 vertical 3 = 3", Read(s3).Seats[0].Score == 3);

        // E4: joins a horizontal 3 and vertical 3 -> 3+3-1 = 5.
        var s4 = PlayLastDraft(LastDraftFixture(Fixture(), 2, 0,
            new[] { (2, 0, 1), (2, 1, 1), (1, 2, 2), (3, 2, 2) }), 2, 0);
        Check("E4 both runs = 5", Read(s4).Seats[0].Score == 5);

        // E5: a gap breaks the run — tile lands at col0, col2 pre-filled -> run 1.
        var s5 = PlayLastDraft(LastDraftFixture(Fixture(), 2, 0,
            new[] { (2, 2, 1) }), 2, 0);
        Check("E5 gap isolated = 1", Read(s5).Seats[0].Score == 1);
    }

    // ------------------------------------------------------------------- floor

    private static void FloorChecks()
    {
        // Line overflow lands on the floor; score = wall 1 - floor 3 (clamped 0).
        var a = Fixture();
        a.Factories[0].AddRange(new[] { 0, 0, 0, 0 });
        var r = Act(Wrap(a), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 });
        var b = Read(r.NewState!);
        Check("overflow legal via line spill", r.IsValid);
        Check("overflow floor clamps score to 0", b.Seats[0].Score == 0 && b.Seats[0].PatternLines[0].Count == 0);

        // Marker on a holder's floor costs exactly one and returns to the center.
        var c = Fixture();
        c.MarkerSeat = 0;
        c.Center.Add(0);
        c.Seats[0].PatternLines[1].Add(0);
        c.Seats[0].Score = 5;
        var cr = Act(Wrap(c), 0, AzulActionType.DraftFromCenter, new DraftFromCenterPayload { Color = "Blue", LineIndex = 1 });
        var cb = Read(cr.NewState!);
        Check("marker costs one at tiling", cb.Seats[0].Score == 5); // +1 isolated wall (6) then -1 marker (5)
        Check("marker returns to center at round end", cb.MarkerSeat == -1);

        // Beyond-floor overflow goes to discard with no extra penalty.
        var d = Fixture();
        d.Factories[0].AddRange(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }); // nine blues
        d.Factories[0].AddRange(new[] { 1 }); // keep center feed? factory remainder goes center
        var dr = Act(Wrap(d), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 });
        var db = Read(dr.NewState!);
        // line0: 1 placed (completes, no tiling yet since pool lives on), 8 to floor(7)+discard(1); Red leftover in center.
        Check("beyond-floor overflow is discarded", db.Discard.Count == 1 && db.Seats[0].Floor.Count == AzulCatalog.FloorCapacity && db.RoundNumber == 1);
    }

    // --------------------------------------------------------- round lifecycle

    private static void RoundLifecycleChecks()
    {
        var a = Fixture();
        a.Bag = Enumerable.Range(0, AzulCatalog.ColorCount).SelectMany(x => Enumerable.Repeat(x, AzulCatalog.TilesPerColor)).ToList();
        a.Bag.Remove(0); // the single drafted tile comes from factory0, not the bag
        AzulState.Shuffle(a.Bag, new Random(5));
        a.Factories[0].Add(0);
        var r = Act(Wrap(a), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 });
        var b = Read(r.NewState!);
        Check("pool drain ends round", r.IsValid && b.RoundNumber == 2);
        Check("round end refills factories", b.Factories.All(f => f.Count == AzulCatalog.TilesPerFactory));
        Check("round end starts at first player", b.CurrentPlayerIndex == b.FirstPlayerSeat);
        Check("completed line cleared into wall", b.Seats[0].PatternLines[0].Count == 0 && b.Seats[0].Wall[0][0] == 0);
        Check("conservation across round boundary", Count(b) == AzulCatalog.TotalTiles);

        // Incomplete lines persist across a still-running round.
        var c = Fixture();
        c.Bag.AddRange(Enumerable.Repeat(0, 40));
        c.Bag.AddRange(Enumerable.Repeat(1, 40));
        c.Factories[0].AddRange(new[] { 0, 1 });
        c.Seats[0].PatternLines[4].Add(2);
        var cr = Act(Wrap(c), 0, AzulActionType.DraftFromFactory, new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 0 });
        var cb = Read(cr.NewState!);
        Check("incomplete line persists when pool nonempty", cb.RoundNumber == 1 && cb.Seats[0].PatternLines[4].Contains(2));
    }

    // ---------------------------------------------------------------- endgame

    private static void EndGameChecks()
    {
        var a = Fixture();
        for (var col = 0; col < 4; col++)
        {
            a.Seats[0].Wall[0][col] = col; // four different colors; last cell open
        }

        // Completing the row scores the placement (horizontal run of 5) then ends.
        var s = PlayLastDraft(LastDraftFixture(a, 0, 4, []), 0, 4);
        var b = Read(s);
        Check("game ends on completed row", s.IsOver);
        Check("end bonus applied (2/row + placement 5)", b.Seats[0].Score == 7);
        Check("winner declared", s.Winner?.UserId == Guids[0]);
        Check("after over every action fails", Err(Act(s, 0, AzulActionType.TakeFirstPlayer, new { })) == "azul.gameOver");

        // Tie on score and rows -> shared victory, Winner null.
        var t = Fixture();
        for (var col = 0; col < 5; col++)
        {
            t.Seats[0].Wall[0][col] = col;
            t.Seats[1].Wall[0][col] = col;
        }

        t.Seats[0].Score = 5;
        t.Seats[1].Score = 5;
        t.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-1);
        var tr = Act(Wrap(t), 0, AzulActionType.GameTimeExpired, new { });
        Check("tied finish is shared", tr.IsValid && tr.GameEnded && tr.NewState!.IsOver && tr.NewState.Winner is null);

        // Column/color bonuses count in the ladder.
        var m = Fixture();
        for (var row = 0; row < 5; row++)
        {
            m.Seats[0].Wall[row][0] = row; // full column 0; no full row
        }

        m.GameEndsAtUtc = DateTime.UtcNow.AddSeconds(-1);
        var mr = Act(Wrap(m), 0, AzulActionType.GameTimeExpired, new { });
        Check("vertical column bonus applied (7)", Read(mr.NewState!).Seats[0].Score == 7 && mr.IsValid);
    }

    // ------------------------------------------------------------ validactions

    private static void ValidActionsChecks()
    {
        var a = Fixture();
        a.Factories[0].AddRange(new[] { 0, 1 });
        a.Center.Add(2);
        var s = Wrap(a);
        var actions = Game.GetValidActions(s, Pid(0));

        var allAccepted = true;
        foreach (var act in actions)
        {
            var res = Game.ProcessAction(Wrap(Read(s)), act);
            if (!res.IsValid)
            {
                allAccepted = false;
                break;
            }
        }

        Check("all enumerated actions accepted as-is", allAccepted);
        Check("TakeFirstPlayer offered when marker free", actions.Any(x => x.ActionType == nameof(AzulActionType.TakeFirstPlayer)));
        Check("valid actions empty for non-current seat", Game.GetValidActions(s, Pid(1)).Count == 0);
        Check("valid actions non-empty for current", actions.Count > 0);
        Check("forced-floor sentinel offered when all lines conflict", ForcedSentinelOffered());
    }

    private static bool ForcedSentinelOffered()
    {
        var a = Fixture();
        a.Center.Add(0);
        for (var l = 0; l < 5; l++)
        {
            a.Seats[0].PatternLines[l].Add(1);
        }

        var acts = Game.GetValidActions(Wrap(a), Pid(0));
        return acts.Any(x =>
        {
            if (x.ActionType != nameof(AzulActionType.DraftFromCenter)) return false;
            var p = JsonSerializer.Deserialize<DraftFromCenterPayload>(x.Payload);
            return p?.LineIndex == AzulFloorSentinel.Value;
        });
    }

    // ------------------------------------------------------------ conservation

    private static void ConservationChecks()
    {
        var state = Game.CreateGame(new GameOptions
        {
            GameType = "Azul",
            Players = Enumerable.Range(0, 4).Select(Pid).ToList(),
            Settings = "{\"Seed\":3}"
        });
        Check("conservation at setup", Count(Read(state)) == AzulCatalog.TotalTiles);
    }

    // ------------------------------------------------------------ persistence

    private static void PersistenceChecks()
    {
        var a = Fixture();
        a.Factories[0].Add(0);
        a.Factories[1].Add(1); // keep the pool alive after the draft
        a.Bag.AddRange(Enumerable.Repeat(2, 20));
        var s = Act(Wrap(a), 0, AzulActionType.DraftFromFactory,
            new DraftFromFactoryPayload { FactoryIndex = 0, Color = "Blue", LineIndex = 2 }).NewState!;
        var fresh = Read(s);
        var roundTrip = GameState.FromJson(s.ToJson());
        var b = Read(roundTrip);
        Check("bag order survives round-trip", b.Bag.SequenceEqual(fresh.Bag));
        Check("center survives round-trip", b.Center.SequenceEqual(fresh.Center));
        Check("pattern lines survive round-trip", JsonSerializer.Serialize(b.Seats[0].PatternLines) == JsonSerializer.Serialize(fresh.Seats[0].PatternLines));

        // TryGetString JsonElement branch: hand the state over as a materialized JSON string element.
        var s2 = GameState.FromJson(s.ToJson());
        s2.Data["AzulState"] = JsonDocument.Parse($"\"{JsonSerializer.Serialize(s2.Data["AzulState"] as string).Trim('"')}\"").RootElement;
        var cur = Read(s2).CurrentPlayerIndex;
        Check("TryGetString handles JsonElement payloads", Game.GetValidActions(s2, Pid(cur)).Count >= 0 && Game.GetPlayerView(s2, Pid(cur)).Data.ContainsKey("AzulState"));
    }

    // -------------------------------------------------------------- hiddeninfo

    private static void HiddenInfoChecks()
    {
        var a = Fixture();
        a.Bag.AddRange(new[] { 0, 1, 2, 3, 4 });
        a.Factories[0].Add(0);
        var view = Game.GetPlayerView(Wrap(a), Pid(0));
        view.TryGetString("AzulState", out var json);
        Check("projection hides bag order", json is not null && !json.Contains("\"Bag\":["));
        Check("projection keeps bag count", json is not null && json.Contains("\"BagCount\""));
        var v = JsonSerializer.Deserialize<AzulView>(json!)!;
        Check("projection BagCount correct", v.BagCount == a.Bag.Count);
        Check("projection viewer flags", v.ViewerSeat == 0 && v.ViewerIsCurrentPlayer);
        Check("authoritative state still has bag", a.ToJson().Contains("\"Bag\":["));
    }

    // ------------------------------------------------------------------ timer

    private static void TimerChecks()
    {
        var a = Fixture();
        a.NextActionDeadlineUtc = DateTime.UtcNow.AddMinutes(5);
        Check("timeout before deadline rejected", Err(Act(Wrap(a), 0, AzulActionType.TurnTimeout, new { })) == "azul.timerNotExpired");

        var b = Fixture();
        b.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        b.TimerConfig = new AzulTurnTimerConfig();
        var r = Act(Wrap(b), 0, AzulActionType.TurnTimeout, new { });
        Check("timeout skips to next seat", r.IsValid && Read(r.NewState!).CurrentPlayerIndex == 1);

        // 3 consecutive timeouts eliminate; in a 2p game the survivor wins.
        var cs = Wrap(Fixture());
        GameResult? last = null;
        for (var i = 0; i < 4; i++)
        {
            var cc = Read(cs);
            cc.CurrentPlayerIndex = 0;
            cc.MarkerSeat = -1;
            cc.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
            cc.TimerConfig = new AzulTurnTimerConfig { MaxAfkTurns = 3 };
            last = Game.ProcessAction(Wrap(cc), new GameAction { PlayerId = Pid(0), ActionType = "TurnTimeout", Payload = "{}" });
            cs = last.NewState!;
            if (last.GameEnded)
            {
                break;
            }
        }

        Check("2p AFK eliminates to survivor win", last is { IsValid: true, GameEnded: true } && last.NewState!.IsOver && last.NewState.Winner?.UserId == Guids[1]);

        // 3p+ keeps playing after an elimination.
        var three = Fixture(3);
        three.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
        three.TimerConfig = new AzulTurnTimerConfig { MaxAfkTurns = 3 };
        var ts = Wrap(three);
        for (var i = 0; i < 3; i++)
        {
            var cc = Read(ts);
            cc.CurrentPlayerIndex = 0;
            cc.NextActionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);
            cc.TimerConfig = three.TimerConfig;
            ts = Game.ProcessAction(Wrap(cc), new GameAction { PlayerId = Pid(0), ActionType = "TurnTimeout", Payload = "{}" }).NewState!;
        }

        var tb = Read(ts);
        Check("3p AFK removes seat and continues", !ts.IsOver && tb.EliminatedSeats.Contains(0) && tb.CurrentPlayerIndex != 0);

        var e = Fixture();
        e.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(30);
        Check("time expiry before limit rejected", Err(Act(Wrap(e), 0, AzulActionType.GameTimeExpired, new { })) == "azul.timeNotUp");
    }

    // ------------------------------------------------------------ invariantsoak

    private static void InvariantSoakChecks()
    {
        var state = Game.CreateGame(new GameOptions
        {
            GameType = "Azul",
            Players = Enumerable.Range(0, 4).Select(Pid).ToList(),
            Settings = "{\"Seed\":11}"
        });

        var rng = new Random(20);
        var turns = 0;
        var ok = true;
        while (!state.IsOver && turns < 400)
        {
            var a = Read(state);
            var actions = Game.GetValidActions(state, Pid(a.CurrentPlayerIndex));
            if (actions.Count == 0)
            {
                break;
            }

            var res = Game.ProcessAction(state, actions[rng.Next(actions.Count)]);
            if (!res.IsValid || res.NewState is null)
            {
                ok = false;
                break;
            }

            state = res.NewState;
            var b = Read(state);
            if (Count(b) != AzulCatalog.TotalTiles)
            {
                ok = false;
                break;
            }

            foreach (var s in b.Seats)
            {
                if (s.Floor.Count > AzulCatalog.FloorCapacity || s.Score < 0)
                {
                    ok = false;
                }

                for (var l = 0; l < 5 && ok; l++)
                {
                    var line = s.PatternLines[l];
                    ok = line.Count <= AzulSeatState.LineCapacity(l) && (line.Count <= 1 || line.Distinct().Count() == 1);
                }

                foreach (var row in s.Wall)
                {
                    var filled = row.Where(c => c >= 0).ToList();
                    if (filled.Distinct().Count() != filled.Count)
                    {
                        ok = false; // I-5: a wall row duplicated a color
                    }
                }
            }

            if (!ok)
            {
                break;
            }

            turns++;
        }

        Check($"invariant soak clean ({turns} turns, over={state.IsOver})", ok);
        Check("seeded game reproducible", Signature(11) == Signature(11));
        Check("different seeds diverge", Signature(11) != Signature(12));
    }

    private static string Signature(int seed)
    {
        var state = Game.CreateGame(new GameOptions
        {
            GameType = "Azul",
            Players = Enumerable.Range(0, 3).Select(Pid).ToList(),
            Settings = $"{{\"Seed\":{seed}}}"
        });

        var rng = new Random(99);
        var guard = 0;
        while (!state.IsOver && guard++ < 300)
        {
            var a = Read(state);
            var actions = Game.GetValidActions(state, Pid(a.CurrentPlayerIndex));
            if (actions.Count == 0)
            {
                break;
            }

            var res = Game.ProcessAction(state, actions[rng.Next(actions.Count)]);
            if (!res.IsValid || res.NewState is null)
            {
                return "invalid";
            }

            state = res.NewState;
        }

        var f = Read(state);
        return JsonSerializer.Serialize(new
        {
            f.RoundNumber,
            Scores = f.Seats.Select(s => s.Score).ToList(),
            Wall = f.Seats.Select(s => s.Wall.Sum(r => r.Count(c => c >= 0))).ToList()
        });
    }
}
