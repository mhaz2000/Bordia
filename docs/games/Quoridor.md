# Quoridor - Game Documentation

> **Status: specification only — implementation NOT started (2026-09-17).**
> This document is the implementation-ready specification for adding **Quoridor** to the
> BoardGamePlatform, authored the same way as the Splendor and Azul specs (spec-first,
> researched from authoritative rules sources, contract-level detail, physical-board-faithful
> UI as a first-class requirement, implementation explicitly forbidden in this step).
>
> This document owns everything about the Quoridor ruleset:
> - authoritative rules specification and ruleset basis (classic/base 9×9 Quoridor only)
> - component inventory and exact wall-slot / coordinate model
> - setup, turn structure, action semantics
> - pawn moves, jumps (straight and aside), wall legality (bounds, overlap, path preservation)
> - information visibility (fully public), randomness (none), determinism
> - game state design, action model, valid-action generation
> - invariants and the edge-case compendium
> - persistence/serialization, events, timers, reconnection, concurrency
> - frontend requirements (physical-board-faithful UI as a first-class requirement)
> - localization, error model, testing specification
> - integration checklist, rules audit trail, game-specific Critical Decisions
>
> Platform-wide contracts (IGame, player-view capability, error localization, SignalR
> topology, timers, integration checklist) remain in the root `AGENTS.md`. This file records
> only Quoridor rules, interpretation, implementation decisions, and Quoridor-specific
> critical decisions. `#`-references (`#5`, `#22`) point at sections of this document;
> `I-n`, `OD-n` cross-reference the invariants (#15) and open interpretation defaults (#39).

---

## Table of contents

1. [Scope and game properties](#1-scope-and-game-properties)
2. [Component inventory](#2-component-inventory)
3. [Canonical coordinates and orientation](#3-canonical-coordinates-and-orientation)
4. [Setup and seating](#4-setup-and-seating)
5. [Turn structure and victory](#5-turn-structure-and-victory)
6. [Pawn movement — ordinary steps](#6-pawn-movement--ordinary-steps)
7. [Pawn movement — jumping](#7-pawn-movement--jumping)
8. [Wall placement — legality 1: slot, bounds, overlap](#8-wall-placement--legality-1-slot-bounds-overlap)
9. [Wall placement — legality 2: path preservation](#9-wall-placement--legality-2-path-preservation)
10. [Wall supply](#10-wall-supply)
11. [Turn and action processing contract](#11-turn-and-action-processing-contract)
12. [First-player choice and fairness](#12-first-player-choice-and-fairness)
13. [Reading guide / cross-references](#13-reading-guide--cross-references)
14. [State — `QuoridorState`](#14-state--quoridorstate)
15. [Invariants (`I-1` … `I-12`)](#15-invariants-i-1--i-12)
16. [Actions and payloads](#16-actions-and-payloads)
17. [Error model](#17-error-model)
18. [Timers](#18-timers)
19. [Concurrency, staleness, security](#19-concurrency-staleness-security)
20. [Events and the game log](#20-events-and-the-game-log)
21. [Frontend architecture (roles)](#21-frontend-architecture-roles)
22. [Reconnection and session lifecycle](#22-reconnection-and-session-lifecycle)
23. [Determinism and competitive fairness](#23-determinism-and-competitive-fairness)
24. [Visibility: the whole state is public](#24-visibility-the-whole-state-is-public)
25. [Frontend product requirements (the board is the product)](#25-frontend-product-requirements-the-board-is-the-product)
26. [Frontend: geometry derived from coordinates](#26-frontend-geometry-derived-from-coordinates)
27. [Frontend: interaction model (pawns)](#27-frontend-interaction-model-pawns)
28. [Frontend: interaction model (walls)](#28-frontend-interaction-model-walls)
29. [Frontend: turn strip, counters, log](#29-frontend-turn-strip-counters-log)
30. [Frontend: the board's UI states](#30-frontend-the-boards-ui-states)
31. [Frontend: animations (state-diff, cosmetic, skippable)](#31-frontend-animations-state-diff-cosmetic-skippable)
32. [Frontend: accessibility](#32-frontend-accessibility)
33. [Frontend: RTL and Farsi](#33-frontend-rtl-and-farsi)
34. [Frontend: responsive and touch](#34-frontend-responsive-and-touch)
35. [Frontend: performance](#35-frontend-performance)
36. [Testing specification](#36-testing-specification)
37. [Edge-case compendium](#37-edge-case-compendium)
38. [UI state → action → animation conformance matrix](#38-ui-state--action--animation-conformance-matrix)
39. [Rules audit trail and authoritative sources](#39-rules-audit-trail-and-authoritative-sources)
40. [Integration checklist (for the implementation agent)](#40-integration-checklist-for-the-implementation-agent)

---

## 1. Scope and game properties

Include the **classic, base Quoridor only**. Everything the physical Gigamic game ships in
its standard box — 9×9 board, 20 walls, 2 or 4 pawns, no randomness.

| Property | Value |
|---|---|
| GameType (IGame) | `"Quoridor"` |
| Player count (supported) | **2 or 4** only. 1 and 5+ rejected; **3 is rejected** (see OD-1). |
| MinPlayers / MaxPlayers | `2` / `4` |
| Hidden information | **None.** All pawn positions and all placed walls are public. |
| Randomness | **None.** The only randomness in the entire platform interaction is the seeded random choice of the first player (OD-2). |
| Determinism | Fully deterministic from `GameOptions.Settings.Seed` + the seated player list — the same seed produces the identical game. |
| Shared spatial board | Yes — like Azul, the board is shared and stationary; seats do not own private regions. |
| Engine base | `GameBase` (`GameType`/`MinPlayers`/`MaxPlayers` + default `IsGameOver`/`GetWinner`), all core members overridden. |
| Spin on `IGame` | `CreateGame`, `ProcessAction`, `GetValidActions` all overridden; `IsGameOver`/`GetWinner` can use the base defaults (state-root driven). |
| `IPlayerViewGame` | **Not implemented.** Quoridor has zero hidden state; the full authoritative state is broadcast and served to every client (#24). |

Scope exclusions (all deliberate, mirroring the AGENTS.md Out-of-Scope convention):

- Quoridor **Mini / Pocket / Kid** (7×7), Deluxe, Giant — not supported.
- **3-player** games (asymmetric wall split, Wikipedia: "not recommended") — excluded (OD-1).
- Fan/house variants (e.g., no-walls mode, Blokus-style scoring, "jump always", double jumps) — excluded.
- AI players, tournaments, teams of zero (there are no non-team 4p rules), replays, chat inside the engine.
- Any new platform abstraction: no generic `Board`/`Wall`/`Pawn`/`IGenericBoardGame`, no Quoridor DbContext, no runtime plugin, no factory. Quoridor owns its board model inside `Games/Quoridor`.

---

## 2. Component inventory

| Component | Count | Notes |
|---|---|---|
| Square cells | 81 | A 9×9 grid of cells. |
| Pawns | 2 or 4 | One per seat. |
| Walls | 20 total | Flat pieces, each **two cells long**, placed in grooves between cells. |
| Wall distribution | 2 players: **10 each**; 4 players: **5 each** | 20 walls always. |
| Timing | none physical | Platform turn/total timers apply (#18). |

---

## 3. Canonical coordinates and orientation

The board is an absolute, physical, non-rotating reference frame. The screen rendering may
spin a view toward a seat (frontend, #26), but **coordinates never change** and the board is
**never mirrored for RTL** (#33).

- **Cells** are indexed `(r, c)` with `0 ≤ r ≤ 8` (row; top/North = 0) and `0 ≤ c ≤ 8`
  (column; left/West = 0).

```
row r=0   [0,0] [1,0] [2,0] [3,0] [4,0] [5,0] [6,0] [7,0] [8,0]   <- West column c=0
row r=8   [0,8] [1,8] ...                ...                [8,8]   <- East column c=8
```

- **Grooves** (the channels walls sit in) live *between* cells.
  - Horizontal groove at **row-edge** `rw`, `0 ≤ rw ≤ 7`, runs between row `rw` and row
    `rw+1`.  Within that groove, the **unit edges** are indexed by column `c`, `0 ≤ c ≤ 8`
    (the edge between cell (`rw`,`c`) and (`rw+1`,`c`)).
  - Vertical groove at **column-edge** `cw`, `0 ≤ cw ≤ 7`, runs between column `cw` and
    column `cw+1`.  Within that groove, the unit edges are indexed by row `r`, `0 ≤ r ≤ 8`
    (the edge between cell (`r`,`cw`) and (`r`,`cw+1`)).

- **Wall slot** (canonical representation, mirrors the frontend rendering model #26): a wall
  **covers two consecutive unit edges** in one groove.

  - `H(rw, c)` — horizontal wall in row-edge `rw` covering unit edges (`rw`,`c`) and
    (`rw`,`c+1`), with `0 ≤ rw ≤ 7` and **`0 ≤ c ≤ 7`**.
  - `V(r, cw)` — vertical wall in column-edge `cw` covering unit edges (`r`,`cw`) and
    (`r+1`,`cw`), with **`0 ≤ r ≤ 7`** and `0 ≤ cw ≤ 7`.

  `c ≤ 7` for `H` and `r ≤ 7` for `V` are exactly the "may not jut out of the board"
  constraint — a wall flush with the East rim covers the unit edges 7 and 8 (the last
  column), so index 7 is the last legal home for its first edge.  The pair of indexes always
  names the **lower/left-most unit edge of the two** and the orientation selects which axis
  the other unit edge extends along.  An alternative lattice-point view — "wall at the
  crossing `(x,y)` facing `h|v`" — is a pure re-encoding; the engine canonicalizes to the
  slot form above and so does the action payload (#16).

```
Vertical wall V(r, cw):  two unit edges stacked in column-edge cw:
   cell(r,   cw) | cell(r,   cw+1)
    ____v-unit____        <- unit edge (r,   cw)
    ____v-unit____        <- unit edge (r+1, cw)   (r <= 7)
   cell(r+1, cw) | cell(r+1, cw+1)

Horizontal wall H(rw, c):
   cell(rw,   )  cell(rw + 1, )   (interior cells omitted)
      [ h-unit ]  [ h-unit ]
   edge (rw, c)   edge (rw, c+1)     (c <= 7)
```

---

## 4. Setup and seating

`CreateGame(GameOptions)` reads exactly the players in `options.Players` (seat order) and
`settings.Seed`.

1. Validate player count: `2` or `4` exactly, else throw the coded domain exception
   `quoridor.playerCount` (see OD-1).
2. Assign seats. Seat order is **clockwise starting at the South side** (matches the
   reference-relevant ordering used by algebraic Unicode diagrams and the Wikipedia notation
   examples):

   | Seat | Side | Start cell | Goal side | Goal squares | Team (4p) |
   |---|---|---|---|---|---|
   | 0 | South | `(8, 4)` | North | row `0`, **any column** | Team A |
   | 1 | West | `(4, 0)` | East | column `8`, **any row** | Team B |
   | 2 | North | `(0, 4)` | South | row `8`, **any column** | Team A |
   | 3 | East | `(4, 8)` | West | column `0`, **any row** | Team B |

   In a 2-player game seats `0` (South) and `2` (North) are used; seats `1` and `3` do not
   exist and the team rule is meaningless (`Winner` is the own-pawn mover).  In the 4-player
   game seats `0..3` all exist.  A seat's **goal** is the 9 cells of its opposite edge.
3. Distribute walls: 2 players → `10` per seat; 4 players → `5` per seat.
4. Choose the first player: **uniform random seat** among 0..(players−1), seeded by
   `settings.Seed` (OD-2; the platform precedent from Silver/Splendor/Azul).
5. No walls placed; every pawn on its start cell; `CurrentPlayerIndex = first seat`.

---

## 5. Turn structure and victory

- A turn = **one** of the two action types, exclusively:
  1. **Move** the player's own pawn (#6, #7), or
  2. **Place one wall** from the player's unused supply (#8, #9, #10).
- After a valid action, `CurrentPlayerIndex` advances deterministically: the next seat in
  clockwise order mod `players`, **skipping eliminated seats** (#18).  Team members do NOT
  interleave differently in 4p — turn order is plain `0 → 1 → 2 → 3 → 0…`.
- **Victory:** the moment a pawn lands on any one of the 9 goal squares of its own goal side,
  that pawn's owner (2p) or **team** (4p, OD-3) wins immediately; `IsOver = true`,
  `Winner = the PlayerId of the pawn that crossed`, `GameEnded = true` in the `GameResult`.
  The game ends mid-action, instantly; no further moves or walls.
- Draws are not possible under normal play (a pawn physically reaches an edge and wins), but
  the platform's `GameTimeExpired` force-finish produces a **draw** (`Winner = null`,
  `IsOver = true`) per OD-4.

---

## 6. Pawn movement — ordinary steps

A move is always **one** destination cell per turn, never two steps except the jump of #7.

- An ordinary step moves a pawn **one cell** orthogonally (North/South/West/East), reaching
  a cell at Chebyshev-manhattan distance exactly 1.
- Legality of the step:
  1. Target cell is in bounds (`0 ≤ r,c ≤ 8`).
  2. The shared edge between the origin and target is **not a wall** (check the wall-edge
     model of #3: a `H`/`V` wall segment covering that unit edge).
  3. Target cell is **not occupied** by any pawn (own or other; a cell holds at most one
     pawn).
- Pawns may move backwards and sideways freely (no direction restriction beyond the above).
- A step may **land on a goal square** — that is simply how the game is won (#5).

---

## 7. Pawn movement — jumping

Jumps are the only non-trivial movement rule. They are **optional** — a player may always
take a legal ordinary step instead. A pawn may jump **over exactly one pawn**, never more.

### 7.1 Setup of a jump

Two pawns are “facing” when they occupy **orthogonally adjacent** cells with **no wall
between them**, and it is the turn of one of them.  Let the mover `M` be at `(r0,c0)` and
the jumped pawn `J` next to it, in one of the four directions `d` (N/S/W/E).  The “jump
behind” landing square is `B = (r0,c0) + 2·d`.

### 7.2 Straight jump (Jump Behind)

`M` may move directly to `B` iff:

1. `B` is in bounds (not off the edge);
2. `B` is unoccupied by any pawn;
3. the edge between `J`'s cell and `B` is **not a wall** (`B` is not “fenced in” from `J`).

When legal, the straight jump is a single move of the pawn to `B`.  In 4p, `J` may be a
**teammate** or an opponent — the rule is identical (Wikipedia: the two pawns “face each
other”; BGA: “jump the opponent's pawn”).  It is never mandatory to jump.

### 7.3 Aside jump (Jump Aside / diagonal)

If the straight jump is **not** legal (any of 7.2.1–3 fails) then, and only then, `M` may
instead move **diagonally one cell** onto either of the two squares diagonally adjacent to
`J` on `M`'s far side:

- In the East-following example (`J` east of `M`): candidates `(r0−1, c0+1)` and
  `(r0+1, c0+1)`.
- Generally: the two cells that are diagonally adjacent to both `B`'s row-line and `J`'s
  cell (the two corner cells of the `2×2` block whose inner corner is B… precisely: the two
  cells `(r0±1, c0+sign(c))` for an East/West face, or `(r0+sign(r), c0±1)` for a
  North/South face), while `J` is the near corner.

Each aside candidate `S` is legal iff **all** of:

1. `S` is in bounds;
2. `S` is unoccupied;
3. the orthogonal neighbour of `M` on the way to `S` (the cell orthogonally adjacent to both
   `M` and `S`, other than `J`) is such that **the two unit edges joining `M`−`S` through the
   corner are both wall-free** — i.e. one may not jump “sideways through” a wall at all
   (Wikipedia: “Walls may not be jumped, including when moving laterally”).

The aside jump is available only because the straight jump is impossible; it never replaces
a legal straight jump (OD-5, majority rule — verify vs the printed rulebook).

### 7.4 Summary truth table (East-facing example, `M` at `(r,c)`, `J` at `(r,c+1)`)

| Behind cell `(r,c+2)` | B legal? | Aside squares available |
|---|---|---|
| free, in-bounds, no wall behind | yes — jump straight | aside not offered for *this* case (straight legal) |
| occupied by a 3rd pawn | no | perhaps `(r±1, c+1)` |
| off the board (c+2 = 9) | no | perhaps `(r±1, c+1)` |
| blocked by a wall behind `J` | no | perhaps `(r±1, c+1)` |
| in bounds but wall-blocked + both aside edges walled | no | none — no jump whatsoever |
| `J` not orthogonally adjacent to `M` | n/a | no jump (never) |
| `M` and `J` separated by a wall | n/a | no jump (they never face) |

---

## 8. Wall placement — legality 1: slot, bounds, overlap

- A wall placement action names a **slot** `(Row, Col, Orientation)` in the canonical edge
  form of #3 (`H`/`V`, `H(Row,Col)` covers edges `(Row,Col)`+`(Row,Col+1)` with `Col ≤ 7`;
  `V(Row,Col)` covers `(Row,Col)`+`(Row+1,Col)` with `Row ≤ 7`).
- **Bounds:** the slot must be fully inside the board — equivalent to `Col ≤ 7` for `H` and
  `Row ≤ 7` for `V` — else `quoridor.wallOutOfBounds`.  A wall flush with the board edge
  (East rim for `H`, South rim for `V`) is legal.
- **Overlap:** a wall may be placed only where it shares **no unit edge** with any existing
  wall, and where a perpendicular wall would not **cross through its middle** (the `+`
  junction — each wall passes through the other's interior point).  Walls may **touch at a
  single lattice point (corner)** — two `H` walls in the same row-edge with a shared vertex
  but no shared unit edge, or an `H` and a `V` wall whose segments meet end-to-side
  (`T`-touch) or corner-to-corner.  They may **never share a unit edge** (two `H` walls
  covering a common column edge; a `V` that lands exactly along an already-covered edge),
  and an `H`/`V` pair with the **same `(Row, Col)` slot** is a visible `+` cross → illegal.
  Enforcement is purely geometric: the candidate wall's 2 unit edges must not intersect the
  union of every existing wall's 2 unit edges, and its slot must not equal any perpendicular
  existing wall's slot.  Violation → `quoridor.wallOverlap`.  (OD-6 records that
  corner/tangential touching is the prescribed reading; the only things ever forbidden are
  shared-edge overlap and the `+` slot-crossing.)
- Placing a wall where a pawn currently stands is **legal** — pawns occupy cells, walls
  occupy grooves, they cannot collide.  Path preservation (#9) still applies, and a pawn may
  later be blocked by a wall placed adjacent to it, including while it stands beside it.

---

## 9. Wall placement — legality 2: path preservation

- A wall placement is legal only if, **after** the placement, **every pawn still has at least
  one complete path** from its current cell to any goal square of its own goal side.
  (BGA: “May not block all paths for each pawn to reach their destination side.”)
- Formally: `PathExistsForEverySeat()` where, for each seat, BFS (4-neighbour, walls only
  block edges, **pawns do NOT block pathing** — a pawn standing on a cell does not obstruct
  the *reachability* check, matching the physical rule where reachability ignores pawn
  occupation) finds any cell of the seat's goal set.
- A pawn already standing on a goal square trivially satisfies its own check (BFS returns
  `true` with 0 steps).
- Violation → `quoridor.wallBlocksPath`.  This is the single most-important Quoridor
  invariant and it is enforced **on placement only** (walls are permanent; a wall that is
  legal at placement time remains on the board forever and the invariant never needs
  re-checking for existing walls — except in the AFK-elimination edge of `#18`, see OD-9).
- Perfence note: the BFS is over 81 cells; worst case 4 pawns × 81 × 4 edges.  It runs once
  per `PlaceWall` candidate and once per `GetValidActions` enumeration.  Trivial, but the
  harness asserts a sanity wall-clock budget (#36).

---

## 10. Wall supply

- Each seat starts with its per-player-count supply (#4).  Placing a wall decrements the
  owner's supply by 1.  Walls are **never** reclaimed, moved, or removed.
- A seat with `0` walls remaining cannot issue `PlaceWall` (`quoridor.noWallsLeft`); when
  *all* seats are at 0, the game continues with pawn moves only (there is no rule that ends
  play when the walls run out — the canonical “shortest-path endgame count” of Wikipedia is
  a play-by-agreement convention, **not** a platform rule; OD-7).

---

## 11. Turn and action processing contract

The engine follows the platform `IGame` contract exactly like Azul/UNO:

- `CreateGame(options)` → initial `GameState` (#4, #14).
- `ProcessAction(state, action)` — **never mutates the input state**; returns a new state on
  success or a `GameResult` carrying `IsValid=false` + `ErrorCode`/`ErrorArgs` on failure.
- `GetValidActions(state, playerId)` — full enumeration of every legal `MovePawn` target and
  every legal `PlaceWall` slot for the acting player, or an **empty list** when `playerId`
  is not the current player / the game is over.  Number of entries bounded: ≤ 4 move
  targets (`≤ 4` because a pawn has 4 neighbours at most; jump targets included) and ≤ 128
  wall slots (8×8×2 − overlaps − path-blocked).  Harness asserts this bound (#36).
- Actions are **atomic**: a `PlaceWall` that technically succeeds but the action's `SequenceNumber`
  is stale (already applied) is handled by the platform concurrency layer (#19), not the engine —
  but the engine must still make identical actions idempotency-safe to the extent that a
  **duplicate** of a genuinely-applied action in a *newer* state fails as invalid
  (`quoridor.notYourTurn` or `quoridor.invalidState` depending on seat/step), exactly the
  Azul/UNO behaviour.

---

## 12. First-player choice and fairness

- OD-2 prescribes a seeded uniform-random first seat (platform precedent).  Everything else
  is fully deterministic.
- 2p is symmetric except for the coin toss; 4p is symmetric under the clockwise seat order.

---

## 13. Reading guide / cross-references

Timer integration is specified in #18, reconnection and session lifecycle in #22,
state/action/event contracts in #14–#20, concurrency in #19, the frontend in #21 and
#25–#35, testing in #36, and the integration checklist in #40.

---

## 14. State — `QuoridorState`

Mirrored PascalCase on the client (`quoridor.ts`, #21).  `QuoridorState` is stored, like all
games, as a JSON string in `GameState.Data["QuoridorState"]`, read via `TryGetString`
(platform 2026-09-06 decision).  `PlayerNames` joins are handled by the platform pattern
(`GameState.Players` + `Data["PlayerNames"]`).

```json
{
  "SeatCount": 2,
  "PlayerIds": ["<guid>", "<guid>"],
  "PlayerNames": { "<guid>": "Ali", ... },
  "Pawns": [ { "Row": 8, "Col": 4 }, { "Row": 0, "Col": 4 } ],   // per seat; a seat = its pawn
  "Walls": [],                                                    // placed walls
  "Wall": { "Row": 2, "Col": 3, "Orientation": "H" },
  "WallsRemaining": [ 10, 10 ],
  "CurrentPlayerIndex": 0,
  "EliminatedSeats": [],
  "EventLog": [],
  "WinnerSeat": -1,
  "Draw": false,
  "TurnStartUtc": "...",
  "TimerConfig": { "BaseTurnSeconds": 90, "MaxBankSeconds": 180, "MaxOverrunSeconds": 15, "MaxAfkTurns": 3, "TotalGameTimeMinutes": 60 },
  "PlayerTimers": [ { "BankSeconds": 0, "DeferredPenaltySeconds": 0, "ConsecutiveTimeouts": 0 }, ... ]
}
```

Semantics:

- `Pawns[i]` = the current cell of seat `i`'s pawn (`(Row, Col)`).
- `Walls` = every placed wall, canonical slot form of #3, in placement order (append-only).
- `WallsRemaining[i]` = supply left for seat `i`.
- `EliminatedSeats` = AFK-removed seats (#18); `CurrentPlayerIndex` never lands on one.
- `WinnerSeat` + `Draw` are *refined mirrors* of the state-root `Winner`/`IsOver` for the
  client; the state root remains authoritative.
- All timer fields follow the platform's UNO/Silver/Azul timer model (#18).
- **Public information:** the entire object is broadcast verbatim to every client (#24).
  There is **no** hidden field, no `Bag`-like secret, no projection.

---

## 15. Invariants (`I-1` … `I-12`)

Enforced by construction and asserted by the harness after every single action of a seeded
soak (#36):

- **I-1** `SeatCount ∈ {2,4}`; `Pawns`, `WallsRemaining`, `PlayerTimers`, `EliminatedSeats`
  have exactly `SeatCount` entries.
- **I-2** Every pawn cell is in bounds.
- **I-3** At most one pawn per cell (occupancy — includes start cells).
- **I-4** Every placed wall is in bounds (slot form valid).
- **I-5** No two placed walls share a unit edge (overlap-free).
- **I-6** **Path preservation is always true** for every seat (BFS semantics of #9).
- **I-7** `WallsRemaining[i] + wallsPlacedBy(i) = initialSupply` for every seat.
- **I-8** `CurrentPlayerIndex` is never an eliminated seat; turn order advances one seat at a
  time skipping eliminated seats.
- **I-9** `isOver ⇔ (winner set ∨ draw)`; once over, every subsequent action fails
  (`quoridor.gameOver`) and state stops changing.
- **I-10** A pawn reaching a goal square coincides with `gameEnded = true` in that same
  `ProcessAction`, exactly once.
- **I-11** Determinism: same seed + same action sequence → byte-identical state at every
  step (harness fixture, seeded).
- **I-12** `EliminatedSeats` only ever grows; an eliminated seat's own walls stay on the
  board; its pawn is removed from play (OD-9).

---

## 16. Actions and payloads

Two player action types, exact `ActionType` strings and PascalCase payloads (System.Text.Json
default, matching the frontend `quoridor.ts` builders):

| ActionType | Payload | Notes |
|---|---|---|
| `MovePawn` | `{ "Row": int, "Col": int }` | Destination cell, absolute coordinates. Single step, straight jump, or aside jump — the engine rederives which from the current board. Row/Col are the *target* cell. |
| `PlaceWall` | `{ "Row": int, "Col": int, "Orientation": "H"\|"V" }` | Canonical slot form of #3 (`H`: first/left unit edge, `Col ≤ 7`; `V`: first/top unit edge, `Row ≤ 7`). |

Payload validation: strict (extra fields rejected, out-of-range ints rejected, wrong
`Orientation` string rejected) — the platform's `azul.invalidPayload` precedent generalizes
to `quoridor.invalidPayload`.  `PlayerId` in the action must equal the current seat.

---

## 17. Error model

`Games/Quoridor/Errors.cs` (game-local; codes surface through `GameResult.ErrorCode`/`Args`
→ server middleware localizes).  Every code below needs a template in **both**
`errors.en.json` and `errors.fa.json` (platform localization decision; `quoridor.*`
namespace, `{0}`-style positional args).

| Code | Meaning |
|---|---|
| `quoridor.playerCount` | `CreateGame` rejects a player count other than 2 or 4 (incl. 3, OD-1) |
| `quoridor.gameOver` | any action after `IsOver` (I-9 / I-10) |
| `quoridor.notYourTurn` | actor ≠ current seat |
| `quoridor.invalidPlayer` | actor seat doesn't exist / not a game player |
| `quoridor.playerEliminated` | actor was AFK-removed (#18) |
| `quoridor.invalidState` | missing/corrupt `QuoridorState` in `Data` |
| `quoridor.unknownActionType` | unparseable `ActionType` |
| `quoridor.invalidPayload` | malformed JSON / extra fields / bad types |
| `quoridor.outOfBounds` | `MovePawn` target or `PlaceWall` slot out of range |
| `quoridor.cellOccupied` | move target occupied by any pawn |
| `quoridor.moveBlockedByWall` | the unit edge between origin and move target (or inside a jump) is a wall |
| `quoridor.invalidJump` | the claimed jump is not a legal straight/aside jump per #7 |
| `quoridor.wallOverlap` | candidate wall shares a unit edge with an existing wall (not a corner touch) |
| `quoridor.wallOutOfBounds` | slot index range violation (covers "may not jut out") |
| `quoridor.wallBlocksPath` | placement would cut some pawn off from its goal (path-preservation fails) |
| `quoridor.noWallsLeft` | seat supply exhausted |
| `quoridor.timerNotExpired` | player-forced `TurnTimeout`/`GameTimeExpired` before the deadline |
| `quoridor.timeNotUp` | system `TurnTimeout` dispatched without an expired deadline |

---

## 18. Timers

The platform owns the timer *enforcement* surface (`GameState.NextActionDeadlineUtc`,
`GameState.GameEndsAtUtc`, `TurnTimeoutService`) and the engine owns the *consequences* —
identical architecture to UNO/Silver/Azul:

- Turn timers use the platform `TimerConfig` block (base 90s, max allowance 180s, overrun
  15s, AFK 3, total 60 min as the Quoridor defaults — same shape and semantics as
  Silver/Azul defaults; recorded in code as config).
- **`TurnTimeout`:** skip only.  Never fabricate a move or wall.  The turn advances; the
  skipped seat keeps its walls; a `skip` event enters the log; the skipped seat's
  `ConsecutiveTimeouts` increments; at `MaxAfkTurns` the seat is **eliminated**:
  - 2p → the **other** player instantly wins (`Winner = survivor`,
    `GameEnded = true`) — the platform's post-AFK 2p-survivor rule (covered by `I-10`? no —
    I-10 is about goal-crossing; record this as a distinct win path; the harness treats both
    paths as valid `gameEnded` moments).
  - 4p → the seat is eliminated; its pawn is **removed** from the board (OD-9), walls stay,
    the team's surviving member keeps playing (a team still wins if either pawn crosses;
    if *both* members of a team are eliminated, that team can no longer win — the other team
    wins on its next crossing; the pair of eliminations does not instantly end the game).
- **`GameTimeExpired`:** `Winner = null`, `IsOver = true` — a **draw** (OD-4) unless the
  deadline fires on a session that is already over.  “Who is closer” countback is explicitly
  NOT implemented (OD-4 rationale).
- `NextActionDeadlineUtc` is set at create/action as the platform pattern; `GameEndsAtUtc`
  at create = `CreatedUtc + TotalGameTimeMinutes`.

---

## 19. Concurrency, staleness, security

- **Server authoritative.** All legality (#6–#10) is enforced in the engine; the client only
  previews.  A forged client payload (impossible jump, overlapping wall, wall that cuts a
  path, move from the wrong seat) is rejected by the engine with the coded error (#17).
- Stale/duplicate actions fail naturally (`quoridor.notYourTurn`, `quoridor.invalidState`)
  after the session moved on.
- The platform's optimistic-concurrency version token + retry loop
  (`ProcessGameActionCommandHandler`, Advis loaded) and `TurnTimeoutService`'s
  loss-of-race handling are reused unchanged; no Quoridor-specific concurrency code.
- Idempotency of `ReconnectPlayer` (#22) is the platform's.
- No head-of-line issues: `GetValidActions` and every requirement here is safe to run inside
  one `ProcessAction`.

---

## 20. Events and the game log

Events are emitted as structured envelopes in `GameResult.Events` per platform convention
(the icons/log the client renders via `events.*` keys).  Envelope `{"c":"code","d":{...}}`:

| code | payload | when |
|---|---|---|
| `move` | `{seat, from:{r,c}, to:{r,c}}` | ordinary single step |
| `jump` | `{seat, from, to, kind:"straight"\|"aside"}` | any jump (#7) |
| `wall` | `{seat, row, col, orientation}` | wall placed |
| `win` | `{seat}` | pawn crossed to goal (2p always, 4p via this mover) |
| `teamWin` | `{seat, team:"A"\|"B"}` | 4p win banner (the `winner` playerId remains the mover) |
| `draw` | `{}` | `GameTimeExpired` force-finish |
| `skip` | `{seat, reason:"timeout"\|"gameTimeExpired"}` | timeout skip / forced finish |
| `eliminated` | `{seat, reason:"afk"}` | AFK elimination |

`QuoridorState.EventLog` accumulates these envelopes (platform pattern) plus plain-text
legacy fallbacks.  The frontend **icons and copy** come from the `events.quoridor.*`
dictionary; the numbers/positions come from the payload params.  The animation layer does
**not** trust the log for motion — it state-diffs (#31).

---

## 21. Frontend architecture (roles)

Exact platform pattern from Azul (§27–§31) applied to Quoridor:

| File | Role |
|---|---|
| `frontend/src/features/game/quoridor.ts` | Client mirror of `QuoridorState` (PascalCase, exact), safe parser `parseQuoridorState` that **rejects foreign/leaked schemas** (defensive, like `parseSilverState`/`parseAzulState`), action builders `quoridorActions.move(row,col)` / `.placeWall(row,col,'H'\|'V')`, and a **client rule mirror** for highlighting/previews only (#27–#28). |
| `frontend/src/features/game/QuoridorGameView.tsx` | The game page body: seat rail, board, turn strip, walls counters, log, overlays. |
| `frontend/src/features/game/quoridorBoard.tsx` (or inline) | The physical-board component, single source of truth for geometry (#26, #30). |
| `frontend/src/features/game/quoridorFlights.ts` | State-diff animation orchestrator (#31), same pattern as `azulFlights.ts`. |
| `frontend/src/features/game/GamePage.tsx` | Add the `'Quoridor'` view-switch branch (same as the Azul branch). |
| `frontend/src/features/lobby/gameMeta.ts` | Add `Quoridor: { gradient: '…' }` to `GAME_THEME` (e.g., `'from-stone-500 via-amber-700 to-stone-800'` — a wood/seafield vibe matching the physical board), and remove any `comingSoon` placeholder only at implementation time. |
| `frontend/src/i18n/locales/en.ts` / `fa.ts` | `games.Quoridor` section (title/tagline/description/`rules[]`/`actionCards[]`, exactly the `Dict['games']['UNO']` shape), `quoridor:` view-string group, `events.quoridor.*` (both locales, `fa: Dict` compile parity). |
| `shared/api/game.ts` | No backend-client change: `GameState`, actions and events are already generic. |

---

## 22. Reconnection and session lifecycle

- Reuses the platform `ReconnectPlayer` (single-tab takeover with connection-identity
  idempotency, re-register connection, `JoinSession` idempotent — the 2026-09-06 platform
  decisions), pause/resume, `sessions/mine` rejoin, and the chat drawer exactly as the
  shipped games.
- Reconnect returns the **same full state** to the rejoining player as to everyone else —
  there is nothing to hide (#1).
- On disconnect nothing is removed from the board; a disconnected seat still counts as
  having a turn (turn keeps ticking; timeout/AFK semantics of #18 apply as usual).
- A player whose session ended sees the standard game-over modal; the session's
  `sessions/mine` entry disappears when `status = Finished` per platform.

---

## 23. Determinism and competitive fairness

- Zero randomness in rules means zero RNG state in `QuoridorState`.  The only randomness is
  the platform's seeded first-seat drawer at create (#4, OD-2); the seed lives only in
  `settings.Settings` and is never re-derived from the state.
- Same seed + same seats + same action list = identical history (I-11) — the harness bounds
  the soak accordingly (#36).
- Mirror symmetry is a strategy feature, not a rules feature: nothing in the engine
  specially handles mirrored games.

---

## 24. Visibility: the whole state is public

- The engine does **not** implement `IPlayerViewGame`; `PlayerViewProjection` passes the
  authoritative state through untouched (#1).
- This makes Quoridor the platform's first fully-open spatial game chosen *because* its
  information model is trivial; do not invent secrets (no opponent-wall “tint” visibility
  gating, no fog of war).

---

## 25. Frontend product requirements (the board is the product)

The Quoridor screen must read as a **crafted wooden abstract board game**, matching the
Azul frontend standard (“reproduce the physical board as closely as reasonably possible”),
per the platform's established frontend-first-class requirement. Concretely:

1. **The 9×9 grid is the whole play surface.**  No deck lists, no admin tables, no
   steppers.  The single interaction surface is the board itself (cells + grooves).
2. Pawns and walls are **themed pieces** with real material feel (top-down wooden look,
   subtle grain/bevel, shadows); wall pieces visibly span **two grooves** with the classic
   two-notch silhouette; pawns are round wooden tokens with a ring, tinted per seat
   (2p: amber vs slate; 4p: amber, emerald, indigo, rose) and optionally an owner-stripe on
   each wall.
3. The **grooves are real** — visible channels, as on the physical board — not a thin
   border trick; the double-resolution grid rendering (#26) renders grooves as physical
   material.
4. The frame keeps the platform game-page frame (dark-glass header, status strip, seat
   panel + game log, chat drawer).  “Your seat” may be rotated to the bottom of the board
   (canvas rotation, coordinates unchanged — #3).
5. Minimal menus: help modal + game log in the same drawer pattern as the other games.
6. Anti-goals (rejected for the play surface): 3D-perspective board that obscures cells, a
   “chessboard with no grooves”, using coordinates-labels as the primary interface, and any
   wall-preview that isn't a real wall piece on the actual grooves.

Usability over literalism: we do not rotate *opponents'* seating to face them; the board is
one canvas, players may spin it toward their seat via a small compass control (or it stays
fixed; see #26 note).

---

## 26. Frontend: geometry derived from coordinates

The board component computes all layout from the canonical coordinates (#3) — there is
**no** hard-coded pixel table.

- **Double-resolution CSS Grid**: a 17×17 track grid.  Every even track = a cell row/col
  (`flex = 1`), every odd track = a groove (`flex = grooveRatio`, e.g. 0.35).  With
  `grid-auto-rows/cols: minmax(0,1fr)` cells stay square and grooves proportional.
  - Cell `(r,c)` → `grid-row: 2r+1`, `grid-column: 2c+1`.
  - `H(rw,c)` wall → spans `grid-row: 2rw+2` (the groove track), `grid-column: 2c+1 / span
    2` (two cell tracks + the inner groove).
  - `V(r,cw)` wall → `grid-column: 2cw+2`, `grid-row: 2r+1 / span 2`.
  - This is automatically **RTL-proof and rotation-proof**: it is pure absolute grid
    placement, never `left/right` logical directions.  The board never mirrors under any
    `dir` (#33).
- Pawns absolutely-positioned to their cell center (transform-translate), animated by the
  flight layer (#31).
- Unit derivation is declarative; a board-size CSS variable scales everything (responsive,
  #34).
- Fidelity cues: the board sits on the felt as a wooden slab with an inlaid grid; the goal
  sides carry engraved/「WANTA」-style edge marks per seat color; start cells get a small
  home dot in each seat's color.

---

## 27. Frontend: interaction model (pawns)

Classic hover→highlight→click with **client-side legality mirror only**.

| State | Visual | Action |
|---|---|---|
| Not your turn | pawns static, no highlights | clicks ignored (disabled visuals) |
| Your turn, hover own pawn | natural grip cursor | show **legal target dots** (client mirror #6/#7) |
| Hover a legal target cell | green glow + arrow path hint | click → `MovePawn` |
| Hover an illegal target | red tint + tooltip reason (`moveBlockedByWall` / `cellOccupied`) | click rejected, tooltip |
| Client thinks legal, server rejects | server error toast (coded, localized) | nothing moved; board stays |
| Your pawn adjacent to another | jump targets highlighted with a distinct ring (straight vs aside arrow) | click → `MovePawn` to that cell |

Jumps are never auto-played; the mirror computes exactly the #7 truth table and highlights
only truly-legal cells.  The engine re-validates everything — a mismatch just shows a toast.

---

## 28. Frontend: interaction model (walls)

Design goal: placing a wall is a **single continuous gesture** (hover → orient → drop),
never a form.

- Wall supply ≤ 0: groove hover shows a dimmed slot; click → toast `noWallsLeft`.
- Hovering a groove **segment** (a unit edge) previews an `H` or `V` wall piece of exactly
  two unit edges, oriented to run along the hovered groove; the thumb spans the pair in the
  under-cursor groove, snapping to the 2-unit slot.
  - Rotating: pressing **R** (or a small rotate affordance) flips the preview 90° so it
    spans the *crossing* groove at the same shared lattice corner (the piece keeps one unit
    edge touching the original spot) — matching the physical act of pivoting a wall on its
    single notch.
- Preview legality (client mirror #9/#10/#8): green when legal (no overlap + path preserved
  + in bounds), red otherwise, with a tooltip citing the exact reason
  (`wallOverlap`/`wallBlocksPath`/`wallOutOfBounds`).
- **Do not** block hover/drag on path-check cost: the mirror runs the tiny 81-cell BFS in
  the UI thread per preview (≤ ~0.1 ms) — no async, no debounce, no per-hover HTTP (see
  #35).
- Click = place (`PlaceWall`).  Esc / click-away cancels the preview.  No drag-and-drop of
  walls across the board (a click on the groove is the faithful, accessible model — like
  Azul's click-select decision).

---

## 29. Frontend: turn strip, counters, log

- **Seat rail** (shared platform `GamePlayersPanel` conventions): each seat card shows
  avatar, name, wall supply as **pips** (mini wall glyphs, `WallsRemaining`), “eliminated”
  state, and a turn halo on the active seat.  In 4p, team members share a subtle outline
  (Team A / Team B), and the card shows the team's members.
- Turn indicator on the active card + the platform TimerRing (soft/hard deadlines, signed
  countdown — the UNO/Azul precedent) for the actor.
- Game log renders `events.quoridor.*` strings (with the moved positions e.g. “D۵: H(2,3)”).
- No score area exists; the strip's middle slot shows “N walls left — you” / current player
  hint instead (localized).

---

## 30. Frontend: the board's UI states

From projected state + hub (exact Azul/Splendor taxonomy):

`loading` (waiting-veil) → `yourTurn idle | yourTurn pawnFocused | yourTurn wallPreview |
yourTurn sendInFlight` → `opponentTurn` (their glide/set-down animations play from
broadcast; turn ring on their card) → `reconnecting` (hub-offline chip, board frozen) →
`takenOver` (shared → lobby) → `paused` (session banner) → `gameOver` (winner modal:
2p winner card, 4p team banner “تیم A برد” / “Team A wins”, draw note for `GameTimeExpired`)
→ AFK `removedFromGame` overlay.

Stale-state guard: `parseQuoridorState` returns `null` for any payload that isn't a
recognizable board shape (e.g. missing `Pawns`/`Walls`/`SeatCount`), rendering the
waiting-veil rather than crashing (platform 2026-09-12 leak-guard precedent — though for
Quoridor the guard is purely defense-in-depth, since everything is public).

---

## 31. Frontend: animations (state-diff, cosmetic, skippable)

Follow `azulFlights.ts`/`splendor` `tokenFlight.ts` pattern exactly: diff two consecutive
projected `QuoridorState`s by **(seat → cell)** and **(wall slot set)**, and drive motion
through **transform-based** flights. All ≤ ~350 ms, staggered for parallel walls/pawns.

| observed change | animation |
|---|---|
| seat pawn moved cell `A→B` | glide (translate), slight ease-out; straight jumps get a small arc lift, aside jumps curve |
| `Walls` gained a slot | wall piece **rises and settles** into the groove (scale+translate+opacity, one step overshoot), with a soft visual “set-down” cue (no audio — the platform has no sound system; any future sound must ship behind a platform-level flag) |
| win | goal cell pulse-ring in the winner's color + confetti-less modal (platform standings) |
| draw | gray ring pulse + draw modal note |
| skip / eliminated | seat card flash; in 4p the removed pawn fades out (OD-9) |
| any rejected action | nothing animates (diff is empty — no phantom motion) |

Animations never gate play: input is disabled only during the actor's own in-flight send;
opponent flights are cosmetic and stoppable by `prefers-reduced-motion` (#32).

---

## 32. Frontend: accessibility

- Every cell, groove slot, wall, pawn and seat control has an `aria-label` built from
  localized copy + coordinates (e.g. “move to row 4, column 5” / “place H wall at row 2,
  column 4, horizontal”), honoring `aria-pressed` style semantics for previews.
- Color never carries information alone: team/seat distinctions repeat through glyphs
  (ring patterns, wall-stripe shapes) and names — the colorblind-safe set used by Azul's
  motifs carries over: each seat color also has a unique pawn glyph (plain/ridged/dot/etc.).
- Keyboard: arrow keys move a highlight between legal pawn targets; `W`/`Space` on a groove
  lays a wall; `R` rotates; `Esc` cancels.  Full click path remains for pointers.
- `prefers-reduced-motion` renders instant jumps (no flights).
- High-contrast groove-vs-cell distinction (groove ratio + material tone difference).

---

## 33. Frontend: RTL and Farsi

- The board **never mirrors** — it's an absolute physical object (#3/#26).  All UI chrome
  uses the platform's logical utilities (`ms-`, `me-`, `start-`, `end-`) so headers, seat
  rail, log, and controls flip correctly while the grid stays put.
- Coordinates are rendered in **neutral numerals** (the platform's `.tabular-nums`
  fragments) so `(r,c)` citations read identically in both languages; Farsi event strings
  like `events.quoridor.move` interpolate the coords.
- No English is hardcoded in components; both locales ship with compile-time parity.

---

## 34. Frontend: responsive and touch

- **Cells**: derive board size from `min(viewport height − chrome, viewport width − padding)`
  via one CSS variable; everything (cells, grooves, pawns, walls) scales linearly.
- lg: full board centered, seats along one edge, log side panel.
- md: board centered, seats as a wrap row above/below; log collapses to a drawer (platform).
- sm: board scaled to width, seats as a compact thin rail; wall counter pips shrink.
- Touch: tap = hover-preview + first in a two-tap sequence (tap groove → tap confirm),
  rotation button always visible (no `R` key on touch), target toggle instead of hover.

---

## 35. Frontend: performance

- The board is ~81 cells + ≤20 walls + ≤4 pawns: trivially small DOM.  The double-resolution
  grid renders in one pass; walls/pawns are positioned elements only.
- Client path-check BFS is a micro-loop (~81 cells × 4 edges) run in the render/hover
  handler; no debounce, no memo-cache, no worker needed.  Asserted in the harness against a
  sane micro-budget only conceptually (#36 final bullet); the real check is a fast first
  paint + no layout thrash — transform-only animations, `will-change: transform` on flying
  pieces.
- **Zero per-hover/per-move network calls**; the only traffic is `MovePawn`/`PlaceWall`
  sends and the reflected broadcasts.  Hover previews never hit the server.
- No i18n re-bind churn: the grid component reads `t` once at mount for chrome strings.

---

## 36. Testing specification

`tests/Quoridor.Harness` console project (the Silver/Splendor/Azul pattern:
`Check(name, cond)`, seeded determinism, exit code, `QuoridorHarness.Run()`). Minimum
coverage:

**Setup** — 2p and 4p: seat counts, start cells (from #4 table), wall distribution 10/10 and
5/5, first-seat inside range and seeded (=same seed, same seat), 1p/3p/5p rejected
(`quoridor.playerCount`), deadlines set, state-root fields sane.

**Movement** — orthogonal steps all four directions; boundary cell; blocked-by-wall edge →
`moveBlockedByWall`; occupied target → `cellOccupied`; backwards/sideways legal.

**Jumps** — facing-without-wall yields straight jump; straight-blocked-by-wall, by-3rd-pawn,
off-board → aside offered (both diagonal cells each validated: occupied/wall-corner
blocked); aside never offered when straight legal (OD-5); jumps optional (a plain step is
always still listed); walls never jumped laterally; multiple pawn jumps impossible
(4p: 2-pawn deep wall rejected).

**Walls** — slot/bounds (`wallOutOfBounds`); overlap fixtures: shared unit edge rejected
(same-orientation overlap, offset overlap), corner-touch and `T`-touch allowed, the `+`
junction (identical `(Row, Col)` slot) rejected (OD-6); placement under a pawn allowed;
`noWallsLeft`; wall count across turns.

**Path preservation** — fixtures: a wall cutting the *only* path of any seat → blocked; a
wall that cuts one of two paths → legal; a wall behind a pawn against the edge is legal
(that pawn keeps its other exits); metric: BFS ignores pawn occupation (a pawn standing in a
corridor does not count as a blocked path).

**Turn order & victory** — strict alternation; skip+eliminated seats; reaching any of the 9
goal squares in the seat's goal set wins instantly (`gameEnded`, `Winner` = mover); 4p team
win when the teammate crosses; 2p AFK → survivor-wins (`#18`); 4p AFK → elimination + continue
+ pawn removed (OD-9); both-team-elimination end path.

**Timeouts** — `TurnTimeout` skips only (never fabricates a move) (OD-4 counterpart);
bank/allowance accounting per the platform formula; 3×AFK ladder; `GameTimeExpired` → draw
(`Winner = null`, OD-4); `timeNotUp`/`timerNotExpired` for player-forced system actions.

**ValidActions** — full enumeration equality: every listed `MovePawn` target and
`PlaceWall` slot accepted as-is by `ProcessAction` (Silver/Azul style); empty on wrong
seat/over; count ≤ 4 + ≤ 112 bound.

**Serialization** — `CreateGame → ToJson → FromJson → actions → ToJson → …` loops (the
platform `TryGetString`/`JsonElement` branch); timer deadlines survive; seed path intact;
`PlayerNames` canonicalized.

**Determinism / soak** — same seed → identical 500+ turn game recorded hash at every phase;
different seed → diverges (only via the first-player seat).  Invariant battery I-1…I-12
after **every** action of the soak (definitely including I-6 path-preservation after each
wall and each move).

**Security/staleness** — actions from wrong player, after over (I-9/`quoridor.gameOver`),
duplicated after applied (stale), forged payloads (`invalidJump`, overlapping wall),
replayed connection.  Reconnect returns the same full state (no projection drift possible).

**Frontend (manual checklist)** — highlight parity with the mirror, wall preview rotation,
touch two-tap, responsive folds, RTL (board unmoved), `prefers-reduced-motion`, keyboard
drive, colorblind redundancy.  (Same convention as Azul's manual checklist.)

---

## 37. Edge-case compendium

(OD numbers → #39.  ⋆ marks open-interpretation defaults.)

| Case | Behaviour |
|---|---|
| Straight jump landing cell off the board | not legal; aside candidates only (#7.3) |
| Straight jump landing cell occupied by a 3rd pawn (4p) | not legal; aside candidates only |
| Wall directly behind the jumped pawn | straight blocked; aside candidates only |
| Both aside candidates blocked (wall-corner / occupied) | no jump at all; only ordinary steps remain |
| `M` and `J` separated by a wall | they never “face”; no jump (a wall also cannot be crossed by an ordinary step) |
| Pawn pinned between walls | still moves via the one open direction, or out the back |
| Pawn standing beside an unpinned corridor | a wall may legally be placed “against” the pawn (path check passes if an exit remains) |
| Placing a wall under where a pawn stands | legal (groove vs cell, #8) |
| Wall at the outer rim of the board (edge-adjacent slot) | legal so long as it doesn't jut out (slot index within range) |
| Two walls touching at one corner | legal — corners are points, not unit edges (OD-6) |
| `+`-junction: perpendicular walls sharing the same `(Row, Col)` slot | illegal — each crosses the other's middle (OD-6) |
| `T`-touch: a perpendicular wall's end meeting another wall's side | legal — tangential contact only (OD-6) |
| Two parallel walls sharing one unit edge (offset overlap) | illegal (overlap) |
| Wall that cuts the *only* remaining path of the placing player's own pawn | illegal — path preservation covers every seat, self included |
| Wall that cuts one of two paths of the opponent | legal |
| Wall that would cut a teammate in 4p | illegal (path preservation is per pawn, whole game) |
| First move is a wall | legal (`1. e1v`-style openings; no rule bans it) |
| All walls gone | play continues with moves; no auto-end (OD-7) |
| `GameTimeExpired` with pawns not on a goal | draw, `Winner = null` (OD-4) |
| `GameTimeExpired` same tick as a goal-crossing action | the crossing action wins the optimistic race (platform version token); state is already over — the deadline becomes a no-op |
| Timeout on the last remaining seat (team member eliminated, etc.) | moves to next seat; a lone winning team member simply plays on |
| 4p AFK elimination of one member | pawn removed (board cell frees — OD-9), walls stay, team continues |
| 4p both members of a team eliminated | the other team still plays until its pawn crosses (win) or a draw fires |
| Reconnect mid-flight | impossible — flights are purely client-side post-broadcast cosmetics |
| Duplicate `MovePawn{…}` | first succeeds, second fails stale (`notYourTurn`/`invalidState`) |
| Client “legal” mirror disagrees with engine | server wins; localized error toast; board unchanged |
| Pawn on its own goal square already (only after I-10) | game already over; no moves thereafter (I-9) |
| Rotated view in 4p (camera on your seat) | only the canvas rotates; coordinates and goals stay absolute (#3/#26) |

---

## 38. UI state → action → animation conformance matrix

| UI state | possible input | sent action | on broadcast diff | resulting state logic |
|---|---|---|---|---|
| `yourTurn idle` | hover/click cells & grooves | — | — | read-only; mirror computes highlights |
| `yourTurn pawnFocused` | click a legal target | `MovePawn{Row,Col}` | pawn glide; jump arc | turn advances; maybe `win` |
| `yourTurn wallPreview` | click preview / R rotate | `PlaceWall{Row,Col,Orientation}` | wall settle into groove | turn advances; `wall` log |
| `sendInFlight` | disabled (all mirrors off) | — | (already sent) | — |
| `opponentTurn` | nothing (few diagnostics allowed) | — | their flights replay | mirror recomputes “legal only for actor” |
| `gameOver` (win/draw) | none | — | goal pulse / ring | standings modal; `GetValidActions` empty |
| `reconnecting` / `paused` | frozen board | nothing until hub back | — | revenue only via broadcast / session status |
| `takenOver` | — | — | — | leave hub, navigate to `/lobby` |

---

## 39. Rules audit trail and authoritative sources

Precedence order (fetched 2026-09-17; re-download originals at implementation time):

1. **Board Game Arena — Quoridor game help** (`en.doc.boardgamearena.com/Gamehelpquoridor`,
   licensed digital implementation): 9×9, 2/4 players, wall distribution 10/5 each, turns =
   move XOR wall, orthogonal movement, Jump Behind vs Jump Aside (both figures), “may not
   jut out of the board”, “may not block all paths for each pawn”, first to the opposite
   side wins, 4p team rule (“A team wins if either pawn reaches its destination side”).
2. **English Wikipedia — Quoridor** (component counts, Gigamic publisher, Mirko Marchesi
   designer, Mensa Select 1997): wall = any groove not already occupied; legal-move figure
   (jump behind, jump aside to either adjacent square when the behind square is off-board /
   blocked by a 3rd pawn / a wall); “multiple pawns may not be jumped”; “walls may not be
   jumped, including when moving laterally” (backs the aside-wall restriction #7.3.3);
   algebraic-start table (e1/a5/e9/i5 and 2p e1/e9 ↔ #4 seats); “though it can be played
   with three players, doing so is not recommended”; no-randomness; competitive time
   control.
3. **Gigamic product page / printed English rulebook** — publisher metadata + to verify at
   implementation time: the printed English/файл rules PDF for OD-1 (3 players), OD-3
   (team win), OD-5 (aside-when-straight-blocked reading), OD-6 (corner-touch walls) and
   the wall-distribution sentence for 3 players (the classic 20-wall game only describes
   2/4).

**Cross-source discrepancies and resolutions** (all captured as numbered ODs):

- **3 players.** BGA only documents 2/4; the physical 20-wall game's 3p split (5/5/10 by the
  remembered rulebook text) is asymmetric and Wikipedia explicitly advises against 3p
  ⇒ **OD-1: support 2 and 4 only; reject 3.** (Lobby caveat: rooms are sized 2–4, so a
  3-player Quoridor room would fail at Start with the localized `quoridor.playerCount`;
  acceptable and documented — the lobby can't express “2 or 4”.)
- **First player.** No official draw ⇒ **OD-2: seeded uniform-random first seat**
  (platform precedent).
- **4-player victory.** BGA explicit “team wins if either pawn reaches its side”;
  Wikipedia silent ⇒ **OD-3: 4p is played in two fixed teams (seats 0&2 vs 1&3), team win on
  either crossing; `Winner` = the crossing mover.** Verify vs PDF.
- **Game-time expiry outcome.** No official rule (physical games just keep playing) ⇒
  **OD-4: `GameTimeExpired` = draw (`Winner = null`).** No closest-to-goal countback.
  (A forced winner would contradict “first to cross wins” and invent a scorer Quoridor
  doesn't have.)
- **Aside jump when straight jump is legal.** Physical rulebook wording implies aside
  exists *when* the back square is blocked; the majority digital reading (incl. BGA UI)
  offers aside only when straight is impossible ⇒ **OD-5 (⋆): aside allowed only when the
  straight jump is impossible.** Verify vs PDF; an isolated branch if the PDF says
  otherwise.
- **Wall corner-touching.** “May not overlap” = may not share a unit edge; touching at a
  single point (corner-to-corner, or end-to-side `T`-touch) is the accepted physical reading.
  A `+` junction — perpendicular walls on the **same `(Row, Col)` slot** — visibly crosses
  through each other's middle and is rejected ⇒
  **OD-6 (⋆): only shared-unit-edge overlap and same-slot `+`-crossing are forbidden.**
  Verify vs PDF figure.
- **Endgame when all walls are spent.** Wikipedia's shortest-path “game ends by agreement”
  is a house convention, not a rule ⇒ **OD-7: play continues with pawn moves only.**
- **Timer turn-skip.** Platform precedent (UNO/Silver/Azul: timeout = skip, never fabricate)
  ⇒ **OD-8: `TurnTimeout` skips the turn only.**
- **AFK-eliminated seat in 4p.** Platform precedent removes a player and continues ⇒
  **OD-9 (⋆): the eliminated seat's pawn is removed from the board (cell frees), walls stay,
  teammates play on.** The path-preservation invariant I-6 is never re-checked on existing
  walls (only on placement), and a freed cell only ever widens paths — record as deliberate.

---

## 40. Integration checklist (for the implementation agent)

Completion = every box checked with evidence in the recorded Critical Decision (the
platform's integration points are fixed; Quoridor must not add new ones):

1. `backend/src/Games/Quoridor/Quoridor.csproj` — net9.0, references **only**
   `GameEngine.Core`, added to `BoardGamePlatform.sln` and `.slnx`.
2. `QuoridorGame : IGame` (GameType `"Quoridor"`, Min 2 / Max 4), `CreateGame` validates
   2 or 4 (OD-1), partial-class split welcome (core / moves+jumps / walls+path / valid-actions).
3. `QuoridorState` (per #14) + `Errors.cs` (#17) + **both** localization catalogs updated
   with the full `quoridor.*` list (`errors.en.json` / `errors.fa.json`).
4. Action types + payloads #16; events #20; invariants #15.
5. One DI line next to UNO/Silver/Splendor/Azul in `Game.Infrastructure`
   (`services.AddSingleton<GameEngine.Core.IGame, Quoridor.QuoridorGame>();`); catalog
   endpoint zero changes; **verify `GET /api/game/games` shows Quoridor (2–4).**
6. Timers #18 — reuse the sweep/bank; defaults recorded in code as config (90/180/15/3/60).
7. **Rules-audit pass:** re-read #4–#10 against the printed Gigamic English rulebook PDF
   (Next Move at implementation time); resolve every OD-1…OD-9 either “confirmed” or
   “corrected”, each correction its own dated Critical Decision; never silently alter a
   documented default.
8. Frontend: `quoridor.ts` mirror+parser+guard; `QuoridorGameView.tsx` + board component per
   #25–#35; original SVG art (wooden pawns/walls, groove material; replaceable with a
   licensed pack per the Splendor precedent); `GamePage` switch; `GAME_THEME` entry;
   `comingSoon` removal only at implementation time; both locales' `games.Quoridor` +
   `events.quoridor.*` (`fa: Dict` parity).
9. `tests/Quoridor.Harness` per #36 — green, count recorded.
10. `dotnet build` + `tsc && vite build` + `npm run build` all green; locales parity.
11. Docker playtest: 2p and 4p full games, a timeout skip, an AFK elimination, a forced
    draw; verify raw SignalR payloads contain nothing hidden (there is none), reconnect
    returns full state, chat drawer works.
12. Update root `AGENTS.md` status table + Game Documentation index (row “Quoridor” →
    implemented); record the implementation Critical Decision(s); resolve ODs per step 7.

---

## Critical Decisions (Quoridor)

Game-specific decisions only; platform decisions live in the root `AGENTS.md`.

<!-- Add new Quoridor decisions below this line -->

- **2026-09-17** — Quoridor selected as a future game; specification prepared; implementation NOT started
  - **Context:** Four games are shipped (UNO, Silver, Splendor, Azul). The owner directed a
    comprehensive, implementation-ready specification authored exactly like the Azul
    spec-first process: research from authoritative rules sources, contract-level detail,
    physical-board-faithful UI as a first-class requirement, and implementation explicitly
    forbidden in this step.
  - **Decision:** This document (`docs/games/Quoridor.md`) is the sole deliverable. No
    `Games/Quoridor` project, no platform, engine, game meta, or frontend changes, no
    `Games/Quoridor/Errors.cs`, and no localization catalog edits. The Game Documentation
    table row in root `AGENTS.md` is **not** changed by this step (that is implementation
    step 12). Quoridor remains unlisted/`comingSoon` until the owner explicitly starts
    implementation.
  - **Rationale:** Rules were written from two mutually corroborating sources (BGA's
    licensed game help and the English Wikipedia article; both agree on component counts,
    wall distribution, jump-behind/jump-aside, wall legality, and victory) with a mandatory
    printed-PDF verification step during implementation (#40 step 7). Every ambiguity or
    omission became a numbered OD (#39) with a prescribed default. Quoridor stresses a
    different engine shape than any shipped game: a pure abstract spatial game with zero
    hidden information and zero randomness, whose entire UX is the physical board and the
    wall-preview gesture — hence the product-level board sections (#25–#35) rather than an
    afterthought.