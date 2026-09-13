# UNO - Game Documentation

> Extracted from `AGENTS.md` (2026-09-13). This document owns the UNO ruleset's
> implementation notes and game-specific Critical Decisions. Platform-wide
> contracts and decisions remain in the root `AGENTS.md`.

## Scope

UNO base game (implemented first, 2026-09-05): standard 108-card deck, colors
red/blue/green/yellow plus wilds, 2-10 players. No house rules, no stacking
penalties, official Wild Draw Four challenge semantics.

## Engine notes

- `Games/UNO` implements `IGame` only (no hidden-information projection: UNO
  serves full state to every client by accepted design).
- Actions: `PlayCard`, `DrawCard` (voluntary, once per turn), `Pass`,
  `AcceptDraw` (take the accumulated penalty), `ChallengeWildDrawFour`,
  `CallUno`; system actions `TurnTimeout` / `GameTimeExpired` go through the
  shared `TurnTimeoutService` sweep (see the platform timer decisions).
- Penalty ownership (`PendingDrawTargetIndex`), accumulation, and challenge
  challengeability (`PendingDrawChallengeable`) are UNO domain rules enforced
  engine-side; see the decisions below.
- `UnoGameState` is serialized into `GameState.Data["UnoState"]` (read via
  `TryGetString`), with `PlayerNames` as a separate `Data` key; events go
  through the `UnoEvent` JSON-envelope log rendered by `events.uno.*`.
- Timers reuse the platform mechanism with UNO defaults 30/120/15/3/60
  (base/bank/overrun/AFK/game-minutes; allowance = min(120, base + bank - penalty)).

## Frontend

`frontend/src/features/game/uno.ts` (state mirror + `formatEvent`) and
`UnoGameView.tsx`; the pending-draw banner, +4 challenge hint, challenge-result
toasts, and the deck draw-flash are presentation of the engine events.

---

# Critical Decisions (UNO)

Game-specific decisions only; platform decisions live in the root `AGENTS.md`.

