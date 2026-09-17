# Azul - Game Documentation

> **Status: fully implemented (backend 2026-09-15; frontend 2026-09-16).**
> Backend: engine (`Games/Azul`, `IGame` + `IPlayerViewGame`), Game Service DI
> registration, `azul.*` EN/FA error catalogs, and a 100-check verification
> harness (`tests/Azul.Harness`, all passing).
> Frontend: `azul.ts` mirror + parser + leak guard, `AzulGameView.tsx` +
> `AzulBoardVisuals.tsx` (original SVG art — Persian haft-rangi tiles, factory
> discs, boards, physical wall mosaic on the empty holes; §27-§30
> physical-board layout),
> §29 animation layer (`azulFlights.ts` state-diff tile flights, wall glides,
> round veil, score floats, floor flash, marker hand-off, Esc cancel),
> GamePage switch, `GAME_THEME`, lobby/room/auth decorations, and full EN/FA
> dictionary copy (`games.Azul`, `azul.*`, `events.azul.*`, `fa: Dict` parity).
> This file remains the authoritative rules and integration specification.
>
> This document owns everything about the Azul ruleset:
> - authoritative rules specification and ruleset basis
> - component inventory and exact tile/color model
> - setup, turn structure, action semantics
> - pattern-line / wall / floor mechanics
> - scoring (draft-time, end-of-round, end-of-game) with worked examples
> - information visibility, randomness
> - game state design, action model, valid-action generation
> - state invariants and tile conservation
> - persistence/serialization, events, timers, reconnection, concurrency
> - frontend requirements (physical-board-faithful UI as a first-class requirement)
> - localization, error model, testing specification
> - edge-case compendium, integration checklist, rules audit trail
> - game-specific Critical Decisions
>
> Platform-wide contracts (IGame, player-view capability, error localization,
> SignalR topology, timers, integration checklist) remain in the root `AGENTS.md`.

---

## Table of contents

