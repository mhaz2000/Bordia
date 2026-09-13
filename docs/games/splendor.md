# Splendor - Game Documentation

> **Status: documentation/specification prepared (2026-09-13); Splendor implementation NOT started.**
> Nothing in this document has been implemented: there is no `Games/Splendor` project,
> no engine, no frontend view, no tests. This file is the authoritative rules and
> integration specification a future implementation agent follows to build the game.

> This document owns everything about the Splendor ruleset:
> - authoritative rules specification
> - game state requirements
> - action model
> - information visibility
> - randomness
> - scoring
> - end-game behavior
> - frontend requirements
> - persistence requirements
> - testing requirements
> - game-specific Critical Decisions
>
> Platform-wide contracts remain in the root `AGENTS.md`.

---

## Table of contents

1. [Scope and identity](#1-scope-and-identity)
2. [Design philosophy and architectural constraints](#2-design-philosophy-and-architectural-constraints)
3. [Exact component definitions](#3-exact-component-definitions)
4. [Setup](#4-setup)
5. [Turn structure and action semantics](#5-turn-structure-and-action-semantics)
6. [Gem-token rules](#6-gem-token-rules)
7. [Development-card rules](#7-development-card-rules)
8. [Reservation rules](#8-reservation-rules)
9. [Bonuses and purchasing](#9-bonuses-and-purchasing)
10. [Nobles](#10-nobles)
11. [End of game, scoring, and tie-breaks](#11-end-of-game-scoring-and-tie-breaks)
12. [Information visibility model](#12-information-visibility-model)
13. [Randomness](#13-randomness)
14. [Game state design](#14-game-state-design)
15. [GameAction model](#15-gameaction-model)
16. [GetValidActions specification](#16-getvalidactions-specification)
17. [State invariants](#17-state-invariants)
18. [Persistence and serialization](#18-persistence-and-serialization)
19. [Events](#19-events)
20. [Timers](#20-timers)
21. [Reconnection](#21-reconnection)
22. [Concurrency, duplicates, and stale actions](#22-concurrency-duplicates-and-stale-actions)
23. [Frontend requirements](#23-frontend-requirements)
24. [Localization](#24-localization)
25. [Error model](#25-error-model)
26. [Testing specification](#26-testing-specification)
27. [Edge-case compendium](#27-edge-case-compendium)
28. [Integration checklist](#28-integration-checklist)
29. [Rule audit trail and authoritative sources](#29-rule-audit-trail-and-authoritative-sources)
30. [Critical Decisions (Splendor)](#critical-decisions-splendor)

---

## 1. Scope and identity

- **Game:** *Splendor* — designer **Marc André**, illustrator **Pascal Quidault**,
  publisher **Space Cowboys / JD Éditions (Asmodee)**, first published **2014**.
- **Target:** the **classic base game only** (the 2014 ruleset documented in this file).
- **Players:** **2–4**. `SplendorGame.MinPlayers => 2`, `SplendorGame.MaxPlayers => 4`;
  `CreateGame` must reject any configured player count outside 2–4 (the Lobby already
  caps room size at `MaxPlayers` from the game catalog).
- **Objective:** accumulate **prestige points** (called *victory points* / VIP in this
  document) by purchasing development cards and attracting nobles, as the Renaissance
  gem merchants.
- **Expected duration:** ~30 minutes per the box.

### In scope

- The complete base-game ruleset: gem tokens, gold jokers, three development-card
  levels, blind/visible reservations, permanent bonuses, noble visits, the
  reach-15 end-of-round trigger, and the official tie-break ladder.

### Explicitly out of scope (never to be added without a new decision in §Critical Decisions)

- **Cities of Splendor** (2017) and *all* of its modules: The Cities, The Trading Posts,
  The Orient, The Strongholds — including their 5-noble-fixed variants, city tiles,
  trading-post gold rules, "take 4/5 same-color" 2-player options, and Orient premium cards.
- **Splendor Duel** (2022), **Splendor: Marvel** (2020), and any other Splendor-family
  product. None of their rules (Infinity Gauntlet end condition, Avenger symbols,
  Duel tile layout, VIP-16 thresholds, etc.) may appear anywhere in this implementation.
- Fan-made variants, house rules, tournament rules, probabilistic "recommended noble
  selection" adjustments, digital-platform-specific inventions.
- Any card, count, or rule not present in §3's component tables.

### Ruleset interpretation policy

The rules in §§3–13 are transcribed from the **official 2014 Space Cowboys US rulebook**
(verified source: `Rules_Splendor_US.pdf`, archived copy — see §29) and cross-checked
against independent references (Wikipedia EN/FR, multiple published rule summaries, and
a photograph-verified extraction of the 90 physical development cards). Where sources
disagree, the resolution is stated in the text **and** recorded as a Critical Decision.
Sub-behaviors the rulebook does not pin down are collected in
**§29 Open interpretation decisions (OD-1 … OD-7)** with a prescribed implementation default; the
implementation agent must apply the default and record the final choice as a Critical
Decision — never silently.

---

## 2. Design philosophy and architectural constraints

Splendor is a **game-specific implementation of the existing `IGame` contract** — nothing more:

```text
Game Service → IGame (+ IPlayerViewGame) → SplendorGame → SplendorState
```

The implementation must:

- Implement the existing `GameEngine.Core.IGame` (`GameType`, `MinPlayers`, `MaxPlayers`,
  `CreateGame`, `ProcessAction`, `ProcessAction` must not mutate the input state,
  `GetValidActions`, `IsGameOver`, `GetWinner`).
- Implement `GameEngine.Core.IPlayerViewGame` as well — Splendor **does** have information
  that no client may ever see (deck order; hidden reserved-card identities; see §12).
  The projection is **viewer-independent** (see §12) — it reuses the mechanism the Game
  Service already integrates (`PlayerViewProjection.Project`, per-connection pushes in
  `GameRealTimeNotifier`), and requires **zero** Game Service changes.
- Live entirely under `backend/src/Games/Splendor/`, referencing **only** `GameEngine.Core`
  (mirroring `Games/UNO` and `Games/Silver`).

The engine project must contain **no**:

- ASP.NET Core / HTTP / controller code
- SignalR / hub code
- EF Core / PostgreSQL / Redis / RabbitMQ references
- frontend code
- `SplendorService`, `SplendorRepository`, `SplendorController`, `SplendorDbContext`,
  `SplendorHub`, `SplendorEngineService`, `GenericCardEngine`, `GenericEuroGameEngine`,
  or any other service/abstraction the platform does not already have
- generic abstractions (`GenericCard`, `GenericDeck`, `GenericBoard`, `GenericResource`,
  `GenericMarket`, `GenericEuroGame`) — none exist today and none are introduced for
  Splendor (per the root AGENTS.md guardrails)

`CreateGame` throws on invalid player count (same pattern as `SilverGame.CreateGame`,
which throws `ArgumentException` outside 2–4); all *action-time* problems return
`GameResult.Failure` with `splendor.*` error codes, never exceptions.

### What must NOT be carried over from the shipped games

These are the two biggest copy-risk areas; the spec is written for the **actual** game:

| Common assumption (from UNO / Silver) | Reality in Splendor |
|---|---|
| A discard pile / pile reshuffle exists (UNO) | **There is no discard pile.** Tokens return to the central supply; purchased cards go to the player's tableau permanently; a depleted level deck simply stops refilling. |
| Players hold private cards they know (Silver: face-down-but-known villages, per-player knowledge maps) | **Blind reservations are unknown even to their owner** until purchased. There is no per-player knowledge tracking, no peeking, no card identities revealed selectively. |
| Multi-step pending sub-phases with timeout resolution (Silver's drawn-card/ability decisions) | **Splendor actions are atomic**: each `GameAction` carries its complete decision set (tokens taken, tokens returned, payment, noble claimed) and the engine resolves the entire turn end in one pass (see §15 rationale). No pending-decision state machine should be built unless the atomic design is proven insufficient; any change requires a Critical Decision. |
| Turn timeout = skip + banked-time accounting with game-specific penalty semantics (UNO) | Reuse the same **platform** timer mechanism; Splendor's timeout is a plain turn skip (§20) — no penalties accumulate in this game, so none may be invented. |
| Silver's timer defaults (90/180) or UNO's (30/120) as "the rule" | They are configuration, not rules (§20). |
| UNO's 108-card deck with ×N copies | Splendor's 90 development cards are **90 unique cards** (no duplicate card instances) — see §3.3. |
| Per-viewer different state (Silver) | Splendor's view is the **same for every viewer** — the secrets are hidden from everyone (§12). |

---

## 3. Exact component definitions

### 3.1 Gem tokens (40 total)

Per the official rulebook "Contents" panel:

| Token | Color shown | Base count |
|---|---|---:|
| Diamond | white | 7 |
| Sapphire | blue | 7 |
| Emerald | green | 7 |
| Ruby | red | 7 |
| Onyx | black | 7 |
| Gold (joker) | yellow | 5 |

- The five colored tokens are collectively **gems**; gold is a separate, wildcard token
  obtainable **only by reserving** a card (§5.3) and spendable as any one gem when paying (§9).
- The **supply** (table pool) is shared/public; per-player holdings are public too
  (physical rule: "the tokens owned by a player must be visible by all players at all times").
- Setup quantities depend on player count — see §4. The base game has **no discard pile
  for tokens** (tokens always move between supply ↔ player pools).

### 3.2 Noble tiles (10 total, all distinct, 3 VP each)

There are exactly **10 noble tiles**: five require **4+4** bonuses of two colors, five
require **3+3+3** bonuses of three colors. Each tile is worth **3 prestige points**.
Every noble tile is **distinct** (no duplicate noble tiles in the box), so any setup
selection is duplicate-free by construction.

The pairing is structural: each **4-4 pair** noble and each **3-3-3 triple** noble are
complements of one another over the 5-cycle **Diamond–Sapphire–Emerald–Ruby–Onyx–Diamond**
(pair colors adjacent on the cycle). Verified against the rulebook example
(3 Sapphire + 3 Emerald + 3 Diamond, the complement of Ruby+Onyx) and two independent
published card/noble datasets in full agreement.

| Noble ID | Requirements (bonuses only — tokens never count) | VP |
|---|---|---:|
| `N-DS` | 4 Diamond + 4 Sapphire | 3 |
| `N-SE` | 4 Sapphire + 4 Emerald | 3 |
| `N-ER` | 4 Emerald + 4 Ruby | 3 |
| `N-RO` | 4 Ruby + 4 Onyx | 3 |
| `N-OD` | 4 Onyx + 4 Diamond | 3 |
| `N-ERO` | 3 Emerald + 3 Ruby + 3 Onyx (complement of `N-DS`) | 3 |
| `N-DSO` | 3 Diamond + 3 Sapphire + 3 Onyx (complement of `N-ER`) | 3 |
| `N-SER` | 3 Sapphire + 3 Emerald + 3 Ruby (complement of `N-OD`) | 3 |
| `N-DRO` | 3 Diamond + 3 Ruby + 3 Onyx (complement of `N-SE`) | 3 |
| `N-DSE` | 3 Diamond + 3 Sapphire + 3 Emerald (complement of `N-RO`) — *the rulebook's example noble* | 3 |

Setup reveals `players + 1` of the 10 tiles at random (§4); the rest are removed from
the game **and never enter play**. Tiles are not replenished; a claimed tile leaves the
market permanently; an awarded noble can never return or be awarded twice.

**Naming:** the official base-game tiles carry portraits and no printed names this
document can verify. Do **not** invent noble names. The UI identifies nobles by their
requirement composition (localized gem names + counts). Historical-figure names that
appear in fan sources (Anne of Brittany, Machiavelli, …) are trivia, **not** spec.

### 3.3 Development cards (90 total, all distinct)

**Structure (authoritative — official 2014 rulebook):**

| Level | Cards | Theme (rulebook text) | VP on cards | Copies per design |
|---|---:|---|---|---|
| 1 | **40** | mines | 0, except five 1-VP cards | **1 (every card unique)** |
| 2 | **30** | transportation & artisans | 1–3 | 1 |
| 3 | **20** | jewelers | 3–5 | 1 |

> **Edition-conflict resolution (see Critical Decision 2026-09-13):** popular fan
> descriptions claim "4 levels / 100 cards / 40 noble tiles". That is **false for this
> game** (it mixes up digital variants / other products). The official rulebook's
> Contents panel is explicit: **90 development cards (40 + 30 + 20), 10 noble tiles**.
> Implementation must match §4's setup exactly against 3 decks and 10 nobles.

Every development card has:

- a **tier** (1, 2, or 3),
- a single **bonus color** (the gem icon top-right — permanent discount once purchased),
- a **cost** in colored gems (never in gold; gold is only how you *pay*, via the joker),
- a **prestige point value** (top-left; 0 on most Level-1 cards).

**Card catalogue** — complete, verified against a photograph-extracted inventory of the
physical 90 cards (see §29). Bonus colors: `D` Diamond (white), `S` Sapphire (blue),
`E` Emerald (green), `R` Ruby (red), `O` Onyx (black). Cost notation: `3D 2S` = three
Diamond tokens + two Sapphire tokens.

#### Level 1 — 40 cards (8 designs per bonus color, VP 0 unless noted)

| Card ID | Bonus | VP | Cost |
|---|---|---:|---|
| `L1D01` | D | 0 | `3S` |
| `L1D02` | D | **1** | `4E` |
| `L1D03` | D | 0 | `2R 1O` |
| `L1D04` | D | 0 | `2S 2O` |
| `L1D05` | D | 0 | `3D 1S 1O` |
| `L1D06` | D | 0 | `2S 2E 1R` |
| `L1D07` | D | 0 | `1S 1E 1R 1O` |
| `L1D08` | D | 0 | `1S 2E 1R 1O` |
| `L1S01` | S | 0 | `3O` |
| `L1S02` | S | **1** | `4R` |
| `L1S03` | S | 0 | `1D 2O` |
| `L1S04` | S | 0 | `2E 2R` |
| `L1S05` | S | 0 | `1D 3E 1R` |
| `L1S06` | S | 0 | `1D 2E 2R` |
| `L1S07` | S | 0 | `1D 1E 1R 1O` |
| `L1S08` | S | 0 | `1D 1E 2R 1O` |
| `L1E01` | E | 0 | `3R` |
| `L1E02` | E | **1** | `4O` |
| `L1E03` | E | 0 | `2D 1S` |
| `L1E04` | E | 0 | `2S 2O` |
| `L1E05` | E | 0 | `1D 3S 1R` |
| `L1E06` | E | 0 | `1S 2R 2O` |
| `L1E07` | E | 0 | `1D 1S 1R 1O` |
| `L1E08` | E | 0 | `1D 1S 1R 2O` |
| `L1R01` | R | 0 | `3D` |
| `L1R02` | R | **1** | `4D` |
| `L1R03` | R | 0 | `2S 1E` |
| `L1R04` | R | 0 | `2D 2O` |
| `L1R05` | R | 0 | `1D 1E 3O` |
| `L1R06` | R | 0 | `2D 1E 2O` |
| `L1R07` | R | 0 | `1D 1S 1E 1O` |
| `L1R08` | R | 0 | `2D 1S 1E 1O` |
| `L1O01` | O | 0 | `3E` |
| `L1O02` | O | **1** | `4S` |
| `L1O03` | O | 0 | `2E 1R` |
| `L1O04` | O | 0 | `2D 2E` |
| `L1O05` | O | 0 | `1E 3R 1O` |
| `L1O06` | O | 0 | `2D 2S 1R` |
| `L1O07` | O | 0 | `1D 1S 1E 1R` |
| `L1O08` | O | 0 | `1D 2S 1E 1R` |

#### Level 2 — 30 cards (6 designs per bonus color)

| Card ID | Bonus | VP | Cost |
|---|---|---:|---|
| `L2D01` | D | 2 | `5R` |
| `L2D02` | D | 3 | `6D` |
| `L2D03` | D | 2 | `5R 3O` |
| `L2D04` | D | 2 | `1E 4R 2O` |
| `L2D05` | D | 1 | `3E 2R 2O` |
| `L2D06` | D | 1 | `2D 3S 3R` |
| `L2S01` | S | 2 | `5D` |
| `L2S02` | S | 3 | `6S` |
| `L2S03` | S | 2 | `5D 3S` |
| `L2S04` | S | 2 | `2D 1R 4O` |
| `L2S05` | S | 1 | `2S 2E 3R` |
| `L2S06` | S | 1 | `2S 3E 3O` |
| `L2E01` | E | 2 | `5E` |
| `L2E02` | E | 3 | `6E` |
| `L2E03` | E | 2 | `5S 3E` |
| `L2E04` | E | 2 | `4D 2S 1O` |
| `L2E05` | E | 1 | `2D 3S 2O` |
| `L2E06` | E | 1 | `3D 2E 3R` |
| `L2R01` | R | 2 | `5O` |
| `L2R02` | R | 3 | `6R` |
| `L2R03` | R | 2 | `3D 5O` |
| `L2R04` | R | 2 | `1D 4S 2E` |
| `L2R05` | R | 1 | `2D 2R 3O` |
| `L2R06` | R | 1 | `3S 2R 3O` |
| `L2O01` | O | 2 | `5D` |
| `L2O02` | O | 3 | `6O` |
| `L2O03` | O | 2 | `5E 3R` |
| `L2O04` | O | 2 | `1S 4E 2R` |
| `L2O05` | O | 1 | `3D 2S 2E` |
| `L2O06` | O | 1 | `3D 3E 2O` |

#### Level 3 — 20 cards (4 designs per bonus color)

| Card ID | Bonus | VP | Cost |
|---|---|---:|---|
| `L3D01` | D | 4 | `7O` |
| `L3D02` | D | 4 | `3D 3R 6O` |
| `L3D03` | D | 3 | `3S 3E 5R 3O` |
| `L3D04` | D | 5 | `3D 7O` |
| `L3S01` | S | 4 | `7D` |
| `L3S02` | S | 4 | `6D 3S 3O` |
| `L3S03` | S | 3 | `3D 3E 3R 5O` |
| `L3S04` | S | 5 | `7D 3S` |
| `L3E01` | E | 4 | `7S` |
| `L3E02` | E | 4 | `3D 6S 3E` |
| `L3E03` | E | 3 | `5D 3S 3R 3O` |
| `L3E04` | E | 5 | `7S 3E` |
| `L3R01` | R | 4 | `7E` |
| `L3R02` | R | 4 | `3S 6E 3R` |
| `L3R03` | R | 3 | `3D 5S 3E 3O` |
| `L3R04` | R | 5 | `7E 3R` |
| `L3O01` | O | 4 | `7R` |
| `L3O02` | O | 4 | `3E 6R 3O` |
| `L3O03` | O | 3 | `3D 3S 5E 3R` |
| `L3O04` | O | 5 | `7R 3O` |

**Rulebook cross-check:** the rulebook's worked example — "a 4-prestige card with a blue
bonus costing 3 blue, 3 black and 6 white" — is exactly `L3S02`. The implementation agent
should use that card as a fixture in the harness (see §26).

**Card identity for the engine:** because all 90 cards are unique, the **Card ID above is
the physical instance id** (no copies). The engine stores cards by id; card definitions
(cost/bonus/VP/tier) are static catalogue data inside `Games/Splendor` (a readonly
dictionary), *not* serialized into the state (the state persists ids + positions only —
see §14/§18).

**Setup selection:** all 90 cards are always used (shuffled into 3 tier decks). Unlike
nobles, there is no "select active subset" step for cards.

---

## 4. Setup

`CreateGame(GameOptions options)` must perform, deterministically, in this order:

1. **Validate players:** reject counts outside 2–4.
2. **Token supply** (start-of-game table pool), by player count (rulebook Contents +
   "Game with 2 or 3 players" panels):

   | Color supply (each of D/S/E/R/O) | Gold | Nobles revealed |
   |---:|---:|---:|
   | 2 players: **4** | 5 | **3** |
   | 3 players: **5** | 5 | **4** |
   | 4 players: **7** | 5 | **5** |

   (The rulebook phrases 2p/3p as "remove 3/2 tokens of each gem color… don't touch the
   gold"; the leftover-of-a-full-7 result is shown above. 4 players use the full supply.
   The 2014 rulebook explicitly states for 2–3 players: **"There are no other changes"** —
   base-game 2-player games use the **same four actions**; the "take 4 of a kind" option
   is an expansion/variant and is OUT OF SCOPE, as is any deck-restriction for 2 players.)
3. **Shuffle each of the three development decks separately** (server RNG — §13). Build
   each deck by shuffling the catalogue cards of that tier (40/30/20 ids).
4. **Market:** reveal **4 face-up cards from each level** (12 market cards, grouped in
   three rows/columns of 4, levels stacked 1→3). Remainders stay as face-down draw decks.
5. **Nobles:** shuffle the 10 tiles, reveal the first **players + 1** (3/4/5 by count);
   the rest are **removed from the game** (not serialized as future options).
6. **Players:** every player starts with **0 tokens, 0 gold, 0 reserved cards, 0 purchased
   cards, 0 bonuses, 0 VP**. No starting cards or resources exist.
7. **Turn order:** the youngest player begins and play runs clockwise in the physical
   game. Digital adaptation (no age data available): **choose the starting seat uniformly
   at random at creation** (platform precedent: Silver 2026-09-11 set a random starting
   player so the generic deadline sweep works from the first second). The chosen seat
   becomes `CurrentPlayerIndex = 0`-equivalent; clockwise = ascending seat order as given
   in `GameOptions.Players`. *Record the final choice as a Critical Decision* (see
   Open OD-4).
8. Set `GameState.GameEndsAtUtc` from the timer configuration (§20) and the first turn's
   `NextActionDeadlineUtc` (§20).

The result is wrapped per the platform convention: full `SplendorState` serialized to
`GameState.Data["SplendorState"]` plus `GameState.Data["PlayerNames"]` (§18).

---

## 5. Turn structure and action semantics

**On their turn, a player performs exactly ONE main action**, chosen from the four
official actions below (the platform's system actions `TurnTimeout`/`GameTimeExpired`
are service-dispatched, not player choices). After the action resolves, end-of-turn
steps run in order: **token-limit return (§6) → noble visit check (§10) →
reach-15 end-game check (§11) → advance to next seat**. In this engine's atomic-action
design (§15) all of these are computed inside `ProcessAction` for the submitted action —
the payload carries the player's return/noble choices, so no multi-action sub-turn state
exists.

### 5.1 Take three different gems

- **Prerequisite:** at least **1** token of **three different colors** in the supply.
- **Payload (semantic):** choose 3 distinct gem colors (never gold); optionally which
  tokens to return if the result would exceed the 10-token limit.
- **Effects:** supply −1 of each chosen color; player +1 token of each; player total
  tokens may exceed 10 mid-turn but must return down to ≤10 at end of turn (§6).
- **Invalid:** duplicate colors in the choice; a color with 0 supply; supplying fewer
  than 3 distinct colors while 3+ are available (see OD-1 for the supply-shortage case);
  not returning the excess when required.
- Ends the turn. No card/noble/VP change; nobles *may* still be visited if the player
  was already eligible from a previous turn (§10).

### 5.2 Take two identical gems

- **Prerequisite:** at least **4 tokens of the chosen color still in the supply at the
  moment the take happens** ("This action is only possible if there are at least 4 tokens
  of the chosen color left when the player takes them" + note "players may not take 2
  tokens of the same color if there are less than 4 tokens available of that color").
- **Payload:** one color; returns as needed.
- **Effects:** supply −2 of that color; player +2; cap handling as §5.1.
- **Invalid:** fewer than 4 of that color in the supply; gold as the "color"; any other
  quantity (not 1, not 3).
- Ends the turn.

### 5.3 Reserve a development card (and take a gold token)

One action, two targets (the player picks **one**):

**(a) Reserve a face-up market card** — choose any of the 12 visible market cards. The
card's identity was public and **stays public** in the reserver's reservation area. The
market slot is **immediately replaced** by the top card of that level's deck (§7).

**(b) Reserve a face-down deck card** — choose a **level** (1/2/3); the engine draws the
**top card of that level's deck** *without showing it to anyone — not even the reserving
player* (physical rulebook: "draw the first card from one of the three decks without
showing it to the other players"; the whole point is the blind luck buy, so the owner is
excluded too — see §12). Only the level is public ("P1 reserved a face-down Level-2 card").

**Then (both variants):** if at least one **gold token remains in the supply**, the
player takes **exactly 1 gold**; if none remain, the player still reserves and gets
nothing ("If there is no gold left, you can still reserve a card, but you won't get any
gold"). Taking gold can push the player above 10 → returns apply (§6).

- **Prerequisites / limits:** the player holds **fewer than 3 reserved cards**
  (max **3**, no exceptions); variant (b) requires the chosen deck to be non-empty.
- **Ends the turn.** Reserving gives no bonuses, no VP, no noble eligibility (§8).

### 5.4 Purchase a development card

From **either** the market (a face-up card) **or** the player's own reserved cards
(the reserved card need not have been reserved "on a previous turn" to be *buyable* —
but a purchase is always a later turn's action, since reserving ends the current turn;
the rulebook's "reserved on a previous turn" is a consequence of the one-action rule).

- **Effective cost:** `max(0, cost_c − bonuses_c)` per color (§9). Gold jokers may
  replace any missing tokens (player chooses the payment mix; see §9 for exact validation
  math).
- **Payment:** submitted tokens + gold move back to the supply; bonuses are *discounts*,
  not payment — spent bonus cards are never tapped.
- **Receipt:** the card joins the player's face-up **tableau** permanently: its bonus
  applies to all later purchases; its VP adds immediately to the player's total; if it
  was reserved, its reservation slot frees and its identity becomes public (it was face
  down only while unowned-and-unpurchased for deck-drawn ones).
- **Refill:** if the purchase came from the market, the slot is immediately refilled from
  that level's deck (§7).
- **End-of-turn:** noble visit check (§10), then reach-15 check (§11).
- **Invalid:** card not in a legal source; insufficient (bonus + token + gold) coverage;
  overpayment; paying with a color the card does not need; game over; wrong player.
- Ends the turn.

### 5.5 What is NOT an action

- Noble visits (automatic, at end of turn, max one per turn — §10).
- Returning excess tokens (obligatory consequence — §6).
- Discarding reserved cards (impossible — §8).
- Replenishing removed nobles (they are gone — §3.2).

---

## 6. Gem-token rules

1. **Ownership limit:** a player may hold **at most 10 tokens total, gold included**
   ("A player can never have more than 10 tokens at the end of their turn (including
   jokers)"). The limit is evaluated **at end of turn only**; transiently exceeding it
   mid-action is fine (take 3 while holding 9 → 12 → return 2 → 10).
2. **Which tokens are returned:** chosen by the player ("A player can return all or some
   of those they've just drawn"). OD-2 default: the player may return **any** of their
   held tokens (not only just-drawn ones), gold included. If the engine never forces a
   return on purchases (they only reduce holdings), this is unambiguous.
3. **Returned tokens go to the supply** (they never vanish or go elsewhere).
4. **Bonuses do not affect the limit** — bonuses live on cards, not tokens; the limit is
   tokens only.
5. **Purchases and returns ordering:** a purchase is itself the whole turn, and it only
   reduces tokens — it can never cause overflow. Overflow must be resolved **within the
   same turn** that caused it (the pipeline of §5/§15), so every player starts their
   turn at ≤10 tokens and cross-turn "return before/after a purchase" ordering never
   arises.
6. **Taking 2 identical** requires ≥4 of that color **available at take time** (this is
   a supply rule, not a holding rule).
7. **Gold** is obtained only via §5.3, counts toward the 10 limit, and may be spent as a
   payment joker or kept.
8. A player with **10 tokens may still reserve** a face-down/face-up card and take the
   gold (overflow resolved by returning 1 in the same turn) — the old "must return a gem
   *before* reserving" shortcut found in some digital implementations is NOT the physical
   rule; implement the end-of-turn limit semantics.
9. A player with 10 tokens may still **purchase** any affordable card (purchases never
   increase token count; an all-bonus (zero-cost) purchase at 10 tokens is legal).

**If an action would take from a supply that cannot satisfy it** → the engine rejects
the action outright (never partial).

---

## 7. Development-card rules

- **Market layout:** 3 levels × 4 face-up slots, always presented per level; a purchased
  or reserved market card is **immediately replaced** from the top of the same level's
  deck ("when a development card from the middle of the table is acquired or reserved,
  it must immediately be replaced by a card of the same level").
- **Invariant:** there are always 4 face-up cards of each level **unless that level's
  deck is empty**, in which case the freed slot(s) **stay empty forever** (no
  cross-level substitution, no reshuffle — Splendor has no discard pile to reshuffle).
- **Refill is market-sourced only:** purchasing/reserving a *reserved* card does not
  touch the market; reserving *from a deck* reduces the deck and does not touch the
  market.
- **Tier availability:** any face-up or reserved card of **any level** may be purchased
  at any time — levels impose no ordering ("you must own Level 1 first" is a variant,
  not the base rule).
- **Purchased cards** leave the game's pools permanently: they go to the buying player's
  tableau, face up, sorted by color (a frontend presentation concern). They are never
  traded, destroyed, reclaimed, or returned.
- **Reserved cards in hand:** §8.
- **Deck exhaustion consequences:** no deck card of that level can be reserved from the
  deck; market slots for that level thin out to zero; nothing else special happens.

---

## 8. Reservation rules

1. **Limit:** at most **3** reserved cards per player (holding 3 makes every reserve
   action illegal).
2. **No discard, no trade, no return:** "the only way to get rid of a card is to buy it".
   Reserved cards are never returned to the market or decks, ever.
3. **Reserving does not reveal anything new** (market card: already public; deck card:
   nobody learns its identity — §12). Reserved cards confer **no bonuses, no VP**, and
   **cannot trigger nobles** — only *purchased* cards' bonuses count (§9, §10).
4. **Reserving a market card refills the market immediately** (§7) — the card leaves the
   market exactly once.
5. **Buying a reserved card** later happens via a purchase action targeting the
   player's own reservation (§5.4). A face-down (deck) reservation's identity is revealed
   to everyone at that moment (it enters the public tableau).
6. **Reserved cards count toward nothing except:** the 3-card limit, the
   *fewest-reserved-cards* final tie-break (§11), and the physical rule they stay hidden
   — they are not "cards played".
7. **Gold on reserve:** see §5.3 — +1 gold if supply > 0, still legal at 0 gold, still
   exactly +1 (never more, never optional).
8. **Same-turn reserve→purchase is impossible** (one action per turn). A client action
   like "PurchaseReserved" targeting a reservation index that does not exist, or
   belonging to the market (double-sourcing a card), must be rejected.
9. **A reserved market card cannot simultaneously remain in the market** (invariant E7).

---

## 9. Bonuses and purchasing

- Each purchased card provides a **permanent, reusable bonus** of its single color.
  A player's bonus count per color = number of purchased cards of that bonus color
  (tableau only — reserved/market/deck cards contribute nothing).
- **Discounts, not currency:** when buying, `required_c = max(0, cost_c − bonus_c)` for
  each color. Bonuses never pay gold-required anything (there is no gold cost) and never
  exceed the printed cost (a 0-bonus-needed color stays 0, never negative, and surplus
  bonuses in colors the card doesn't need are simply unused).
- **Payment:** the player submits per-color token counts `p_c` and the engine derives
  gold usage. Validation (exact, no interpretation needed):
  - `p_c ≥ 0`, `p_c ≤ held_c`, and `p_c ≤ required_c` (no overpayment in any color);
  - gold needed `G = Σ_c max(0, required_c − p_c)`;
  - valid iff `G ≤ heldGold`;
  - all `p_c` colored tokens + `G` gold return to the supply; `Σ p_c + G = Σ required_c`.
- **Zero-cost purchase is legal** ("they can even purchase a card without spending any
  tokens") — e.g. 3 emerald bonuses buying a `3E` card with no tokens spent. (The
  engine's `Payment` payload may be all zeros; it is still validated, not skipped.)
- The card purchased **while being paid for** provides no discount to itself (bonuses
  count only from cards already in the tableau — "on previous turns").
- After receipt: bonuses updated, VP updated (`totalVP += card.vp`), market refill if
  applicable, then noble check and end check per §5.

---

## 10. Nobles

- **When checked:** **at the end of every turn** (any action type — take/reserve/purchase
  and system timeouts), for the active player, against the nobles still *available in
  the market* (not yet claimed by anyone). Physical rulebook, "The nobles": "At the end
  of their turn, each player checks the noble tiles…"
- **Condition:** the player's **bonuses only** (§9) meet or exceed the tile's
  requirement per color. Tokens, gold, and reserved cards are irrelevant to eligibility.
- **Automatic, never refused:** "It is impossible to refuse the visit from a noble, which
  is not considered to be an action."
- **At most ONE visit per turn:** "players can only get a single [noble] per turn." If
  the player is eligible for 2+ available nobles at end of turn, **the player chooses
  which one to receive**; the others remain in the market for later (including the next
  turn's end-of-turn check — see example below).
- **Receiving a noble**: tile moves to the player's area face up, +3 VP immediately.
  It does not consume or extend the action.
- **Nobles can end the game:** a noble's +3 can push the player to ≥15 at end of turn —
  the end-of-round trigger (§11) fires from the end-turn pipeline just like a purchase
  crossing the threshold.
- **Simultaneous-eligibility worked example:** player qualifies for `N-DS` and `N-DSE`
  after one purchase. They choose `N-DS` this turn (+3 VP, end check runs). Next turn
  (even a token-take turn), end-of-turn check runs again: they still qualify for
  `N-DSE` → they take it. There is no way to lose eligibility (bonuses never shrink).
- **Claimed tiles cannot be claimed again** (they left the market); availability only
  shrinks; if all revealed nobles are claimed, end-turn checks are trivially empty.

---

## 11. End of game, scoring, and tie-breaks

**Trigger:** as soon as a player has **15 or more VP** — which can only occur at the end
of *their own* turn, after a purchase and/or a noble visit (the two VP sources) — the
current round is completed:

1. The triggering player **finishes** their current turn (the pipeline of §5 completes:
   returns, noble, threshold check *last*).
2. Play continues from the next seat; **every other player takes exactly one more turn**
   ("complete the current round so that each player has played the same number of turns"
   — i.e. equal total turn counts, the trigger player gets no extra turn).
3. After the last final turn's own pipeline (including any noble visits from final-round
   purchases, and any VP gains above 15 during the final round — these never re-trigger
   an extension), the game ends: `IsOver = true`.

Notes the implementation must get right:

- Crossing 15 **during the final round** does not extend anything further.
- The trigger can be reached via a noble at the end of a non-purchase turn — the
  end-turn pipeline evaluates VP **after** the noble step.
- `IsGameOver(state)` ⇒ true only once the equal-turns sequence has completed;
  `Winner` is computed at that moment from the final scoring below.
- Mid-final-round states must be persisted so a reconnect/restart resumes the exact
  remaining count of final turns (state fields `FinalRoundTriggered` /
  `FinalTurnsRemaining`, §14).

**Final scoring:** each player's score = sum of VP on their purchased development cards
(0–5 per card per §3.3 tables) + **3 per owned noble tile**. Reserved cards, tokens,
gold, and bonuses are worth **nothing**.

**Winner / tie-break ladder (see Critical Decision 2026-09-13):**

1. **Most VP wins.**
2. Tied on VP: **fewest purchased development cards** wins (cards in the tableau,
   purchased — reserved cards do not count here).
3. Still tied: **fewest reserved cards (in hand)** wins — *addition from the current
   official printing, recorded in the current rulebook as cited by the sources in §29;
   the 2014 text stops at step 2.*
4. Still tied: the tied players **share the victory** → `Winner = null` with
   `IsOver = true` (the platform `GameResult`/`GameState` model supports a no-winner end
   exactly like UNO's time-expiry draw).

"The fewest **tokens** wins" (found in some digital summaries) is **not** an official
Splendor tie-break and must not be implemented.

---

## 12. Information visibility model

### The actual secret structure

Splendor has hidden information, but it is **hidden from everyone**, not asymmetric per
viewer:

| Information | Category | In authoritative state? | In client projection? |
|---|---|---|---|
| Three deck **orders** (sequence of remaining card ids per level) | Secret — no one may see future cards | Yes (persisted — draws must be deterministic and restart-safe) | **No — never** |
| Identity of each **face-down (deck) reservation** | Secret — including from its owner until purchased | Yes (bound to `ownerSeat + index`) | **No — projection replaces with `{Tier, Source=Deck}`** |
| Identity of each **face-up (market) reservation** | Public (chosen from public market) | Yes | Yes |
| Market contents (12 slots, level-ordered) | Public | Yes | Yes |
| Available nobles | Public | Yes | Yes |
| Token supply counts | Public | Yes | Yes |
| Each player's tokens by color + gold | Public | Yes | Yes |
| Each player's bonuses by color | Public (derived) | Store or derive | Yes (either) |
| Each player's purchased tableau (cards, bonuses, VP) | Public | Yes | Yes |
| Each player's reserved-card **count** and per-slot tier/source | Public | Yes | Yes (identity redacted per row above) |
| Current player, turn number, `FinalRoundTriggered` state | Public | Yes | Yes |
| Display names | Public | `Data["PlayerNames"]` | Yes |

**Engine stores everything authoritative (§14). Clients receive the projection: the full
state minus the three deck orders and minus every hidden-reservation identity.** Because
the projection is identical for all viewers, the implementation satisfies
`IPlayerViewGame` with a **viewer-independent** projection — but it must implement the
interface (the Game Service only suppresses the raw state on per-connection pushes when
the engine declares the capability, per the platform 2026-09-11/2026-09-12 decisions).
Reusing the existing mechanism requires zero Game Service changes; do NOT invent a
"public-only broadcast" alternative and do NOT build per-viewer knowledge maps (Silver
specific — there is nothing player-asymmetric to track).

**Leak discipline for responses/events:**

- Action responses, `GET /state`, reconnect responses, and the `GameStateUpdated`
  SignalR pushes all carry the **projected** state (this is automatic once the engine
  implements `IPlayerViewGame`, and every notification publisher must set
  `GameType` — see platform 2026-09-12 decision).
- Events (§19) must never contain a hidden reservation's card id; the client learns a
  deck reservation's identity only when its owner purchases it.
- The projection may expose *derived* views (e.g. effective cost) since the underlying
  inputs are public anyway.
- A defensive client-side guard is required, mirroring the Silver precedent:
  `parseSplendorState` must reject payloads shaped like the *authoritative* state
  (e.g. a `Decks` array of card ids where the view requires only `DeckCounts`) and fall
  back to the "waiting for state" screen instead of rendering — a leaked raw blob must
  never render (platform decision 2026-09-12 applies to every projection game).

---

## 13. Randomness

**All randomness is server-authoritative, inside `SplendorGame`, at exactly two
creation-time moments:**

1. **Shuffling the three development decks** (Fisher–Yates via a private `Random`;
   the `Random` instance itself is never serialized — the *resulting order* is, §14).
2. **Choosing the starting seat** (OD-4 default) and the **noble reveal subset**
   (`players + 1` of 10, i.e. shuffle the 10 and take the first k).

Everything else — every legal action's outcome, draws from decks, market refills — is
deterministic given the persisted state.

**Clients can never control randomness.** Allowed payload shapes only *reference
player-legitimate choices*: a color, a market slot/card id, a reservation index, a
**level** (for `ReserveDeckCard` — never a card id), payment breakdown, return list,
noble id. A request like `ReserveDeckCard { CardId: "L2R04" }` must be rejected
(unknown-field / invalid payload). This mirrors UNO's forged-count closure (2026-09-07):
the engine draws `deck[0]`; the client has no channel to name it.

**Testing-only determinism:** support an optional integer **`Seed`** in
`GameOptions.Settings` (exact Silver precedent): when present, all creation-time RNG is
seeded, making setup reproducible for the harness. `Seed` is a testing capability, never
a gameplay feature; with no `Seed`, behavior is fresh-random.

---

## 14. Game state design

Conceptual authoritative state (serialized into `GameState.Data["SplendorState"]`; the
implementer may choose the cleanest concrete C# shape — keep it flat and simple):

```text
SplendorState
  Players:          seat -> { Name (from PlayerNames map), }
  CurrentPlayerIndex: int                      // seat whose turn it is (mirrored on GameState root)
  TurnNumber:       int                        // public turn counter (monotonic; drives display + tests)
  RoundNumber:      int?                       // optional derived view of TurnNumber / playerCount
  TokenSupply:      { Diamond, Sapphire, Emerald, Ruby, Onyx: int, Gold: int }
  Decks:            List<string>[]             // 3 lists (level 1..3) of remaining card ids, FRONT = next drawn
  Market:           (string? CardId)[3][4]     // 4 slots per level; null = permanently empty (deck exhausted)
  NoblesInMarket:   List<NobleId>              // remaining unclaimed revealed nobles (order = display order)
  PlayerStates[]:   (per seat)
      Tokens:       { D, S, E, R, O: int, Gold: int }     // 0..10 total (post end-of-turn)
      Reserved:     List<ReservedCard>          // max 3
          ReservedCard { CardId?: string, Source: Market|Deck, Tier: 1|2|3 }
                     // CardId = null ⇔ Source=Deck AND still in a hand unowned? no:
                     // CardId is ALWAYS known to the engine (deck draw bound it at
                     // reservation time); it is simply *hidden* from every client until
                     // purchased. Owner cannot purchase-blind reveal it early either:
                     // it flips public exactly on PurchaseReservedCard.
      Purchased:    List<string>                // card ids, public; bonus/VP derived or cached
      Bonuses:      { D, S, E, R, O: int }      // derived from Purchased (may be cached)
      NoblesOwned:  List<NobleId>               // public
      VictoryPoints: int                        // derived: Σ vp(Purchased) + 3·NoblesOwned (may be cached)
  FinalRoundTriggered: bool
  TriggerSeat:      int?                        // who crossed 15 (public)
  FinalTurnsRemaining: int                      // seats still owed a last turn
  EliminatedSeats:  List<int>                   // platform AFK handling (§20); [] if none
  TimerConfig + per-player TimerState           // UNO/Silver shape (§20)
  EventLog:         List<string>                // splendor.* envelopes (§19)
```

Field-by-field requirements:

| Member | Owner | Visibility | Persisted? | Derivable? | Notes |
|---|---|---|---|---|---|
| `Decks` (order) | engine | **secret to everyone** | **yes** | no (RNG result) | draw = remove at index 0; refill = insert/remove at 0 of same level list |
| `Market` contents | shared | public | yes | no | slot ⇒ also a membership key for "a card exists in exactly one place" (E3) |
| `PlayerStates[].Reserved[].CardId` (deck-sourced slots) | engine-bound at reservation time | **secret to everyone** (incl. its owner) | yes | no | redacted in projection; becomes public on purchase |
| `PlayerStates[].Tokens` | each player | public | yes | no | sum ≤10 enforced end-of-turn |
| `PlayerStates[].Purchased` | each player | public | yes | no | never shrinks |
| `Bonuses`, `VictoryPoints` | each player | public | cache or derive | **yes** | derivation is the test oracle for E8/E9 |
| `NoblesInMarket`, `NoblesOwned` | engine / players | public | yes | no | disjoint; total across both = revealed count (E10) |
| `FinalRoundTriggered`, `FinalTurnsRemaining` | engine | public | yes | no | drives §11 |
| `CurrentPlayerIndex`, `TurnNumber` | engine | public | yes (index also mirrored on `GameState`) | — | |
| Timer fields | engine | public | yes | no | §20 |
| `EventLog` | engine | public (sanitized) | yes | no | same envelope mechanism as UNO/Silver |

---

## 15. GameAction model

`GameState`/`GameAction` come from `GameEngine.Core` unchanged. Splendor's `ActionType`
strings (dispatched like `SilverActionType` / UNO's strings) and their semantics:

| # | ActionType | Payload (conceptual JSON) | Legal actor | Resolves |
|---|---|---|---|---|
| 1 | `TakeThreeGems` | `{ Colors: ["D","S","E"], Return?: [{Color:"R",Count:1},…] }` | current player, one main action | §5.1 + end-of-turn pipeline |
| 2 | `TakeTwoGems` | `{ Color: "D", Return? }` | current player | §5.2 |
| 3 | `ReserveMarketCard` | `{ CardId, Return? }` (or `Level`+`Slot` position) | current player, <3 reserved | §5.3a |
| 4 | `ReserveDeckCard` | `{ Level: 1|2|3, Return? }` | current player, <3 reserved, deck non-empty | §5.3b |
| 5 | `PurchaseMarketCard` | `{ CardId, Payment: { D,S,E,R,O,Gold }, ClaimNoble?: NobleId }` | current player | §5.4 (market) |
| 6 | `PurchaseReservedCard` | `{ ReservationIndex: 0..2, Payment, ClaimNoble? }` | current player | §5.4 (hand) |
| 7 | `TurnTimeout` | `{}` | **system only** (Game Service sweep) | §20 |
| 8 | `GameTimeExpired` | `{}` | **system only** | §20 |

**Payload rules (universal):**

- Color tokens: the five gem letters and `Gold`/`G`-key reserved for gold in payment
  payloads; exact enum names are the implementer's choice (`SplendorColor` enum), but the
  **engine never trusts client-supplied state values** — supply deltas, deck contents,
  effective costs, noble eligibility, and VP are all recomputed by the engine from its
  own state; payload ids are *targets*, never *facts*.
- `Return` is **required** iff the action's gains would leave the player above 10, and
  its total must be exactly `after − 10`; a `Return` when not needed is invalid (OD-2:
  any held tokens, gold allowed; each returned color must be held in sufficient count).
- `ClaimNoble` is **required** iff, after resolution, **2+** nobles are available and the
  player is eligible (including carried-over eligibility from a previous turn); if
  exactly 1, the engine awards it automatically (a mismatching `ClaimNoble` is rejected);
  if 0, a submitted `ClaimNoble` is rejected. Max one noble per turn — enforced by the
  pipeline, not by trusting the payload.
- Malformed/unknown JSON, unknown enum values, unexpected properties → reject with
  `splendor.invalidPayload`-family codes (fail closed, never partially apply).

**Why atomic payloads instead of Silver-style sub-phase pending states:** every
player choice in Splendor is either (a) a target among *public* objects the client can
evaluate locally (market cards, colors, payment, returns), or (b) a noble choice among
public eligible tiles after a deterministic purchase outcome. The engine can therefore
validate a complete turn in one pass, making timeouts, reconnects, and duplicate-request
handling trivial (§20/§21/§22). Silver's multi-step pending machinery must NOT be copied
here. If the implementer genuinely disagrees, the deviation requires a Critical Decision.

**End-of-turn pipeline (single code path, run for every accepted action, including
`TurnTimeout`):**

```text
1. apply action effect (supply/player deltas, market refill, deck draw, reservation add)
2. token-limit enforcement result: state must already satisfy ≤10 via payload Return
   (otherwise the action was rejected; nothing to enforce)
3. noble visit: eligible = available nobles satisfied by bonuses;
   if 2+: payload ClaimNoble (must be among eligible) awarded; if 1: awarded; if 0: none
4. VP recompute; if active player ≥ 15 and not already FinalRoundTriggered:
   FinalRoundTriggered = true; TriggerSeat = actor; FinalTurnsRemaining = players − 1
   else if a turn just completed and FinalRoundTriggered:
   FinalTurnsRemaining--; if 0 → IsOver=true, compute Winner via §11 ladder, GameEnded=true
5. advance CurrentPlayerIndex to next non-eliminated seat; set TurnNumber++,
   NextActionDeadlineUtc = turnStart + allowance + overrun grace (platform pattern)
6. append public-safe events to EventLog; emit GameEvents for the notification layer
```

---

## 16. GetValidActions specification

`GetValidActions(state, playerId)` returns the **enumerated concrete legal choices**
(exact payloads the engine would accept), never broad categories:

- `TakeThreeGems` — **one entry per 3-combination** of colors with supply ≥ 1 (plus,
  when supply-limited, the forced fewer-colors variants of OD-1). Entries carry empty
  `Return` plans; the caller may add valid returns — enumeration stays bounded.
- `TakeTwoGems` — one entry per color with supply ≥ 4.
- `ReserveMarketCard` — one per **non-empty market slot**, only if the player holds <3
  reserved.
- `ReserveDeckCard` — one per level (1..3) with non-empty deck, only if <3 reserved.
- `PurchaseMarketCard` — one per market card the player can afford
  (`bonus + tokens + gold` per §9) — the action enumerates the **card**; payment mixes are
  validated on submission (affordability computed with *some* valid mix).
- `PurchaseReservedCard` — one per affordable reservation index.
- **Empty list** when: game over; `IsOver`; not the player's turn; unknown player;
  eliminated seat; `FinalRoundTriggered` and this player's final turn already taken is
  never a condition (equal turns are structurally guaranteed: after the trigger, only
  seats with an untaken final turn are current — so "wrong player" already covers it).

This is the engine-level contract used by tests (not an HTTP endpoint; UNO/Silver
precedent).

---

## 17. State invariants

The engine should assert these cheaply (harness checks all of them; runtime
`Debug.Assert`/guard checks where practical — never as a throw that kills a session):

- **I1** Token supply never negative; per-color `supply + Σ players' holdings` equals
  that color's **setup count** (4/5/7 per §4; gold always 5) — tokens are only moved,
  never created or removed (a `ReserveDeckCard` at 0 gold creates nothing; unused
  off-table gems of a color were never in play).
- **I2** No player holds more than 10 tokens at end-of-turn evaluation points (start of
  any turn, end of pipeline step 3).
- **I3** Each of the 90 card ids exists in **exactly one** location: a player's
  `Purchased`, a player's `Reserved`, a `Market` slot, or a `Decks` list — never two.
  (A market-reserved card is not in the market; a purchased reservation is not in
  `Reserved`.)
- **I4** Every player has ≤3 `Reserved`; `PurchaseReservedCard` removes its slot.
- **I5** A `Decks`-sourced reservation's `CardId` is exactly the card the engine drew
  from that deck at that moment (no retro-editing).
- **I6** Market contains ≤4 slots per level; a null slot is never refilled after its
  deck emptied; refill only via a market purchase/reservation.
- **I7** A reserved card cannot simultaneously be in the market (the refill happened at
  reservation time).
- **I8** `Bonuses` per color = count of purchased cards with that bonus color.
- **I9** `VictoryPoints` = Σ VP(purchased) + 3·count(nobles owned) — recomputed, not
  trusted incrementally.
- **I10** The 10 noble ids partition into `NoblesInMarket` ∪ ⋃ `NoblesOwned`
  (of the revealed subset), each at most once; claimed nobles never return.
- **I11** `CurrentPlayerIndex` always references an active, non-eliminated seat (or the
  game is over).
- **I12** Gold is only ever moved by reservation actions (+1 from supply) and payments
  (−G, 0≤G≤gold-held) — never taken as a "gem take" and never awarded >1 per reservation.
- **I13** Purchased/reserved/owned lists never contain duplicate card ids (90 unique
  cards).
- **I14** After the game is over, `ProcessAction` accepts nothing (every action type
  returns failure) — `GameOver` is final.
- **I15** `TurnNumber`/`Version` monotonically increase; the engine never mutates the
  `GameState` it was given.

---

## 18. Persistence and serialization

Follow the existing platform architecture exactly — **no Splendor-specific persistence
mechanism**:

- `GameState.Data["SplendorState"]` = the JSON **string** of `SplendorState` (§14),
  containing: all seats, token supplies and holdings, the **three deck orders**, the
  market, revealed nobles + claims, reservations (with hidden ids — they live only in
  the authoritative blob), turn/final-round state, timer config + per-player accounting,
  event log.
- `GameState.Data["PlayerNames"]` = JSON string userId→display-name map (UNO/Silver
  pattern); rehydrated at load.
- **Always read `Data` via `GameState.TryGetString`** — the known `JsonElement`
  round-trip issue (platform decision 2026-09-06) applies identically here; a plain
  `is string` check breaks after any DB round-trip.
- Round-trip contract (verified repeatedly by the harness, §26):
  ```text
  CreateGame → ToJson → FromJson → ProcessAction → ToJson → FromJson → ProcessAction → …
  ```
  Deck order, market, hidden reservation ids, payments-then-state, nobles, timer
  deadlines, and final-round counters must all survive byte-equivalent functional
  round-trips (same semantic state, `Version` bumped per platform convention).
- The Game Service owns persistence unchanged: PostgreSQL `game_sessions.current_state_json`
  + Redis state cache + `GameActionLog` rows. **No Splendor tables, no Splendor
  services.** A restarted Game Service rebuilds everything from the stored JSON —
  including deck order and hidden reservation identities (they are persisted *authoritatively*,
  which is exactly what makes blind draws replayable).
- Dev-stage schema caveat to record in the implementation's final Critical Decision
  (Silver precedent): sessions persisted under earlier drafts of `SplendorState` are not
  resumable across engine schema changes during development.

---

## 19. Events

Two existing mechanisms, both reused unchanged:

1. **Persistent event log** inside `SplendorState.EventLog`: JSON envelopes
   `{"c":"<code>","d":{params}}` (a `SplendorEvent.Build` helper mirroring
   `UnoEvent`/`SilverEvent`), rendered client-side via `events.splendor.*`.
2. **`GameEvent`/`GameResult.Events`** per action, consumed by the notifier layer.

Suggested log codes (parameters are ids/counts only):

| Code | When | Params |
|---|---|---|
| `splendor.gameStarted` | creation (first state broadcast) | player names |
| `splendor.gemsTaken` | §5.1/§5.2 success | colors, count(s) |
| `splendor.tokensReturned` | any end-of-turn return happened | color:count list |
| `splendor.cardReserved` | §5.3 | seat, tier, source (Market/Deck), **cardId only when source=Market** |
| `splendor.goldTaken` | §5.3 with gold | seat |
| `splendor.cardPurchased` | §5.4 | seat, tier, cardId, payment breakdown, new VP |
| `splendor.nobleClaimed` | §10 award | seat, nobleId, new VP |
| `splendor.turnAdvanced` | every turn | next seat |
| `splendor.finalRoundTriggered` | §11 step 4 | trigger seat, final count |
| `splendor.gameFinished` | over | winner (or none), final scores per seat |

**Leak rules:** an event must never carry a `Deck`-sourced reservation's card id, a deck
order, or an unowned-card preview. The purchase event of a formerly-hidden reservation is
the **first** moment its id may appear. "Avoid putting the entire authoritative state in
events" — events are narrative params; state is delivered via the (projected) state
broadcasts.

---

## 20. Timers

**Reuse the existing platform mechanism unchanged — no Splendor timer service, no new
sweep, no Splendor-specific infrastructure.** The `TurnTimeoutService` already polls
`NextActionDeadlineUtc` / `GameEndsAtUtc` on the state root and dispatches
`TurnTimeout` / `GameTimeExpired` system actions through the normal engine path (platform
decisions 2026-09-06/07, and the 2026-09-12 requirement: the Splendor publisher must keep
setting `GameType` on every `StateUpdated` notification — the harness/service integration
test checks the notifier projects).

Splendor-specific policy inside the engine:

- **Configuration** (settings `Timer` key, same shape as UNO/Silver): recommended
  defaults **60 s base turn / 180 s max total allowance / 15 s overrun grace /
  3 consecutive skips AFK threshold / 60 min total game time**. These are UX
  configuration, **not rules** — any deviation needs a Critical Decision, and neither
  UNO's nor Silver's numbers may be documented as "the rule".
- **Deadline start:** each turn (`SetTurnClock` at pipeline step 5) with the platform's
  bank/allowance accounting: `NextActionDeadlineUtc = turnStart + allowance + MaxOverrun`
  (hard deadline; the client derives the soft end — UNO/Silver precedent).
- **`TurnTimeout`** (deadline passed): charge the standard overrun (bank-first then
  deferred penalty, floor `Base − MaxOverrun`), **skip the turn** (Splendor has no
  pending-decision sub-states thanks to §15, so "resolving" one is a no-op), count the
  consecutive timeout, then run the normal end-of-turn pipeline — note a skipped turn
  **still checks nobles** (the player may claim an eligible noble without acting; the
  physical rule says nobles come at end of turn regardless; deterministic choice:
  award the first eligible noble in **noble display order** `N-DS, N-SE, N-ER, N-RO,
  N-OD, N-ERO, N-DSO, N-SER, N-DRO, N-DSE` §3.2 — this auto-choice order is OD-5 and must
  be recorded).
- **AFK elimination at 3 consecutive timeouts:** platform pattern: 2-player → the
  survivor wins immediately (`Winner` set, `IsOver`); 3–4 players → seat is added to
  `EliminatedSeats`, skipped forever, **its board remains on the table publicly**
  (purchased cards/nobles keep counting for scoring of *others*' tie-breaks? No — scoring
  is per-player; eliminated players are simply excluded from winner determination; their
  cards/tokens stay put — no redistribution). If the eliminated seat was the §11 trigger
  source mid-final-round, remaining final turns still run among active seats (OD-6:
  exact elimination interaction must be recorded as a Critical Decision at implementation;
  the platform pattern above is the default).
- **`GameTimeExpired`** (60-min cap): engine force-finishes: `IsOver = true`, winner by
  the §11 ladder evaluated on current scores (a full unresolved ladder → `Winner = null`,
  draw — platform precedent UNO 2026-09-07). Rejected before the limit with
  `splendor.timeNotUp`.
- Splendor has **no multi-step player actions** (§15) — explicitly no pending-decision
  timeout logic to copy from Silver.

---

## 21. Reconnection

No Splendor-specific reconnection endpoint or mechanism. Existing platform behavior
already covers it, provided the engine projects (§12):

- **Browser refresh / SignalR reconnect:** client re-`joinSession`; the server
  `ReconnectPlayerCommand` returns **the projected state for that seat** (viewer-
  independent here, so effectively "the public state") — including the reconnecting
  player's still-unrevealed blind reservations **without** their identities, and their
  token totals.
- **Game Service restart / PostgreSQL reload:** full authoritative state comes back from
  `CurrentStateJson` (deck orders and hidden ids intact → blind draws stay deterministic);
  Redis is a cache; loss is survivable per current platform behavior (PostgreSQL is the
  source of truth).
- **Single-tab `SessionTakenOver`** enforcement: unchanged, applies automatically.
- A reconnect during the **final round** must show `FinalRoundTriggered` +
  `FinalTurnsRemaining` (public fields) so the UI renders the end-of-game banner; the
  projected turn deadline is restored with the state.
- Reconnection never grants *more* information than a live player — the hidden-id rule
  (§12) applies equally to `GET /state`, action responses, broadcasts, and reconnect.

---

## 22. Concurrency, duplicates, and stale actions

The Game Service processes a session's actions **sequentially** through
`ProcessGameActionCommandHandler` (DB-read → engine → save; single-instance dev-stage
limitation already documented platform-wide). The Splendor engine's assumptions:

- The engine is a **pure deterministic function** `(state, action) → result`; it must
  never read clocks, ids, or external state. The bank/deadline checks use the action's
  `Timestamp` and the state's own timers (UNO precedent).
- **Duplicate submissions** (double-click, StrictMode re-invoke, retry): a stale
  duplicate arrives against post-action state and fails naturally — `notYourTurn` (turn
  advanced), `unknownCard`/`invalidReservation` (its target moved), or `gameOver`. The
  engine must **not** add dedup infrastructure; the frontend's in-flight guard
  (`actionInFlight` in `GamePage`) and server sequence numbers already exist.
- **Stale clients** acting from an old projected state: any target reference is validated
  against authoritative state, so stale actions fail closed with coded errors (never
  partial application). The client refreshes from the broadcast (state `Version` bump).
- **Two simultaneous requests from the same player:** the service path is serialized;
  the second is a stale-duplicate case per above.
- `GameAction.SequenceNumber`/`Timestamp` are recorded (action log) but Splendor's rules
  do not consult them for ordering — never introduce gameplay keyed on them.
- No game-specific concurrency primitives (no optimistic version checks inside the
  engine; platform decision territory if ever needed).

---

## 23. Frontend requirements

Follow the UNO/Silver conventions exactly; no new infra, no duplicated plumbing:

- `frontend/src/features/game/splendor.ts` — typed mirror of the **projected** state
  (PascalCase JSON mirror like `silver.ts`), `parseSplendorState(state)` extracting
  `Data["SplendorState"]`, plus **the authoritative-shape rejection guard** (§12 last
  bullet) and small helpers (effective cost display given public data, affordability).
- `frontend/src/features/game/SplendorGameView.tsx` (+ focused subcomponents) and a
  shared `SplendorCardVisual.tsx` / noble/token visuals under
  `shared/components/` (UNO/Silver precedents: `UnoCardVisual.tsx`,
  `SilverCardVisual.tsx`).
- Wire into `GamePage.tsx`'s view switch (`gameType === 'Splendor'`).
- `gameMeta.ts`: **remove `comingSoon: true`** from the existing `Splendor` theme entry
  (a placeholder shipped earlier); catalog/room creation discover the game automatically
  via `GET /api/game/games` once registered.
- Replace the placeholder `games.Splendor` i18n section (§24).

### Board layout (must make the state understandable and playable)

**Shared/table area**

- Three tier rows (Level 3 at the back/top, Level 1 nearest — rulebook stacks ascending)
  × 4 slots; empty slots visibly "gone" when a deck exhausts, with remaining deck count
  beside each tier (count only — never contents).
- Nobles strip: 3–5 tiles showing requirement gem icons + "3" VP.
- Token bank: 6 piles (D/S/E/R/O/Gold) with live counts, click-to-take affordances.

**Per-opponent panels**

- Tokens by color (counts) + gold; bonus chips per color; VP total + noble tiles;
  reserved-card slots: count and tier, market-sourced cards shown face-up (they were
  public picks), **deck-sourced slots shown as face-down backs with no information**
  (no badge implying the viewer knows anything).
- Current-player highlight; final-round badge and per-seat "final turn pending/taken".

**Own player area**

- Same as opponents, plus interaction. The player's **own blind reservations are shown
  face down to the owner too** (they do not know them — this is the sharpest contrast
  with Silver's known face-down cards; render backs, full stop).
- Purchased tableau grouped by bonus color (physical-staggered rows), running VP.

**Interaction model (engine-authoritative; UI derives availability from projected state +
`Version`, never client-side rules):**

- Gem taking: pick 3 distinct colors (highlight when only ≤3 of a color remain for the
  2-of-a-kind path — disable the pair button when supply <4 with an explanatory tooltip);
  when the take would exceed 10, open the **return selector** (any held tokens, incl. the
  new ones) before submit — mirroring the atomic payload.
- Reserve: click a market card or a tier's deck (disabled at 3 reserved, with reason).
- Purchase: click any market card or own reservation → purchase panel shows: printed
  cost, **bonuses struck through / effective cost**, a payment composer
  (colored tokens + gold), a live "can afford: yes/no — why" line
  (`3 emerald bonus covers the emerald cost; still need 2 gold-worth` style), and a
  noble-choose step when 2+ eligible (shown as a required pre-selection because the
  engine requires `ClaimNoble`; disabled/hidden when ≤1).
- Illegal targets render as disabled with the localized reason (client mirrors the
  public rules — but the engine is the only enforcer).
- End-game presentation: threshold banner ("15 points reached — finishing the round"),
  final-turn markers, and the game-over standings modal with the tie-break result
  ("shared victory" case must render — `Winner = null`).
- Turn timer: reuse the proven `TimerRing`/`useNow` soft/hard-deadline pattern.
- RTL: all logical Tailwind utilities per the platform i18n decision; gem/token
  circular chips are direction-neutral; the tier rows stack vertically and mirror
  naturally.
- **No game rules in React:** affordability/eligibility shown by the UI are display
  aids derived from public state; every legality decision belongs to the engine.

---

## 24. Localization

Everything user-facing exists in **both** `locales/en.ts` and `locales/fa.ts` (the
`fa: Dict` type makes missing Farsi keys a compile error — platform mechanism), with
full-parity `events.splendor.*` keys and a replaced `games.Splendor` section (title,
tagline, description, 6-odd `rules[]` entries with valid `RuleIconName`s — the current
placeholder must be overwritten, not appended to).

Key inventory:

- `games.Splendor.*`: UI copy for every Splendor screen string (section analogous to
  `games.UNO`/`games.Silver`, incl. a `view` strings section like the existing `silver`
  view section — the pattern exists at `uno`/`silver` view string groups).
- `events.splendor.*`: templates for §19 codes with `{param}` interpolation, colors
  rendered through a `splendorGems` name map (below).
- Gem names (shared EN/FA): Diamond الماس / Sapphire یاقوت کبود / Emerald زمرد /
  Ruby یاقوت سرخ / Onyx اونیکس / Gold طلا (implementer verifies phrasing; these are the
  standard Farsi gem terms).
- Tier names: Level 1 (mines), Level 2 (transport & artisans), Level 3 (jewelers).
- Action names (Take three gems / Take two identical / Reserve / Buy…), status lines,
  validation/error UI strings ("You already have 3 reserved cards", "Supply too low to
  take two — needs 4", "Only the trigger player's last turn…", etc.).
- Noble labels built from gem names ("4 Diamonds + 4 Sapphires") + "Noble"/"اشراف‌زاده".
- End-game/tie-break messages, including the shared-victory variant.
- Hardcoding English or Persian strings inside components is forbidden.
- **Server-side error localization (mandatory):** every `splendor.*` engine error code
  (§25) must be added to `BuildingBlocks.Domain/Localization/errors.en.json` **and**
  `errors.fa.json` (EN mirrors the coded message table style; FA per the platform's
  Farsi catalog) so the `GlobalExceptionHandlerMiddleware`/`X-Language` boundary
  localizes them; the client `backendMessages.ts` remains a legacy fallback only.

---

## 25. Error model

Smallest useful `Errors` class inside `Games/Splendor` (mirroring `UNO/Errors.cs`,
`Silver/Errors.cs` — game codes stay in the game project; **not** in `ErrorCodes.cs`
beyond the platform entries). Every code appears in **both** server catalogs (§24):

| Code (proposed) | Meaning |
|---|---|
| `splendor.gameOver` | game already finished |
| `splendor.invalidState` | missing/corrupt `SplendorState` payload |
| `splendor.invalidPlayer` | actor not in the session's seats |
| `splendor.notYourTurn` | actor is not the current player |
| `splendor.unknownActionType` | unrecognized ActionType |
| `splendor.invalidPayload` | malformed JSON / unknown fields / bad shapes |
| `splendor.invalidGemColor` | not a valid gem color / gold used as a take color |
| `splendor.duplicateColor` | colors repeat in a "3 different" take |
| `splendor.insufficientSupply` | supply can't cover the take/payment |
| `splendor.cannotTakeTwo` | fewer than 4 of that color on the table |
| `splendor.exactGemsRequired` | 3-different take submitted with wrong count (OD-1) |
| `splendor.tokenLimitExceeded` | missing/incorrect `Return` for the 10-cap |
| `splendor.invalidReturn` | return list over-held / wrong total / gold mismatch |
| `splendor.reservationLimit` | already 3 reserved cards |
| `splendor.deckEmpty` | blind reserve from an exhausted deck |
| `splendor.cardUnavailable` | market card id absent (taken/never there) |
| `splendor.reservationNotFound` | bad `ReservationIndex` |
| `splendor.insufficientFunds` | payment mix cannot cover the effective cost |
| `splendor.invalidPayment` | overpayment / wrong-color payment |
| `splendor.nobleUnavailable` | claim targets a gone/never-eligible noble |
| `splendor.nobleChoiceRequired` | 2+ eligible and none claimed |
| `splendor.nobleChoiceInvalid` | claim given but not among eligible |
| `splendor.timerNotExpired` | player-forced `TurnTimeout` misuse (deadline not passed) |
| `splendor.timeNotUp` | forced `GameTimeExpired` before the cap |
| `splendor.playerEliminated` | action from an eliminated seat |

Positional args (`{0}`…) carry names/counts like the UNO codes; harness tests assert the
code (not the English text).

---

## 26. Testing specification

A **console verification harness project `tests/Splendor.Harness`** (exact
`Silver.Harness` pattern: net9.0 console project in the solution, `Check(name, cond)`
pass/fail counters, exit code 0/1, deterministic via the `Seed` setting where setup RNG
matters) is **required** — smoke tests are not sufficient. Every rule section above maps
to checks; the implementation agent records the final pass count in a Critical Decision.
Coverage minimums (grouped like Silver's 240+ checks):

**Setup**
- 2/3/4-player creation: token supply 4/5/7 per color + 5 gold; nobles 3/4/5; decks
  40/30/20; market 4×3; players empty of everything; `CurrentPlayerIndex` set; deadlines
  set; 1 and 5 players rejected.
- Catalogue integrity: 90 unique ids; exact cost/bonus/VP tables (§3.3) — assert totals
  (L1 VP sum 5, L2 sum 55, L3 sum 80; per-level counts 40/30/20; per-color design counts
  8/6/4); 10 nobles with the §3.2 requirements; rulebook example card `L3S02` present
  verbatim (4 VP, S bonus, 6D+3S+3O).

**Gems**
- take 3 different: supply deltas, exact holdings; reject duplicates/zero-stock colors;
- take 2 identical: allowed exactly at supply ≥4 (boundary tests at 4 and 3); reject
  gold; exact −2 deltas;
- supply-shortage OD-1 behavior (2 colors left → forced 2-color take; 1 → 1; 0 → no
  take action legal);
- 10-cap: take to 11+ with valid `Return` (any mix incl. gold, incl. just-taken); reject
  missing return, over-return, under-return; 10-cap untouched by purchases; zero-cost
  purchase at 10 tokens succeeds; reserve-at-10 → return-1 path.
- conservation invariant I1 after every token action.

**Reservation**
- market reserve: card moves, market refills from that tier's deck top (seeded), slot
  null when deck empty, gold +1 when supply, gold 0-legal when exhausted, still exactly
  one gold;
- deck reserve: blind — projection/`EventLog`/other-player data **never** contain the id
  (assert on projected states and log strings); owner state still hides it in
  projection; purchase reveals;
- 3-limit: reserve #4 rejected at both variants;
- reserved card grants no bonus/VP/noble eligibility; reserved cards cannot be
  discarded/returned; wrong-index/foreign-reservation rejects.

**Purchasing**
- exact cost; bonus discounts (partial, over-covering → floored at 0, surplus other-color
  bonuses unused); gold-joker payment mixes; all-bonus zero-cost; rejection of
  under/over/wrong-color payment; market refill timing; reserved-source purchase (no
  refill); VP + bonus updates; purchased card gone from all pools (I3);
- purchase of a face-down reservation reveals its id only from then on.

**Nobles**
- eligibility math (bonuses-only, ≥ comparison); auto-award at exactly 1 eligible;
  choose-when-2+ (valid, invalid, missing → `nobleChoiceRequired`); claimed tile never
  awardable again; carry-over eligibility awarded next turn's end; noble on a take/reserve
  turn (end-of-turn check independent of action type); VP +3 flows into scoring and can
  trigger §11; nobles never refilled; all-nobles-claimed edge.

**End game**
- trigger exactly at 15 (and >15 in one buy); trigger via noble; equal final turns
  (each other seat gets exactly one); trigger seat gets none; final-round crossings don't
  extend; game ends with `IsOver` + correct winner;
- tie-break ladder: VP → fewer purchased cards → fewer reserved → shared (`Winner=null`),
  each rung tested with a fixture state;
- I14: every action rejected after over.

**ValidActions**
- per §16 on scripted states: exhaustion (no deck, no market), supply limits, cap
  reachability, wrong player → empty, game over → empty, reservation limit, affordability
  enumeration incl. bonus+gold coverage; empty-market/deck corner.

**Serialization**
- `Create → Serialize → Deserialize → Continue` loops: market, deck order preserved,
  tokens, **hidden reservation ids**, purchased lists, nobles, current player, final-round
  counters, timers (deadlines as UTC), event log; `TryGetString` path (JsonElement branch).

**Randomness / information security**
- seeded determinism of setup; unseeded variance (different orders across creations);
- payloads cannot name hidden cards; **no client path (projection, events, reconnect
  response, timeout-driven state) ever exposes** deck order or blind-reservation ids —
  assert across **every viewer seat** including the owner of a blind reservation.

**Invariants**
- run the §17 battery (I1–I15) after every action in a long scripted game (e.g. seeded
  50-turn random-valid-action fuzz via `GetValidActions` — Silver-style soak).

**Timers / system actions**
- `TurnTimeout` only past deadline; overrun bank charge; 3× AFK elimination (2p survivor
  wins / 3-4p removal); `GameTimeExpired` force-finish ladder; timeout turn still runs
  noble check + auto-claim order (OD-5 fixture).

---

## 27. Edge-case compendium

Each row is an **answer the engine must implement**; ⋆ marks open-interpretation defaults
(§OD) that additionally require a recorded decision.

| Case | Behavior |
|---|---|
| 10 tokens, wants to take 3 | take resolves to 13, payload must `Return` exactly 3 (any mix) |
| 10 tokens, wants to take 2 identical | overflow 2 → return 2 |
| 9 tokens, take 2 identical | 11 → return 1 |
| 10 tokens, reserve with gold available | still legal: +1 gold → return 1 (the gold may be returned back to supply) |
| 10 tokens, reserve with **0** gold | legal, no gold, no returns forced |
| 10 tokens, purchase (any cost ≥0) | legal; purchases never exceed the cap |
| Purchase fully covered by bonuses | zero payment accepted (tokens untouched) |
| Bonus exceeds printed cost in a color | `max(0, …)` — no negative, no refund |
| Supply: 4 of a color | take-2 legal (ends at 2); a third color take unaffected |
| Supply: 3 of a color | take-2 illegal; take-3-different legal (−1) |
| Supply: 0 of a color | that color absent from any take; nobles/market unaffected |
| Only 2 colors with tokens, take action | ⋆ OD-1: must take exactly 2 different (or take-2/other action instead) |
| Only 1 color has tokens | OD-1 default: the "different colors" take resolves to 1; take-2 still possible at ≥4 |
| Level deck empty, market slots of that tier | slots drain to null on purchases/reserves and stay null; `ReserveDeckCard` illegal for that tier |
| Last market card reserved | slot refills (deck had cards); if deck emptied exactly then, next drain to null |
| 3rd reservation | legal (limit is "more than three" illegal); 4th always rejected |
| Reserve market card of a tier whose deck is empty | slot goes null immediately |
| Buy a reserved card at reservation index that moved (bought earlier) | index bounds → `reservationNotFound` |
| Blind reserve then immediately purchase next turn without knowing | legal by design — luck buy; identity revealed at purchase |
| Payment mix where gold could cover multiple colors | any split is legal iff each color's shortfall ≤ gold spent on it (engine-derived) |
| Noble eligible on a token-taking turn | awarded (end-of-turn check is unconditional) |
| Noble eligible + purchase pushes to 15, and also 2 nobles eligible | pipeline order: purchase VP → award **one** noble → threshold check after noble (so a 14→15 crossing with a pending noble resolves before triggering; the 2nd noble stays for later turns) |
| 15 reached by the *non-trigger* player during the final round | game does not extend; ladder runs at scheduled end |
| Two players reach ≥15 in the same final round | both count in the ladder; no re-trigger |
| Exact tie after ladder rung 3 | `Winner = null` — UI must render shared victory |
| Everyone's market cards exhausted / nobody can act | not possible to "stall": takes always available while any supply exists; if all colors at 0 and all decks empty… (35 gem tokens vs 4 players×10 — a full starvation is unreachable; tokens cycle back on purchases; the engine still behaves: legal action set may shrink to purchases/reserves; if genuinely empty, `GetValidActions` returns [] and only timers progress the game — platform timer handles AFK) — document as theoretically-impossible-in-practice |
| Duplicate/identical submission of a purchase (double click) | first applies; second fails on moved target (`cardUnavailable`/`notYourTurn`) — no dedup logic needed |
| Action from a non-current seat | `notYourTurn` |
| Action after game over | `gameOver` (I14) |
| Reconnect mid-final-round | full public state incl. `FinalTurnsRemaining` (no leak risk) |
| TurnTimeout while player was about to return tokens | not applicable — atomic actions: an unsubmitted take never half-applied (server never mutates before validation) |
| GameTimeExpired mid-final-round | force-finish ladder from current totals |
| Client submits `ReserveDeckCard { CardId }` | rejected (`invalidPayload`) — clients name levels, never hidden cards |
| 2-player game "take 4 of a kind" | **does not exist in base game** — rejected (expansion rule) |
| Noble requirement partially met (3 of 4) | no award (strict ≥ on every color of the requirement) |
| Gold used in payment for color X then claimed "discount" double-count | engine math (§9) is single-source; payment tokens + gold exactly cover post-discount cost, nothing more |

---

## 28. Integration checklist

For the future implementation agent. **Completion = every box checked with evidence in
the recorded Critical Decision.** No step may require touching UNO/Silver or platform
projects beyond the named localization-catalog additions.

1. Create `backend/src/Games/Splendor/Splendor.csproj` (net9.0, references **only**
   `GameEngine.Core`, XML docs on) and add to the solution.
2. Static catalogue: 90-card table + 10-noble table exactly as §3 (ids, costs, bonuses,
   VP) — transcribe, then **re-audit against §3.3 character-by-character** (a wrong cost
   silently breaks balance tests).
3. `SplendorGame : IGame, IPlayerViewGame` — `GameType => "Splendor"`, 2–4 players;
   partial-class split welcome (Silver pattern: core / actions / valid-actions / view).
4. `SplendorState` (+ view model) per §14/§12; serialization per §18 (`TryGetString`).
5. `CreateGame` per §4 (seeded-RNG support §13).
6. Action dispatch + per-action validation per §15 (atomic payloads incl.
   `Return`/`ClaimNoble`), end-turn pipeline exactly §15 steps 1–6.
7. Nobles/purchasing/threshold logic per §5–§11; tie-break ladder per §11.
8. `GetValidActions` per §16 (concrete enumeration).
9. Invariants §17 — runtime guards where cheap; all of them harness-tested.
10. Error codes §25 in a `Games/Splendor/Errors.cs`, **plus both**
    `errors.en.json` and `errors.fa.json` catalog entries.
11. One DI line next to the existing games in
    `Game.Infrastructure/Extensions/ServiceCollectionExtensions.cs`:
    `services.AddSingleton<GameEngine.Core.IGame, Splendor.SplendorGame>();` — the
    `GET /api/game/games` catalog and `GameEngineProvider` then need zero changes; no
    plugin loading, no factory.
12. Event log + `GameEvent` types per §19 (client-renderable envelopes, no leaks).
13. Timers per §20 — reuse only; keep `GameType` on every `StateUpdated` publish
    (platform 2026-09-12 crash/deadlock-of-secrets precedent).
14. Frontend: `splendor.ts` (+ projection-shape rejection guard), `SplendorGameView.tsx`
    + `SplendorCardVisual.tsx`, `GamePage` switch, `GAME_THEME` `comingSoon` removal,
    `games.Splendor` (replace placeholder) + `events.splendor.*` + view strings in
    **both** locales; RTL-safe.
15. `tests/Splendor.Harness` covering every §26 group; run it green; record count.
16. `dotnet build` solution-wide + `npm run build` (tsc+vite) green.
17. Live playtest path: docker compose env → create Splendor room (2/3/4 seats) →
    full game to a 15+ finish incl. a tie-break; verify leak checks by inspecting raw
    SignalR payloads for `Decks`/hidden ids (must be absent).
18. **Final rules audit:** re-read §§3–13 top-to-bottom against the actual code; then
    update the root `AGENTS.md` status/index (row "Splendor" → implemented, link here)
    and record the implementation Critical Decision(s) (with any OD-n resolutions)
    **below the marker in §Critical Decisions** of this file. Never silently alter a
    documented rule.

---

## 29. Rule audit trail and authoritative sources

Sources used for this specification (in precedence order):

1. **Official 2014 Space Cowboys rulebook (US English), `Rules_Splendor_US.pdf`** —
   full text extracted 2026-09-13 from the Internet Archive capture of the publisher
   URL (218 captures, 2015–2026); this document's §§3.1, 4, 5, 6, 7, 8, 9, 10, 11 rules
   and the 90/40-30-20/10-noble component facts come from it, with quoted wording.
2. **`anicolao/splendor`** (2026) — a photo-verified extraction of **all 90 physical
   development cards** with per-card level/bonus/prestige/cost; adopted as the §3.3
   catalogue (its records reproduce the rulebook's worked example card `L3S02` exactly,
   and the README documents that physical photographs override a community deck list
   which had at least one wrong cost).
3. **Community reference datasets** (`caeleel/splendor` implementation,
   `filipmlynarski/splendor-ai` card/noble CSVs) — cross-checks; they confirm the
   3-level/90-card structure and the noble 5-cycle + complementary-triples layout
   (§3.2); where they disagree with the rulebook/photos they are ignored.
4. **Wikipedia (EN/FR)** and **UltraBoardGames** rule transcriptions — cross-checks of
   the current-printing tie-break ladder (§11) and historical-noble trivia; the
   rulebook-derived text is the authority (EN Wikipedia's "fewest reserved cards"
   addition is attributed to the *current official rulebook printing* it cites).

Documented discrepancies and their resolutions are in the Critical Decisions below
(3-level vs fan "4-level"; tie-break ladder; 2-player "take 4" exclusion; noble
automatic-choice).

**Remaining open interpretation decisions (defaults prescribed; each must be converted
into a final Critical Decision at implementation):**

- **OD-1** — "Take 3 different" when fewer than 3 colors have supply: prescribed
  default — forced smaller take of exactly `min(3, availableColors)` distinct colors
  (at least 1), no voluntary taking-fewer while 3+ exist.
- **OD-2** — Which tokens may satisfy the post-take return: prescribed default — any of
  the player's held tokens (any colors incl. gold), the rulebook sentence only clarifies
  just-drawn tokens are *not protected* from return.
- **OD-3** — `Return` payload when overflow occurs but the player submits none:
  prescribed default — reject with `splendor.tokenLimitExceeded` (no auto-returns;
  the player chooses).
- **OD-4** — Physical "youngest player starts": prescribed default — random start seat
  at creation (seeded under `Seed`), platform precedent from Silver.
- **OD-5** — Auto-ordering of noble choice when a timeout/AFK turn still owes a
  multi-eligible award: prescribed default — first eligible in the §3.2 table order.
- **OD-6** — AFK-elimination asset handling (tokens/board stay put; excluded from
  winner determination): prescribed default per §20 (platform pattern), pending explicit
  confirmation that no token reclamation happens (the physical game has nothing to say —
  elimination is a digital-convenience concept).
- **OD-7** — Tie-break ladder depth (§11 steps 3–4) — adopted from the current official
  printing as cited above; the implementer must re-verify against the latest publisher
  PDF if reachable and record any correction.

---

# Critical Decisions (Splendor)

Game-specific decisions only; platform decisions live in the root `AGENTS.md`.

<!-- Add new Splendor decisions below this line -->

- **2026-09-13** — Splendor selected as the next game after UNO and Silver (specification prepared; implementation NOT started)
  - **Context:** Two games are shipped. UNO exercised the simple turn/shedding loop; Silver exercised hidden information and multi-step pending decisions. The platform has repeatedly listed Splendor as a future game (the original spec even named it first; the 2026-09-10 Silver decision noted "Splendor will follow"). A third game should stress parts of `GameEngine.Core` neither shipped game touched.
  - **Decision:** Splendor (Marc André, Space Cowboys 2014) — **classic base game only** — is selected as the next implementation target. This document is the authoritative specification; it was authored with zero code changes (`Games/Splendor` does not exist).
  - **Rationale:** Splendor is low-randomness and strategy-oriented — a useful contrast to UNO (high-card-interaction shedding) and Silver (memory/hidden-village deduction). It introduces, for the first time on the platform: a **shared token economy** (supply conservation), **permanent engine-building bonuses** (derived public state), a **market with tiered decks and immediate refill** (multi-region card-movement invariants), **deterministic multi-constraint validation** (payment composition + wildcards), and **automatic end-of-turn triggers** (nobles, threshold, equal final turns) — a genuinely different shape of state machine while still fitting `IGame` + one DI line with no platform changes. Its hidden information (deck order, blind reservations) is real but **viewer-symmetric**, exercising `IPlayerViewGame` from the opposite direction and proving the existing projection mechanism generalizes without per-viewer knowledge machinery.

- **2026-09-13** — Base game only; expansions and Spin-offs excluded
  - **Context:** Splendor's ecosystem (Cities of Splendor's four modules, Marvel, Duel) changes noble counts, token rules (take 4/5), adds city/premium cards, and changes end conditions.
  - **Decision:** Only the rules in §§3–13 are in scope. No expansion components or rules may enter the engine, settings, or UI. 2-player games use the base actions with reduced token supply and 3 nobles — explicitly **not** the expansion "take 4 of a kind" (the 2014 rulebook states "There are no other changes").
  - **Rationale:** Keeps the game catalogue honest against the printed classic and prevents the common digital-hybrid drift; the Out-of-Scope list of the root AGENTS.md applies to games the same way.

- **2026-09-13** — Official component configuration is 3 levels / 90 unique cards / 10 unique nobles / 40 tokens (7+7+7+7+7+5)
  - **Context:** Widely circulated fan/app descriptions claim four card levels (100 cards, 40/30/20/10), ×4 card copies, and 40 noble tiles; other community datasets (e.g. one RL CSV with a 45-card Level 1 and wrong VP values) are demonstrably non-official. The official 2014 rulebook Contents panel and photographs of the physical deck settle it.
  - **Decision:** §§3.1–3.3's tables (from the rulebook + the photograph-verified card extraction, §29) are authoritative: three decks of 40/30/20 **unique** cards (each design appears exactly once), 10 distinct noble tiles, 7 tokens of each gem, 5 gold. The catalogue in §3.3 is to be transcribed verbatim.
  - **Rationale:** The physical/publisher artifact outranks summaries; a wrong deck model would silently corrupt market size, refill, reservation and all end-game tests. Cross-validated three ways (rulebook example card, photo extraction, community datasets' structural agreement).

- **2026-09-13** — Information model: secrets hidden from all viewers; `IPlayerViewGame` implemented with a viewer-independent projection
  - **Context:** Splendor's only hidden information (deck orders; blind-reservation identities — unknown even to the owner) is symmetric: no player legitimately knows anything another cannot. Silver built the per-viewer projection for asymmetric secrets; UNO ships everything.
  - **Decision:** `SplendorGame` implements `IPlayerViewGame` returning the **same projection for every viewer**: full state minus deck sequences and minus `Deck`-source reservation card ids. No per-player knowledge state, no visibility framework, no Silver-style knowledge maps — and the owner's own blind reservations render as backs to their owner.
  - **Rationale:** Reuses the platform's existing projection wiring (per-connection pushes, action/get/reconnect responses) with zero new concepts; anything more (asymmetric knowledge) would be over-engineering for this game; anything less (UNO-style full broadcast) would leak future market cards and blind picks — both strategically meaningful.

- **2026-09-13** — Atomic action payloads (no pending sub-phases)
  - **Context:** Silver needs drawn-card sub-states; Splendor's decisions (which tokens, which returns, which payment, which noble) are all made from public information in one sitting.
  - **Decision:** Every Splendor `GameAction` carries its complete decision set (`Colors`, `Return`, `Payment`, `ClaimNoble`) and resolves the whole turn — action effect, cap enforcement, noble award, threshold, advance — in one `ProcessAction` call. Multi-step pending states are forbidden without a new Critical Decision.
  - **Rationale:** Keeps timeout handling, reconnection, idempotency and the harness trivially deterministic; matches how the physical turn actually collapses onto one choice point. (The UI still prompts sequentially — it just submits once per turn.)

- **2026-09-13** — Nobles are automatic end-of-turn awards: max one per turn, chosen on multiplicity, eligibility carried over
  - **Context:** Digital implementations vary ("pick exactly one ever", "auto all at once"). The 2014 rulebook is explicit: automatic, cannot refuse, not an action, one per turn, player chooses among 2+, and checks happen at the end of **each turn**.
  - **Decision:** The end-turn pipeline (§15 step 3) evaluates all available nobles against the active player's bonuses; awards exactly one (payload `ClaimNoble` required at 2+, auto at 1); unchosen eligible nobles remain and are picked up at the end of later turns; nobles can themselves trigger the ≥15 end.
  - **Rationale:** Literal-rule fidelity; makes "multiple simultaneous eligibility" unambiguous; the only unspecified corner (timeout-driven multi-eligibility auto-order) is OD-5.

- **2026-09-13** — End-of-game: equal final turns; tie-break ladder VP → fewest purchased → fewest reserved → shared victory
  - **Context:** "First to 15 wins" is a wrong shorthand; and the 2014 rulebook text lists only the first tie-break while the current printing adds two more. A common digital myth ("fewest tokens wins") is not official.
  - **Decision:** §11's pipeline is authoritative: the triggerer finishes the turn, every other seat takes exactly one more turn, then final scoring; ladder 1–4 with `Winner = null` on the shared-victory rung; token-count tie-breaks explicitly rejected.
  - **Rationale:** Engine determinism + platform `Winner` model; resolves the edition disagreement by adopting the current official text while documenting the 2014 baseline; OD-7 flags the re-verification requirement.

- **2026-09-13** — Randomness: server-only, creation-time, seedable for tests
  - **Context:** Shuffles and the blind deck draw are the only random events; a client must never influence them.
  - **Decision:** All RNG lives in `SplendorGame` (deck shuffles, noble subset + start seat). Drawn cards are selected by the engine (`ReserveDeckCard` takes a level only). Optional `Seed` setting (Silver precedent) is a **testing capability only**.
  - **Rationale:** Mirrors the platform's existing server-authoritative randomness pattern; harness needs reproducible setups.

- **2026-09-13** — State & persistence: single `Data["SplendorState"]` JSON blob, platform mechanisms only
  - **Context:** Every prior game stores its state as one JSON string in `GameState.Data`, read via `TryGetString`; nothing else is game-persistent.
  - **Decision:** Splendor follows §18 exactly: one blob (including deck order and hidden reservation ids — required for blind-draw replay), `PlayerNames` beside it, zero new tables/services/caches.
  - **Rationale:** Keeps the Game Service game-agnostic (the integration contract: a game is a `Data` payload plus an `IGame`), reuses the solved `JsonElement` round-trip semantics, and makes the round-trip contract (§18) the single serialization test surface.

- **2026-09-13** — Testing decision: console verification harness (no placeholder assertions)
  - **Context:** UNO used scratch harnesses; Silver institutionalized a full `tests/Silver.Harness` project — the AGENTS.md-out-of-scope note makes such a harness the platform's accepted per-game verification shape.
  - **Decision:** Implementation requires `tests/Splendor.Harness` covering every group in §26 with rule-faithful assertions (catalogue totals, invariants I1–I15, leak checks, ladder fixtures), seeded where randomness matters; pass results recorded in the implementation's Critical Decision.
  - **Rationale:** Splendor's risk surface is combinatorial legality (payment/cap/noble/threshold) and leaks; only exhaustive mechanical checks can certify the catalogue and pipeline.

- **2026-09-13** — Specification-only step; no implementation performed
  - **Context:** The owner directed that this step produce the rules/implementation specification alone.
  - **Decision:** This file is the sole deliverable (plus one index line in the root `AGENTS.md`'s Game Documentation table, required by that file's own documentation-organization rules). No `Games/Splendor` project, no frontend changes beyond leaving the existing `comingSoon` placeholder untouched, no engine/platform edits.
  - **Rationale:** Documentation-first per explicit instruction; the root AGENTS.md explicitly forbids creating a Splendor project before a new decision — this decision (§ above) records the selection; implementation remains a future, separately-directed step.
