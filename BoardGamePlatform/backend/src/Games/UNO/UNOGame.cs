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
            EventLog = new List<string> { $"Game started with {options.Players.Count} players" },
            PlayerNames = playerNames,
            PlayerIds = options.Players,
            TimerConfig = timerConfig
        };

        // Apply initial card effect if it's an action card
        ApplyInitialCardEffect(unoState, topCard.Value);

        // Start the first player's turn clock
        unoState.SetTurnClock();

        var gameState = new GameState
        {
            SessionId = options.Players.Count > 0 ? Guid.NewGuid() : Guid.Empty,
            GameType = GameType,
            Players = options.Players,
            CurrentPlayerIndex = unoState.CurrentPlayerIndex,
            IsOver = false,
            Version = 1,
            NextActionDeadlineUtc = unoState.NextActionDeadlineUtc,
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
                Error = "Game is already over"
            };
        }

        // Deserialize UNO state
        if (!state.TryGetString("UnoState", out var unoStateJson) || unoStateJson is null)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = "Invalid game state: missing UNO state"
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
                Error = "Invalid player"
            };
        }

        // Check if it's the player's turn
        if (playerIndex != unoState.CurrentPlayerIndex)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = "Not your turn"
            };
        }

        // Parse action type from the action's type field
        if (!Enum.TryParse<UnoActionType>(action.ActionType, ignoreCase: true, out var actionType))
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = "Invalid action type"
            };
        }

        UnoActionResult result = actionType switch
        {
            UnoActionType.PlayCard => ProcessPlayCard(unoState, playerIndex, action.PayloadAs<PlayCardPayload>()),
            UnoActionType.DrawCard => ProcessDrawCard(unoState, playerIndex, action.PayloadAs<DrawCardPayload>()),
            UnoActionType.CallUno => ProcessCallUno(unoState, playerIndex),
            UnoActionType.ChallengeWildDrawFour => ProcessChallengeWildDrawFour(unoState, playerIndex),
            UnoActionType.AcceptDraw => ProcessAcceptDraw(unoState, playerIndex),
            UnoActionType.TurnTimeout => ProcessTurnTimeout(unoState, playerIndex),
            _ => UnoActionResult.Failure($"Unknown action type: {actionType}")
        };

        if (!result.IsValid)
        {
            return new GameResult
            {
                IsValid = false,
                NewState = state,
                Error = result.Error
            };
        }

        // Turn timer accounting: bank unused time / charge overruns after genuine
        // turn actions, and clear the deadline when the game has ended.
        if (result.GameEnded)
        {
            unoState.NextActionDeadlineUtc = null;
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

        // DrawCard action (if no playable cards or player chooses to draw)
        if (playableCards.Count == 0 || true) // Can always choose to draw
        {
            var drawCount = unoState.PendingDrawCount > 0 ? unoState.PendingDrawCount : 1;
            actions.Add(CreateAction(UnoActionType.DrawCard, new DrawCardPayload { Count = drawCount }));
        }

        // CallUno action
        if (hand.HasUno && !unoState.UnoCalled && unoState.UnoPendingPlayerIndex == playerIndex)
        {
            actions.Add(CreateAction(UnoActionType.CallUno, new CallUnoPayload()));
        }

        // ChallengeWildDrawFour action
        if (unoState.PendingDrawCount == 4 && unoState.DiscardPile.Count > 0)
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

        // AcceptDraw action
        if (unoState.PendingDrawCount > 0)
        {
            actions.Add(CreateAction(UnoActionType.AcceptDraw, new AcceptDrawPayload()));
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
        state.EventLog.Add($"First card: {card}");

        switch (card.Value)
        {
            case CardValue.Skip:
                state.NextPlayerSkipped = true;
                state.EventLog.Add("First card is Skip - first player is skipped!");
                break;
            case CardValue.Reverse:
                state.Direction = state.Direction == PlayDirection.Clockwise
                    ? PlayDirection.CounterClockwise
                    : PlayDirection.Clockwise;
                state.EventLog.Add("First card is Reverse - direction changed!");
                break;
            case CardValue.DrawTwo:
                state.PendingDrawCount = 2;
                state.EventLog.Add("First card is Draw Two - first player must draw 2!");
                break;
        }
    }

    private UnoActionResult ProcessPlayCard(UnoGameState state, int playerIndex, PlayCardPayload? payload)
    {
        if (payload?.Card == null)
            return UnoActionResult.Failure("Card is required");

        var card = payload.Card;
        var hand = state.PlayerHands[playerIndex];
        var topCard = state.TopCard;

        if (topCard == null)
            return UnoActionResult.Failure("No top card");

        // Verify card is in hand
        if (!hand.Contains(card))
            return UnoActionResult.Failure("Card not in hand");

        // Verify card can be played
        if (!card.CanPlayOn(topCard.Value, state.CurrentColor))
            return UnoActionResult.Failure("Card cannot be played on current top card");

        // For wild cards, verify chosen color
        if (card.IsWild && !payload.ChosenColor.HasValue)
            return UnoActionResult.Failure("Must choose a color for wild card");

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

        var events = new List<string> { $"{GetPlayerName(state, playerIndex)} played {card}" };

        // Handle card effects
        switch (card.Value)
        {
            case CardValue.Skip:
                state.NextPlayerSkipped = true;
                events.Add($"{GetPlayerName(state, playerIndex)} played Skip - next player skipped!");
                break;

            case CardValue.Reverse:
                state.Direction = state.Direction == PlayDirection.Clockwise
                    ? PlayDirection.CounterClockwise
                    : PlayDirection.Clockwise;
                events.Add($"{GetPlayerName(state, playerIndex)} played Reverse - direction changed!");
                // In 2-player game, Reverse acts like Skip
                if (state.PlayerHands.Count == 2)
                {
                    state.NextPlayerSkipped = true;
                    events.Add(" (acts as Skip in 2-player)");
                }
                break;

            case CardValue.DrawTwo:
                state.PendingDrawCount += 2;
                events.Add($"{GetPlayerName(state, playerIndex)} played Draw Two - next player must draw {state.PendingDrawCount}!");
                break;

            case CardValue.Wild:
                state.CurrentColor = payload.ChosenColor;
                events.Add($"{GetPlayerName(state, playerIndex)} played Wild - chose {payload.ChosenColor}!");
                break;

            case CardValue.WildDrawFour:
                state.CurrentColor = payload.ChosenColor;
                state.PendingDrawCount += 4;
                events.Add($"{GetPlayerName(state, playerIndex)} played Wild Draw Four - chose {payload.ChosenColor}, next player must draw 4 (or challenge)!");
                break;
        }

        // Check for UNO
        if (hand.HasUno)
        {
            state.UnoPendingPlayerIndex = playerIndex;
            events.Add($"{GetPlayerName(state, playerIndex)} has UNO!");
        }

        // Check for win
        if (hand.IsEmpty)
        {
            events.Add($"{GetPlayerName(state, playerIndex)} wins!");
            return UnoActionResult.Success(state, events, gameEnded: true, winnerIndex: playerIndex);
        }

        // Advance to next player
        state.AdvancePlayer(state.PlayerHands.Count);

        return UnoActionResult.Success(state, events);
    }

    private UnoActionResult ProcessDrawCard(UnoGameState state, int playerIndex, DrawCardPayload? payload)
    {
        var drawCount = payload?.Count ?? (state.PendingDrawCount > 0 ? state.PendingDrawCount : 1);
        var hand = state.PlayerHands[playerIndex];
        var events = new List<string>();

        // Reshuffle if needed
        if (state.DrawPile.Count < drawCount)
        {
            state.DrawPile.ReshuffleDiscard(state.DiscardPile);
            events.Add("Reshuffled discard pile into draw pile");
        }

        var drawnCards = state.DrawPile.Draw(drawCount);
        hand.AddRange(drawnCards);
        state.PendingDrawCount = 0;

        events.Add($"{GetPlayerName(state, playerIndex)} drew {drawnCards.Count} card(s)");

        Card? playableDrawnCard = null;
        if (drawnCards.Count > 0)
        {
            var topCard = state.TopCard;
            if (topCard != null)
            {
                playableDrawnCard = drawnCards.FirstOrDefault(c => c.CanPlayOn(topCard.Value, state.CurrentColor));
            }
        }

        // If there was a pending draw penalty, player must accept the draw (can't play)
        // Otherwise, they can choose to play the drawn card if playable
        bool wasPenalty = drawCount > 1;

        // Advance to next player (unless they play the drawn card)
        if (!wasPenalty || playableDrawnCard == null)
        {
            state.AdvancePlayer(state.PlayerHands.Count);
        }

        return UnoActionResult.Success(state, events, gameEnded: false, drawnCard: playableDrawnCard, canPlayDrawnCard: !wasPenalty && playableDrawnCard != null);
    }

    private UnoActionResult ProcessCallUno(UnoGameState state, int playerIndex)
    {
        var hand = state.PlayerHands[playerIndex];

        if (!hand.HasUno)
            return UnoActionResult.Failure("You don't have exactly one card");

        if (state.UnoCalled)
            return UnoActionResult.Failure("UNO already called");

        if (state.UnoPendingPlayerIndex != playerIndex)
            return UnoActionResult.Failure("Not your turn to call UNO");

        state.UnoCalled = true;
        state.UnoPendingPlayerIndex = null;

        var events = new List<string> { $"{GetPlayerName(state, playerIndex)} called UNO!" };
        return UnoActionResult.Success(state, events);
    }

    private UnoActionResult ProcessChallengeWildDrawFour(UnoGameState state, int playerIndex)
    {
        if (state.PendingDrawCount != 4)
            return UnoActionResult.Failure("No Wild Draw Four to challenge");

        if (state.DiscardPile.Count == 0)
            return UnoActionResult.Failure("No card to challenge");

        var lastCard = state.DiscardPile[^1];
        if (!lastCard.IsWild || lastCard.Value != CardValue.WildDrawFour)
            return UnoActionResult.Failure("Last card is not Wild Draw Four");

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
            $"{GetPlayerName(state, playerIndex)} challenged Wild Draw Four!"
        };

        if (challengeSuccessful)
        {
            // Challenger wins - challenger draws 4 instead of 6 (2 extra)
            state.PendingDrawCount = 6; // 4 for Wild Draw Four + 2 penalty
            events.Add($"Challenge successful! {GetPlayerName(state, lastPlayerIndex)} had a matching color card. They must draw 6!");
        }
        else
        {
            // Challenge failed - challenger draws 6 (4 + 2 penalty)
            state.PendingDrawCount = 6;
            events.Add($"Challenge failed! {GetPlayerName(state, playerIndex)} must draw 6!");
        }

        // Don't advance player here - the draw will be handled by AcceptDraw
        return UnoActionResult.Success(state, events, wasChallenged: true, challengeSuccessful: challengeSuccessful);
    }

    private UnoActionResult ProcessAcceptDraw(UnoGameState state, int playerIndex)
    {
        if (state.PendingDrawCount == 0)
            return UnoActionResult.Failure("No pending draw to accept");

        var hand = state.PlayerHands[playerIndex];
        var drawCount = state.PendingDrawCount;
        state.PendingDrawCount = 0;

        // Reshuffle if needed
        var events = new List<string>();
        if (state.DrawPile.Count < drawCount)
        {
            state.DrawPile.ReshuffleDiscard(state.DiscardPile);
            events.Add("Reshuffled discard pile into draw pile");
        }

        var drawnCards = state.DrawPile.Draw(drawCount);
        hand.AddRange(drawnCards);

        events.Add($"{GetPlayerName(state, playerIndex)} accepted draw of {drawnCards.Count} card(s)");

        // Advance to next player
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
            return UnoActionResult.Failure("Turn timer has not expired");
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
            $"{GetPlayerName(state, playerIndex)} ran out of time - turn skipped"
        };

        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            state.EliminatedPlayerIndexes.Add(playerIndex);
            events.Add($"{GetPlayerName(state, playerIndex)} was removed for inactivity (AFK)");

            var remaining = state.PlayerHands.Count - state.EliminatedPlayerIndexes.Count;
            if (remaining <= 1)
            {
                var winnerIndex = Enumerable.Range(0, state.PlayerHands.Count)
                    .First(i => !state.EliminatedPlayerIndexes.Contains(i));
                events.Add($"{GetPlayerName(state, winnerIndex)} wins!");
                state.NextActionDeadlineUtc = null;
                return UnoActionResult.Success(state, events, gameEnded: true, winnerIndex: winnerIndex);
            }
        }

        state.AdvancePlayer(state.PlayerHands.Count);
        state.SetTurnClock();

        return UnoActionResult.Success(state, events);
    }

    /// <summary>
    /// Whether the action type consumes the current player's turn clock.
    /// CallUno is an auxiliary action and is not timed.
    /// </summary>
    private static bool IsTurnAction(UnoActionType type)
        => type is UnoActionType.PlayCard
            or UnoActionType.DrawCard
            or UnoActionType.AcceptDraw
            or UnoActionType.ChallengeWildDrawFour;
}