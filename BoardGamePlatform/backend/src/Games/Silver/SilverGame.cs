using System.Text.Json;
using GameEngine.Core;
using GameEngine.Core.Models;
using Silver.Models;

namespace Silver;

/// <summary>
/// Silver (Bézier Games, base game) implementation of the platform Game Engine
/// contract. Contains only Silver rules: no HTTP, SignalR, EF Core, or database
/// dependencies. Registered in DI as an <see cref="IGame"/>; also implements
/// <see cref="IPlayerViewGame"/> so the Game Service can project per-viewer
/// states instead of leaking hidden cards.
/// </summary>
public sealed partial class SilverGame : IGame, IPlayerViewGame
{
    /// <summary>The game type identifier used across the platform to select this game.</summary>
    public string GameType => "Silver";

    /// <inheritdoc />
    public int MinPlayers => 2;

    /// <inheritdoc />
    public int MaxPlayers => 4;

    private const int CardsPerVillage = 5;
    private const int TotalRounds = 4;

    /// <inheritdoc />
    public GameState CreateGame(GameOptions options)
    {
        if (options.Players.Count < MinPlayers || options.Players.Count > MaxPlayers)
        {
            throw new ArgumentException("Silver requires 2-4 players");
        }

        var playerNames = new Dictionary<Guid, string>();
        var timerConfig = new SilverTurnTimerConfig();
        int? seed = null;
        if (!string.IsNullOrWhiteSpace(options.Settings))
        {
            try
            {
                using var doc = JsonDocument.Parse(options.Settings);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("PlayerNames", out var namesElement)
                        && namesElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in namesElement.EnumerateObject())
                        {
                            if (Guid.TryParse(prop.Name, out var userId)
                                && prop.Value.ValueKind == JsonValueKind.String)
                            {
                                playerNames[userId] = prop.Value.GetString()!;
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("Seed", out var seedElement)
                        && seedElement.ValueKind == JsonValueKind.Number)
                    {
                        seed = seedElement.GetInt32();
                    }

                    if (doc.RootElement.TryGetProperty("Timer", out var timerElement)
                        && timerElement.ValueKind == JsonValueKind.Object)
                    {
                        if (timerElement.TryGetProperty("BaseTurnSeconds", out var b)) timerConfig.BaseTurnSeconds = b.GetDouble();
                        if (timerElement.TryGetProperty("MaxBankSeconds", out var m)) timerConfig.MaxBankSeconds = m.GetDouble();
                        if (timerElement.TryGetProperty("MaxOverrunSeconds", out var o)) timerConfig.MaxOverrunSeconds = o.GetDouble();
                        if (timerElement.TryGetProperty("MaxAfkTurns", out var a)) timerConfig.MaxAfkTurns = a.GetInt32();
                        if (timerElement.TryGetProperty("TotalGameTimeMinutes", out var t)) timerConfig.TotalGameTimeMinutes = t.GetDouble();
                    }
                }
            }
            catch
            {
                // Ignore parsing errors, fall back to empty names and defaults.
            }
        }

        var count = options.Players.Count;
        var silver = new SilverState
        {
            Round = 1,
            Villages = Enumerable.Range(0, count).Select(_ => new List<SilverCard>()).ToList(),
            CumulativeScores = Enumerable.Repeat(0, count).ToList(),
            Knowledge = Enumerable.Range(0, count).Select(_ => new Dictionary<Guid, int>()).ToList(),
            PeeksUsed = Enumerable.Repeat(0, count).ToList(),
            Seed = seed,
            TimerConfig = timerConfig,
            PlayerNames = playerNames,
            PlayerIds = options.Players,
            // The full deck; StartRound collects, shuffles, and deals it.
            Deck = BuildDeck()
        };

        StartRound(silver, randomStartPlayer: true);

        if (timerConfig.TotalGameTimeMinutes > 0)
        {
            silver.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(timerConfig.TotalGameTimeMinutes);
        }

