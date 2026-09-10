using GameEngine.Core;
using GameEngine.Core.Models;
using UNO.Models;

namespace UNO;

/// <summary>
/// UNO implementation of the platform Game Engine contract.
/// Contains only UNO rules: no HTTP, SignalR, EF Core, or database dependencies.
/// </summary>
public class UNOGame : IGame
{
    /// <summary>
    /// The game type identifier used across the platform to select this game.
    /// </summary>
    public string GameType => "UNO";

    /// <inheritdoc />
    public int MinPlayers => 2;

    /// <inheritdoc />
    public int MaxPlayers => 8;

    /// <inheritdoc />
    public GameState CreateGame(GameOptions options)
    {
        if (options.Players.Count < 2 || options.Players.Count > 10)
        {
            throw new ArgumentException("UNO requires 2-10 players");
        }

        // Parse player names and turn timer config from settings
        var playerNames = new Dictionary<Guid, string>();
        var timerConfig = new UnoTurnTimerConfig();
        if (!string.IsNullOrWhiteSpace(options.Settings))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(options.Settings);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("PlayerNames", out var namesElement)
                        && namesElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var prop in namesElement.EnumerateObject())
                        {
                            if (Guid.TryParse(prop.Name, out var userId)
                                && prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                playerNames[userId] = prop.Value.GetString()!;
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("Timer", out var timerElement)
                        && timerElement.ValueKind == System.Text.Json.JsonValueKind.Object)
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
                // Ignore parsing errors, fall back to empty names and defaults
            }
        }

        // Create and shuffle deck
        var deck = Deck.CreateStandard();

        // Deal 7 cards to each player
        var playerHands = new List<PlayerHand>();
        for (int i = 0; i < options.Players.Count; i++)
        {
            var hand = new PlayerHand();
            hand.AddRange(deck.Draw(7));
            playerHands.Add(hand);
        }

        // Set up discard pile - draw until we get a non-wild card
        Card? topCard = null;
        while (topCard is null or { IsWild: true })
        {
            topCard = deck.Draw();
            if (topCard is { IsWild: true })
            {
                // Put wild cards back and reshuffle
                deck.AddToBottom(topCard.Value);
                deck.Shuffle();
                topCard = null;
            }
        }

        var discardPile = new List<Card> { topCard!.Value };

        // Create game state
        var unoState = new UnoGameState
        {
            DrawPile = deck,
            DiscardPile = discardPile,
            PlayerHands = playerHands,
            CurrentColor = null,
            Direction = PlayDirection.Clockwise,
            CurrentPlayerIndex = 0,
            PendingDrawCount = 0,
            NextPlayerSkipped = false,
            UnoCalled = false,
            UnoPendingPlayerIndex = null,
            EventLog = new List<string> { UnoEvent.Build("gameStarted", new { count = options.Players.Count }) },
            PlayerNames = playerNames,
            PlayerIds = options.Players,
            TimerConfig = timerConfig
        };

        // Apply initial card effect if it's an action card
        ApplyInitialCardEffect(unoState, topCard.Value);

        // Start the first player's turn clock and the overall game clock
        unoState.SetTurnClock();
        if (timerConfig.TotalGameTimeMinutes > 0)
        {
            unoState.GameEndsAtUtc = DateTime.UtcNow.AddMinutes(timerConfig.TotalGameTimeMinutes);
        }

        var gameState = new GameState
        {
            SessionId = options.Players.Count > 0 ? Guid.NewGuid() : Guid.Empty,
            GameType = GameType,
            Players = options.Players,
            CurrentPlayerIndex = unoState.CurrentPlayerIndex,
            IsOver = false,
            Version = 1,
            NextActionDeadlineUtc = unoState.NextActionDeadlineUtc,
            GameEndsAtUtc = unoState.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["UnoState"] = unoState.ToJson(),
                ["PlayerNames"] = System.Text.Json.JsonSerializer.Serialize(playerNames)
            }
        };

        return gameState;
    }

    /// <inheritdoc />
    public GameResult ProcessAction(GameState state, GameAction action)
    {
        if (state.IsOver)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = Errors.GameOver, ErrorCode = Errors.GameOver
            };
        }

        // Deserialize UNO state
        if (!state.TryGetString("UnoState", out var unoStateJson) || unoStateJson is null)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = Errors.InvalidState, ErrorCode = Errors.InvalidState
            };
        }

        var unoState = UnoGameState.FromJson(unoStateJson);
        unoState.PlayerIds = state.Players;
        if (state.TryGetString("PlayerNames", out var playerNamesJson) && playerNamesJson is not null)
        {
            try
            {
                unoState.PlayerNames = System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, string>>(playerNamesJson);
            }
            catch
            {
                // Ignore parsing errors, fall back to no names
            }
        }
        var playerIndex = GetPlayerIndex(state, action.PlayerId);

        if (playerIndex < 0 || playerIndex >= unoState.PlayerHands.Count)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = Errors.InvalidPlayer, ErrorCode = Errors.InvalidPlayer
            };
        }

        // Check if it's the player's turn
        if (playerIndex != unoState.CurrentPlayerIndex)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = Errors.NotYourTurn, ErrorCode = Errors.NotYourTurn
            };
        }

        // Parse action type from the action's type field
        if (!Enum.TryParse<UnoActionType>(action.ActionType, ignoreCase: true, out var actionType))
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = Errors.UnknownActionType, ErrorCode = Errors.UnknownActionType,
                ErrorArgs = new object?[] { action.ActionType }
            };
        }

        UnoActionResult result = actionType switch
        {
            UnoActionType.PlayCard => ProcessPlayCard(unoState, playerIndex, action.PayloadAs<PlayCardPayload>()),
            UnoActionType.DrawCard => ProcessDrawCard(unoState, playerIndex, action.PayloadAs<DrawCardPayload>()),
            UnoActionType.CallUno => ProcessCallUno(unoState, playerIndex),
            UnoActionType.ChallengeWildDrawFour => ProcessChallengeWildDrawFour(unoState, playerIndex),
            UnoActionType.AcceptDraw => ProcessAcceptDraw(unoState, playerIndex),
            UnoActionType.Pass => ProcessPass(unoState, playerIndex),
            UnoActionType.TurnTimeout => ProcessTurnTimeout(unoState, playerIndex),
            UnoActionType.GameTimeExpired => ProcessGameTimeExpired(unoState),
            _ => UnoActionResult.Failure(Errors.UnknownActionType, new object?[] { actionType.ToString() })
        };

        if (!result.IsValid)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = result.Error,
                ErrorCode = result.ErrorCode,
                ErrorArgs = result.ErrorArgs
            };
        }

        // Turn timer accounting: bank unused time / charge overruns after genuine
        // turn actions, and clear the deadlines when the game has ended.
        if (result.GameEnded)
        {
            unoState.NextActionDeadlineUtc = null;
            unoState.GameEndsAtUtc = null;
        }
        else if (IsTurnAction(actionType))
        {
            var turnEnded = result.NewState is not null
                && result.NewState.CurrentPlayerIndex != playerIndex;
            unoState.ApplyTurnTimeAccounting(playerIndex, turnEnded);
        }

        // Append events to the persistent log so every client gets the full history
        // through state updates (both action responses and SignalR pushes).
        if (result.NewState is not null)
        {
            result.NewState.EventLog.AddRange(result.Events);
        }

        // Create new game state (GameState is not a record, so create new instance)
        var newState = new GameState
        {
            SessionId = state.SessionId,
            GameType = state.GameType,
            Players = state.Players,
            CurrentPlayerIndex = result.NewState?.CurrentPlayerIndex ?? state.CurrentPlayerIndex,
            IsOver = result.GameEnded,
            Winner = result.WinnerIndex.HasValue ? state.Players[result.WinnerIndex.Value] : null,
            Version = state.Version + 1,
            NextActionDeadlineUtc = unoState.NextActionDeadlineUtc,
            GameEndsAtUtc = unoState.GameEndsAtUtc,
            Data = new Dictionary<string, object?>
            {
                ["UnoState"] = result.NewState!.ToJson(),
                ["PlayerNames"] = state.TryGetString("PlayerNames", out var existingNamesJson)
                    ? existingNamesJson
                    : System.Text.Json.JsonSerializer.Serialize(
                        unoState.PlayerNames ?? new Dictionary<Guid, string>())
            }
        };

        var gameEvents = result.Events.Select(e => new GameEvent
        {
            Type = "UnoEvent",
            PlayerId = new PlayerId(state.Players[playerIndex].UserId),
            Payload = System.Text.Json.JsonSerializer.Serialize(new { Event = e })
        }).ToList();

        return new GameResult
        {
            IsValid = true,
            NewState = newState,
            Events = gameEvents,
            GameEnded = result.GameEnded
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId)
    {
        var actions = new List<GameAction>();

        if (state.IsOver) return actions;

        if (!state.TryGetString("UnoState", out var unoStateJson) || unoStateJson is null)
            return actions;

        var unoState = UnoGameState.FromJson(unoStateJson);
        unoState.PlayerIds = state.Players;
        var playerIndex = GetPlayerIndex(state, playerId);

        if (playerIndex < 0 || playerIndex != unoState.CurrentPlayerIndex)
            return actions;

        var hand = unoState.PlayerHands[playerIndex];
        var topCard = unoState.TopCard;

        if (topCard == null) return actions;

        // Pending penalty: the debtor may only accept the draw or (for a Wild Draw
        // Four) challenge it. No card plays, no voluntary draw, no pass. Other
        // players act normally - the debt survives their skipped turn.
        if (unoState.PendingDrawCount > 0
            && (unoState.PendingDrawTargetIndex is null || unoState.PendingDrawTargetIndex == playerIndex))
        {
            actions.Add(CreateAction(UnoActionType.AcceptDraw, new AcceptDrawPayload()));

            if (unoState.PendingDrawCount == 4
                && unoState.PendingDrawChallengeable
                && !unoState.PendingDrawOffenderIndex.HasValue)
            {
                var lastCard = unoState.DiscardPile[^1];
                if (lastCard.IsWild && lastCard.Value == CardValue.WildDrawFour)
                {
                    var lastPlayerIndex = GetPreviousPlayerIndex(unoState);
                    if (lastPlayerIndex != playerIndex)
                    {
                        actions.Add(CreateAction(UnoActionType.ChallengeWildDrawFour, new ChallengeWildDrawFourPayload()));
                    }
                }
            }

            return actions;
        }

        // Get playable cards
        var playableCards = hand.GetPlayableCards(topCard.Value, unoState.CurrentColor);

        // PlayCard actions
        foreach (var card in playableCards)
        {
            var payload = new PlayCardPayload { Card = card };
            if (card.IsWild)
            {
                // For wild cards, create actions for each possible color choice
                foreach (var color in new[] { CardColor.Red, CardColor.Blue, CardColor.Green, CardColor.Yellow })
                {
                    var p = payload with { ChosenColor = color };
                    actions.Add(CreateAction(UnoActionType.PlayCard, p));
                }
            }
            else
            {
                actions.Add(CreateAction(UnoActionType.PlayCard, payload));
            }
        }

        // Voluntary draw: once per turn (alternatively to playing).
        if (!unoState.DrawnThisTurn)
        {
            actions.Add(CreateAction(UnoActionType.DrawCard, new DrawCardPayload { Count = 1 }));
        }
        else
        {
            // After a voluntary draw the turn can only be ended (or UNO called).
            // Note: an unplayable drawn card already auto-passed in ProcessDrawCard,
            // so reaching this branch means the drawn card is playable.
            actions.Add(CreateAction(UnoActionType.Pass, new PassPayload()));
        }

        // CallUno action
        if (hand.HasUno && !unoState.UnoCalled && unoState.UnoPendingPlayerIndex == playerIndex)
        {
            actions.Add(CreateAction(UnoActionType.CallUno, new CallUnoPayload()));
        }

        return actions;
    }

    /// <inheritdoc />
    public bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public PlayerId? GetWinner(GameState state) => state.Winner;

    private static int GetPlayerIndex(GameState state, PlayerId playerId)
    {
        for (int i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].UserId == playerId.UserId) return i;
        }
        return -1;
    }

    private static int GetPreviousPlayerIndex(UnoGameState state)
    {
        var prev = state.CurrentPlayerIndex - (int)state.Direction;
        if (prev >= state.PlayerHands.Count) prev = 0;
        if (prev < 0) prev = state.PlayerHands.Count - 1;
        return prev;
    }

    private static string GetPlayerName(UnoGameState state, int playerIndex)
    {
        if (state.PlayerIds == null || state.PlayerNames == null)
            return "Player";
        if (playerIndex < 0 || playerIndex >= state.PlayerIds.Count)
            return "Player";
        var userId = state.PlayerIds[playerIndex].UserId;
        return state.PlayerNames.TryGetValue(userId, out var name) ? name : "Player";
    }

    private static GameAction CreateAction(UnoActionType type, object payload)
    {
        return new GameAction
        {
            ActionType = type.ToString(),
            Payload = System.Text.Json.JsonSerializer.Serialize(payload)
        };
    }

    private static void ApplyInitialCardEffect(UnoGameState state, Card card)
    {
        state.EventLog.Add(UnoEvent.Build("firstCard", new { card = card.ToString() }));

        switch (card.Value)
        {
            case CardValue.Skip:
                state.NextPlayerSkipped = true;
                state.EventLog.Add(UnoEvent.Build("firstCardSkip"));
                break;
            case CardValue.Reverse:
                state.Direction = state.Direction == PlayDirection.Clockwise
                    ? PlayDirection.CounterClockwise
                    : PlayDirection.Clockwise;
                state.EventLog.Add(UnoEvent.Build("firstCardReverse"));
                break;
            case CardValue.DrawTwo:
                state.PendingDrawCount = 2;
                state.PendingDrawTargetIndex = state.CurrentPlayerIndex;
                state.PendingDrawChallengeable = false;
                state.EventLog.Add(UnoEvent.Build("firstCardDrawTwo"));
                break;
        }
    }

    private UnoActionResult ProcessPlayCard(UnoGameState state, int playerIndex, PlayCardPayload? payload)
    {
        if (payload?.Card == null)
            return UnoActionResult.Failure(Errors.CardRequired);

        var card = payload.Card;
        var hand = state.PlayerHands[playerIndex];
        var topCard = state.TopCard;

        if (topCard == null)
            return UnoActionResult.Failure(Errors.InvalidState);

        // Classic UNO: a pending Draw Two / Wild Draw Four penalty must be drawn
        // (or a Wild Draw Four may be challenged). Playing a card is not allowed -
        // without this guard the penalty would silently transfer to the next player.
        // The debt belongs to a specific player: after a turn skip it survives, so
        // only the debtor is locked out; everyone else keeps playing normally.
        if (state.PendingDrawCount > 0
            && (state.PendingDrawTargetIndex is null || state.PendingDrawTargetIndex == playerIndex))
        {
            return state.PendingDrawCount == 4
                ? UnoActionResult.Failure(Errors.AcceptOrChallenge)
                : UnoActionResult.Failure(Errors.MustDraw, state.PendingDrawCount);
        }

        // Whether a debt that is exactly 4 is a pure Wild Draw Four (challengeable)
        // or includes cards merged from earlier rounds (not challengeable).
        var hadOutstandingDebt = state.PendingDrawCount > 0;

        // Verify card is in hand
        if (!hand.Contains(card))
            return UnoActionResult.Failure(Errors.CardNotInHand);

        // Verify card can be played
        if (!card.CanPlayOn(topCard.Value, state.CurrentColor))
            return UnoActionResult.Failure(Errors.CannotPlayOnTop);

        // For wild cards, verify chosen color
        if (card.IsWild && !payload.ChosenColor.HasValue)
            return UnoActionResult.Failure(Errors.ChooseWildColor);

        // Remove card from hand
        hand.Remove(card);
        state.DiscardPile.Add(card);
        state.UnoCalled = false;
        state.UnoPendingPlayerIndex = null;

        // A non-wild card ends a previously chosen wild color; the active color
        // then derives from the top card of the discard pile.
        if (!card.IsWild)
        {
            state.CurrentColor = null;
        }

        var events = new List<string> { UnoEvent.Build("played", new { name = GetPlayerName(state, playerIndex), card = card.ToString() }) };

        // Handle card effects
        switch (card.Value)
        {
            case CardValue.Skip:
                state.NextPlayerSkipped = true;
                events.Add(UnoEvent.Build("skipEffect", new { name = GetPlayerName(state, playerIndex) }));
                break;

            case CardValue.Reverse:
                state.Direction = state.Direction == PlayDirection.Clockwise
                    ? PlayDirection.CounterClockwise
                    : PlayDirection.Clockwise;
                events.Add(UnoEvent.Build("reverseEffect", new { name = GetPlayerName(state, playerIndex) }));
                // In 2-player game, Reverse acts like Skip
                if (state.PlayerHands.Count == 2)
                {
                    state.NextPlayerSkipped = true;
                    events.Add(UnoEvent.Build("reverseTwoP", new { name = GetPlayerName(state, playerIndex) }));
                }
                break;

            case CardValue.DrawTwo:
                state.PendingDrawCount += 2;
                events.Add(UnoEvent.Build("drawTwoEffect", new { name = GetPlayerName(state, playerIndex), count = state.PendingDrawCount }));
                break;

            case CardValue.Wild:
                state.CurrentColor = payload.ChosenColor;
                events.Add(UnoEvent.Build("wildEffect", new { name = GetPlayerName(state, playerIndex), color = (int?)payload.ChosenColor }));
                break;

            case CardValue.WildDrawFour:
                state.CurrentColor = payload.ChosenColor;
                state.PendingDrawCount += 4;
                events.Add(UnoEvent.Build("wdfEffect", new { name = GetPlayerName(state, playerIndex), color = (int?)payload.ChosenColor }));
                break;
        }

        // Check for UNO
        if (hand.HasUno)
        {
            state.UnoPendingPlayerIndex = playerIndex;
            events.Add(UnoEvent.Build("hasUno", new { name = GetPlayerName(state, playerIndex) }));
        }

        // Check for win
        if (hand.IsEmpty)
        {
            events.Add(UnoEvent.Build("wins", new { name = GetPlayerName(state, playerIndex) }));
            return UnoActionResult.Success(state, events, gameEnded: true, winnerIndex: playerIndex);
        }

        // Advance to next player
        state.AdvancePlayer(state.PlayerHands.Count);

        // The draw debt belongs to whoever's turn it just became. Only a debt that
        // is exactly the Wild Draw Four on top of the pile (no earlier merged
        // penalties) is challengeable as a whole.
        if (card.Value is CardValue.DrawTwo or CardValue.WildDrawFour)
        {
            state.PendingDrawTargetIndex = state.CurrentPlayerIndex;
            state.PendingDrawChallengeable = card.Value == CardValue.WildDrawFour && !hadOutstandingDebt;
        }

        return UnoActionResult.Success(state, events);
    }

    private UnoActionResult ProcessDrawCard(UnoGameState state, int playerIndex, DrawCardPayload? payload)
    {
        // The draw count always comes from server state, never from the client
        // payload: a pending penalty must be accepted via AcceptDraw, and a regular
        // draw is exactly one card. Ignoring the payload prevents forged counts.
        _ = payload;

        // A pending penalty is accepted exclusively through AcceptDraw (which may be
        // preceded by a Wild Draw Four challenge); DrawCard is the voluntary draw.
        // The restriction applies only to the debt owner - other players keep
        // playing normally while a skipped player's debt is outstanding.
        if (state.PendingDrawCount > 0
            && (state.PendingDrawTargetIndex is null || state.PendingDrawTargetIndex == playerIndex))
            return UnoActionResult.Failure(Errors.UseAcceptDraw);

        // Classic UNO: one draw per turn.
        if (state.DrawnThisTurn)
            return UnoActionResult.Failure(Errors.OneDrawPerTurn);

        var hand = state.PlayerHands[playerIndex];
        var events = new List<string>();

        // Reshuffle if needed
        if (state.DrawPile.Count < 1)
        {
            state.DrawPile.ReshuffleDiscard(state.DiscardPile);
            events.Add(UnoEvent.Build("reshuffled"));
        }

        var drawnCards = state.DrawPile.Draw(1);
        if (drawnCards.Count == 0)
            return UnoActionResult.Failure(Errors.NoCardsLeft);

        hand.AddRange(drawnCards);
        state.DrawnThisTurn = true;

        // Classic UNO: drawing is an alternative to playing. If the drawn card is
        // playable the player may play it or pass; if not, the turn ends
        // automatically. Do NOT SetTurnClock here - ApplyTurnTimeAccounting detects
        // the turn ended (player index changed) and starts the next clock itself.
        var topCard = state.TopCard;
        var drawnPlayable = topCard != null && drawnCards[0].CanPlayOn(topCard.Value, state.CurrentColor);

        if (!drawnPlayable)
        {
            events.Add(UnoEvent.Build("drewUnplayable", new { name = GetPlayerName(state, playerIndex) }));
            state.AdvancePlayer(state.PlayerHands.Count);
        }
        else
        {
            events.Add(UnoEvent.Build("drewCard", new { name = GetPlayerName(state, playerIndex) }));
        }

        return UnoActionResult.Success(state, events, drawnCard: drawnCards[0], canPlayDrawnCard: drawnPlayable);
    }

    private UnoActionResult ProcessPass(UnoGameState state, int playerIndex)
    {
        // Pass is only valid after a voluntary draw this turn.
        if (!state.DrawnThisTurn)
            return UnoActionResult.Failure(Errors.PassAfterDraw);

        var events = new List<string> { UnoEvent.Build("passed", new { name = GetPlayerName(state, playerIndex) }) };

        // No SetTurnClock here: ApplyTurnTimeAccounting sees the turn ended and
        // starts the next player's clock (also banks the passing player's time).
        state.AdvancePlayer(state.PlayerHands.Count);

        return UnoActionResult.Success(state, events);
    }

    private UnoActionResult ProcessCallUno(UnoGameState state, int playerIndex)
    {
        var hand = state.PlayerHands[playerIndex];

        if (!hand.HasUno)
            return UnoActionResult.Failure(Errors.NotOneCard);

        if (state.UnoCalled)
            return UnoActionResult.Failure(Errors.UnoAlreadyCalled);

        if (state.UnoPendingPlayerIndex != playerIndex)
            return UnoActionResult.Failure(Errors.NotYourUnoTurn);

        state.UnoCalled = true;
        state.UnoPendingPlayerIndex = null;

        var events = new List<string> { UnoEvent.Build("calledUno", new { name = GetPlayerName(state, playerIndex) }) };
        return UnoActionResult.Success(state, events);
    }

    private UnoActionResult ProcessChallengeWildDrawFour(UnoGameState state, int playerIndex)
    {
        if (state.PendingDrawCount != 4)
            return UnoActionResult.Failure(Errors.NoWdfToChallenge);

        // Only a debt that is exactly the Wild Draw Four's 4 cards is challengeable;
        // accumulated debts (e.g. +2 from an earlier round) cannot be.
        if (!state.PendingDrawChallengeable)
            return UnoActionResult.Failure(Errors.NotChallengeable);

        // A challenge may only be decided once: after a successful challenge the
        // offender is recorded and further challenge attempts are rejected.
        if (state.PendingDrawOffenderIndex.HasValue)
            return UnoActionResult.Failure(Errors.ChallengeResolved);

        // Only the debtor may challenge.
        if (state.PendingDrawTargetIndex is { } target && target != playerIndex)
            return UnoActionResult.Failure(Errors.NotYourChallenge);

        if (state.DiscardPile.Count == 0)
            return UnoActionResult.Failure(Errors.NoChallengeCard);

        var lastCard = state.DiscardPile[^1];
        if (!lastCard.IsWild || lastCard.Value != CardValue.WildDrawFour)
            return UnoActionResult.Failure(Errors.LastNotWdf);

        var lastPlayerIndex = GetPreviousPlayerIndex(state);
        var lastPlayerHand = state.PlayerHands[lastPlayerIndex];
        var topCardBeforeWild = state.DiscardPile.Count >= 2 ? state.DiscardPile[^2] : (Card?)null;

        bool challengeSuccessful = false;
        if (topCardBeforeWild != null)
        {
            // Check if the player who played Wild Draw Four had a matching color card
            var matchingColorCards = lastPlayerHand.Cards.Where(c =>
                !c.IsWild && c.Color == topCardBeforeWild.Value.Color).ToList();

            challengeSuccessful = matchingColorCards.Count > 0;
        }

        var events = new List<string>
        {
            UnoEvent.Build("challenged", new { name = GetPlayerName(state, playerIndex) })
        };

        if (challengeSuccessful)
        {
            // Official rule: a successful challenge means the offender draws the 4
            // immediately and the challenger is off the hook, keeping their turn.
            if (state.DrawPile.Count < 4)
            {
                state.DrawPile.ReshuffleDiscard(state.DiscardPile);
                events.Add(UnoEvent.Build("reshuffled"));
            }
            var offenderCards = state.DrawPile.Draw(4);
            state.PlayerHands[lastPlayerIndex].AddRange(offenderCards);
            state.PendingDrawCount = 0;
            state.PendingDrawTargetIndex = null;
            state.PendingDrawOffenderIndex = null;
            state.PendingDrawChallengeable = false;
            events.Add(UnoEvent.Build("challengeSuccess", new { offender = GetPlayerName(state, lastPlayerIndex), count = offenderCards.Count }));

            // No advance: the challenger keeps the turn (play or draw next).
            return UnoActionResult.Success(state, events, wasChallenged: true, challengeSuccessful: true);
        }

        // Official rule: a failed challenge costs the challenger 6 (4 + 2 penalty)
        // immediately and forfeits the turn.
        if (state.DrawPile.Count < 6)
        {
            state.DrawPile.ReshuffleDiscard(state.DiscardPile);
            events.Add(UnoEvent.Build("reshuffled"));
        }
        var challengeCards = state.DrawPile.Draw(6);
        state.PlayerHands[playerIndex].AddRange(challengeCards);
        state.PendingDrawCount = 0;
        state.PendingDrawTargetIndex = null;
        state.PendingDrawChallengeable = false;
        events.Add(UnoEvent.Build("challengeFailed", new { name = GetPlayerName(state, playerIndex), count = challengeCards.Count }));

        state.AdvancePlayer(state.PlayerHands.Count);

        return UnoActionResult.Success(state, events, wasChallenged: true, challengeSuccessful: false);
    }

    private UnoActionResult ProcessAcceptDraw(UnoGameState state, int playerIndex)
    {
        if (state.PendingDrawCount == 0)
            return UnoActionResult.Failure(Errors.NoPendingDraw);

        // Only the debt owner (or the challenge-offender flow) may accept.
        if (state.PendingDrawTargetIndex is { } target && target != playerIndex)
            return UnoActionResult.Failure(Errors.NotYourPenalty);

        var hand = state.PlayerHands[playerIndex];
        var drawCount = state.PendingDrawCount;
        state.PendingDrawCount = 0;
        state.PendingDrawTargetIndex = null;
        state.PendingDrawChallengeable = false;

        // Reshuffle if needed
        var events = new List<string>();
        if (state.DrawPile.Count < drawCount)
        {
            state.DrawPile.ReshuffleDiscard(state.DiscardPile);
            events.Add(UnoEvent.Build("reshuffled"));
        }

        // Successful-challenge flow: the OFFENDER draws the 4 cards, not the
        // challenger, who keeps the turn.
        if (state.PendingDrawOffenderIndex is { } offenderIndex && offenderIndex != playerIndex)
        {
            var offenderHand = state.PlayerHands[offenderIndex];
            var offenderCards = state.DrawPile.Draw(drawCount);
            offenderHand.AddRange(offenderCards);
            state.PendingDrawOffenderIndex = null;
            events.Add(UnoEvent.Build("challengePenalty", new { name = GetPlayerName(state, offenderIndex), count = offenderCards.Count }));

            // The challenger keeps the turn: they may play or draw now.
            state.DrawnThisTurn = false;
            return UnoActionResult.Success(state, events);
        }

        var drawnCards = state.DrawPile.Draw(drawCount);
        hand.AddRange(drawnCards);

        events.Add(UnoEvent.Build("acceptedDraw", new { name = GetPlayerName(state, playerIndex), count = drawnCards.Count }));

        // Classic UNO: a penalty draw forfeits the turn - drawn cards may not be played.
        state.AdvancePlayer(state.PlayerHands.Count);

        return UnoActionResult.Success(state, events);
    }

    /// <summary>
    /// Handles a system-dispatched turn timeout for the current player.
    /// Charges the standard overrun (bank first, then deferred penalty), skips the
    /// turn, and removes the player if they have hit the AFK threshold.
    /// </summary>
    private UnoActionResult ProcessTurnTimeout(UnoGameState state, int playerIndex)
    {
        if (state.NextActionDeadlineUtc is not { } deadline || DateTime.UtcNow <= deadline)
        {
            return UnoActionResult.Failure(Errors.TimerNotExpired);
        }

        var config = state.TimerConfig ?? new UnoTurnTimerConfig();
        var timer = state.EnsureTimer(playerIndex);

        // Charge the standard overrun: bank first, then defer the remainder (capped).
        var bankCover = Math.Min(timer.BankSeconds, config.MaxOverrunSeconds);
        timer.BankSeconds -= bankCover;
        timer.DeferredPenaltySeconds += Math.Min(config.MaxOverrunSeconds, config.MaxOverrunSeconds - bankCover);
        timer.ConsecutiveTimeouts += 1;

        var events = new List<string>
        {
            UnoEvent.Build("turnSkipped", new { name = GetPlayerName(state, playerIndex) })
        };

        // A pending draw debt SURVIVES the skip: the debtor still owes it on their
        // next turn (the play/draw guards and AcceptDraw enforce it there), so a
        // timeout can never be used to dodge a penalty. Penalties from different
        // rounds accumulate (+2 then +4 = draw 6).
        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            state.EliminatedPlayerIndexes.Add(playerIndex);
            events.Add(UnoEvent.Build("afkRemoved", new { name = GetPlayerName(state, playerIndex) }));

            // Resolve the removed player's unresolved debt: a successful challenge
            // makes the OFFENDER pay immediately; otherwise the debt is dropped.
            if (state.PendingDrawCount > 0 && state.PendingDrawTargetIndex == playerIndex)
            {
                var drawCount = state.PendingDrawCount;
                state.PendingDrawCount = 0;
                state.PendingDrawTargetIndex = null;

                if (state.PendingDrawOffenderIndex is { } offender)
                {
                    state.PendingDrawOffenderIndex = null;
                    if (state.DrawPile.Count < drawCount)
                    {
                        state.DrawPile.ReshuffleDiscard(state.DiscardPile);
                        events.Add(UnoEvent.Build("reshuffled"));
                    }
                    var offenderCards = state.DrawPile.Draw(drawCount);
                    state.PlayerHands[offender].AddRange(offenderCards);
                    events.Add(UnoEvent.Build("challengePenalty", new { name = GetPlayerName(state, offender), count = offenderCards.Count }));
                }
                else
                {
                    events.Add(UnoEvent.Build("debtDropped", new { name = GetPlayerName(state, playerIndex) }));
                }
            }

            var remaining = state.PlayerHands.Count - state.EliminatedPlayerIndexes.Count;
            if (remaining <= 1)
            {
                var winnerIndex = Enumerable.Range(0, state.PlayerHands.Count)
                    .First(i => !state.EliminatedPlayerIndexes.Contains(i));
                events.Add(UnoEvent.Build("wins", new { name = GetPlayerName(state, winnerIndex) }));
                state.NextActionDeadlineUtc = null;
                return UnoActionResult.Success(state, events, gameEnded: true, winnerIndex: winnerIndex);
            }
        }

        // A timeout is not a played turn: ConsecutiveTimeouts keeps counting until
        // the player completes a real turn (ApplyTurnTimeAccounting resets it).
        state.AdvancePlayer(state.PlayerHands.Count);
        state.SetTurnClock();

        return UnoActionResult.Success(state, events);
    }

    /// <summary>
    /// Handles the system-dispatched end of the game's total time limit: the game
    /// is force-finished and the player with the fewest cards wins. When the lowest
    /// count is shared, the game ends in a draw (no winner).
    /// </summary>
    private UnoActionResult ProcessGameTimeExpired(UnoGameState state)
    {
        if (state.GameEndsAtUtc is not { } endsAt || DateTime.UtcNow < endsAt)
        {
            return UnoActionResult.Failure(Errors.TimeNotUp);
        }

        var activeIndexes = Enumerable.Range(0, state.PlayerHands.Count)
            .Where(i => !state.EliminatedPlayerIndexes.Contains(i))
            .ToList();

        var minCards = activeIndexes.Min(i => state.PlayerHands[i].Count);
        var fewestCards = activeIndexes.Where(i => state.PlayerHands[i].Count == minCards).ToList();

        var events = new List<string>
        {
            UnoEvent.Build("timeUp")
        };

        if (fewestCards.Count == 1)
        {
            var winnerIndex = fewestCards[0];
            events.Add(UnoEvent.Build("winsFewest", new { name = GetPlayerName(state, winnerIndex), count = minCards }));
            state.NextActionDeadlineUtc = null;
            state.GameEndsAtUtc = null;
            return UnoActionResult.Success(state, events, gameEnded: true, winnerIndex: winnerIndex);
        }

        events.Add(UnoEvent.Build("tieDraw", new { players = fewestCards.Count, count = minCards }));
        state.NextActionDeadlineUtc = null;
        state.GameEndsAtUtc = null;
        return UnoActionResult.Success(state, events, gameEnded: true);
    }

    /// <summary>
    /// Whether the action type consumes the current player's turn clock.
    /// CallUno is an auxiliary action and is not timed.
    /// </summary>
    private static bool IsTurnAction(UnoActionType type)
        => type is UnoActionType.PlayCard
            or UnoActionType.DrawCard
            or UnoActionType.AcceptDraw
            or UnoActionType.Pass
            or UnoActionType.ChallengeWildDrawFour;
}