- **2026-09-05** — First game implementation: UNO
  - **Context:** Need to choose the first game to implement in Phase 2. The original spec mentioned Splendor, but UNO was chosen instead.
  - **Decision:** Implement UNO (base game, no expansions) as the first game instead of Splendor.
  - **Rationale:** UNO has simpler rules (no complex card interactions like Splendor's nobles/tokens), well-known mechanics, and is easier to validate the platform's game engine infrastructure. The turn-based structure maps cleanly to the `IGame` interface. Splendor will follow as the second game. *(Superseded 2026-09-10: Silver is the second game — see the 2026-09-10 decision below.)*

- **2026-09-05** — UNO `Card` is a readonly struct; display names persist in `GameState.Data`
  - **Context:** During the UNO implementation, `Card` was modeled as a `readonly record struct`. Code used `(Card?)null` for a nullable card, and attempted to access `topCardBeforeWild!.Color`. The `!` null-forgiving operator suppresses nullable *warnings* but does NOT unwrap `Nullable<T>` for a struct, producing `CS1061: 'Card?' does not contain a definition for 'Color'`. Separately, `UnoGameState.PlayerNames`/`PlayerIds` are `[JsonIgnore]` (never serialized), so the display-name map was silently lost after the first action.
  - **Decision:** (1) For nullable struct members, access via `.Value` (or the null-checked value) instead of `!`. (2) `PlayerNames` is persisted beside the serialized `UnoState` as the `PlayerNames` key in `GameState.Data`; `ProcessAction` rehydrates it into `UnoGameState` before processing. `PlayerIds` is always re-derived from `state.Players`.
  - **Rationale:** `.Value` on `Nullable<T>` is the only correct way to access members of a nullable struct; `!` only silences analyzer warnings. Persisting names in `GameState.Data` keeps display names accurate across actions while keeping `UnoGameState`'s runtime-only dictionaries unserialized. Avoided serializing `PlayerNames` inside `UnoState` itself to keep the persisted state free of private user-id → display-name mappings.

- **2026-09-07** — Draw debts survive turn skips and accumulate; ownership-tracked penalties
  - **Context:** With the timeout system, a player hit with +2/+4 could dodge the penalty entirely by timing out (the skip previously auto-drew it), and the "can't play while pending" guard wrongly blocked EVERY player — including the penalizer on their next turn. Requested semantics: the debt survives the skip, the debtor still owes it on their next turn and cannot play until it is accepted, penalties from later rounds accumulate (+2 then +4 → draw 6), and such merged debts cannot be challenged.
  - **Decision:** New `UnoGameState.PendingDrawTargetIndex` records who owes the debt (set after `AdvancePlayer` when a Draw Two/WDF is played; also for the initial-card Draw Two). The play/draw/challenge/accept guards now apply only when the target is the current player (`null` = legacy state, conservatively treated as "current player owes"). `ProcessTurnTimeout` no longer auto-draws the debt — it survives the skip and keeps its target; if the debtor is AFK-eliminated, a challenge-offender debt is paid by the offender immediately, otherwise the debt is dropped. `ProcessAcceptDraw` enforces ownership, accepts the full accumulated amount, and forfeits the turn. New `PendingDrawChallengeable` flag is true only when the debt is exactly a freshly played Wild Draw Four's 4 cards (a WDF stacked onto existing debt sets it false, so merged debts cannot be challenged); challenge offered/rejected accordingly. Frontend: `iOweDraw` (target == me or null) gates the hand, draw/pass buttons, and the pending banner; other players play normally while someone's debt is outstanding.
  - **Rationale:** Timeout-skip must never erase a penalty, and the penalizer must be able to keep playing (their turn is not the debtor's). Ownership tracking is the minimal way to scope the guards; the challengeable flag prevents challenging a merged debt where 2 of the 4 cards came from an unrelated +2. Verified with a 25-check scratch harness reproducing the exact reported scenario (+2 → skip → +4 → skip → 6 owed, no challenge, accept clears and forfeits) plus the pure-+4 control (challengeable).

- **2026-09-07** — Classic-rule draw penalties: no playing while owing, official +4 challenge, one draw + Pass per turn
  - **Context:** Critical bug: when a player played a +2/+4, the next player could simply **play a card** (and a played +2 even stacked via `PendingDrawCount += 2`), transferring the penalty onward — `ProcessPlayCard` never checked `PendingDrawCount`. Related rule breaks: the WDF challenge set `PendingDrawCount = 6` for **both** outcomes (official: success → offender draws 4, challenger keeps the turn; failure → challenger draws 6); a penalty draw let the drawer keep the turn when a drawn card was playable; and `GetValidActions` offered `DrawCard` unconditionally (`|| true`), enabling infinite re-draws.
  - **Decision:** Classic UNO rules, no stacking: (1) `ProcessPlayCard`/voluntary `DrawCard` are rejected while `PendingDrawCount > 0`; `GetValidActions` offers only `AcceptDraw` (+ `ChallengeWildDrawFour` when pending == 4). (2) Challenge success records `PendingDrawOffenderIndex`; the subsequent `AcceptDraw` draws the 4 cards into the **offender's** hand and the challenger keeps the turn. Failure keeps the challenger drawing 6 and forfeiting the turn. A challenge may be decided **once**: `ProcessChallengeWildDrawFour` rejects attempts while `PendingDrawOffenderIndex` is set (repeats otherwise re-run the challenge and only spam the log). (2026-09-07 refinement) The challenge now **resolves immediately** — success draws the 4 into the offender's hand right away (challenger keeps the turn), failure draws 6 into the challenger's hand and forfeits the turn — removing the intermediate "resolved but not applied" state whose stale-UI repeat clicks surfaced 409s. (3) A penalty `AcceptDraw` (or a `TurnTimeout` with a pending penalty — the penalty is enforced even on skip) always forfeits the turn; drawn penalty cards may not be played. (4) New `DrawnThisTurn` flag (reset in `AdvancePlayer`): one voluntary draw per turn, and a new `Pass` action ends the turn after drawing instead of playing. (5) Refinement: an **unplayable** drawn card auto-passes inside `ProcessDrawCard` (turn ends immediately, no manual Pass); `Pass` remains only for declining a *playable* drawn card. Frontend: cards render unclickable while a penalty is pending, the Challenge button hides once the challenge is resolved, and the banner names the offender with an "Apply draw & continue" action.
  - **Rationale:** The play-through-pending hole let players dodge +2/+4 entirely; official challenge semantics prevent the 6-either-way exploit; the draw flag removes the draw-again loop and gives the classic "draw, then play or pass" choice a concrete action. Verified with a 34-check scratch harness (pending rejections, exact draw counts, offender/challenger hand deltas, turn advance/keep, timeout-with-penalty, DrawnThisTurn lifecycle).
