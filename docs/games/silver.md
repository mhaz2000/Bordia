# Silver - Game Documentation

> Extracted from `AGENTS.md` (2026-09-13). This document owns everything about the Silver
> ruleset: the authoritative rules specification, design notes, and the game-specific
> Critical Decisions. Platform-wide contracts and decisions remain in the root `AGENTS.md`.

---


> **Status: fully implemented (2026-09-11 — see Critical Decisions): engine + Game Service projection + React view + EN/FA localization.**
>
> **Amended 2026-09-12 (see the re-audit Critical Decision below):** the implementation was re-checked line-by-line against the exact-rules specification supplied by the project owner and several rule details below are now **superseded**: character naming (2 Enchanter, 3 Guard, 4 Trickster, 7 Apprentice Seer, 8 Seer, 9 Beholder), the Squire is a passive per-turn reveal with a takeable display area (not an activated once-per-turn ability), Trickster draws one extra per face-up copy and returns the rest to the **top** of the deck in drawn order, the Guard is an explicit card→card protection with Move/Remove actions, Robber may steal face-up cards, the Witch resolves in two steps (peek, then exchange-or-decline), the Master supports multi-card replacement and a decline, the Revealer's *target* chooses which of their cards flips, a matching set may contain at most one Doppelgänger (two match only each other), a village ending the round with exactly two Doppelgängers scores 13, setup removes 5×(4−players) cards from the game so deck+discard is always 32, the Amulet is assigned to the starting player in round 1, "calling for a vote" is called **census** in copy, and replacement-card orientation follows the source (owner clarification 2026-09-12, superseding the earlier literal reading of §9): a card drawn **from the deck** enters the village **face down, known only to the drawer** — including the Witch's peeked card; cards from the **discard pile or the Squire display** were public and enter **face up**. The optional 100-point scoring mode and Kamikaze rule from the owner's specification are explicitly **out of scope** (owner directive 2026-09-12). Where this section and the 2026-09-12 Critical Decisions disagree, the newest Critical Decision wins.

## Scope and identity

- **Game:** *Silver* by Bézier Games (Ted Alspach, 2019) — the original standalone base game (internally the "Amulet" card set).
- **In scope:** base Silver only — 2–4 players, a single deck, four rounds, the standard rules documented below.
- **Out of scope:** Silver Bullet, Silver Coin, and Silver Dagger decks; combining multiple Silver decks; any variants or house rules. These require separate explicit approval and their own Critical Decisions.
- **Ruleset interpretation policy:** the rules below are transcribed from the official Silver rulebook / character reference guide and cross-checked against independent summaries. Where editions or third-party summaries differ, this document is authoritative for this codebase. A handful of sub-behaviors the rulebook does not pin down are listed under **Open interpretation decisions**; the implementer must apply the prescribed default and record the final choice as a Critical Decision.

## Design philosophy

Silver is a **game-specific implementation of the existing `IGame` interface** — nothing more:

```text
Game Service → IGame → SilverGame → SilverState
```

- Use the existing generic engine infrastructure (`CreateGame`, `ProcessAction`, `GetValidActions`, `IsGameOver`, `GetWinner`) unchanged.
- Do **not** create generic abstractions for Silver (`CardGameBase`, `DeckGameBase`, `HiddenInformationGameBase`, `MemoryGameBase`, `VillageGameBase`, `ICardAbility`, `IDeck`, `IPlayerView`, `IVisibilityEngine`, `IGamePhaseManager` are all forbidden unless a future game genuinely needs them).
- Silver-specific models (`SilverGame`, `SilverState`, `SilverCard`, payload records, `SilverEvent`, `Errors`) live only in `Games/Silver/`.
- No HTTP, SignalR, EF Core, PostgreSQL, Redis, or RabbitMQ references in `Games/Silver/` (same purity rules as UNO).

## Rules the implementation must follow

### Players

Silver is designed for **2–4 players**. `SilverGame.MinPlayers => 2`, `SilverGame.MaxPlayers => 4`; `CreateGame` must reject any configured player count outside 2–4 (the `MaxPlayers` value already prevents 5-player rooms from being created in the Lobby).

### Objective

Every card shows a **number 0–13**, representing how many werewolves follow that resident into your village. Each player's tableau ("village") is a row of cards; your score is driven by the total value of the cards in your village. The game is played over exactly **four rounds**; the player with the **fewest cumulative points** after round 4 wins (see Scoring).

### Cards and deck composition

The deck is **52 cards**: values 0–13, with the following characters and counts (4 copies each except where noted):