        return WrapState(silver, sessionId: Guid.NewGuid(), version: 1, isOver: false, winner: null);
    }

    /// <inheritdoc />
    public GameResult ProcessAction(GameState state, GameAction action)
    {
        if (state.IsOver)
        {
            return Fail(state, Errors.GameOver);
        }

        if (!state.TryGetString("SilverState", out var json) || json is null)
        {
            return Fail(state, Errors.InvalidState);
        }

        var silver = SilverState.FromJson(json);
        Rehydrate(silver, state);

        var seat = IndexOf(state, action.PlayerId);
        if (seat < 0 || seat >= silver.Villages.Count)
        {
            return Fail(state, Errors.InvalidPlayer);
        }

        if (silver.EliminatedPlayerIndexes.Contains(seat))
        {
            return Fail(state, Errors.PlayerEliminated);
        }

        if (!Enum.TryParse<SilverActionType>(action.ActionType, ignoreCase: true, out var type))
        {
            return Fail(state, Errors.UnknownActionType, new object?[] { action.ActionType });
        }

        // Peeks and the Revealer's opponent-choice are the only actions
        // available outside the actor's turn.
        SilverActionResult result;
        if (type == SilverActionType.PeekVillageCard)
        {
            result = ProcessPeekVillageCard(silver, seat, action.PayloadAs<PeekVillageCardPayload>());
        }
        else if (type == SilverActionType.ChooseRevealCard)
        {
            if (silver.RevealerChooserSeat != seat)
            {
                return Fail(state, Errors.NotYourTurn);
            }
            result = ProcessChooseRevealCard(silver, seat, action.PayloadAs<ChooseRevealCardPayload>());
        }
        else
        {
            if (seat != silver.CurrentPlayerIndex)
            {
                return Fail(state, Errors.NotYourTurn);
            }

            result = type switch
            {
                SilverActionType.DrawFromDeck => ProcessDrawFromDeck(silver, action.PayloadAs<DrawFromDeckPayload>()),
                SilverActionType.TakeDiscard => ProcessTakeDiscard(silver),
                SilverActionType.TakeSquireCard => ProcessTakeSquireCard(silver, action.PayloadAs<TakeSquireCardPayload>()),
                SilverActionType.ChooseDrawnCard => ProcessChooseDrawnCard(silver, action.PayloadAs<ChooseDrawnCardPayload>()),
                SilverActionType.DiscardDrawnCard => ProcessDiscardDrawnCard(silver),
                SilverActionType.ExchangeWithDrawn => ProcessExchange(silver, fromDeck: true, action.PayloadAs<ExchangePayload>()),
                SilverActionType.ExchangeWithDiscard => ProcessExchange(silver, fromDeck: false, action.PayloadAs<ExchangePayload>()),
                SilverActionType.UseAbility => ProcessUseAbility(silver, seat, action.PayloadAs<UseAbilityPayload>()),
                SilverActionType.SkipAbility => ProcessSkipAbility(silver),
                SilverActionType.MoveGuard => ProcessMoveGuard(silver, seat, action.PayloadAs<MoveGuardPayload>()),
                SilverActionType.RemoveGuard => ProcessRemoveGuard(silver, seat, action.PayloadAs<RemoveGuardPayload>()),
                SilverActionType.CallCensus => ProcessCallCensus(silver, seat),
                SilverActionType.PlaceAmulet => ProcessPlaceAmulet(silver, seat, action.PayloadAs<PlaceAmuletPayload>()),
                SilverActionType.TurnTimeout => ProcessTurnTimeout(silver, seat),
                SilverActionType.GameTimeExpired => ProcessGameTimeExpired(silver),
                _ => FailResult(Errors.UnknownActionType, new object?[] { action.ActionType })
            };
        }

        if (!result.IsValid)
        {
            return Fail(state, result.Error!, result.ErrorArgs);
        }

        // Census countdown: every completed turn of a non-caller consumes one of
        // the final turns; when the last one is taken the round ends. The turn
        // that ended belongs to the player whose turn it is (the actor) - not
        // necessarily the seat that submitted the action: a Revealer prompt can
        // be answered by the census caller themselves, and answering it ends the
        // ACTOR's (final) turn. Timeout actions already advanced the turn, so
        // for those the acting seat is the turn owner.
        var turnOwner = result.PlayerAdvanced ? seat : silver.CurrentPlayerIndex;
        if (result.TurnEnded && !result.GameEnded && !result.RoundEnded
            && silver.CensusCallerIndex is { } caller && turnOwner != caller)
        {
            silver.RemainingCensusTurns--;
            if (silver.RemainingCensusTurns <= 0)
            {
                result.RoundEnded = true;
            }
        }

        // A completed turn passes play clockwise and reopens the turn-start phase.
        if (result.TurnEnded && !result.RoundEnded && !result.GameEnded && !result.PlayerAdvanced)
        {
            silver.Phase = TurnPhase.TurnStart;
            AdvancePlayer(silver);
        }

        // Face-up Squires reveal one deck card per Squire after every turn
        // (refilling the display area to the current Squire count).
        if (result.TurnEnded && !result.RoundEnded && !result.GameEnded)
        {
            RefillSquireDisplay(silver, result.Events);
        }

        // Round-end checks after a resolved rule action: two face-up Villagers
        // in one village, or a depleted draw deck.
        if (!result.RoundEnded && result.CheckRoundEnd)
        {
            if (HasDoubleFaceUpVillager(silver) || silver.Deck.Count == 0)
            {
                result.RoundEnded = true;
            }
        }

        if (result.RoundEnded)
        {
            ScoreRound(silver, result);
            if (silver.Round >= TotalRounds)
            {
                result.GameEnded = true;
                result.WinnerIndex = DetermineWinner(silver);
                result.Events.Add(SilverEvent.Build("silver.gameFinished", new
                {
                    winner = result.WinnerIndex is { } w ? NameOf(silver, w) : null
                }));
            }
            else
            {
                silver.Round++;
                // Feed the event through the action's event list so the log
                // keeps true chronological order (roundStarted lands after the
                // scoring events of the round it follows).
                StartRound(silver, randomStartPlayer: false, events: result.Events);
            }
        }

        if (result.GameEnded)
        {
            silver.NextActionDeadlineUtc = null;
            silver.GameEndsAtUtc = null;
        }
        else if (result.IsTurnAction)
        {
            // The accounting charges the actor; the fresh clock set for a new
            // round is applied by StartRound, so suppress the redundant one.
            silver.ApplyTurnTimeAccounting(seat, result.TurnEnded && !result.RoundEnded);
        }

        silver.CleanupGuards();
        silver.EventLog.AddRange(result.Events);

        var winner = result.WinnerIndex is { } winnerIndex ? state.Players[winnerIndex] : (PlayerId?)null;
        var newState = WrapState(silver, state.SessionId, state.Version + 1, result.GameEnded, winner);
        return new GameResult
        {
            IsValid = true,
            NewState = newState,
            Events = result.Events.Select(e => new GameEvent { Type = "SilverEvent", Payload = e }).ToList(),
            GameEnded = result.GameEnded
        };
    }

    /// <inheritdoc />
    public bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public PlayerId? GetWinner(GameState state) => state.Winner;

    // ----- shared helpers -----

    private static GameState WrapState(SilverState silver, Guid sessionId, long version, bool isOver, PlayerId? winner)
    {
        return new GameState
        {
            SessionId = sessionId,
            GameType = "Silver",
            Players = silver.PlayerIds ?? new List<PlayerId>(),
            CurrentPlayerIndex = silver.CurrentPlayerIndex,
            IsOver = isOver,
            Winner = winner,
            Version = version,
            NextActionDeadlineUtc = silver.NextActionDeadlineUtc,
            GameEndsAtUtc = silver.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["SilverState"] = silver.ToJson(),
                ["PlayerNames"] = JsonSerializer.Serialize(silver.PlayerNames ?? new Dictionary<Guid, string>())
            }
        };
    }

    private static GameResult Fail(GameState state, string code, object?[]? args = null)
    {
        return new GameResult
        {
            IsValid = false,
            NewState = state,
            Error = code,
            ErrorCode = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static SilverActionResult FailResult(string code, object?[]? args = null)
    {
        return new SilverActionResult
        {
            IsValid = false,
            Error = code,
            ErrorCode = code,
            ErrorArgs = args ?? Array.Empty<object?>()
        };
    }

    private static SilverActionResult Ok()
    {
        return new SilverActionResult { IsValid = true };
    }

    private static void Rehydrate(SilverState silver, GameState state)
    {
        silver.PlayerIds = state.Players;
        if (state.TryGetString("PlayerNames", out var namesJson) && namesJson is not null)
        {
            try
            {
                silver.PlayerNames = JsonSerializer.Deserialize<Dictionary<Guid, string>>(namesJson);
            }
            catch
            {
                // Ignore parsing errors, fall back to no names.
            }
        }
    }

    private static int IndexOf(GameState state, PlayerId playerId)
    {
        for (var i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].UserId == playerId.UserId) return i;
        }
        return -1;
    }

    private static int PlayerCount(SilverState silver) => silver.Villages.Count;

    private static string NameOf(SilverState silver, int seat)
    {
        if (silver.PlayerIds is { } ids && seat >= 0 && seat < ids.Count
            && silver.PlayerNames is { } names && names.TryGetValue(ids[seat].UserId, out var name))
        {
            return name;
        }
        return $"Player {seat + 1}";
    }

    private static GameAction CreateAction(SilverActionType type, object payload)
    {
        return new GameAction
        {
            ActionType = type.ToString(),
            Payload = JsonSerializer.Serialize(payload)
        };
    }

    private static bool HasDoubleFaceUpVillager(SilverState silver)
    {
        return silver.Villages.Any(v => v.Count(c => c.FaceUp && c.Value == 0) >= 2);
    }

    private static bool HasFaceUp(SilverState silver, int seat, int value)
    {
        return silver.Villages[seat].Any(c => c.FaceUp && c.Value == value);
    }

    private static bool HasFaceUpAnywhere(SilverState silver, int value)
    {
        return silver.Villages.Any(v => v.Any(c => c.FaceUp && c.Value == value));
    }

    /// <summary>
    /// Whether the seat has any card the owner may legally exchange away: the
    /// Guard protects against outsiders, but the owner may still burn/move the
    /// guarded card; only the Silver Amulet also binds the owner.
    /// </summary>
    private static bool HasOwnerExchangeable(SilverState silver, int seat)
    {
        return silver.Villages[seat].Any(c => !silver.IsAmuletProtected(c));
    }

    /// <summary>Whether the seat has a face-down card its owner may look at (Guard allowed, Amulet not).</summary>
    private static bool HasOwnViewable(SilverState silver, int seat)
    {
        return silver.Villages[seat].Any(c => !c.FaceUp && !silver.IsAmuletProtected(c));
    }

    private static List<int> OwnViewableSlots(SilverState silver, int seat)
    {
        var village = silver.Villages[seat];
        var slots = new List<int>();
        for (var i = 0; i < village.Count; i++)
        {
            if (!village[i].FaceUp && !silver.IsAmuletProtected(village[i])) slots.Add(i);
        }
        return slots;
    }

    /// <summary>Face-down slots in any village that are hidden from everyone (not Guard/Amulet covered).</summary>
    private static List<int> HiddenViewableSlots(SilverState silver, int targetSeat)
    {
        var village = silver.Villages[targetSeat];
        var slots = new List<int>();
        for (var i = 0; i < village.Count; i++)
        {
            if (!village[i].FaceUp && !silver.IsProtected(village[i])) slots.Add(i);
        }
        return slots;
    }

    private static void AddKnowledge(SilverState silver, int seat, SilverCard card)
    {
        if (silver.Knowledge.Count <= seat)
        {
            while (silver.Knowledge.Count <= seat) silver.Knowledge.Add(new Dictionary<Guid, int>());
        }
        silver.Knowledge[seat][card.Id] = card.Value;
    }

    private static bool SetMatches(IEnumerable<SilverCard> set)
    {
        // The matching set: all non-Doppelgänger values equal; at most ONE
        // Doppelgänger acts as the wildcard (two Doppelgängers match each
        // other, and nothing else).
        var cards = set.ToList();
        var wilds = cards.Count(c => c.Value == 13);
        var others = cards.Where(c => c.Value != 13).Select(c => c.Value).Distinct().ToList();
        if (others.Count > 1) return false;
        return wilds switch
        {
            0 or 1 => true,
            2 => others.Count == 0,
            _ => false
        };
    }

    /// <summary>
    /// Counts how many times the given ability name appears in this turn's use
    /// list (per-instance uses are recorded as "Ability:{cardId}").
    /// </summary>
    private static int AbilityUsesThisTurn(SilverState silver, string ability)
    {
        return silver.AbilitiesUsedThisTurn.Count(e => e == ability || e.StartsWith(ability + ":", StringComparison.Ordinal));
    }

    private static void MarkAbilityUsed(SilverState silver, string entry)
    {
        silver.AbilitiesUsedThisTurn.Add(entry);
    }

    /// <summary>
    /// The seat among <paramref name="candidates"/> seated closest clockwise
    /// after <paramref name="afterSeat"/> (the rulebook's "closest to the left").
    /// </summary>
    private static int FirstClockwiseFrom(List<int> candidates, int afterSeat, int playerCount)
    {
        for (var step = 1; step <= playerCount; step++)
        {
            var seat = (afterSeat + step) % playerCount;
            if (candidates.Contains(seat)) return seat;
        }
        return candidates[0];
    }

    // ----- rounds and scoring -----

    private static List<SilverCard> BuildDeck()
    {
        var deck = new List<SilverCard>(52);
        deck.Add(new SilverCard(0));
        deck.Add(new SilverCard(0));
        for (var value = 1; value <= 12; value++)
        {
            for (var copy = 0; copy < 4; copy++)
            {
                deck.Add(new SilverCard(value));
            }
        }
        deck.Add(new SilverCard(13));
        deck.Add(new SilverCard(13));
        return deck;
    }

    private void StartRound(SilverState silver, bool randomStartPlayer, List<string>? events = null)
    {
        var count = PlayerCount(silver);

        var all = CollectAllCards(silver);
        foreach (var card in all)
        {
            card.FaceUp = false;
        }
        Shuffle(all, CreateRng(silver, stream: 1));

        silver.Villages = Enumerable.Range(0, count).Select(_ => new List<SilverCard>()).ToList();
        var next = 0;
        for (var i = 0; i < CardsPerVillage; i++)
        {
            for (var seat = 0; seat < count; seat++)
            {
                silver.Villages[seat].Add(all[next++]);
            }
        }

        // Player-count removal: exactly 5*(4 - players) cards leave the game
        // for the round, so the deck + discard always contain 32 cards.
        var removedCount = 5 * (4 - count);
        silver.Removed = all.Skip(next).Take(removedCount).ToList();
        next += removedCount;

        var discardCard = all[next++];
        discardCard.FaceUp = true;
        silver.Discard = new List<SilverCard> { discardCard };
        silver.Deck = all.Skip(next).ToList();
        silver.Display = new List<SilverCard>();
        silver.PendingDraw = new List<SilverCard>();
        silver.PendingSource = DrawSource.Deck;
        silver.Phase = TurnPhase.TurnStart;
        silver.DiscardedAbilityCardId = null;
        silver.WitchPeekedCardId = null;
        silver.RevealerChooserSeat = null;
        silver.Guards = new Dictionary<Guid, Guid>();
        silver.CensusCallerIndex = null;
        silver.RemainingCensusTurns = 0;
        silver.AmuletPlacedCardId = null;
        silver.Knowledge = Enumerable.Range(0, count).Select(_ => new Dictionary<Guid, int>()).ToList();
        silver.PeeksUsed = Enumerable.Repeat(0, count).ToList();
        silver.AbilitiesUsedThisTurn = new List<string>();
        silver.ActedThisTurn = false;

        int start;
        if (randomStartPlayer || silver.AmuletHolderIndex is not { } holder
            || silver.EliminatedPlayerIndexes.Contains(holder))
        {
            start = CreateRng(silver, stream: 2).Next(count);
        }
        else
        {
            start = holder;
        }

        // Skip seats eliminated in earlier rounds.
        var guard = 0;
        while (silver.EliminatedPlayerIndexes.Contains(start) && guard <= count)
        {
            start = (start + 1) % count;
            guard++;
        }

        // The Amulet is initially assigned to the starting player.
        if (silver.Round == 1)
        {
            silver.AmuletHolderIndex = start;
        }

        silver.RoundStartPlayerIndex = start;
        silver.CurrentPlayerIndex = start;
        silver.SetTurnClock();

        var roundStartedEvent = SilverEvent.Build("silver.roundStarted", new { round = silver.Round });
        if (events is not null) events.Add(roundStartedEvent);
        else silver.EventLog.Add(roundStartedEvent);
    }

    private static List<SilverCard> CollectAllCards(SilverState silver)
    {
        var all = new List<SilverCard>(52);
        foreach (var village in silver.Villages) all.AddRange(village);
        all.AddRange(silver.Deck);
        all.AddRange(silver.Discard);
        all.AddRange(silver.Display);
        all.AddRange(silver.PendingDraw);
        all.AddRange(silver.Removed);
        return all;
    }

    /// <summary>
    /// Seeded when the session settings provided one (deterministic testing),
    /// otherwise fresh per shuffle so concurrent sessions stay independent.
    /// Nothing random is ever serialized.
    /// </summary>
    private static Random CreateRng(SilverState silver, int stream)
    {
        return silver.Seed is { } seed
            ? new Random(unchecked(seed * 397 + silver.Round * 31 + stream * 7))
            : new Random();
    }

    private static void Shuffle(List<SilverCard> cards, Random rng)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>
    /// After every player's turn, each face-up Squire (in any village) reveals
    /// one more deck card into the display area; cards already revealed stay
    /// put even if the Squire count later shrinks. Reveals stop at the deck
    /// size when it cannot supply everything.
    /// </summary>
    private static void RefillSquireDisplay(SilverState silver, List<string> events)
    {
        var squires = silver.FaceUpInVillages(1);
        var revealed = 0;
        while (silver.Display.Count < squires && silver.Deck.Count > 0)
        {
            var card = silver.DrawTop()!;
            card.FaceUp = true;
            silver.Display.Add(card);
            revealed++;
        }
        if (revealed > 0)
        {
            events.Add(SilverEvent.Build("silver.squireRevealed", new { count = revealed }));
        }
    }

    private void ScoreRound(SilverState silver, SilverActionResult result)
    {
        var count = PlayerCount(silver);
        var sums = silver.Villages.Select(VillageScore).ToList();
        var scores = new int[count];
        var minSum = sums.Min();
        for (var i = 0; i < count; i++)
        {
            scores[i] = i == silver.CensusCallerIndex
                ? (sums[i] == minSum ? 0 : sums[i] + 10)
                : sums[i];
        }

        for (var i = 0; i < count; i++)
        {
            silver.CumulativeScores[i] += scores[i];
        }

        silver.LastRoundScores = scores.ToList();
        silver.LastRoundCensusCallerIndex = silver.CensusCallerIndex;

        // Silver Amulet: the round's lowest scorer holds it next round; ties
        // keep the current holder, otherwise the tied seat closest clockwise
        // after this round's start player. Only a successful caller may place it.
        var minScore = scores.Min();
        var tied = Enumerable.Range(0, count)
            .Where(i => scores[i] == minScore && !silver.EliminatedPlayerIndexes.Contains(i))
            .ToList();
        if (tied.Count == 0)
        {
            tied = Enumerable.Range(0, count).Where(i => scores[i] == minScore).ToList();
        }
        int? newHolder;
        if (silver.AmuletHolderIndex is { } current && !silver.EliminatedPlayerIndexes.Contains(current)
            && tied.Contains(current))
        {
            newHolder = current;
        }
        else
        {
            newHolder = FirstClockwiseFrom(tied, silver.RoundStartPlayerIndex, count);
        }
        silver.AmuletHolderIndex = newHolder;
        silver.AmuletPlaceable = silver.CensusCallerIndex is { } called
            && called == newHolder
            && sums[called] == minSum;

        result.Events.Add(SilverEvent.Build("silver.roundScored", new
        {
            scores = scores,
            caller = silver.CensusCallerIndex is { } c ? NameOf(silver, c) : null
        }));
        result.Events.Add(SilverEvent.Build("silver.roundEnded", new { round = silver.Round }));
    }

    /// <summary>
    /// A village's round score: the sum of all card values (face up and face
    /// down), except a village holding exactly two Doppelgängers scores 13
    /// instead of their combined 26.
    /// </summary>
    private static int VillageScore(List<SilverCard> village)
    {
        if (village.Count(c => c.Value == 13) == 2) return 13;
        return village.Sum(c => c.Value);
    }

    private int? DetermineWinner(SilverState silver)
    {
        var count = PlayerCount(silver);
        var contenders = Enumerable.Range(0, count)
            .Where(i => !silver.EliminatedPlayerIndexes.Contains(i))
            .ToList();
        if (contenders.Count == 0) return null;
        var min = contenders.Min(i => silver.CumulativeScores[i]);
        var tied = contenders.Where(i => silver.CumulativeScores[i] == min).ToList();
        if (tied.Count == 1) return tied[0];

        if (silver.AmuletHolderIndex is { } holder && !silver.EliminatedPlayerIndexes.Contains(holder))
        {
            if (tied.Contains(holder)) return holder;
            return FirstClockwiseFrom(tied, holder, count);
        }

        // No amulet holder (force-finished before any round was scored):
        // an unresolvable tie is a draw.
        return null;
    }

    private static void AdvancePlayer(SilverState silver)
    {
        silver.AbilitiesUsedThisTurn = new List<string>();
        silver.ActedThisTurn = false;

        var count = PlayerCount(silver);
        var guard = 0;
        do
        {
            silver.CurrentPlayerIndex = (silver.CurrentPlayerIndex + 1) % count;
            guard++;
        }
        while (silver.EliminatedPlayerIndexes.Contains(silver.CurrentPlayerIndex) && guard <= count);
    }
}