1. [Scope and identity](#1-scope-and-identity)
2. [Design philosophy and architectural constraints](#2-design-philosophy-and-architectural-constraints)
3. [Exact component inventory](#3-exact-component-inventory)
4. [Tile / color model](#4-tile--color-model)
5. [Setup](#5-setup)
6. [Physical-board mental model](#6-physical-board-mental-model)
7. [Round and turn structure](#7-round-and-turn-structure)
8. [Drafting action semantics (complete)](#8-drafting-action-semantics-complete)
9. [Pattern lines](#9-pattern-lines)
10. [Wall placement](#10-wall-placement)
11. [Floor line](#11-floor-line)
12. [Scoring](#12-scoring)
13. [Round lifecycle](#13-round-lifecycle)
14. [Game end, final scoring, and tie-breaks](#14-game-end-final-scoring-and-tie-breaks)
15. [First-player marker](#15-first-player-marker)
16. [Information visibility model](#16-information-visibility-model)
17. [Randomness](#17-randomness)
18. [Game state design](#18-game-state-design)
19. [GameAction model](#19-gameaction-model)
20. [GetValidActions specification](#20-getvalidactions-specification)
21. [State invariants](#21-state-invariants)
22. [Persistence and serialization](#22-persistence-and-serialization)
23. [Events](#23-events)
24. [Timers](#24-timers)
25. [Reconnection](#25-reconnection)
26. [Concurrency, duplicates, and stale actions](#26-concurrency-duplicates-and-stale-actions)
27. [Frontend: physical-board experience (first-class requirement)](#27-frontend-physical-board-experience-first-class-requirement)
28. [Frontend: board layout](#28-frontend-board-layout)
29. [Frontend: tile interaction and animations](#29-frontend-tile-interaction-and-animations)
30. [Frontend: UI states and responsive layout](#30-frontend-ui-states-and-responsive-layout)
31. [Localization](#31-localization)
32. [Error model](#32-error-model)
33. [Testing specification](#33-testing-specification)
34. [Edge-case compendium](#34-edge-case-compendium)
35. [Integration checklist](#35-integration-checklist)
36. [Rules audit trail and authoritative sources](#36-rules-audit-trail-and-authoritative-sources)
37. [Critical Decisions (Azul)](#critical-decisions-azul)

---

## 1. Scope and identity

- **Game:** *Azul* — designer **Michael Kiesling**, illustrators **Philippe Guérin / Chris
  Quilliams**, publisher **Plan B Games (2017) / Next Move Games (2018+)**, 2018 *Spiel des Jahres* winner.
- **Target:** the **classic/base Azul only** (the 2017 ruleset documented here).
- **Players:** **2–4**. `AzulGame.MinPlayers => 2`, `AzulGame.MaxPlayers => 4`; `CreateGame`
  rejects any other configured player count (the Lobby already caps rooms at `MaxPlayers`
  from the game catalog).
- **Objective:** draft colored tiles from the "factories" and decorate the wall of the
  Palace of Evora with the most prestige points across 5-color rows and columns.
- **Expected duration:** 30–45 minutes.
- **Core loop:** draft → place into a pattern line → (round end) completed lines tile the
  wall and score → refill factories → repeat until someone completes a full wall row.

### In scope

The complete base-game ruleset on the **colored-wall side** of the player boards
(the classic two-sided boards): fixed wall positions per pattern line (not the gray
free-placement variant — see §36 and OD-5), five tile colors, 2–4 players.

### Explicitly out of scope (never added without a new decision in §Critical Decisions)

- **Azul: Stained Glass of Sintra**, **Azul: Summer Pavilion**, **Azul: Queen's Garden**,
  **Azul: Master Chocolatier**, **Azul Duel**, **5211: Azul**, the **Azul Mini** sets.
- The **Crystal Mosaic** expansion (joker tiles) — even though it is "base Azul compatible",
  it changes the tile economy and is excluded.
- The **gray/advanced player-board side** (free wall placement within row/column color
  constraints) — see OD-5; the implementation uses the fixed blue/cyan side only.
- Fan variants, house rules, tournament rules, 1-player "solo automa" modes, and any
  rule not present in this document's components/tables.

### Ruleset interpretation policy

§§3–15 transcribe the official rules as documented by the sources listed in §36. Where
independent authoritative summaries disagree or are silent, the choice is stated in the
text, marked **OD-n** (§36), and recorded as a Critical Decision — never applied silently.
An implementation agent must keep every OD decision and, per §35 step 8, verify the OD list
against a printed/publisher rulebook PDF before shipping.

---

## 2. Design philosophy and architectural constraints

Azul is **one more game-specific implementation of the existing `IGame` contract** — like
UNO/Silver/Splendor before it, with zero platform changes:

```text
Game Service → IGame → AzulGame → GameState.Data["AzulState"] (JSON string)
```

The implementation must:

- Implement `GameEngine.Core.IGame` (`GameType`, `MinPlayers`, `MaxPlayers`, `CreateGame`,
  `ProcessAction` (non-mutating), `GetValidActions`, `IsGameOver`, `GetWinner`) and also
  `GameEngine.Core.IPlayerViewGame` **solely** to strip the hidden bag order from
  broadcasts — Azul's *board* is 100 % public (every factory, the center, all boards, all
  scores), so unlike Silver there is **no per-viewer state and no knowledge to track**: the
  projection is identical for every viewer (the Splendor model, §16). Implementing the
  capability is required, not optional: without it the group broadcast would leak the
  100-tile draw sequence (card counting is real strategy here).
- Live entirely under `backend/src/Games/Azul/`, referencing **only** `GameEngine.Core`
  (mirroring `Games/UNO`, `Games/Silver`, `Games/Splendor`).

The engine project must contain **no**:

- ASP.NET Core / HTTP / controllers, SignalR / hubs, EF Core / PostgreSQL / Redis /
  RabbitMQ references, frontend code.
- `AzulService`, `AzulRepository`, `AzulController`, `AzulDbContext`, `AzulHub`, or any
  service/abstraction the platform does not already have.
- Generic abstractions: no `IBoard`, `ITile`, `IGamePiece`, `ResourceToken`, or shared
  "tile-drafting engine" layer. Azul stays Azul: tiles are `int` color indices, factories
  are lists, boards are Azul-specific records. The superficial similarity to Splendor
  (market + drafting) is **not** a reason to abstract (§48).

`CreateGame` throws `ArgumentException` on invalid player count (Silver/Splendor pattern);
all *action-time* problems return `GameResult.Failure` with `azul.*` error codes
(§32), never exceptions.

### Anti-copy warnings for the implementation agent

These are the mistakes of importing UNO/Silver/Splendor habits into Azul:

| Assumption from shipped games | Reality in Azul |
|---|---|
| Splendor: actions are pure engine calls, no sub-phase | Azul also has **no sub-phases** — but for a different reason: round-end (wall tiling, scoring, refill) is **not a player decision point**; it runs automatically inside the final drafting action of the round (§13). |
| Splendor: hidden deck order → projection | Azul: only the **bag order** is hidden (and re-shuffle on discard refill); the board itself is fully public — implement the same viewer-independent projection mechanism, nothing more. |
| Silver/Splendor timers with bank/accounting | Same platform mechanism, and Azul's timeout behavior is **pass** = draft from center if possible, else the turn is skipped — see §24: an explicit deterministic auto-draft is NOT allowed (only a skip, which can never advance the game unfairly? — decided in OD-3; default = timeout **skips** the turn, no forced draft). |
| "Game ends when X is drafted" | Azul's end trigger is evaluated **only in the tiling phase** at round end (§14) — never mid-draft. |
| A move can be a draw + a separate play | Azul: one draft **must** immediately place all kept tiles in **one** pattern line (plus forced floor/discard) in the same action. Never model "hand" state between draft and place. |
| Finite deck with reshuffle | Azul has **no deck** — a bag with refill from discard/unused (§4, §13). |

---

## 3. Exact component inventory

Per the official base game (German Wikipedia, citing the Pegasus/Plan B rulebook; BGA help):

| Component | Count | Notes |
|---|---:|---|
| Tile bag (cloth) | 1 | Server-side: shuffled draw list. |
| Tiles (resin "azulejos") | **100** | **20 of each of 5 colors** — see §4. |
| Factory displays ("Manufakturplättchen", round discs) | **9** | Only **5 (2p) / 7 (3p) / 9 (4p)** are in play; each holds up to 4 tiles. Unused physical discs are simply not used; the engine models only the active count. |
| Player boards (two-sided: colored wall / gray wall) | 4 | Only **the colored (fixed) side** is in scope. Contains: 5 pattern lines (1–5 spaces), the 5×5 wall grid, 7-space floor line, and a floor-line first-player marker space. |
| Scoring markers | 4 | Start at 0; track 0–99 on the board (engine: plain `int`). |
| First-player marker | 1 | Moves between center / a player's floor-line marker space (§15). |

### Physical structure of one player board (the state that matters)

```text
Wall (5×5 grid)   ← each of the 5 wall ROWS corresponds 1:1 to a pattern line
                    (wall row 5 ↔ pattern line of length 5, …, wall row 1 ↔ length 1)
Pattern line 5:  [ ][ ][ ][ ][ ]   (5 spaces)
Pattern line 4:  [ ][ ][ ][ ]       (4 spaces)
Pattern line 3:  [ ][ ][ ]          (3 spaces)
Pattern line 2:  [ ][ ]             (2 spaces)
Pattern line 1:  [ ]                (1 space)
Floor line:      [◆][ ][ ][ ][ ][ ][ ][ ]   (marker space + 7 tile spaces)
```

Each of the 25 wall spaces is printed with the symbol of the pattern line that feeds it;
a wall row is therefore fed by exactly one pattern line and holds **at most one tile of
each color** (that constraint is the strategic core of the game, §9–§10).

### Shared table components

```text
Ring of factory displays (5/7/9)  around  Center (pile of leftover tiles, unbounded)
```

The center has no capacity limit in the rules; tiles accumulate there during a round
and return to the bag at round end (§13).

---

## 4. Tile / color model

### 4.1 Colors

Canonical enum (`SplendorColor`-style, Azul-specific):

```text
0 = Blue, 1 = Red, 2 = Yellow, 3 = Black, 4 = White
```

- Display names/colors are presentation concerns (§31); the engine uses the indices.
- **20 tiles of each color, 100 total.** Tiles are **identical within a color** — there is
  no per-tile identity (unlike Splendor's unique cards). The engine models every tile as
  its color index; a tile's identity is only its location.

### 4.2 Tile locations (the conservation domains)

Every one of the 100 tiles is at each instant in exactly one of:

```text
Bag | Factory[i] | Center | Player[p].PatternLine[l] | Player[p].Floor | Player[p].Wall
```

There is no separate "discard box" state: tiles removed from play (floor overflow beyond
7, or floor tiles at end of round) are placed into an engine **discard pool** and are
**shuffled back into the bag** when the bag cannot cover a refill (§13); the audit
invariant I-1 in §21 counts the discard pool as part of the bag-side total.

### 4.3 Randomness of tiles

All bag operations are server-side (`Random` in `AzulGame`):

- **Draw** = take from the **front** of a pre-shuffled list, i.e. `index 0` is the next drawn
  tile (the Splendor convention: `Decks ... FRONT = next drawn`; an implementation may store
  reversed with a pop-end for efficiency, but the *serialised* field is defined front-first
  so round-trip tests are unambiguous). Shuffle once, pop deterministic; the *sequence* is
  only ever exposed as concrete tiles.
- **Refill-bag** = append current discard to bag and **re-shuffle** the bag (§13).
- Clients never learn the bag order or remaining distribution: only `BagCount` survives the
  projection (§16) — the Splendor deck-order model applied to a shuffled bag.

---

## 5. Setup

`CreateGame(GameOptions options)` must perform, in this order, deterministically:

1. **Validate players:** reject counts outside 2–4 (`ArgumentException`).
2. **Bag:** build `List<int>` = twenty 0s, twenty 1s, …, twenty 4s; **shuffle** (server RNG,
   seedable §17).
3. **Factories:** instantiate `factoryCount = 5 (2p) / 7 (3p) / 9 (4p)` factory displays;
   for each, **draw 4 tiles from the bag** (in that order — first 4 drawn → factory 0, etc.).
4. **Center:** starts **empty** for all player counts (OD-1 — see §36: some summaries of 2p
   setup add 4 center tiles; the two publisher-licensed summaries fetched for this spec
   both start the center empty).
5. **Player boards (per seat):**
   - Wall: 5×5 grid, all empty.
   - Pattern lines: 5 lines, all empty.
   - Floor: 7 spaces, all empty (marker space empty or holding the marker per step 6).
   - Score: 0.
6. **First-player marker:** placed in the **Center** location. The round-1 first player =
   a **uniform-random seat chosen at creation** (platform precedent from Silver/Splendor
   random start; the physical rulebook says "whoever most recently visited Portugal", else
   arbitrary — digital adaptation decided in the implementation Critical Decision, OD-2).
   Turn order = ascending seat index from that player (wrap-around), skipping eliminated
   seats (§24).
7. **Root `GameState`:** `CurrentPlayerIndex` = first player; set `NextActionDeadlineUtc`
   (first turn) and `GameEndsAtUtc` from the timer config (§24).
8. Wrap per the platform convention: serialize this state to
   `GameState.Data["AzulState"]` (JSON **string**) + `GameState.Data["PlayerNames"]`
   (JSON string, same as UNO/Silver/Splendor) — §22.

**Setup invariants** (harness-verified):

```text
|Bag| + 4*factoryCount == 100          (center empty, boards empty)
Every factory has exactly 4 tiles      (a drawn bag can never run out here: 4*9=36 ≤ 100)
Marker location == Center
Scores all 0; walls/patterns/floors empty
```

---

## 6. Physical-board mental model

The digital implementation reproduces the **physical** board; the state structure should
keep that structure visible rather than flatten it into generic collections:

```text
Tile Bag (server RNG, hidden order)
   │  refill 4 tiles per factory at round start
   ▼
Factory Displays (5/7/9 discs × ≤4 tiles)
   │  draft: take ALL tiles of ONE color from ONE factory
   │  remaining tiles of that factory → Center
   ▼
Center (single unbounded leftover pool)
   │  draft: take ALL tiles of ONE color from the center
   │  (first center-draft of the round additionally takes the First-Player Marker)
   ▼
One chosen Pattern Line (exactly one line; single color per line; capacity 1..5)
   │  tiles that don't fit / illegal line choice →
   ▼
Floor Line (7 spaces, marker space)      ── overflow beyond 7 → discard pool (back to bag
   │                                        on the next refill that needs it)
   ▼
(end of round — tiling phase) every COMPLETE pattern line:
   one tile → the corresponding wall row's next empty space (its color),
   the other completed-line tiles → discard/back to bag;
   incomplete pattern lines KEEP their tiles into the next round.
   Floor tiles → back to bag (marker → back to center); wall tiles are permanent.

Score: immediate wall-placement scoring + end-of-round floor penalties +
       end-of-game bonuses (§12, §14).
```

Each arrow is a transition between **named state fields** in §18 — the implementation
must not shortcut these into intermediate structures (e.g. never a "player hand" between
draft and placement: drafting and placement are **one atomic action**, §19).

---

## 7. Round and turn structure

### 7.1 Round structure (official flow)

A round consists of the two phases the rulebook names:

1. **Pattern phase (drafting):** starting with the round's first player, players alternate
   turns (clockwise). Each turn = **exactly one draft action** (§8). The phase ends the
   moment **no tile remains in any factory and in the center** (all drafted/overflowed).
2. **Tiling phase (end-of-round, automatic):** in order, for every player (the rulebook's
   simultaneous phase — the engine runs it deterministically; §13 defines exact order):
   completed pattern lines tile wall spaces with immediate scoring, floor penalties apply,
   cleanup/refill happens, and the game-end check runs (§14).

The tiling phase is **not** a player-action phase — no input is awaited (§13).

### 7.2 Whose turn it is

- `CurrentPlayerIndex` (mirrored on `GameState.CurrentPlayerIndex`) is the only source of truth.
- Turn order: ascending seat index, wrapping, skipping eliminated seats (§24) — physical
  "clockwise".
- The round's first player is the holder of the first-player marker from the **previous**
  round's center drafting, or the random seat of round 1 (OD-2), or unchanged if nobody
  took the marker last round (§15).

### 7.3 Turn → next turn

Every accepted draft action: resolves placement (possibly forcing floor/discard) →
if the round's pool is now empty → run the tiling phase + refill (inside the same
`ProcessAction` call, before advancing) → advance `CurrentPlayerIndex` to the next
active seat → start the turn clock (§24). The last drafter of a round is a normal turn
boundary — there is **no special final-turn rule** (unlike Splendor's 15-point trigger:
a round ends when its pool drains, no matter who drained it; the tiling phase then ends
the round for everyone — "same number of turns" is automatic because drafting stops, and
the end trigger is checked only in tiling §14).

---

## 8. Drafting action semantics (complete)

One action per turn. The engine's atomic payload (§19) carries the whole decision:

```text
Draft(source, color, line)
  source = Factory(index) | Center
  color  = 0..4
  line   = 0..4   (the pattern line to receive the tiles)
```

### 8.1 Draft from a factory

**Prerequisites:** it is your turn; factory `index` is in the active set (index <
factoryCount); factory contains ≥1 tile of `color`.

**Resolution, in exact order:**
1. Remove **all** tiles of `color` from the factory → these are the *drafted tiles* (count k ≥ 1).
2. **All remaining tiles** of that factory (other colors) move to the **Center**. The factory
   is now empty (and stays empty for the rest of the round — it is refilled at round end).
3. Place the k drafted tiles into the chosen pattern line (§9 rules). Overflow that cannot
   be placed in that line → floor line, in **draft order** (see §11 for what "in order"
   means for scoring parity; floor order is irrelevant except for the marker space).
4. End of turn → advance.

**Invalid cases** (§32 codes): wrong turn, game over, factory out of range/empty of that
color (`azul.invalidFactory`, `azul.noTilesOfColor`), illegal line choice (§9: line full /
occupied by another color / **wall row conflict** with that color), malformed payload.

### 8.2 Draft from the center

**Prerequisites:** it is your turn; center contains ≥1 tile of `color`.

**Resolution:**
1. Remove **all** tiles of `color` from the Center → drafted tiles.
2. **First-player marker (§15):** if the marker is currently **in the center** (nobody has
   claimed it this round), the current player **must** take it onto their board
   (marker space of their floor line). This happens **before** placement: the marker occupies
   the floor-line's first space and **reduces that line's tile capacity for the
   rest of the round** and scores −1 in the tiling phase (§11). If the marker is already held
   (by this or any player from this round — impossible twice per round) nothing happens.
3. Place the drafted tiles into the chosen pattern line (§9); overflow → floor; floor
   beyond capacity → discard pool (§11).

**Invalid cases:** center lacks color (`azul.centerNoTilesOfColor`). In addition, the
standalone marker action — `TakeFirstPlayer` (§19) — is legal **iff** the marker is in the
center; a player may take it as their entire turn. ⚠ This "take the marker instead of
tiles" option is asserted by the printed rulebook (community-standard understanding) but
does **not** appear in the two summaries fetched for this spec — recorded as **OD-12**; if
PDF verification (§35 step 8) finds it absent, remove the action entirely (the mandatory
claim-on-first-center-draft rule is confirmed and stands regardless). When the center holds
no tiles and the marker is in the center, `TakeFirstPlayer` is the only center-side action
available anyway; a plain `DraftFromCenter` with no tiles fails.

### 8.3 Placement decision and forced floor

A drafted color can be placed in pattern line `l` iff all of:
- `l ∈ 0..4`;
- line `l` is empty OR already contains only tiles of the drafted color (§9.1);
- the wall row corresponding to `l` does **not already contain** the drafted color (§9.4);
- and placing at least one tile is possible (line not full).

The chosen line receives `min(k, freeSpaces)` tiles; any remainder spills to the floor
line in draft order. Because one draft's tiles must enter **one** line (never split
across two), the legal choices are the lines above.

**The floor as a destination.** A line is never "optional": if the player wants to dump a
draft entirely onto the floor (e.g., to dodge a bad wall row, or because every legal line
would be a self-harm they prefer over keeping the tiles) — the rules do **not** offer a
free "floor everything" option. The floor-only destination is legal **only when no legal
pattern line exists** for the drafted color in the current state (all lines either full,
color-conflicting, or wall-row-conflicting). The engine models this with the sentinel
`LineIndex = -1` (see §19/§20):

```text
LineIndex >= 0  : legal iff a line with space + color-compatible + wall-row-compatible.
LineIndex == -1 : legal iff NO LineIndex >= 0 line is legal for that color — all drafted
                  tiles go to floor (capacity 7), overflow beyond 7 to the discard pool,
                  with NO penalty for discarded overflow tiles (§11).
```

If a draft (forced or not) leaves the player with floor tiles, they occupy spaces left→
right; nothing else changes. If the pool then empties, the round resolves in the same
action (§13) and the floor penalty applies in tiling as for every other floor cause.

---

## 9. Pattern lines

Per line `l` (capacity `l+1`, `l = 0..4`):

1. **One color per line, ever** — not just per round: a partially filled line from the
   previous round permanently restricts that line to its existing color until it completes
   and tiles (wall) at round end, when it empties.
2. **Capacity:** at most `l+1` tiles; drafted tiles beyond available space spill to the floor
   in the draft's tile order.
3. **Cross-round persistence:** at the tiling phase only **complete** lines are cleared
   (into wall + discard); **incomplete** lines keep their tiles for the next round.
4. **Wall-row color constraint:** you may not place color `C` into line `l` if the
   corresponding wall row `l` **already contains** `C` — because the completed line would
   have nowhere legal to go. (The physical board enforces this via matching printed symbols.)
5. **No capacity is ever "wasted upward":** you may draft 4 reds into line 1 (capacity 1):
   1 placed, 3 → floor (and beyond the floor's 7 spaces → discard). Deliberately wasteful
   but legal when that's all you can do — and illegal to choose when a better legal line
   exists (OD-4).
6. **Completion check** is at the tiling phase only — never mid-draft (a line of capacity 3
   with tiles [C, C] is "not complete" even if its next space is unreachable — it simply
   needs 1 more C later, which may be impossible this round — normal play).

**Invariants:**

```text
I-6: line tiles ∈ [0..l+1]; line colors uniform
I-7: completed line (== capacity) tiles the wall exactly 1 position in the tiling phase,
     then empties (all its tiles leave the line: 1 to wall, rest to discard)
I-8: at end of tiling phase, at least one line is incomplete OR the game ended
     (a full 5-complete board with no refill is the terminal case — see §14)
```

---

## 10. Wall placement

The wall is 5 rows × 5 columns. **Wall row ↔ pattern line mapping: line `l` (0-based)
feeds wall row `l`**, and **the tile occupies the row's lowest-indexed empty cell**
(OD-6: "left to right" in board terms; direction doesn't affect rules because wall rows
fill contiguously and the wall never loses tiles). The placed tile's **color = the line's
color**; one completion places **exactly one** tile into the wall, the remaining `l` tiles
of the completed line (capacity `l+1`) go to the discard pool (§13).

When (only): the **tiling phase** — never immediately at completion during drafting. A
completed line whose wall row is full (all 5 colors) places nothing — but that state can
only occur in the round the game ends; the tiling phase still scores what it can (§12).

Wall cells are permanent. Wall tiles are **excluded from all bag recycling** forever.

**Immediate scoring:** each wall placement during the tiling phase scores per §12.1,
sequentially (row 5→0 top-down, per §13 — an ordering that is observable when one player
completes several lines and chains grow).

---

## 11. Floor line

- 7 tile spaces + the **marker space** (the first position; occupied only by the
  first-player marker, never by a tile).
- Sources of floor tiles: (a) forced overflow of a chosen pattern line; (b) forced all-to-floor
  when the color has no legal line (with a marker also possible).
- **Penalty:** at the tiling phase, each floor tile = **−1 point**; the marker on the board
  (from §8.2/§15) = **additional −1** (rulebook: the marker itself costs a point when the
  tiling phase is scored; it then **leaves the floor** and goes to… see §15 — the holder keeps
  "first player" status; the marker goes back to the **center** at round start and its holder
  is the next round's starter).

**Scoring floor:** the penalty applies once per round (all floor tiles simultaneously in
the tiling phase). **After** the tiling phase the floor line is **emptied**; its tiles go
to the discard pool (→ back into the bag on refill, §13); the marker has already returned
to the Center in the same cleanup (§13 step 1c) — the holder remains the next round's
starter (§13 step 1e).
- Floor can never exceed 7 tiles: the 8th+ tile of a forced overflow goes to the **discard
  pool** immediately, and is still scored nowhere (it already cost its −1 if placed into a
  floor space; a discarded-overflow tile **did not occupy a floor space** → **no penalty**
  for it). (Rulebook is explicit: full floor line → discard, no additional score cost.)
- Negative scores are clamped to **≥ 0** during the game per BGA/licensed rules ("Your score
  cannot be less than 0") — OD-7 default: **floor clamp to 0 is applied** (matches the two
  licensed summaries). The physical rulebook does not clamp (floor can push below zero)?
  Contradiction: German text says penalties reduce score, BGA says clamp at 0. **Decision:
  clamp at 0** (OD-7), consistent with the licensed rules summary; verify at implementation.

---

## 12. Scoring

### 12.1 Placing on the wall (tiling phase, per placement)

When a tile is placed on the wall, look at the maximal **contiguous chain of same-adjacent
placed tiles** in its wall row and in its wall column. **Note:** Azul wall rows/columns
never contain two tiles of the same color (§9.4), so a "chain" is a chain of **adjacent
occupied cells** regardless of color — the printed rule counts occupied neighbors, not
same-color neighbors:

```text
score = (number of consecutively filled cells in the horizontal run containing the tile,
         including the new tile)
       + (number of consecutively filled cells in the vertical run containing the new tile,
         including the new tile)
       − 1                                   ← the new tile counted twice above
```

A tile with no occupied neighbors scores **1** (1 + 1 − 1). Rules text verbatim (BGA):
"1 point for each tile in the horizontal row linked to that tile, including the tile just
placed; 1 point for each tile in the vertical column…; if both, do both (tile gets
scored twice)."

**Worked examples:**

```text
E1  Isolated tile (row and column empty):           1 + 1 − 1 = 1
E2  Extends a horizontal run of 2 (A B [X]):        3 + 1 − 1 = 3
E3  New column below existing single (vertical 2):  1 + 2 − 1 = 2
E4  Joins horizontal 2 AND vertical 2:              3 + 3 − 1 = 5
E5  Completes the middle of a horizontal run with
    gap:  A A _ [X] B  →  horizontal run = 1, not merged (gap breaks continuity).
    (Only *consecutive* neighbors chain; "linked row" = maximal contiguous run.)
E6  Empty cells between tiles NEVER chain.
```

Adjacency is orthogonal (4-neighbor), never diagonal; wall chains are computed on the
**current** wall (earlier tiling-phase placements of the same round already score first:
order top line → bottom line, per §13).

### 12.2 Floor penalty (tiling phase, after wall scoring, per round)

```text
score -= (# floor tiles) + (1 if the marker was on this player's floor this round)
clamped at 0 (OD-7)
```

### 12.3 End-of-game bonuses (§14): +2 per completed horizontal wall row, +7 per completed
vertical wall **column**, +10 per color fully present on the wall (5 tiles of that color
across any rows). OD-8 flag: the licensed summaries state "2 points per horizontal line";
the printed rulebook is believed to say "2 per tile in a completed horizontal line (=10)";
this discrepancy is a rules-audit item (§36) — **implement as written by the two licensed
sources: 2 points per completed horizontal line** unless the printed-PDF verification
(§35 step 8) contradicts, in which case the PDF wins and the Critical Decision is amended.

### 12.4 Score order

Score changes are sequential and deterministic: **wall placements (top pattern line →
bottom) → floor penalty** within each player; players are processed in turn order
(first player → … ) (OD-9: "simultaneously or consecutively" in the rulebook; digital
needs a fixed order — turn order, top-to-bottom; observable only in score-animation order
since the amounts don't depend on other players' state).

---

## 13. Round lifecycle (exact engine sequencing)

The tiling phase runs inside the **same `ProcessAction`** that drains the last tile of the
pool. Exact order:

```text
[Pool check]  factories all empty AND center empty (marker in center allowed — the
              marker is not a tile; the last draft takes the center's last tiles; if the
              center had ONLY the marker… that draft is the marker-only action §8.2,
              which does NOT clear a tile — the pool was already empty; tiling triggers on
              a normal draft when the last tiles leave the pool. Edge: center contains only
              the marker and factories empty: no drafting is possible; the player whose
              turn it is takes marker-only, and — no tiles changed — the pool was already
              empty BEFORE the action ⇒ trigger tiling at the END of that action too.
              Invariant: tiling runs exactly once whenever, after an action, the pool has
              zero tiles and the game is not over.)

1. For each player seat p, in current round turn order (starting from the round's first
   player):
   a. For each pattern line, top (5) → bottom (1) (index order 4..0):
      if line completed:
        - wall tile of the line's color into that wall row's next empty cell (if a cell
          is free and the row lacks that color — always true pre-end); score per §12.1;
        - remove the whole line (placed tile → wall, others → discard pool).
      if line incomplete: keep (tiles persist).
   b. Floor penalty §12.2 (marker occupancy included).
   c. Clear floor line (tiles → discard pool); marker → Center (§15).
2. Game-end check: any wall row complete (5 tiles) for ANY player?
   → YES: final scoring §14 + winner §14 → IsOver, GameEnded. No refill.
   → NO:
   d. Refill: for each active factory i, draw 4 tiles from the bag.
      - If bag < 4 tiles: gather discard pool → append to bag → shuffle; draw what's
        available (factories may end partially filled — legal endgame rare case, rulebook
        "played with some unfilled displays"). If bag+discard is entirely empty, all
        tiles are on walls/lines — the game will end next tiling (a full wall is implied;
        harness still covers it: round proceeds, drafting from empties is impossible ⇒
        any draft action is invalid ⇒ the timer/AFK path or the invariant: this state is
        unreachable in practice — see invariant I-11.)
      - Bag after refill: unchanged order for already-present tiles; newly returned
        discards are shuffled into the bag on the append.
   e. RoundNumber++; the first-player marker's holder (if it was taken this round, else
      unchanged owner-or-random for round 1) is the next round's first player.
      CurrentPlayerIndex = that seat.
   f. Start the turn clock for the new round's first player.
3. Advance CurrentPlayerIndex to the next active seat (skip eliminated seats) and set
   NextActionDeadlineUtc. Append public events (§23): roundStarted / roundFinished /
   tilesDiscarded / firstPlayerChanged / scoresUpdated.
```

**Who starts the next round:** the player holding the first-player marker at the moment
the last tile left the pool (i.e., took it this round) — or, if nobody took it this
round (possible only in round 1 if nobody drafts from center), the same first player
starts again; round 1 uses the OD-2 random pick. (The marker physically leaves the
holder's floor at cleanup and goes back to the center at the start of the next round —
the holder remains the round's starter until someone claims it again.)

---

## 14. Game end, final scoring, and tie-breaks

**Trigger:** during the tiling phase of round R, **at least one wall row is complete**
(5 consecutive same-row wall tiles). Check happens after all wall placements (§13 step 2).

**No round continues, no "last turns"** — the round simply ends (unlike Splendor; Azul
drafts equal turns naturally because the pool drains). Game ends: `IsOver = true`.

**Final scoring** (every player):

```text
finalScore = score (already includes round-end wall scores and floor penalties)
           + 2  * (# completed horizontal wall rows)
           + 7  * (# completed vertical wall columns)
           + 10 * (# colors with all 5 copies on the wall)
```

**Winner:** highest final score. **Tie-break:** more completed **horizontal** wall rows
(both fetched rulebook summaries agree; the first-player marker holder does **not**
decide). If still tied: **shared victory** (`Winner = null`, platform precedent, OD-10).

**I-14:** after `IsOver`, every action fails with `azul.gameOver`.

---

## 15. First-player marker

States: `Center` | `Seat(i)` (on seat i's floor marker space). Lifecycle:

```text
Round 1 start: Center (round starter = OD-2 random seat, marker unheld)
During a round: the FIRST player to draft from the center MUST take the marker
                (placed onto their floor's marker space; costs −1 in tiling).
                Alternative action (any center turn, marker in center, any center contents):
                draft ONLY the marker (no tiles) — counts as the turn.
Round end (tiling phase): marker leaves the floor line (after its −1 penalty) and returns
                to Center at the start of the next round…
                …but the holder of the marker at the moment the round ended is the NEXT
                round's first player.
If nobody took it this round: same starter; marker was already at center.
```

The marker is the **only** non-tile object in the state. It is never a scoring tile; it
occupies no floor tile space (only its dedicated marker space) and never goes to the bag.
Movement is: Center → Seat **only** during a draft (§13's cleanup is the single place the
marker leaves a board, and it always lands back in the Center, never hand-to-hand).

---

## 16. Information visibility model

| Information | In authoritative state | To clients |
|---|---|---|
| All factories (contents per color) | yes | yes — public |
| Center contents | yes | yes |
| Each player's wall / pattern lines / floor / score | yes | yes |
| First-player marker location + round starter | yes | yes |
| **Bag:** count | yes | yes (`Bag.Count` — public; players track it anyway) |
| **Bag:** remaining order/contents | yes | **no** — count only |

Consequence: the projection = the state minus bag internals. Since the platform broadcasts
GameState to the whole session group for non-`IPlayerViewGame` engines (UNO precedent), the
engine must **store the bag order in the authoritative state only** and must **not** leak
draw-ahead info through events. §22 serialization: `Bag` is serialized (persistence needs it)
— but without a projection the raw state (with bag order) would be broadcast to everyone,
leaking future draws. **Decision (Critical Decisions below: "Azul implements IPlayerViewGame
only to hide the bag order") — Azul DOES implement `IPlayerViewGame`**: not for a hidden per-viewer state
(there is none; the projection is viewer-independent, the Splendor model), but to strip the
bag order (`BagCount` survives, its contents are replaced). The Game Service integration
already handles viewer-independent projections with zero changes
(platform decision 2026-09-11/12). Frontend must guard against authoritative-shape
payloads exactly like Silver/Splendor (reject a state whose `Bag` array is visible).

No other hidden information: no seat-specific data at all.

---

## 17. Randomness

Server-authoritative, in `AzulGame`, at these moments only:

1. Setup: bag shuffle (Fisher–Yates, private `Random`).
2. Tiling/refill: **only** when discards are re-shuffled into the bag (append + shuffle).
3. Round-1 first player: uniform random seat (OD-2).

Everything else (draws = popping the shuffled sequence) is deterministic. **Testing seed:**
optional integer `Seed` in `GameOptions.Settings` (Silver/Splendor precedent) drives all RNG;
absent → fresh random. Clients can never influence: payloads name a factory **index** and a
**color**, never which physical tile is drawn; `DraftFromCenter` names only a color; the
bag order is server-only (§16).

---

## 18. Game state design

Authoritative state blob serialized as JSON string into `GameState.Data["AzulState"]`
(+ `GameState.Data["PlayerNames"]` — same convention as every shipped game; platform
decision 2026-09-06 `TryGetString` round-trip applies).

```text
AzulState
  RoundNumber        : int (starts 1)
  CurrentPlayerIndex : int (mirrored on GameState.CurrentPlayerIndex)
  FactoryCount       : int (5 | 7 | 9 by player count — immutable after setup)
  Bag                : List<int>       (colors in draw order; FRONT = next drawn — never
                                         broadcast; count only goes to clients)
  Factories          : List<List<int>> (length == FactoryCount; each 0..4 tiles)
  Center             : List<int>       (leftovers; order is the arrival order — display only)
  MarkerLocation     : enum Center | Seat(i)   + FirstPlayerSeat: int
  Discard            : List<int>       (tiles that fell off a full floor / left completed
                                         lines and floor at cleanup; re-bagged on refill)
  Players[]          (per seat)
      Wall          : int[5][5]        (-1 empty; else 0..4 color)  [row = pattern line idx]
      PatternLines  : List<int>[5]     (capacity line index + 1; color-uniform; persists)
      Floor         : List<int>        (0..7 tiles; marker space separate, see MarkerLocation)
      Score         : int              (clamped ≥ 0)
      Timer         : AzulPlayerTimer  (platform bank/deficit/AFK fields, §24)
  TimerConfig        : { BaseTurnSeconds=60, MaxBankSeconds=180, MaxOverrunSeconds=15,
                         MaxAfkTurns=3, TotalGameTimeMinutes=60 }  (§24 — config, not rules)
  EliminatedSeats    : List<int>        (platform AFK mechanism, §24)
  EventLog           : List<string>     (public-safe JSON envelopes, §23)
```

Field justification (why each exists): bag order → deterministic reconnection (§25) and
refill; Discard pool → conservation re-entry; MarkerLocation vs FirstPlayerSeat → the
marker is at the center during everyone else's turn but the last taker starts next round;
Wall as grid (not a color map) because adjacency scoring is spatial; PatternLines as
lists (not counts) because the line color is implicit but must be readable for validation.
Everything the clients need is the same structure minus `Bag` contents (§16).

---

## 19. GameAction model

ActionType strings (`AzulActionType` enum, dispatched like Splendor's):

| # | ActionType | Payload (PascalCase JSON) | Legal actor | Resolves |
|---|---|---|---|---|
| 1 | `DraftFromFactory` | `{ FactoryIndex: int, Color: int, LineIndex: int }` | current player | §8.1, tiling if pool drained, advance |
| 2 | `DraftFromCenter` | `{ Color: int, LineIndex: int }` | current player | §8.2 (+ marker per §15) |
| 3 | `TakeFirstPlayer` | `{}` | current player; **iff the marker is in the center** (an alternative to a center draft; ⚠ OD-12 — PDF-verified keep-or-drop) | marker Center→floor; counts as the entire turn; if the pool was already empty the tiling phase also runs after it (§13 parenthetical) |
| 4 | `TurnTimeout` | `{}` | system (Game Service sweep) | §24 (OD-3: **skip** the turn — never auto-drafts; no "must draft if possible" auto-action) |
| 5 | `GameTimeExpired` | `{}` | system | force-finish: run tiling phase if pool empty? no — end game immediately: final scoring from current state, winner via §14 ladder; `IsOver` |

- `TakeFirstPlayer` is legal only when the marker is in the center; when legal and it's
  the first center contact of the round, the marker must have been taken anyway (§15).
- There is deliberately **no** `SelectLine` second action: the line is part of the draft
  payload (atomic — one turn, one GameAction, mirrors Splendor's atomic design; multi-step
  sub-phase state machines are forbidden without a Critical Decision).

**Payload rules:** unknown ActionType → `azul.unknownActionType`; malformed/extra fields /
out-of-range ints → `azul.invalidPayload`; the engine **recomputes everything** from its own
state — payload names targets (index, color, line), never facts (no tile arrays, no scores).
A forged `DraftFromFactory{ Color: 2 }` on a factory without color 2 fails `azul.noTilesOfColor`.

End-of-turn pipeline: the entire §13 tiling + refill happens **inside** the action that
drains the pool, before `AdvancePlayer` (the last drafter's turn is the last turn of the
pattern phase — the next `CurrentPlayerIndex` is the next round's starter per §13).

---

## 20. GetValidActions specification

`GetValidActions(state, playerId)` returns **concrete legal payloads** (like Splendor §16
convention: enumerated, engine-acceptable as-is), for the current player only; empty when
over / wrong seat / eliminated. Enumerate:

```text
1. For each active factory f with tiles, for each color C present in factory f,
   for each pattern line l legal for C (§8.3: has any free space, its color is empty-or-C,
   wall row l lacks C):  DraftFromFactory{f, C, l}.
   - If C has NO legal line: DraftFromFactory{f, C, LineIndex = FloorSentinel(255)}
     (the floor-only forced case §8.3) — offered exactly when the draft is the player's
     only choice? No — offered always when legal (it is a legal, if self-harming, draft).
2. Center: same with DraftFromCenter for each color present in center (l or sentinel);
   plus TakeFirstPlayer when the marker is in the center.
3. (Never TurnTimeout/GameTimeExpired — system actions.)
```

The forced-floor case uses the same **sentinel** as §8.3: `LineIndex = -1` (integer,
serializes fine in PascalCase JSON; the engine validates the range: `>= 0` normal line,
`-1` floor-forced). The harness and frontend use this sentinel consistently; any other
value is `azul.invalidPayload`.

---

## 21. State invariants (runtime guards where cheap; every one harness-tested)

```text
I-1  CONSERVATION: 100 == |Bag| + Σ|Factories| + |Center| + Σ_p (|Σ PatternLines| + |Floor|
     + wallTiles) + |Discard|          (checked after every action incl. tiling)
I-2  Every tile is int ∈ 0..4          (no invented colors)
I-3  Factories.Count == FactoryCount; each factory 0..4 tiles (4 only right after refill)
     (during a round factories are either full 4 or empty — a draft empties them fully)
I-4  Pattern line l: 0 ≤ count ≤ l+1; all tiles in a non-empty line share one color
I-5  Wall cell ∈ {-1} ∪ 0..4; a wall row never contains a color twice
I-6  A wall row is non-empty in a left-contiguous prefix? (OD-6 fill order: enforced only
     if the chosen fill rule is "next empty left→right" — contiguity follows)
I-7  Floor: ≤ 7 tiles; marker occupies only its own space
I-8  Bag order is NEVER exposed in any client payload or event (§16)
I-9  CurrentPlayerIndex is an active seat (not eliminated) unless IsOver
I-10 Scores ≥ 0 (OD-7 clamp) and change only via §12 events
I-11 Round ends exactly when pool (factories+center tiles) == 0 after a draft;
     tiling+refill run exactly once per round; after refill every factory has 4 tiles
     unless bag+discard < needed (rare endgame) — never fewer than the bag+discard allow
I-12 Game ends only from the tiling-phase trigger (§14) or GameTimeExpired
I-13 Marker transitions Center→Seat→Center (never two holders; never Seat→Seat directly)
I-14 After IsOver: every action → azul.gameOver
I-15 Version/round/turn counters monotonic; ProcessAction never mutates its input state
```

---

## 22. Persistence and serialization

- Everything into `GameState.Data["AzulState"]` — the platform's single-JSON-string pattern
  (no new tables/services/caches). `PlayerNames` beside it, rehydrated via
  `GameState.TryGetString` (the 2026-09-06 `JsonElement` round-trip decision applies
  identically; a plain `is string` check would break after DB round-trip).
- Round-trip contract verified repeatedly by the harness:
  `CreateGame → ToJson → FromJson → ProcessAction → ToJson → FromJson → …`. The **bag
  order** must survive byte-equivalently (determinism + hidden draws), wall grid, marker
  enum, sentinel line values, timer deadlines (UTC), event log, discard pool.
- Enums (`MarkerLocation`, colors) serialize as stable **strings** (Silver's pattern), so
  renumbering an enum never corrupts stored rows. The standard dev-stage schema caveat
  still applies (platform precedent): sessions persisted under pre-release `AzulState`
  shapes are not resumable; no production data will exist before launch.
- Collections: `int` arrays only — no boxed object round-trips. No culture-dependent
  formatting anywhere in the blob (scores/tiles are ints).
- Numeric types: counts are `int` everywhere; scores are `int` (can be −…, but clamped
  to ≥ 0 at every write).

---

## 23. Events

Two mechanisms, both platform-standard (same envelope approach as UNO/Silver/Splendor —
client-side localization through `events.azul.*`):

| Code (`azul.` prefix) | When | Params (public-safe only) |
|---|---|---|
| `gameStarted` | creation | players |
| `tileDrafted` | draft resolved | player, count, color, source (factory idx \| center) |
| `tilesMovedToCenter` | factory remainder | color counts, source factory |
| `firstPlayerTaken` | marker Center→Seat | player |
| `tilesPlaced` | placement into a line | player, line, count, color |
| `tilesDroppedFloor` | overflow incl. forced-floor | player, count, color |
| `patternLineCompleted` | tiling, per completed line | player, line, color |
| `tileOnWall` | per wall placement | player, color, row, points |
| `floorPenalty` | tiling, per player | player, penalty |
| `scoreUpdated` | any score change | player, new total |
| `roundFinished` | after cleanup/refill, when continuing | round, bagCount |
| `gameFinished` | IsOver | winner (or null), final scores per seat |

Events **never** include: bag contents (only counts), a player's "planned" move, or
internal sequences. The purchase-style leak discipline from Splendor §19 applies unchanged.

---

## 24. Timers

**Reuse the existing platform mechanism entirely** (`NextActionDeadlineUtc`,
`GameEndsAtUtc`, `TurnTimeoutService` sweep, bank/allowance/grace accounting,
`GameState.Data` root mirror fields). No Azul-specific timer, no new sweep.

Azul policy (inside the engine):

- Defaults via settings `Timer` key, same shape as UNO/Silver/Splendor: base 60 s /
  max allowance 180 s / overrun grace 15 s / 3 consecutive timeouts = AFK / total game
  time 60 min (all configuration, not rules; the values and their rationale are recorded
  in this section and must be re-affirmed by the implementation's Critical Decision).
- **`TurnTimeout`:** charge overrun per platform accounting and **skip the turn** (OD-3:
  no auto-draft — the platform precedent (UNO/Silver/Splendor timeouts never fabricate
  player choices) plus Azul's drafting is strategic: forcing a "random cheapest draft"
  punishes and rewards arbitrarily; skip only). AFK elimination at `MaxAfkTurns`: platform
  pattern (2p → survivor wins immediately; ≥3p → seat eliminated, skipped forever, its
  board and tiles **stay on the wall/pattern lines** — eliminated players' wall tiles still
  exist; they are excluded from winner determination and final scoring is over remaining
  players; a player eliminated while holding the marker returns it to the center).
  If ≤1 active seat remains → game ends (winner = survivor, else shared).
  Elimination never "returns" an eliminated player's board tiles to the bag (no rule for
  that in the physical game; digital convenience keeps the mosaic frozen).
- **`GameTimeExpired`:** force-finish from the current state (no partial tiling — if the
  pool is mid-round, wall tiles aren't placed; final scoring runs on the current wall
  scores; ladder §14; pre-limit rejection `azul.timeNotUp`).

Every `StateUpdated` notification must carry `GameType` (platform 2026-09-12 decision —
the projection publisher rule; Azul implements `IPlayerViewGame`, so the notifier projects
per connection; forgetting GameType would broadcast the bag order = a leak).

---

## 25. Reconnection

No Azul-specific mechanism: platform `joinSession` → `ReconnectPlayerCommand` → projected
state (§16) including bag **count** only, marker location, all public areas, deadline.
Game Service restart reloads authoritative JSON (bag order intact → refill determinism
survives). Reconnection grants no extra information (the projection is the same for
everyone; §16).

---

## 26. Concurrency, duplicates, and stale actions

- Platform serialization holds: per-session action processing is sequential; the
  `version` optimistic-concurrency token (platform 2026-09-15 decision) closes
  action-vs-sweep races; a conflicting client action fails and reloads from the broadcast.
- Stale duplicate draft (double-click): after the first draft the pool changed → second
  submission fails `azul.invalidFactory` / `azul.noTilesOfColor` / wrong-turn naturally;
  no dedup machinery.
- `GetValidActions` is advisory; the engine re-validates every payload field.

---

## 27. Frontend: physical-board experience (first-class requirement)

**The digital implementation should reproduce the experience of playing the physical Azul
board game as closely as reasonably possible.** This is a product requirement, not
polish: a player who has never read this document should understand the game *visually*
by sitting at the board. Requirements:

1. The **table** dominates the screen: factory ring + center in the middle, player boards
   as the real two-part boards (wall + staircase pattern + floor), scoring tracks, marker.
2. **Recognizable components:** circular factory discs with tiles seated around their rim
   (like the real trays), the center as a loose pile, tiles as rounded-square azulejo
   discs with a color-specific printed motif (§31 — and shape/pattern differs per color,
   doubling as accessibility redundancy), player boards with the wall grid visually
   attached to the staircase lines (connection cues: faint symbol or line per row).
3. **Drafting feels like picking tiles:** hover lift + glow on pickable factories/center
   colors; a selected color's tiles lift; unchosen factory remainder *slide into the
   center*; tiles *fly* from the chosen factory to the picked pattern line.
4. **No forms/dropdowns in the play surface** — no "Submit" dashboard. Click-a-thing-
   then-click-a-thing interaction only; a tiny contextual cancel affordance is allowed.
5. **Minimal menus:** rules/replay live in the same drawer pattern as UNO/Silver/Splendor
   (help modal + game log); the board itself is never replaced by a settings screen.
6. The page keeps the platform's game-page frame (dark-glass header with brand, status
   strip, scoreboard + game log, chat drawer) — Azul's own area is the *table*.

Anti-goals (all rejected for the play surface): card grids with prices, admin-table
layouts, wizard steppers for normal moves, a "board" that is a list of numbers.
Usability over literalism (e.g., we do not rotate the opponent boards to "face" the
viewer's side of the table).

---

## 28. Frontend: board layout

```text
┌───────────────────────── header (shared platform shell) ─────────────────────────┐
│ Bordia.        AZUL · Session …                         Language   Timer          │
├────────────────────────────── TABLE (felt) ─────────────────────────────────────┤
│  opponent boards (top edge, compact rows: wall grid + staircase + floor + score)│
│                                                                                 │
│                 ┌ factories arranged in a RING (positions on an ellipse)         │
│                 │   each: disc + tiles around the rim + count/labels             │
│                 │        center pile sits INSIDE the ring (absolute center)      │
│                 │        first-player marker floats by the center when at center │
│                 └                                                               │
│   your board (bottom, largest):                                                 │
│     score track rail • floor line • staircase pattern lines (1..5 slots each,    │
│     real-board silhouette) • wall 5×5 with per-row symbol matching each line     │
├──────────────────────── status strip / scoreboard / log (shared) ───────────────┤
```

- Factory ring geometry: `FactoryCount` discs placed at equal angles of an ellipse
  (center slightly wider than tall); the **center** pile rendered as tiles fanning at the
  ellipse center; marker as its own chip. On narrow screens the ring collapses to a 2×N
  grid + center chip strip (§30).
- Opponent boards = the same component scaled; "your" board = the same component with
  interaction enabled and the staircase right-justified below the wall (mirrors the
  physical layout orientation).
- A wall cell shows its feeding pattern-line symbol faintly (printed on the real board).
- Scores use the shared seat-panel conventions from Splendor (VP pill, bonus chips →
  here: wall tiles + line contents are already visible on the board itself — do not
  duplicate in a corner list; keep the seat panel to name + score + marker ownership).

---

## 29. Frontend: tile interaction and animations

**Interaction (two-click draft, no payloads exposed):**

```text
state machine (client-side UX only — legality stays server-side):
  idle → [pick factory or center: tap disc/tile]
       → color chips of that source highlight; tapping a color selects all its tiles
         (they lift), remainder-of-factory preview grays "will go to center"
       → legal pattern lines on YOUR board glow; tapping one confirms
         (a forced-floor draft glows the floor line instead)
       → optimistic? NO — send DraftFromFactory/DraftFromCenter, show tiles in flight
         while the action is in flight; on state broadcast, reconcile positions
       cancel: tap elsewhere / Esc clears the selection; sending is never a dialog
  your board only; factory taps while it's not your turn are ignored (disabled visuals)
```

**Animations (purpose: explain rules, never gate play — all ≤ ~400 ms, skippable):**
- `tileDrafted`: tiles fly factory→line (staggered ~40 ms), remainder slides to center.
- `tileOnWall`: completed line's tile glides to its wall cell; score pill pulses +N.
- floor drop: tiles roll to the floor; on penalty the floor flashes red −n.
- round transition: factory refills animate tiles from a bag chip icon onto each disc;
  a "Round R" veil (≤900 ms) with the marker hand-off if changed.
- `firstPlayerTaken`: marker chip flies to the holder's floor.
- game end: shared-standings modal (platform pattern; shared-victory `Winner = null`
  renders explicitly).
- TimerRing (soft/hard deadline) + final-round… (Azul has no final-round banner; the
  round veil + next-starter highlight suffice).

**No** drag-and-drop of individual tiles (they move as color-sets); click-select is the
faithful and accessible interaction model.

---

## 30. Frontend: UI states and responsive layout

UI states the board must render (all from projected state + hub, like Splendor/Silver):
`loading` → `waitingState` veil; `yourTurn idle/factoryPicked/colorPicked/lineGlow`;
`opponentTurn` (turn ring on the active seat; their draft animations play from broadcast);
`reconnecting` (hub offline chip, board frozen); `takenOver` (shared); `paused` (session
status banner — the service owns pause); `gameOver` modal (standings, tie note, winner or
shared); AFK `removedFromGame` overlay. Stale-state guard: `parseAzulState` must **reject
authoritative-shaped payloads** (presence of a `Bag` array instead of `BagCount`) and render
the waiting-veil (platform 2026-09-12 leak-guard precedent — a leaked bag order must never
render).

Responsive:
- ≥ lg: full ring + full own board + opponent boards.
- md: ring → single row of factory discs (scroll-x inside the felt) + center; own board
  full, opponents compact one-row strips (wall only, expanded on tap via the standard
  peek popover).
- sm: factories as horizontal snap scroller; own board scaled to width; opponent boards a
  collapsible strip list. The metaphor survives at every size (scrolling the factory ring
  or switching to a list of numbers is the explicit line: keep discs).
- RTL: all logical utilities; the ring is geometrically symmetric; opponent/own vertical
  order unchanged; score rails mirror.

---

## 31. Localization

Everything user-facing in **both** `locales/en.ts` and `locales/fa.ts` (`fa: Dict` compile
parity), plus the same `games.Azul` section pattern (title, tagline, description,
`rules[]`, `actionCards[]`-equivalents) and a `splendor`-like `azul:` view-string group:

- Gem names: Blue/Rot/Gelb/Schwarz/Weiß translations (Farsi: آبی، قرمز، زرد، سیاه، سفید);
  Azul tile colors deliberately match the platform's existing gem naming where possible
  (Azure/Sapphire… no — use **plain color words**; Azul's white tiles exist —
  unlike Splendor's diamonds — names: `azul.tiles.blue|red|yellow|black|white`
  / `آبی|قرمز|زرد|سیاه|سفید`).
- Component nouns: `factory`, `center`, `patternLine N`, `floorLine`, `wall`,
  `firstPlayer`, `bag`, `scores`, `roundOf`.
- Turn/round/status strings; scoring popups (+n, −n, "completed line", "no legal line —
  all to floor", "shared victory", tie-break note); tooltips for every disabled state
  (why a factory is dim, why a line is locked); event templates `events.azul.*` with
  `{param}` interpolation and localized color words.
- Error codes `azul.*` in **both** server catalogs (`errors.en.json` / `errors.fa.json`)
  — mandatory (platform localization decision); client keeps the coded-message fallback.
- Accessibility: every tile/factory/marker control carries an aria-label built from these
  strings + the tile's shape name (not just its color word); color never carries
  information alone (the printed motifs differ per color — see §27 requirement 2).
- No hardcoded English/Persian in components.

---

## 32. Error model

`Games/Azul/Errors.cs` (game-local, like Silver/Splendor; codes surface through
`GameResult.ErrorCode/Args` → middleware localizes):

| Code | Meaning |
|---|---|
| `azul.gameOver` | actions after IsOver (I-14) |
| `azul.notYourTurn` | actor ≠ current player |
| `azul.invalidPlayer` / `azul.playerEliminated` | seat wrong / AFK-removed |
| `azul.invalidState` | missing/corrupt AzulState |
| `azul.unknownActionType` | unparseable ActionType |
| `azul.invalidPayload` | malformed JSON / out-of-range ints / extra fields |
| `azul.invalidFactory` | index out of active range / factory gone empty |
| `azul.noTilesOfColor` | factory/center lacks the claimed color |
| `azul.centerEmpty` | center draft with no tiles (and marker not available for TakeFirstPlayer) |
| `azul.lineFull` | chosen pattern line has no space for any tile… (chosen line capacity reached) |
| `azul.lineColorMismatch` | line already holds a different color |
| `azul.wallRowHasColor` | choosing a line whose wall row already holds the color (I-5) |
| `azul.floorNotOptional` | floor sentinel chosen while a legal line exists (OD-4) |
| `azul.firstPlayerNotAvailable` | marker not in center / already taken this round |
| `azul.timerNotExpired` / `azul.timeNotUp` | player-forced system actions rejected |

Validation of action *shape* (lengths) via the existing validation-code conventions
where relevant; positional args `{0}` carry counts/colors/names for localized messages.

---

## 33. Testing specification

`tests/Azul.Harness` console project (Silver/Splendor pattern: `Check(name, cond)`, exit
code, deterministic via `Seed`; recorded in the implementation Critical Decision).
Minimum coverage:

**Setup** — 2/3/4p: factory counts 5/7/9, every factory 4 tiles, bag 100−4F, center empty,
marker center, boards empty, scores 0; 1p/5p rejected; catalog totals: exactly 20 of each
color across bag+factories.

**Bag & conservation** — seeded draws deterministic; I-1 conservation checked after every
single action of a full scripted game (and after each tiling phase); re-bag on refill:
discards appear back in bags (counts), never before refill.

**Drafting** — factory: color set taken, remainders to center, other factories untouched;
center: same for pool; marker forced on first center draft (center empty → TakeFirstPlayer
alone works; `firstPlayerNotAvailable` otherwise); wrong color / empty source / out-of-range;
draft-all-to-floor legal case; floor-not-optional enforcement (OD-4).

**Pattern lines** — capacity, single-color rule (cross-round persistence too), wall-row
color block, completion only at tiling, completed line → exactly one wall tile at the
row's leftmost free cell (OD-6 order), rest → discard.

**Scoring** — E1–E6 (§12.1) fixtures incl. both-direction chains and gaps; multiple lines
top-down order within one tiling (chain growth changes totals); floor −1 each + marker −1;
OD-7 clamp at 0 (fixture: push a score to 0 via floor, never −).

**Round lifecycle** — tiling triggered exactly when pool hits 0, once; refill counts;
next-round first player = marker taker; incomplete lines persist; bag-short refill
(unfilled displays) fixture.

**Game end** — horizontal row completes → end immediately (no further rounds); final
bonus math (2/row, 7/column, 10/color) per §12.3 + OD-8 verification fixture marked;
tie-break horizontal-lines-count, shared → `Winner=null`; I-14 every action after over.

**ValidActions** — per §20 on scripted states: full enumeration equality vs engine
acceptance (every listed action must be acceptable as-is incl. floor sentinel);
empty on wrong seat/over; first-player action presence.

**Serialization** — CreateGame→Json→From→actions→Json→… loops (platform TryGetString path,
`JsonElement` branch); hidden-bag contract: a projected state never contains the bag
array, only counts; authoritative-shape rejected by the frontend parser (mirror Silver's
guard test style); timer deadlines survive.

**Randomness/seeded** — same seed → identical whole game (setup draws, refills, AFK);
different seeds differ; payload cannot name tiles.

**Invariants** — I-1…I-15 battery on every action of a seeded 50+ turn fuzz (random
valid action via `GetValidActions`, Silver-style soak; assert conservation + all I-s each step).

**Timers** — timeout skips turn only (OD-3), bank accounting, 3×AFK elimination (2p
survivor-wins; ≥3p board stays put, marker returns); GameTimeExpired ladder; timeNotUp/timerNotExpired.

**Security/staleness** — actions from wrong player, after over, duplicated after applied
(fails as stale), forged payloads; reconnect gives same projection.

**UI** (manual checklist in the frontend section): highlighting, fly-animations,
round veil, responsive folds, RTL, colorblind redundancy.

---

## 34. Edge-case compendium

(OD numbers point at §36 defaults; ⋆ marks open-interpretation defaults.)

| Case | Behavior |
|---|---|
| Last draft drains the pool mid-action | the tiling phase + refill run inside that same `ProcessAction` before turn advance (§13) |
| Factories drained leaving the center holding ONLY the marker (all tiles gone, nobody claimed it) | `TakeFirstPlayer` is legal; after it the pool was already empty ⇒ tiling also runs at the end of that action (§13 parenthetical) |
| A draft's color has a legal line but the player submits `LineIndex = -1` | rejected `azul.floorNotOptional` (OD-4) |
| Color has no legal line at all | `LineIndex = -1` forced-floor action is offered by `GetValidActions` and legal (§8.3) |
| Forced floor exceeds 7 spaces | tiles beyond capacity go straight to the discard pool with **no penalty**; penalty counts only the 7 placed |
| Completed line whose wall row is already full | only possible the instant the game ends (row complete ⇒ end); the line still clears; its tile goes to discard; end-trigger is row-completion (§14) |
| Marker taken, same player drafts center again later this round | no second claim (`azul.firstPlayerNotAvailable`) — exactly one claim per round |
| Round ended with nobody having drafted from the center | possible only when no draft ever produced a remainder (every drafted source was a single-color set): marker stays in the center; the same player starts the next round |
| Bag empties mid-refill | refill consumes bag, then the discard pool (reshuffled); factories may end partially filled ("played with some unfilled displays"); refill always distributes everything available |
| Pool refill would leave factories **and** center empty with the game not ended | cannot happen while tiles exist outside walls/discard; if all non-wall tiles are already in discard+lines and refill yields zero (only reachable after massive AFK eliminations/floor churn — theoretically), the next action's tiling ends the round and the end condition is evaluated normally; the harness fuzz asserts no stall (I-11) |
| All 25 wall cells filled before any row completed | impossible: filling the grid necessarily completes every row; the first completed row ends the game at tiling |
| 2p game, one player holds marker and their pattern lines all conflict | forced-floor drafts (§8.3) drain them; round still ends |
| Timeout at the last-draft position (would trigger round end alone) | timeout = skip; the next player drafts (pool unchanged is impossible — pool has tiles or the round ended already) |
| GameTimeExpired mid-round (tiles still in pool) | no tiling of pending lines (OD-11): final scoring from current wall scores + bonuses; ladder §14 |
| Reconnect mid-tiling | impossible — tiling is synchronous inside the action; the broadcast shows the post-tiling state with refill already done |
| Duplicate `DraftFromFactory{2,0,3}` | first succeeds; second fails stale (`azul.noTilesOfColor` / `azul.notYourTurn`) |

---

## 35. Integration checklist (for the implementation agent)

Completion = every box checked with evidence in the recorded Critical Decision (same
convention as Splendor §28 — the platform integration points are fixed; Azul must not add
new ones):

1. `backend/src/Games/Azul/Azul.csproj` — net9.0, references **only** `GameEngine.Core`,
   added to `BoardGamePlatform.sln` **and** `.slnx`.
2. `AzulGame : IGame, IPlayerViewGame` — GameType "Azul", 2–4, `CreateGame` validates;
   partial-class split welcome (core / actions / tiling+scoring / valid-actions / view).
3. `AzulState` per §18 + `AzulView` per §16 (bag count only) + `TryGetString` everywhere.
4. Action types + payloads + error codes §19/§32 + **both** localization catalogs updated
   with `azul.*`.
5. One DI line next to UNO/Silver/Splendor in `Game.Infrastructure`; catalog endpoint needs
   zero changes; **verify `GET /api/game/games` shows Azul** (min 2 / max 4).
6. Events §23 with `GameType` always on `StateUpdated` (platform 2026-09-12).
7. Timers §24 — reuse sweep/bank; defaults recorded in code as config.
8. **Rules-audit pass:** re-read §§3–15 against the printed/publisher rulebook PDF
   (Next Move official English rules — locate at implementation time) and resolve every
   OD-1…OD-11 either "confirmed" or "corrected", each correction its own dated Critical
   Decision; never silently alter documented behavior.
9. Frontend: `azul.ts` mirror+parser+leak guard; `AzulGameView.tsx` per §27–§30; original
   SVG art (tiles with per-color azulejo motifs, factory discs, boards — the
   photo-vs-SVG asset question already decided by the Splendor precedent: SVG, replaceable
   with a licensed pack; Azul tile art is not in any free-asset source we verified);
   `GamePage` switch + game room dark surface (same as Splendor's); `comingSoon` removal
   from `GAME_THEME` only at implementation time.
10. `tests/Azul.Harness` per §33 — green, count recorded.
11. `dotnet build` + `tsc && vite build` + `npm run build` all green; locales parity
    (`fa: Dict`).
12. Docker playtest: 2p and 4p full games incl. a tie; verify raw SignalR payloads contain
    **no bag order**; verify projection on reconnect.
13. Update `AGENTS.md` status table / Game Documentation index (row "Azul" → implemented);
    record the implementation Critical Decision(s); amend OD entries per step 8.

---

## 36. Rules audit trail and authoritative sources

Precedence order used while writing this spec (fetched 2026-09-15; archived copies in the
chat of this decision's creation — implementation should re-download originals):

1. **Board Game Arena — Azul game help** (officially licensed digital implementation of
   the English rules by Next Move, per BGA's own attribution): complete setup/play/scoring
   text including the "one tile of each color per wall row", 1/2/…/10 point chain rule,
   end bonuses (2 row / 7 column / 10 color), tie-break, bag-refill-from-discard,
   factory counts 5/7/9, floor→discard, clamped-at-0 note.
2. **German Wikipedia "Azul (Spiel)"** — explicitly cited to the official German
   Spielanleitung (gesellschaftsspiele.spielen.de PDF, same text family as the printed
   rulebook): corroborates all of the above independently (including the −1 marker/floor,
   first-center-draft obligation, "one color per line", refill rule, 2/7/10 bonuses,
   horizontal-line tie-break), and adds the drafting-phase terminology
   (Musterphase/Fliesungsphase) and the right-to-left placement sentence behind OD-6.
3. **English Wikipedia "Azul (board game)"** — summary-level cross-check (component
   counts, wall mechanism, bonuses "complete sets"), used for nothing ambiguous.
4. Next Move Games product pages (publisher identity, 2018 rebrand from Plan B — used for
   §1 metadata only).

Documented cross-source discrepancies and resolutions (all also captured as the numbered
OD-1…OD-11 list below):

- **2p center at setup**: BGA/DE summaries: none; a community memory of "4 center tiles
  for 2p" exists ⇒ **OD-1: start empty (both licensed summaries agree)**; verify vs PDF.
- **Completed-row end bonus 2 vs 2-per-tile(10)**: both licensed summaries say **2 per
  line** ⇒ **OD-8: implement 2 per line; verify vs PDF; PDF wins.**
- Floor clamping (BGA: cannot go below 0; DE text silent) ⇒ **OD-7: clamp.**
- Fill directions inside pattern lines / wall rows (DE: "von rechts beginnend" for pattern
  placement) ⇒ **OD-6: pattern lines and wall rows fill left→right** (the German phrase
  describes the visual order only; scoring depends solely on adjacency; any *consistent*
  order is rules-equivalent — choose left→right, matching the printed board's staircase
  growth; verify vs PDF).

**Open interpretation decisions (defaults prescribed; implementation records final):**

- **OD-1** center-at-setup: empty for all player counts (both licensed sources).
- **OD-2** round-1 starter: uniform random seat (platform precedent from Silver/Splendor;
  physical: "most recently visited Portugal" — unrepresentable digitally).
- **OD-3** turn timeout: **skip only** (never a forced draft).
- **OD-4** voluntary floor while a legal line exists: rejected.
- **OD-5** board side: colored (fixed) side only; gray variant permanently excluded.
- **OD-6** fill orders: pattern line + wall row fill left→right (cosmetic, consistent).
- **OD-7** score floor penalty clamps at 0.
- **OD-8** horizontal-line end bonus = 2 points per line (not per tile).
- **OD-9** tiling phase order: turn order, pattern lines top→bottom (determinism only).
- **OD-10** tie-break ladder ends at shared victory when horizontal counts are equal.
- **OD-11** GameTimeExpired mid-round: no tiling of pending lines; straight to final
  scoring from current state.
- **OD-12** standalone `TakeFirstPlayer` action (marker instead of tiles): believed to be
  an official rulebook option but unconfirmed by the fetched summaries — implement it, and
  let the printed-PDF verification keep or drop it (the mandatory claim on first center
  draft is independently confirmed).

---

## Critical Decisions (Azul)

Game-specific decisions only; platform decisions live in the root `AGENTS.md`.

<!-- Add new Azul decisions below this line -->

- **2026-09-15** — Azul selected as a future game; specification prepared; implementation NOT started
  - **Context:** Three games shipped (UNO, Silver, Splendor). The platform's planned
    backlog (AGENTS.md) names Azul next after Wingspan among others; the owner directed a
    comprehensive, implementation-ready specification authored exactly like the Splendor
    spec-first process (research from authoritative rules sources, contract-level detail,
    physical-board-faithful UI as a first-class requirement), with implementation explicitly
    forbidden in this step.
  - **Decision:** This document (`docs/games/azul.md`) is the sole deliverable, plus the
    Game Documentation table row in root `AGENTS.md`. No `Games/Azul` project, no platform
    or frontend changes, no engine/platform edits. Azul remains in the AGENTS.md
    Out-of-Scope list until the owner explicitly starts implementation.
  - **Rationale:** Rules text was written from two mutually corroborating,
    publisher-licensed summaries (BGA's official game help and the German Wikipedia
    article citing the printed rulebook PDF), not from memory; every point where they
    disagreed or fell silent became a numbered OD with a prescribed default and a
    mandatory PDF-verification step during implementation (§35 step 8). Like Splendor, Azul
    stresses different engine shapes (shared spatial board, deterministic tiling phase run
    inside the drafting action, a permanent-mosaic wall with adjacency scoring, a
    non-material marker object) while fitting `IGame` + one DI line with zero platform
    changes — and, unlike any shipped game, needs a spatially rich physical-fidelity UI,
    which is why §27 is a product requirement section rather than an afterthought.

- **2026-09-15** — Spec decision: Azul implements `IPlayerViewGame` only to hide the bag order (viewer-independent projection)
  - **Context:** Azul's *board* is fully public, but the bag holds the undrawn future of the
    round (and re-shuffled discards), exactly like Splendor's hidden deck order; the platform
    otherwise group-broadcasts the raw state for engines without the capability.
  - **Decision:** `AzulGame : IGame, IPlayerViewGame` with a **viewer-independent**
    projection: clients receive `BagCount` + everything public, never `Bag` contents or
    draw order; `GameService`/hub changes are zero (the existing `PlayerViewProjection`
    mechanism, same as Splendor); the frontend `parseAzulState` rejects authoritative-shaped
    payloads as a defensive guard.
  - **Rationale:** Reuses the proven mechanism instead of inventing a broadcast filter;
    card counting on a 100-tile bag is real strategy (as in Splendor deck counting), and
    leaking bag order into the client would be a genuine rules advantage. A separate
    "public state" serializer would duplicate the projection contract for one field.

- **2026-09-15** — Backend implemented and verified (frontend intentionally not started)
  - **Context:** The owner directed implementation of Azul's backend only (engine, registration, error catalogs, verification harness) against this specification.
  - **Decision:** Implemented the full Azul backend: (1) `Games/Azul` (net9.0, references only `GameEngine.Core`, added to both `BoardGamePlatform.sln` and `.slnx`) with `AzulGame : IGame, IPlayerViewGame` (partial classes: core / actions / tiling / valid-actions / view), `AzulState` (front-first bag draw order, factory ring, center, discard pool, marker seat + first-player seat, per-seat wall/pattern-line/floor/score, event log, UNO-style timer config/accounting), static `AzulCatalog` (5 colors × 20 tiles, factory counts 5/7/9, floor capacity 7, bonuses 2/7/10), `AzulView` (viewer-independent projection replacing the bag ORDER with `BagCount`), and `Errors` (17 `azul.*` codes added to **both** `errors.en.json`/`errors.fa.json`). (2) One DI line next to UNO/Silver/Splendor in `Game.Infrastructure`; the catalog endpoint needed zero changes. (3) Rules per §§5-15: atomic draft actions (factory/center + line, or the forced-floor sentinel `-1` offered only when no legal line exists), mandatory first-player claim on the first center draft of a round, standalone `TakeFirstPlayer` (OD-12), immediate placement with line→floor→discard spill, automatic in-action round end (tiling in turn order, lines top→bottom; wall chain scoring h+v−1; floor penalty incl. the marker; clamp at 0; discard-then-reclaim refill; the marker holder starts the next round), end exactly when a wall row completes during tiling (final bonuses 2 per completed row / 7 per column / 10 per color set; tie by more completed rows, then shared victory), and platform timers reused (timeout = skip only; 3 strikes AFK with the 2-player survivor rule).
  - **OD resolutions:** OD-1 center starts empty for all counts; OD-2 round-1 starter is a uniform-random seat (seeded); OD-3 timeout skips, never auto-drafts; OD-4 floor legal only when no legal line exists; OD-5 fixed colored board side only; OD-6 lines and wall rows fill left→right; OD-7 score clamps at 0; OD-8 end bonus 2 per completed horizontal line (both licensed summaries agree — still flagged for printed-PDF verification); OD-9 tiling in turn order, top line first; OD-10 remaining ties are shared victories; OD-11 `GameTimeExpired` does NOT tile pending lines; OD-12 standalone `TakeFirstPlayer` implemented. OD-8 and OD-12 remain the only items a PDF audit (integration checklist step 8) can still amend; both are isolated constants/branches.
  - **Verification:** `tests/Azul.Harness` 100/100 checks (setup × player counts; drafting incl. marker rules, floor sentinel and error codes; pattern persistence; wall scoring E1–E5 fixtures; floor/discard; round lifecycle; end-game ladder incl. shared victory; valid-actions enumeration accepted as-is; conservation after every action; seeded reproducibility and divergence; serialization round-trips incl. the `JsonElement` branch; bag order hidden in projections; timers/AFK; and a 400-turn invariant soak over I-1/I-4/I-5/I-7/I-10). Full-solution `dotnet build` 0 errors; Splendor (236) and Silver (249) harnesses unchanged. Dev-stage caveat as usual: sessions persisted under earlier `AzulState` drafts are not resumable; none exist yet since no live game has run.

- **2026-09-16** — Azul frontend implemented (checklist step 9 complete; §27-§31)
  - **Context:** The backend shipped 2026-09-15 by owner direction with the frontend deferred as a separately-directed step; the owner then directed continuation of the half-landed frontend (`azul.ts` mirror/parser, `AzulGameView.tsx`, `AzulBoardVisuals.tsx`, GamePage switch, `GAME_THEME`, EN/FA copy were present; the §29 animation layer, the game-over-modal dictionary keys, and a couple of compile breaks were not).
  - **Decision:** Completed the frontend per §§27-31: (1) fixed the two `tsc` errors (dropped the unused `color` param of `placementPreview`; `AzulMarker` gained a `className` prop). (2) Added the missing `azul.gameOverModalTitle|standings|wins|tie|tieDetail` keys to **both** locales (`fa: Dict` parity). (3) New §29 layer: `features/game/azulFlights.ts` diffs consecutive projected `AzulState`s (the Splendor `tokenFlight` anchor registry, now also exporting generic `flyBetween`/`anchorRect` helpers) and replays the table as motion — drafted color group flies source→glowing line, overflow→floor, factory leftovers slide to the center, completed lines glide one tile into their new wall cell, the first-player marker flies center↔seat, score pills float +N/−N, the round transition shows the veil (`a-round-veil` keyframe, 950 ms), and round-end floor penalties flash the floor line red. Clones render the real azulejo art (`createAzulTileClone`/`createAzulMarkerClone` in `AzulBoardVisuals`, whose per-color motifs became single-source SVG-markup constants shared with the React components). All animations are ≤~450 ms, staggered, cosmetic, and never gate play; legality stays server-side. (4) Esc clears the staged selection (spec §29 cancel), and the latent conditional-hooks bug (timer hooks after the `!azul` early return) was fixed by hoisting `useNow`/`useCountdown` above it. (5) Consistency decorations: Azul hero float on `RoomPage` and the fourth game chip in `AuthShell`.
  - **Rationale:** State-diffing the projection (rather than replaying event-log envelopes) keeps the animation layer honest — it animates what actually changed even when a broadcast arrives out of order or an action was rejected, mirrors the proven Splendor mechanism, and needed zero new platform infrastructure. No drag-and-drop was added (spec forbids it; click-select is the faithful model).
  - **Verification:** `tsc && vite build` green; contract audited against the engine: action names/payload keys (`DraftFromFactory`/`DraftFromCenter`/`TakeFirstPlayer`, `FactoryIndex`/`Color`/`LineIndex` with the `-1` floor sentinel), PascalCase `AzulView` mirror, and all 14 event envelope codes/params match; leak guard rejects authoritative-shaped payloads (no `Bag` array ever renders). Docker playtest (§35 step 12) still requires running services.

- **2026-09-16** — Frontend art: Persian haft-rangi tiles + the board's printed wall mosaic
  - **Context:** Owner review of the running frontend: the board read as lifeless; the wall was rendered as neutral empty holes, losing the physical Azul board's printed cyclic mosaic (row 1: Blue/Yellow/Red/Black/White, each row shifted one step — every wall row AND column contains each color exactly once, the visual encoding of the engine's locked-row rule that a wall row never holds a color twice), and the tile art should evoke traditional Iranian tiles.
  - **Decision:** (1) Empty wall holes now render the printed pattern: full boards show the hole's printed color as a dimmed, saturated-back tile face, compact opponent boards a colored dot, tooltips name the printed color; placed tiles cover the print. (2) Tile art re-skinned to Persian haft-rangi glazes and geometry, one motif per color keeping the §27/§31 accessibility redundancy: khatam 8-pointed star (lapis blue), pomegranate/anar (carpet red), yalduz six-pointed star (saffron), girih diamond cross (black glaze with gold motif), gonbad rosette in a dotted ring (ivory with turquoise ink); painted corner spandrels on md/lg tiles. The five motifs double as the wall-row/line connector glyphs, and the §29 flight clones inherit them automatically (single-source SVG markup). Engine, rules, and platform contracts untouched.
  - **Rationale:** The wall print is decoration, not an extra constraint — scoring stays adjacency-based (h-runs + v-runs − 1) and the row-lock rule was already server-enforced; showing it makes the board self-explanatory (a glance reveals which color each row still owes per column). Persian tile geometry preserves the distinct-shape-per-color requirement without new code paths.
  - **Verification:** `tsc && vite build` green; the cyclic table was asserted programmatically against the owner-supplied 5×5 layout (base sequence [Blue, Yellow, Red, Black, White], cell (r, c) = element `(c − r) mod 5` — all 25 cells match).

- **2026-09-16** — Wall placement confirmed as leftmost-cell (printed mosaic is decoration, NOT a target)
  - **Context:** Playing a live 2p game, the owner read the newly rendered printed wall mosaic as a placement constraint ("I filled row 1 with Red, so Red must sit at the printed red cell — but all tiles land in the first column!") and reported it as a bug. Per §10/OD-6 and the official rulebook, a completed line's tile ALWAYS goes to the row's lowest-indexed empty cell (`AzulGame.Tiling.cs:33` — `Wall[line].FindIndex(c => c < 0)`); the printed cyclic pattern on the physical board is under-glaze decoration. Proof by contradiction: if tiles had to match the printed cell, every wall row could still hold each color at most once by construction, and the rulebook's line-locking rule ("a line can't take a color its wall row already has") would be impossible to ever trigger — yet that rule drives the whole drafting economy (§8.3/§9).
  - **Decision:** Engine and spec unchanged (they were already correct). The frontend was softened to stop the misreading: empty holes now render the printed color as a faint ink wash with a center dot (never a full motif-bearing tile face), the hole tooltip is the new `azul.wallHint` ("Printed design only - completed lines always fill the leftmost open space of their row", EN/FA), and help-modal `rule3` explicitly states the leftmost-open-space rule and the decorative nature of the print in both locales.
  - **Rationale:** Rules fidelity comes from the engine; the UI's job is to not imply constraints that don't exist. The previous faded-tile rendering was too convincing as a placed tile.

- **2026-09-16** — Player board laid out as the physical two-part board: lines left, wall right
  - **Context:** Owner playtest review: the stacked wall-over-staircase rendering did not match the physical Azul board, and the near-black panel blended into the dark felt, making boards hard to read.
  - **Decision:** `AzulPlayerBoard` was rebuilt as the real board's side-by-side halves — LEFT: staircase of pattern lines with the floor line and printed marker space underneath; RIGHT: the 5×5 palace wall. Both halves share a row height and the wall-row connector glyphs sit at the printed seam, mirroring the board's cues. The panel is now a bright blue majolica glaze with pale painted trim (high contrast against the felt; the felt itself was lightened one step). All §29 flight anchors (`seat:i:line:l`, `wall:r:c`, `floor`, `marker`, `score`) are unchanged, so the animation layer needed zero edits; compact opponent boards use the same halves at 16 px cells (cards widened to w-72/sm:w-80, own board to max-w-4xl). This supersedes the stacked-rows sketch in §28's ASCII, which was an approximation; the physical product is the source of truth.
  - **Rationale:** "Reproduce the physical board as closely as reasonably possible" (§27) is the first-class requirement; when the spec sketch and the printed product disagree, the product wins, per the owner's direct review.

- **2026-09-17** — Board re-theme (dark majolica) + bigger tiles + opponent summary cards with a view-board modal
  - **Context:** Owner follow-up on the 2026-09-16 two-part board: keep the dark felt theme for the board (rejecting the separately-proposed bright-blue panel), enlarge the tiles, and replace the permanent full opponent boards with compact summary cards that open a full board on click.
  - **Decision:** (1) `AzulPlayerBoard` reverses the 2026-09-16 glaze: a deep midnight-blue majolica gradient (`#1a2a4a → #07101c`) with carved indigo/slate trim, amber filigree rosettes and medallions and gold score medal — dark theme retained for board + scoreboard/log/rules, bright-blue only as the felt/table tone request stands withdrawn. (2) Tiles get a new `xl` size (`h-11 w-11 sm:h-12 sm:w-12`, motif `h-6/h-7`); board cells are `h-12 w-12 sm:h-14 sm:w-14`, pattern/wall rows share the one cell height, floor slots `h-8/h-9`, marker space `h-11 sm:h-12`. Legal-line picking and forced-floor picking glow (emerald / rose) directly on the board; round-end `flashFloor` pulses the floor line red. (3) Opponents render as `w-48` summary cards (avatar, name, score, marker, eliminated state, turn halo, "view board" hint) instead of full boards — `AzulGameView` strides the opponent grid; clicking a card opens a dark full-size read-only `<Modal>` (`AzulPlayerBoard` with `compact={true}`, no pick callbacks) titled `azul.opponentBoard` and closes via `X` / backdrop. The opponent board in the modal registers flight anchors under `seat:{i}:*` (same scheme as the player's own), which is harmless: an open modal simply overrides that seat's rects while it is visible, and closed opponent anchors vanish with measured member vanishing — no anchor conflicts and no flight-layer code changes.
  - **Rationale:** One board component re-used for all three paths (own, opponent card → modal) keeps §29 anchors and picking logic in a single source of truth. The summary cards shrink the felt footprint so the center table + player's own enlarged board dominate the screen — the requested effect. Dark (not bright) is the theme the owner ultimately required, so the panel was re-themed rather than left blue.