| Value | Character       | Copies | Ability (intent) | When it activates |
|------:|-----------------|-------:|------------------|-------------------|
| 0     | Villager        | 2      | The round ends immediately when two face-up Villagers are in the same village. | Always, while face up in **any** village (0–1 affect all players) |
| 1     | Squire          | 4      | Once per turn, display the top card of the deck face up beside the deck (public information). | Always, while face up in any village |
| 2     | Empath          | 4      | Once per turn, peek at one of your own village cards. | Face up in **your** village (2–4) |
| 3     | Bodyguard       | 4      | Once per turn, protect or unprotect one of your village cards. A protected card may not be viewed or moved by any player (including you) until the end of the round; it still counts toward your card total when calling. | Face up in your village |
| 4     | Rascal          | 4      | When drawing from the deck, you may draw one extra card and choose which of the two to use. | Face up in your village |
| 5     | Exposer         | 4      | Turn one of your own village cards face up. | Only when the card is discarded **immediately after drawing it from the deck** (5–12) |
| 6     | Revealer        | 4      | Turn any one village card (any village) face up. | On deck-draw discard (5–12) |
| 7     | Beholder        | 4      | Peek at up to two of your own village cards. | On deck-draw discard (5–12) |
| 8     | Apprentice Seer | 4      | Peek at one card in an opponent's village. | On deck-draw discard (5–12) |
| 9     | Seer            | 4      | Peek at any one village card (any player's). | On deck-draw discard (5–12) |
| 10    | Master          | 4      | Exchange one of your village cards with **any** card in the discard pile (not just the top card). | On deck-draw discard (5–12) |
| 11    | Witch           | 4      | View the top card of the deck, then exchange it with any one card in any village (yours or an opponent's). | On deck-draw discard (5–12) |
| 12    | Robber          | 4      | Swap one of your village cards with a card in another player's village, then peek at the card you received. | On deck-draw discard (5–12) |
| 13    | Doppelgänger    | 2      | When exchanging a set of cards, a Doppelgänger among them may be treated as any value for determining the match. | During exchanges (13) |

The table's ability intents and activation timings are cross-validated by the official rulebook's ability-icon taxonomy (0–1 faceup-anywhere, 2–4 faceup-own-village, 5–12 on deck-draw discard, 13 on exchanges). Retail boxes are marketed as "56 cards", which counts the 4 player reference cards alongside the 52 game cards above; the implementation uses the 52 game cards. The implementing agent must cross-check this table against the official Silver Amulet rulebook/reference guide during implementation and record any correction as a Critical Decision.

Ability-activation rules that must be enforced by the engine:

- Values **0–1** are always active while face up in **any** village.
- Values **2–4** activate while face up in **your** village, on your turn, once per turn where stated.
- Values **5–12** activate **only** when the card is discarded immediately after being drawn from the deck. A 5–12 card **taken from the discard pile** does not activate its ability.
- Value **13** applies during exchanges (see Turn flow).
- No ability may be used on the turn the player calls for a vote.
- No ability may be activated when discarding a card from your village (only the specific timings above count).
- A moved face-up card remains face up (Robber/Witch movement preserves orientation).
- Protected cards (Silver Amulet or Bodyguard) cannot be viewed or moved by any player, including via Witch/Robber.
- Peek abilities are **secret**: only the peeking player learns the value.

### Initial setup (per round)

1. Shuffle the 52-card deck (server-side RNG only).
2. Deal **five cards face down in a horizontal row** (the village) to each player.
3. Turn one card face up beside the deck to start the **discard pile**; the remainder is the draw pile.
4. The start player is chosen at random in round 1 (server RNG). The engine sets `CurrentPlayerIndex` to that seat immediately at creation so the generic turn-timeout sweep works from the first second.
5. **Initial peek phase:** each player secretly views **two** of their five village cards. Digital adaptation: each player may submit up to two `PeekVillageCard` actions (own village, face-down slots only). These peeks are asynchronous — they do not consume the turn, are not restricted to the current player, and must be available until the round ends so an AFK-adjacent opponent cannot stall the game. (Documented adaptation of the simultaneous physical-game peek.)

Round 2–4 setup repeats the same procedure (fresh shuffle, fresh deal, fresh discard, two new peeks each); the start player of rounds 2–4 is the **Silver Amulet holder** (see Scoring).

### Public vs private information

- **Public:** the number of cards in each village; which village cards are face up (and their values); the top card of the discard pile; the deck card count; the current round; the current player; cumulative scores after each completed round.
- **Private to one player:** the values of their own face-down village cards that they have previously seen (peeked or exchanged in); any values learned from peeks (own or others').
- **Unknown to everyone:** face-down cards not yet seen; face-down cards in other players' villages; the draw pile's order.
- Round scores are revealed only at round scoring; during a round no one sees anyone's village total.
- Knowledge is tracked **per card instance** (not per slot position), so it survives the slot shifting/collapsing of multi-card exchanges and follows a card if an ability moves it. When a card leaves a village, stale knowledge of it is meaningless but harmless.

### Turn flow

On your turn, take **exactly one** of these three actions:

1. **Draw from the deck** — take the top card of the draw pile (the engine picks it; the client never names the card). Then, as a follow-up decision in the same turn, do one of:
   - **Discard it:** place the drawn card face up on the discard pile. If it is a 5–12 card, you may then use its ability (`UseAbility`), or decline.
   - **Exchange it** with one or more of your village cards (see Exchange mechanics). The drawn card enters your village **face down** (you saw it while drawing, so you know it; nobody else does).
2. **Take the top card from the discard pile** — you **must** exchange it into your village (single or multi exchange); it enters **face up** (public). Its 5–12 ability does **not** activate.
3. **Call for a vote** — only if you have **four or fewer cards** (see Calling for a vote).

Additionally, on your turn you may use available face-up abilities (2–4) per their rules, and the Silver Amulet holder may place the amulet as their turn's action (mutually exclusive with calling for a vote). After the turn's actions resolve, play passes clockwise to the next player.

### Exchange mechanics

- **Single exchange:** the old card goes face up on the discard pile; the new card takes the **same slot**.
- **Multi-card exchange:** all exchanged cards must share one value (a face-up Doppelgänger counts as any value). Before discarding, they are flipped face up (proving the match); if they match, all are discarded and the new card goes into any one of the freed slots, then empty slots collapse.
- **Failed match:** if the slid cards do not all match, **all** of them return to the village **face down** (even ones that were face up). If **three or more** cards failed to match, additionally draw one card from the deck and place it face down at either end of the village without looking at it (village grows — the penalty).
- The card just drawn this turn can never be part of a matching-set discard; it is the incoming card, not a discardable one, until your next turn.

### Calling for a vote (ending a round)

- **Who/when:** only the active player, on their turn, instead of drawing/taking/calling anything else. Requires **≤ 4 cards** in their village (the amulet-protected card counts toward this total).
- **Effect:** the caller's turn ends immediately. Each **other** player gets **exactly one more turn** (they may not call for a vote, and the caller obviously acts no more). Then the round ends and scoring happens.
- **No abilities** may be used on the turn a player calls, and the amulet may not be placed that turn.

### Round end conditions

The round ends when any of these occurs:

1. A player called for a vote and every other player took their one extra turn; or
2. The **draw pile is depleted** (round ends immediately; nobody gets a caller bonus); or
3. Two face-up **Villager (0)** cards are in the same village (immediate; no caller bonus).

### Scoring

- Each player's **round score** is the sum of the values of all cards remaining in their village (face up and face down — everything is revealed at scoring).
- The **caller**: if their sum is the lowest (or tied for lowest) → they score **0**. If someone else is strictly lower → they score **sum + 10** (the failed-call penalty).
- **All other players** score their plain sum.
- Cumulative scores are written down publicly (serialized in state) and carry across rounds.
- **Silver Amulet:** the player with the lowest round score receives the amulet and is the start player of the next round. If that player had **successfully called** the vote, they may — on one of their turns during the next round, as that turn's action — place the amulet on one of their village cards; that card may not be viewed or moved by anyone (including them) until round end, and counts toward their card total when calling. They may not call for a vote on the same turn they place the amulet. If the amulet is on their last remaining card, they may only draw-and-discard (using a 5–12 ability if applicable) or call for a vote — they cannot exchange (no other cards to interact with).
- **Round-lowest tie:** if the tied players include the current amulet holder, they keep it; otherwise it goes to the tied player seated closest clockwise after the start player of the finished round.
- **Game end:** after **four rounds**, the player with the fewest cumulative points wins. Tie-break: a tied player holding the amulet wins; otherwise the tied player seated closest clockwise to the amulet holder's seat wins.

### Deck/discard behavior

- No reshuffling within a round: when the draw pile empties, the round simply ends (rulebook behavior — this is why calling early is risky). Reshuffling the whole deck happens only between rounds.
- Only the **top card** of the discard pile is takeable (Master's ability is the sole exception, reaching any card in the pile).
- Discarded cards always go on top, face up.

### Open interpretation decisions (apply the default; record the final choice)

1. **Rascal's unchosen card:** prescribed default — the unchosen extra card goes face down to the **bottom of the deck**.
2. **Squire's display area:** prescribed default — displayed cards sit face up beside the deck as public information and are out of play for the rest of the round.
3. **Master:** the chosen discard card enters your village **face up** (consistent with take-from-discard).
4. **Witch:** the viewed deck card enters the target village **face down**, known only to the Witch player.
5. **Protected cards** may not be targeted by Witch/Robber (protection blocks view *and* move).
6. **Timeout while a drawn-card decision is pending:** the drawn card is discarded without its ability and the turn advances (the debt of the decision must never stall the game).
7. **AFK elimination** (see Timers): a 2-player game ends immediately with the survivor winning; in 3–4 player games the seat stops taking turns and their village is still scored at round end.

## Silver actions

Define Silver-specific `GameAction` types; do **not** reuse UNO's actions and do not introduce a generic `PlayCard` — Silver is draw/replace/ability based. The conceptual action set (the implementer may choose the smallest clean naming that represents the rules accurately):

| Action | Actor | Phase | Payload (conceptual) | Effect |
|---|---|---|---|---|
| `PeekVillageCard` | any player | until round end; ≤2 per player per round | own village slot | records the card value in that player's knowledge |
| `DrawFromDeck` | current player | own turn, nothing drawn yet | — | engine reveals top deck card to this player; state enters the drawn-card decision sub-phase |
| `TakeDiscard` | current player | own turn, nothing drawn yet | — | top discard card is taken; state enters the exchange sub-phase (face up) |
| `DiscardDrawnCard` | current player | drawn-card sub-phase | optional ability choice | drawn card → discard top; optional 5–12 ability use; turn ends |
| `ExchangeWithDrawn` | current player | drawn-card or take-discard sub-phase | village slot(s), placement for multi-exchange | performs single/multi exchange incl. mismatch handling; turn ends |
| `UseAbility` | current player | per the ability's activation timing | ability-specific (target player, village slot, protect/unprotect, peek choice…) | executes the ability effect |
| `CallVote` | current player | own turn, ≤4 cards, vote not already active | — | round-end sequence begins |
| `TurnTimeout` / `GameTimeExpired` | system (Game Service) | deadline passed | — | see Timers |

For every action the engine must validate: actor identity (current player, except peeks), phase/sub-phase correctness, slot ownership and existence, protected-card targeting, deck/discard availability, vote preconditions, and round/game-not-over. Follow UNO's pattern: `ProcessAction` dispatches on action type, returns `GameResult.Failure(silver.*ErrorCode, args)` on any violation, never mutates the input state, and the client-supplied payload can never override engine-drawn content.

`GetValidActions(state, playerId)` must return **only currently legal actions** for that player — wrong player, wrong phase, invalid slot, protected target, unavailable pile, vote-not-allowed, and post-round situations must all be excluded. Like UNO, it is an engine-level contract used by tests, not an HTTP endpoint.

## Hidden information and player-specific state

This is the critical architectural difference from UNO. Silver's authoritative state contains secrets that must never reach other players' clients:

```text
SilverState (authoritative, persisted)
    Public:    round, turn order, current player, deck count, discard top,
               each village's face-up cards, cumulative scores, vote state
    Private:   each player's face-down village cards,
               per-player knowledge (which card instances that player has seen)
```

- **The engine stays authoritative and owns the rules; the Game Service controls what each client sees.**
- The distinction between the **authoritative game state** and the **player-visible game state** must be explicit in the implementation. Today the Game Service serves and broadcasts the **full** `GameState` on every path (`GetGameStateQuery`, the action response, `ReconnectPlayerCommand`, and the `GameStateUpdated` SignalR group broadcast). UNO ships its full state (including hands) — an accepted limitation there — but Silver would leak every hidden card that way.
- **Required small generic capability (the only anticipated engine-contract extension):** add an **optional** interface in `GameEngine.Core`, e.g.

  ```csharp
  public interface IPlayerViewGame
  {
      /// Returns the state as seen by `viewer`: public info plus the viewer's
      /// own private info and knowledge. Secrets belonging to others are removed.
      GameState GetPlayerView(GameState authoritativeState, PlayerId viewer);
  }
  ```

  - `SilverGame` implements it; UNO does **not** and behaves exactly as today.
  - The Game Service calls it when the engine supports it: project in `GetGameStateQueryHandler`, the `ProcessGameActionCommandHandler` response, `ReconnectPlayerCommandHandler`, and — instead of one group broadcast — send per-seat projected states to each `GamePlayer`'s tracked `ConnectionId` in `GameRealTimeNotifier` when the engine implements the interface. No Silver-specific code appears in the Game Service; it only knows "engine projects per viewer".
  - Record this capability as its own Critical Decision when implemented. **Rejected alternatives:** a generic visibility/annotation framework (over-engineering), Silver-specific projection services inside the Game Service (violates game-agnostic Game Service), keeping UNO's leak-everything approach (unacceptable for hidden-information play).
- Events and the shared event log must be **public-safe** (see Events); the viewer's own private information may be added in the player-view projection only.

## Randomness

- All randomness lives inside `SilverGame` (deck shuffles at game creation and between rounds). No other randomness exists.
- Clients **never** submit card identities or draw outcomes — the client requests `DrawFromDeck`; the engine draws the top card. A payload like "draw card #17" is always rejected/ignored, exactly like UNO's forged-count fix (2026-09-07). Payloads reference only village slot indexes, ability targets, and choices among things the actor legitimately knows.
- The deck's order is persisted inside `SilverState`, so draws are deterministic between actions; the `Random` instance itself is never serialized (same as UNO's `Deck`).

## Serialization

- `GameState.Data["SilverState"]` = JSON string of `SilverState` (villages with card instances `{id, value, faceUp, protected}`, deck order, discard pile, per-player knowledge maps, round number, cumulative scores, vote/final-turn state, timer config + per-player accounting, event log).
- `GameState.Data["PlayerNames"]` = JSON string of the userId → display-name map, rehydrated in `ProcessAction` — same pattern as UNO (2026-09-05 decision).
- Read `Data` exclusively via `GameState.TryGetString` — plain strings become `JsonElement` after a persist/load round-trip (2026-09-06 decision).
- Nothing outside `Data` is Silver-specific; no Silver-only serialization mechanism. The round-trip contract `CreateGame → ToJson → FromJson → ProcessAction` must work repeatedly (verified in the harness).
- A restarted Game Service reconstructs the full game — including knowledge and deck order — from `CurrentStateJson` (PostgreSQL) or Redis; no additional storage.

## Events

- Emit events through the persistent `EventLog` inside `SilverState`, using JSON envelopes `{"c":"code","d":{params}}` via a `SilverEvent.Build` helper (mirrors `UnoEvent`); the client renders them through the `events.silver.*` dictionary. Legacy plain-text entries continue to render unchanged.
- Suggested codes (namespacing keeps the catalog tidy): `silver.roundStarted`, `silver.cardDrawn`, `silver.cardDiscarded`, `silver.exchanged`, `silver.abilityUsed`, `silver.cardRevealed`, `silver.voteCalled`, `silver.roundScored`, `silver.roundEnded`, `silver.gameFinished`.
- **Events must not leak private information.** Bad: `Player X drew Werewolf 7`. Good: `Player X drew a card`. Ability peeks emit nothing to the shared log beyond an anonymous entry (e.g. `Player X looked at a card`); the peeking player's actual knowledge changes only inside their own projection.

## Timers

- Reuse the generic mechanism **unchanged**: the engine sets `GameState.NextActionDeadlineUtc` each turn and `GameState.GameEndsAtUtc` at creation; the Game Service's `TurnTimeoutService` sweeps deadlines and dispatches `TurnTimeout` / `GameTimeExpired` through the normal engine/persist/broadcast path. No Silver timer service.
- Silver-specific timer configuration lives in the game's `Timer` settings key, using the same shape/keys as UNO (`BaseTurnSeconds`, `MaxBankSeconds`, `MaxOverrunSeconds`, `MaxAfkTurns`, `TotalGameTimeMinutes`; Silver defaults 90/180/15/3/60 per owner directive 2026-09-12 — 90 seconds per turn, total allowance capped at 180) with per-player accounting inside `SilverState` (bank, deferred penalty, consecutive timeouts — mirror `UnoTurnTimerConfig`/`UnoPlayerTimer`).
- The engine owns what a timeout means for Silver: skip the current player's turn (resolving any pending drawn-card decision per the interpretation list), count consecutive timeouts, eliminate AFK seats per the interpretation list, and force-finish on game-time expiry by applying the current standings (fewest cumulative points wins; tie resolved by the normal amulet/seat tie-break; a fully unresolvable tie is a draw with `Winner = null`).

## Reconnection

- Existing mechanisms work unchanged: SignalR auto-reconnect + `joinSession` re-registration, single-tab `SessionTakenOver` enforcement, `POST /sessions/{id}/reconnect`, and the lobby's active-games recovery via `GET /api/game/sessions/mine`. No Silver-specific reconnection code.
- On reconnect the player receives **their own projected view** — including their accumulated private knowledge — and never another player's hidden cards (the player-view capability handles this; verify it in the hidden-information tests).
- Mid-decision reconnections (drawn-card sub-phase, ability targeting) recover from the state's sub-phase markers; peeks already taken remain known; another player's turn or round scoring presents the normal spectator-safe projection.

## Frontend/UI requirements

Follow the existing React + Vite architecture and UNO's file conventions — no Silver-specific backend endpoints, no duplicated infrastructure:

- `frontend/src/features/game/silver.ts` — typed `SilverState` mirror, `parseSilverState(state)` (extracts `data.SilverState` JSON string), card/ability helpers.
- `frontend/src/features/game/SilverGameView.tsx` (+ small focused subcomponents) and a Silver card visual component under `shared/components/`.
- Wire it into `GamePage.tsx`'s view switch (`currentState?.gameType === 'Silver'`), exactly like the UNO branch.
- `frontend/src/features/lobby/gameMeta.ts`: add `Silver` to `GAME_THEME` (gradient, `comingSoon` **omitted**) — the room catalog automatically offers Silver because `GET /api/game/games` reflects DI registrations.
- The UI must present: the player's own five-card village (distinguishing face-up / face-down-but-known / face-down-unknown / protected), other players' villages (card backs for hidden cards — values must never be rendered), the discard top, the draw-pile count, current player, round number, cumulative scores, available actions, ability targeting (slot/player pickers), the call-vote interaction (enabled at ≤4 cards), the round-scoring reveal, and the game-end standings, plus the turn timer ring (reuse the UNO `TimerRing`/`useNow` pattern; the client derives the soft deadline from state exactly as UNO does).
- RTL/EN-FA layout rules from the i18n decision apply (logical utilities `ms-`/`me-`/`start-`/`end-`, `dir` mirroring).

## Localization

- All Silver user-facing strings exist in **both** `locales/en.ts` and `locales/fa.ts` (the `fa: Dict` type makes a missing Farsi key a compile error): a `games.Silver` section with the same shape as `games.UNO` (title, tagline, description, `rules[]` with valid `RuleIconName` icons, `cardNames` for all 14 characters, `actionCards`).
- Event-log envelopes render via `events.silver.*` keys; card names/values referenced by event params resolve through `games.Silver.cardNames`.
- Engine error codes use a Silver `Errors` class with `silver.*` codes (mirror UNO's `Errors.cs`); **every** code must be added to both `BuildingBlocks.Domain/Localization/errors.en.json` and `errors.fa.json` so the server-side catalog localizes them via `X-Language`/`Accept-Language`. Client-side `backendMessages.ts` remains only as legacy fallback.

## Persistence

- The Game Service persists exactly what it persists today: `GameSession` (`CurrentStateJson`), `GamePlayer`, `GameActionLog`, Redis state cache. **No Silver database, no Silver tables.** All Silver state — including private cards and knowledge — lives inside the serialized `GameState.Data["SilverState"]`.
- Every action is appended to `GameActionLog` by the existing handler; Silver needs no extra auditing.

## Testing expectations

UNO was validated with scratch harnesses; Silver's rules surface is larger (hidden info + scoring), so the implementing agent must verify — with a scratch console harness or a dedicated test project — that the following all pass against the **real rules** (no placeholder assertions), and record the verification in the Critical Decisions entry:

- **Setup:** 2/3/4-player validation and rejection outside the range; deck = 52 cards with the exact composition table; 5-card face-down villages; one face-up discard; remainder in the draw pile; random start player; the two-peek right per player per round.
- **Turn flow:** valid deck draw → discard-with-ability and exchange paths; mandatory exchange on take-discard; single and multi exchanges; mismatch return-to-face-down; 3+ mismatch penalty card; drawn card not discardable-as-set same turn; wrong-player/wrong-phase/invalid-slot rejections; clockwise advancement; end-of-turn effects.
- **Abilities:** for **every** card value 0–13: valid use, invalid use, target validation (including protected-card rejection), resulting state deltas, and activation-timing enforcement (5–12 only on deck-draw discard; 2–4 own village; none on a call turn; none for discard-taken 5–12 cards).
- **Hidden information:** player A's projected view contains no information about B's face-down cards and vice versa; peeks update only the peeker's knowledge; reconnect preserves own knowledge without leaking others'; shared events/log never contain hidden values.
- **Round ending:** valid call at ≤4 cards; call rejected at 5+; each opponent gets exactly one extra turn (and cannot call); Villager double-reveal round end; deck-depletion round end; scoring math (sums, caller 0, failed call +10); round-lowest ties and amulet award/start player; next-round redeal with fresh peeks.
- **Persistence:** `Create → Serialize → Deserialize → Continue` across multiple actions with knowledge and deck order intact.
- **Randomness:** client payloads cannot influence the drawn card (server always draws the top card).
- **Game completion:** exactly four rounds; fewest cumulative points wins; amulet/seat tie-breaks; `GameTimeExpired` force-finish correctness.

## Game registration and integration checklist

Implementation must not require changes to Lobby, Identity, Gateway, BuildingBlocks, or the generic engine model beyond the documented optional player-view capability:

1. New `backend/src/Games/Silver` project (net9.0, references **only** `GameEngine.Core`, added to `BoardGamePlatform.sln`).
2. `SilverGame : IGame` — `GameType => "Silver"`, `MinPlayers => 2`, `MaxPlayers => 4`.
3. Register in `Game.Infrastructure/Extensions/ServiceCollectionExtensions.cs` next to UNO: `services.AddSingleton<GameEngine.Core.IGame, Silver.SilverGame>();` — the `GET /api/game/games` catalog and `GameEngineProvider` resolution then work with zero further changes (the DI-registration decision stands; no `SilverGameFactory`, no runtime plugin loading).
4. Add `silver.*` error codes to `ErrorCodes` + both localization catalogs.
5. Frontend: `silver.ts`, `SilverGameView.tsx`, `GamePage` switch, `GAME_THEME` entry, `games.Silver` + `events.silver.*` in both locale files.
6. Verification harness per Testing expectations; record results and any resolved interpretation decisions under Critical Decisions (Silver) below.

---

# Critical Decisions (Silver)

Game-specific decisions only; platform decisions live in the root `AGENTS.md`.

<!-- Add new Silver decisions below this line -->

- **2026-09-10** — Next game implementation: Silver
  - **Context:** UNO has been implemented and validated as the first game, proving the engine/lobby/frontend pipeline. The next game should expand the platform's game-engine capabilities while remaining reasonably simple to implement. The 2026-09-05 decision had anticipated Splendor as the second game; that expectation is superseded.
  - **Decision:** Silver (Bézier Games, 2019 — the original standalone base game, internally the Amulet card set) is selected as the next game implementation. Silver base game only; no Silver Bullet / Silver Coin / Silver Dagger decks, no combining decks, no expansions, variants, or house rules unless separately approved. The complete rules, action model, hidden-information requirements, and integration checklist live in the **Silver — Next Game Implementation** section of this document. One small generic engine capability is anticipated and specified there: an optional player-specific state view (`GetPlayerView`) so hidden-information games can project per-viewer state — it must be recorded as its own Critical Decision when implemented.
  - **Rationale:** Silver exercises engine capabilities UNO never touched while staying far simpler than large Eurogames (Splendor/Azul/Wingspan/Terraforming Mars): hidden information and private player state (memory element), per-player knowledge tracking, card abilities with strict activation timings, draw/discard decisions, exchange/replacement mechanics (single and multi-card), player interaction (Witch/Robber target other villages), round-ending calls with penalties, and scoring across four rounds with amulet/tie-break rules. It is a useful architectural test after UNO — above all for player-specific state projection, which today's full-state broadcast architecture does not provide — and it requires no changes to the platform's microservice boundaries: the engine stays pure, the Game Service stays game-agnostic, and registration is one DI line under the existing DI-registration/no-plugin-loading decisions.

- **2026-09-11** — Silver backend implemented and verified
  - **Context:** The Silver rules specification (2026-09-10) required the backend engine, Game Service registration, error-code localization, and a verification harness before any frontend work.
  - **Decision:** Implemented the full Silver backend: (1) `Games/Silver` (net9.0, references only `GameEngine.Core`, added to the solution) with `SilverGame : IGame, IPlayerViewGame` (partial classes: core/rounds/scoring, turn actions, abilities+system actions, player view, valid actions), `SilverState` (villages, deck order, discard, display area, pending draw, phases `TurnStart/RascalChoice/DrawnDecision/ExchangeDecision/AbilityPending`, vote state, amulet state, cumulative scores, per-card-instance knowledge maps, per-round peek counters, UNO-style timer config/accounting), `SilverCard {Id, Value, FaceUp, Protected}`, `SilverView`/`SilverViewCard` (player-view model), `SilverEvent` (JSON envelopes `{"c":"code","d":{params}}`), and `Errors` (25 `silver.*` codes added to `errors.en.json`/`errors.fa.json`; game codes stay in the game project's `Errors` class, mirroring UNO — not in BuildingBlocks' `ErrorCodes`). (2) Registered via `services.AddSingleton<GameEngine.Core.IGame, Silver.SilverGame>()` next to UNO; the `GET /api/game/games` catalog picks it up with no further changes. (3) Rules enforced per the spec: 52-card Amulet deck, 5-card villages, two initial peeks (async, ≤2/round, any player), draw/discard/exchange turn flow with the drawn-card decision sub-phase, mandatory exchange on take-discard, multi-exchange matching sets (Doppelgänger wild), mismatch returning all cards face down with everyone learning the values and a 3+ penalty card, vote calling at ≤4 cards with exactly one final turn per opponent, round ends via vote/depletion/double face-up Villager, scoring (caller 0 or +10), amulet award + clockwise tie-breaks, four rounds, force-finish on `GameTimeExpired` from completed-round standings, and UNO-mirroring turn timers (45/120/15/3/60 defaults, hard deadline = allowance + grace, AFK elimination: 2p → survivor wins instantly, 3-4p → seat removed and village still scored). (4) Verification: `tests/Silver.Harness` (console project in the solution) runs 168 checks covering setup/composition, turn flow, every ability 0–13 (valid/invalid/target/timing), hidden-information projections (no leaks, reconnect-safe), round ending/scoring/ties/amulet, persistence round-trips, unforgeable randomness, and game completion — **168/168 passing, run 4×**. The harness found and I fixed two real engine bugs: turn advancement was missing for non-timeout turn-ending actions, and `FirstClockwiseFrom` used the tied-candidate count instead of the player count for clockwise tie-breaks.
  - **Rationale:** Interpretation decisions resolved per the spec's prescribed defaults, now final: Rascal's unchosen card goes face down to the deck bottom; Squire's displayed cards are public and out of play for the round; Master's taken discard card enters face up; Witch's viewed deck card enters face down, known only to the Witch player; Bodyguard/amulet protection blocks Witch/Robber (view and move); a timeout with a pending deck-draw discards the drawn card without its ability (a pending discard-taken card returns to the discard top) and the turn advances; amulet placement is a free action that does not end the turn but blocks calling a vote that turn. Additional documented choices: mismatched exchange sets reveal all their values to every player (the flip-to-prove is public); knowledge is tracked per card instance and survives slot collapsing; a call followed by deck depletion still scores the caller as caller; initial peeks and ability peeks emit no shared event-log entries; `GetValidActions` is the engine-level contract used by the harness (not exposed over HTTP), enumerating concrete legal actions including matching exchange subsets. The frontend view and its localization entries are recorded in the following decision.

- **2026-09-11** — Silver frontend view implemented
  - **Context:** The Silver backend (engine + player-view projection + registration + localization catalog + verification harness) was complete and the frontend checklist from the Silver spec remained: a typed state mirror, a game view, the `GamePage` switch, theme, and EN/FA copy. UI/UX was called out as a priority.
  - **Decision:** Implemented the Silver frontend following the UNO conventions, consuming only the per-viewer projected `GameState` (`data.SilverState` parses into a `SilverView`-shaped mirror in `features/game/silver.ts`). Components: `shared/components/SilverCardVisual.tsx` (face rendered from the projection — a `null` value is a card back, a known face-down card is a dimmed face with a memory-eye badge, protected cards carry a Silver Amulet pendant; tier palettes per the 0–1/2–4/5–12/13 ability families over a moonlit-night table), and `features/game/SilverGameView.tsx` (opponent seats with mini-villages and score chips, deck/discard/Squire-display center, round-progress moons, and the full interaction flow: async peeks (2/round), draw→discard-or-exchange with the drawn card shown, mandatory exchange after taking the discard, single/multi exchange with client-computed match status (Doppelgänger wild, unknowns flagged "uncertain"), the 5–12 ability window with per-ability targeting — Exposer/Revealer/Beholder/Apprentice Seer/Seer target hidden cards, Witch/Robber/Master cross-village targeting with a Master discard-picker modal (full discard values are public — every discard was face up), Rascal two-card choice, Bodyguard protect/unprotect, Silver Amulet placement, and vote calling with confirmation) + status strip with the shared soft/hard-deadline timer math and game clock + scoreboard with last-round/caller/amulet markers + `formatSilverEvent` rendering the `silver.*` envelopes (values and abilities through localized card names, scores joined per seat). `GamePage.tsx` routes `gameType === 'Silver'`; `gameMeta.ts` adds the theme without `comingSoon`; `games.Silver` (14 character names, rules, action cards), a `silver:` view-string section, and `events.silver.*` were added to **both** `locales/en.ts` and `locales/fa.ts` with full parity (`fa: Dict` makes gaps a compile error). All layout uses logical utilities (`ms-`/`me-`/`start-`/`end-`, `marginInlineStart` for the village fan) so RTL mirrors correctly. No new backend endpoints; only generic game-session/state/action/SignalR infrastructure is used.
  - **Rationale:** Matches the spec's frontend requirements exactly and keeps the engine authoritative — the UI never renders a hidden value because the projection never sends one (the harness's hidden-information checks prove that). Documented UX choices: interaction modes reset on every accepted action's state-version bump; the game-clock chip and timer ring reuse UNO's proven countdown pattern; a small `SilverView` addition (`DiscardPile` with public values, `AbilitiesUsedThisTurn`, `ActedThisTurn` — all physically observable at the table) was made to the projection so Master targeting and vote gating are precise client-side. Verification: `npm run build` (tsc + vite) is green; the repo's `npm run lint` fails for lack of an ESLint config — a pre-existing condition unrelated to this work, left untouched.

- **2026-09-12** — Silver rules re-audit against the owner's exact-rules specification (supersedes parts of the 2026-09-11 decisions)
  - **Context:** The project owner supplied a line-by-line official-rules specification (PDF-translated, §1–§39) and asked for a gap audit. The audit found the 2026-09-11 implementation deviated on card naming, the Squire/Trickster/Guard/Robber/Witch/Master/Revealer/Doppelgänger behaviors, setup card removal, the initial Amulet holder, failed-replacement orientation restore, and census terminology. One point — §9's "the replacement card becomes face-up" — conflicted with the 2026-09-11 face-down interpretation; the owner explicitly ruled §9 authoritative (all replacement cards enter villages face up). The optional 100-point scoring mode and Kamikaze rule were later declared not needed by the owner (out of scope).
  - **Decision:** Rewrote the Silver engine to the exact-rules spec: (1) **Naming/identity:** characters are now Villager, Squire, Enchanter(2), Guard(3), Trickster(4), Exposer(5), Revealer(6), Apprentice Seer(7 = peek two own), Seer(8 = peek one opponent), Beholder(9 = peek one anywhere), Master(10), Witch(11), Robber(12), Doppelgänger(13); `SilverAbility`/`TurnPhase.TricksterChoice`/census fields renamed accordingly ("vote" remains only in the stored `silver.voteCalled` event code for log compatibility). (2) **Setup:** each round removes `5×(4−players)` cards from the game (deck+discard is always 32); the Silver Amulet is assigned to the round-1 starting player; optional deterministic testing via a `Seed` game-settings integer (seeded shuffles/deals; no seed = per-shuffle `Random` as before). (3) **Squire:** passive — after every turn the display area auto-refills with one revealed deck card per face-up Squire anywhere (reveal as many as the deck allows); new `TakeSquireCard` action takes a display card instead of drawing (mandatory face-up replacement, no ability); the old once-per-turn Squire ability is removed. (4) **Trickster:** `DrawFromDeck{TricksterExtra=k}` draws 1+k cards (k ≤ face-up Tricksters, clamped to deck); unchosen cards return to the TOP of the deck in drawn order (first-drawn ends on top) and grant no knowledge. (5) **Guard:** `SilverCard.Protected` replaced by an explicit `Guards: guardCardId→protectedCardId` map with `MoveGuard`/`RemoveGuard` actions (once per turn per Guard; links auto-drop when either card leaves a village). Guard protection blocks outsiders only — the owner may still peek/move/burn the covered card; the Silver Amulet additionally binds the owner (separate `IsAmuletProtected`). (6) **Replacements:** incoming cards always enter face up (owner §9 ruling); failed multi-sets return to their original positions **and original orientations** while every player learns the revealed values (3+ sets also draw a face-down unknown penalty card). (7) **Doppelgänger:** at most one wildcard per set (two match only each other); a village holding exactly two Doppelgängers at round end scores 13. (8) **Master:** replaces one or more own cards (matching-set rules) with any discard card; taking the Master itself is rejected; a no-slots invocation declines (Master stays in the discard). (9) **Witch:** two-step — a peek action records the deck-top value for the actor (`WitchPeekedCardId`; the card stays on top) followed by an exchange into the actor's village (single/matching set) or a single card into an opponent's village, or `SkipAbility` to decline; skipping after peeking leaves the card on the deck. (10) **Robber:** steals any uncovered opponent card face up or face down (orientation preserved on both sides); the victim does not learn the received card. (11) **Revealer:** the actor only names the opponent; the prompted player answers with the new `ChooseRevealCard` action (the only non-current-player action besides peeks; also allowed by `ReconnectPlayer`-independent projection); a timeout while prompting clears the pending choice so the game can never stall. (12) **Census:** `CallCensus` (action) / `CensusCallerIndex` / `RemainingCensusTurns`, copy updated EN/FA ("سرشماری"/"census"). The optional 100-point mode and Kamikaze rule are explicitly NOT implemented (owner directive; they were §23–24 of the supplied spec). The Game Service, Gateway, Lobby, Identity, BuildingBlocks (except two new error-catalog keys), and the engine contracts are untouched.
  - **Rationale:** The owner's specification is authoritative for rules text; where it conflicted with earlier interpretation the owner resolved it (face-up replacement) or excluded scope (optional scoring). The engine remains the single rules authority — all new sub-states (guard links, Witch peek, Revealer prompt, removed pile) live inside the serialized `SilverState`, and the player-view projection hides every secret (removed values, deck order, hidden cards, the peeked Witch card except for its peeker). Verification: `tests/Silver.Harness` rewritten around the new action model — **243 checks, 0 failed** — covering setup/removal/padding invariants, Squire reveal+refill+take, Trickster counts and top-order returns, Guard relationships and outsider/owner asymmetry, replacement orientations, double-Doppelgänger scoring, every 5-12 ability (including the two-step Witch and opponent-answered Revealer), Robber face-up steals, census flow and final turns, scoring/ties/amulet, persistence round-trips (guard links, Witch peek, pending draws), timeout resolutions of every pending state, hidden-information projections, and ValidActions enumerations. Both `dotnet build` (full solution incl. harness) and `npm run build` (tsc + vite) are green; EN/FA locale and error catalogs re-paritied (`fa: Dict` enforces it).

- **2026-09-12** — Replacement orientation follows the source (owner correction; supersedes the §9 "face-up" ruling)
  - **Context:** Playtesting showed the deck-drawn replacement card appearing face up on the opponent's screen. The 2026-09-12 re-audit had applied §9 of the supplied spec literally ("the replacement card becomes face-up") for ALL sources, which destroys the memory element for deck draws. The owner confirmed the correct rule: a card coming from the **deck** (including the Witch's peeked deck-top card) enters the village **face down, known only to the player who drew/peeked it**; cards that were already public — the **discard pile top** and **Squire-displayed** cards — enter **face up**.
  - **Decision:** `ApplyReplacement` takes an `incomingFaceUp` parameter derived from the source: `false` for `ExchangeWithDrawn` and both Witch exchanges, `true` for `ExchangeWithDiscard` (discard or Squire display) and the Master (discard-sourced). Failed-match additions follow the same source orientation; the 3+ penalty card stays face-down/unknown. Deck-sourced incoming cards are added to the drawer's knowledge map so the projection shows them to their owner only. Harness checks updated accordingly (new: opponent view cannot see a face-down replacement; the Witch victim does not learn the inserted card; the actor knows it). EN/FA hints updated ("enters face down, known only to you").
  - **Rationale:** This restores official Silver behavior and matches the 2026-09-11 interpretation decision that the re-audit had temporarily overridden. Orientation is a property of the information the card carried before entering the village, so passing it as a parameter at the single replacement core keeps every entry point consistent and testable.

- **2026-09-12** — Census final-turn countdown keys on turn ownership, not the acting seat
  - **Context:** In a live 2-player game a round hung after a census: the last-turn player used the Revealer against the census caller, the caller answered the `ChooseRevealCard` prompt (which ends the actor's turn), but `RemainingCensusTurns` never decremented — the countdown compared the acting seat against the caller, and the acting seat WAS the caller. The caller then received another turn, which a census forbids.
  - **Decision:** The countdown block in `ProcessAction` resolves the owner of the just-ended turn first (`PlayerAdvanced` timeouts → the acting seat; everything else → `CurrentPlayerIndex`, e.g. a chooser answering the actor's prompt) and decrements when that owner is not the caller. Regression check added: "census: caller answering the final-turn Revealer ends the round" (harness now 246/246). A game already in the stuck state self-heals after the fix: the caller's extra turn ends without a decrement, and the next non-caller turn ends the round.
  - **Rationale:** The census rule counts turns, and the Revealer prompt proved that an action's submitter is not always the player whose turn it completes; keying on the submitter conflated the two roles.

