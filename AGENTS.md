# Board Game Platform - Implementation Specification

## Current Status

| Component | Status |
|---|---|
| Foundation (services, BuildingBlocks, gateway, docker, auth, lobby) | COMPLETE |
| UNO (first game) | COMPLETE |
| **Silver** | **COMPLETE — backend engine + player-view projection + frontend view + localization (see Critical Decisions 2026-09-11)** |

## Objective

Build a production-ready online multiplayer board game platform.

The foundation phase established the architecture that all future development builds upon: infrastructure, communication, authentication, rooms, and the game engine foundation. That work is **complete**, and the first game (**UNO**) has been implemented end to end on top of it.

The current goal is to implement the next game, **Silver**, on the existing architecture.

The platform is designed to allow implementing games like:

- UNO *(implemented)*
- Silver *(next)*
- Splendor
- Wingspan
- Azul
- Terraforming Mars
- Ticket to Ride
- etc.

without requiring architectural changes.

The codebase should be maintainable for many years.

### Historical note

Early revisions of this document described only the foundation phase ("the goal is NOT to create a playable game yet") and named Splendor as the first planned game. Those statements are historical: UNO was chosen and shipped first (see Critical Decisions 2026-09-05), and Silver is now the next game (see Critical Decisions 2026-09-10). Do not re-implement UNO and do not treat Splendor as the next game.

---

# Development Principles

Always prioritize:

- Simplicity
- Maintainability
- Scalability
- Separation of concerns
- Clean Architecture
- SOLID principles
- Composition over inheritance
- Domain Driven Design where appropriate

Never over-engineer.

Only build abstractions that are actually needed.

---

# Technology Stack

## Backend

- .NET 9 (latest stable)
- ASP.NET Core
- C#
- Entity Framework Core
- PostgreSQL
- Redis
- RabbitMQ
- SignalR
- JWT Authentication
- FluentValidation
- Serilog
- Swagger/OpenAPI

---

# Architecture

Use Microservice Architecture from the beginning.

Initially create the following services:

- Identity Service
- Lobby Service
- Game Service
- API Gateway (simple reverse proxy, can be minimal)

Create a Shared BuildingBlocks project for common functionality.

Each service must be independently deployable.

Use Docker Compose for local development.

---

# Solution Structure

Create the following repository structure.

```text
BoardGamePlatform/

    backend/

        src/

            BuildingBlocks/

                BuildingBlocks.Domain
                BuildingBlocks.Application
                BuildingBlocks.Infrastructure
                BuildingBlocks.Contracts

            Gateway/

            Services/

                Identity/

                    Identity.Api
                    Identity.Application
                    Identity.Domain
                    Identity.Infrastructure

                Lobby/

                    Lobby.Api
                    Lobby.Application
                    Lobby.Domain
                    Lobby.Infrastructure

                Game/

                    Game.Api
                    Game.Application
                    Game.Domain
                    Game.Infrastructure

            GameEngine/

                GameEngine.Core

            Games/

                UNO/        (implemented)
                Silver/     (next game to implement)

tests/

docs/

docker/
```

---

# Service Responsibilities

## Identity Service

Responsible for:

- Registration
- Login
- JWT
- Refresh Token
- User Profile
- Authentication
- Authorization

Nothing else.

---

## Lobby Service

Responsible for:

- Lobby
- Rooms
- Room Players
- Ready State
- Host Management
- Start Game
- Waiting Room

The Lobby Service must never know game rules.

---

## Game Service

Responsible for:

- Creating Game Sessions
- Managing active games
- SignalR communication
- Game lifecycle
- Calling the Game Engine

Game-specific rules belong inside Games/*.

---

# Shared BuildingBlocks

Create reusable infrastructure.

Include:

- Result pattern
- Base Entity
- Integration Events
- Common Exceptions
- Middleware
- Logging
- Authentication helpers
- Shared Contracts

Do NOT include game-specific code.

---

# Communication

## Synchronous

Use REST APIs.

## Real-time

Use SignalR.

## Asynchronous

Use RabbitMQ.

RabbitMQ should be introduced now even if only a few events exist.

Example future events:

- UserRegistered
- RoomCreated
- RoomClosed
- PlayerJoinedRoom
- PlayerLeftRoom
- GameStarted
- GameFinished

---

# Redis

Configure Redis from the beginning.

Use Redis for:

- Distributed Cache
- SignalR Backplane
- Active Game Sessions
- Temporary Room Data

Even if only part of it is initially used.

---

# Database

Each microservice owns its own PostgreSQL database.

No shared database.

Example:

IdentityDb

LobbyDb

GameDb

---

# Authentication

Implement completely.

Features:

- Register
- Login
- Refresh Token
- Logout
- Current User

JWT authentication.

---

# Lobby

Implement completely.

Features:

- Create Room
- Join Room
- Leave Room
- Ready
- Unready
- Kick Player
- Transfer Host
- Start Game

No matchmaking.

No friends.

No chat.

---

# Waiting Room

Implement using SignalR.

Support real-time updates for:

- Player joined
- Player left
- Player ready
- Host changed
- Room closed

---

# Game Session

Separate Room from Game.

Room

↓

GameSession

↓

GameState

This infrastructure is implemented: the Lobby starts a room by creating a `GameSession`, whose serialized `GameState` lives in the Game Service (PostgreSQL + Redis) and is processed exclusively through the game's `IGame` implementation.

---

# Game Engine

Implement ONLY the engine infrastructure.

The Game Engine is responsible for:

- Loading game
- Creating game
- Processing player actions
- Returning updated game state

The Game Engine must NOT know:

- HTTP
- SignalR
- EF Core
- PostgreSQL
- RabbitMQ

It should be pure business logic.

---

# Games

Games are implemented as separate projects under `Games/`, one per game, each referencing only `GameEngine.Core`.

- `Games/UNO/` — implemented end to end (engine, registration, frontend view, localization).
- `Games/Silver/` — the next game to implement; see the dedicated **Silver — Next Game Implementation** section below for the complete rules and architecture specification.

Splendor remains a possible future game; no project for it exists and none should be created now.

---

# SignalR

Create one GameHub.

Responsibilities:

- Connection
- Join Room
- Leave Room
- Broadcast room updates
- Broadcast game updates

The Hub must contain no business logic.

---

# API Gateway

Create a simple gateway.

Responsibilities:

- Routing
- Authentication forwarding
- Reverse proxy

No business logic.

---

# Persistence

Configure EF Core.

Create migrations.

Create databases.

Only create infrastructure.

---

# Logging

Configure Serilog.

Structured logging.

Log:

- Requests
- Authentication
- Room lifecycle
- Game lifecycle
- Exceptions

---

# Validation

Use FluentValidation.

Validation belongs inside Application layer.

Never inside Controllers.

---

# Error Handling

Global Exception Middleware.

Return RFC7807 ProblemDetails.

---

# Docker

Create Dockerfiles for every service.

Create docker-compose.yml containing:

- API Gateway
- Identity Service
- Lobby Service
- Game Service
- PostgreSQL (one instance per service or separate containers, your architectural choice)
- Redis
- RabbitMQ

Running one command should start the complete development environment.

---

# Swagger

Enable Swagger for every service.

---

# XML Documentation

Enable XML documentation generation.

Document every public API.

---

# Out Of Scope

Do NOT implement:

- Splendor rules
- Wingspan
- Azul
- Silver rules beyond the base game (no Silver Bullet / Silver Coin / Silver Dagger decks, no combining decks, no variants or house rules)
- Chat
- Friends
- Matchmaking
- Rankings
- Replay
- Spectators
- AI Players
- Notifications
- Email
- Mobile App
- Admin Panel
- Payment
- Monitoring
- Kubernetes
- Broad unit/integration test suites (the Silver engine verification harness required by the Silver section is the exception; see its Testing expectations)

---

# Deliverables

At the end of this phase the platform must support:

✅ Microservice architecture

✅ Docker Compose

✅ API Gateway

✅ Identity Service

✅ Lobby Service

✅ Game Service

✅ Shared BuildingBlocks

✅ PostgreSQL

✅ Redis

✅ RabbitMQ

✅ SignalR

✅ JWT Authentication

✅ Room Management

✅ Waiting Room

✅ Game Session infrastructure

✅ Generic Game Engine infrastructure

✅ Swagger

✅ Logging

✅ FluentValidation

✅ XML Documentation

The foundation deliverables are complete and validated by the UNO implementation. The next phase — implementing Silver per the dedicated section below — must not require architectural refactoring.

---

# AI Agent Rules

Follow these rules strictly.

1. Never put business logic inside Controllers.

2. Never put business logic inside SignalR Hubs.

3. Never couple the Game Engine to ASP.NET Core.

4. Every service must be independently deployable.

5. Every service owns its own database.

6. Shared code belongs only inside BuildingBlocks.

7. Keep interfaces small.

8. Do not create generic Board, Card, Tile or Resource abstractions.

9. Prefer composition over inheritance.

10. Build the solution incrementally.

11. Ensure the solution compiles successfully after every major implementation step.

12. Do not generate placeholder code or TODO implementations.

13. Write production-quality code.

14. Keep the architecture simple and extensible.

15. Assume this project will eventually support dozens of board games.

16. Document every critical architectural or technical decision in this file under **Critical Decisions** (see below). Do not rely on chat history alone.

17. **Guardrail — process lifecycle:** If you start any background process (services, dev servers, containers, etc.), you are responsible for stopping it before your turn ends, without being asked. Do not leave running processes behind. Do not make the user repeat this.

---

# Project Responsibilities

## BuildingBlocks (Shared Kernel)

### BuildingBlocks.Domain
Core domain primitives shared across all services.
- `Result<T>` / `Result` — outcome of operations (success/failure with errors)
- `Entity<TId>` — base class for domain entities with identity
- `AuditableEntity` — base class adding `CreatedAt`/`UpdatedAt` auditing (see Shared base `AppDbContext` decision)
- Common exceptions: `DomainException`, `ConcurrencyException`, `NotFoundException`
- Server-side localization: `ErrorCatalog` (loads the embedded `Localization/errors.{language}.json` files — one per language, currently `en`/`fa`) and `ErrorCodes` (constants for every code). Coded exceptions (`DomainExceptionBase` → `ConflictException`, `NotFoundException`, `UnauthorizedException`) carry `Code` + `Args`; the `GlobalExceptionHandlerMiddleware` localizes them per request (see localization decision). FluentValidation validators emit `validation.*` codes instead of English texts.

### BuildingBlocks.Application
Application-layer abstractions and behaviors.
- CQRS is implemented via **MediatR**. Requests are MediatR `IRequest` messages.
- `ICommand<TResult>`, `ICommand`, `IQuery<TResult>` — marker interfaces in `BuildingBlocks.Application.CQRS` that compose with MediatR's `IRequest<T>` / `IRequest`. They are not a hand-rolled mediator.
- Handlers are classes deriving from MediatR's `IRequestHandler<TRequest, TResult>` / `IRequestHandler<TRequest>`, implemented per service and registered via MediatR's assembly scanning.
- All requests return `Result<T>` / `Result`.
- Pipeline behaviors are MediatR's `IPipelineBehavior<TRequest, TResult>` mechanism: `ValidationBehavior` (FluentValidation) and `LoggingBehavior` run automatically before handlers. Used for transactions where needed.
- `IUnitOfWork` — transaction boundary abstraction, implemented as a thin `SaveChangesAsync` wrapper over the service's `XxxDbContext` (see Critical Decisions).
- AutoMapper — object mapping via AutoMapper (see AutoMapper decision), used in handlers to map between entities and DTOs. Profiles are auto-discovered via `AddAutoMapper(assemblies)`.
- FluentValidation integration: `IValidator<T>`

### BuildingBlocks.Infrastructure
Reusable infrastructure implementations.
- `EfCoreUnitOfWork` — EF Core implementation of `IUnitOfWork`
- `Outbox` pattern — `IOutbox`, `OutboxMessage`, background processor for reliable integration events
- `RedisCacheService` — `IDistributedCache` wrapper with typed methods
- `RabbitMqPublisher` — `IIntegrationEventPublisher` for RabbitMQ
- `JwtTokenService` — JWT creation/validation
- `CurrentUserService` — `ICurrentUser` for accessing authenticated user context
- Serilog configuration helpers
- Health checks for PostgreSQL, Redis, RabbitMQ
- EF Core conventions: snake_case, soft delete, auditing interceptors

### BuildingBlocks.Contracts
Shared DTOs and integration event definitions (no logic).
- Integration events: `UserRegistered`, `RoomCreated`, `RoomClosed`, `PlayerJoinedRoom`, `PlayerLeftRoom`, `GameStarted`, `GameFinished`
- Common DTOs: `PagedResult<T>`, `ErrorResponse` (RFC7807), `ApiResponse<T>`
- SignalR contract interfaces: `IGameHubClient`, `ILobbyHubClient` (for type-safe client calls)

---

## Gateway

### Gateway (API Gateway / Reverse Proxy)
- **YARP** reverse proxy routing requests to downstream services
- Routes:
  - `/api/identity/**` → Identity.Api
  - `/api/lobby/**` → Lobby.Api
  - `/api/game/**` → Game.Api
  - `/hubs/**` → Game.Api (SignalR)
- Forwards `Authorization` header to downstream services
- Rate limiting (optional, via YARP)
- Request/response logging
- No business logic, no database

---

## Identity Service

### Identity.Domain
- `User` entity: `Id`, `Email`, `PasswordHash`, `DisplayName`, `CreatedAt`, `LastLoginAt`, `IsActive`
- `RefreshToken` entity: `Token`, `ExpiresAt`, `RevokedAt`, `ReplacedByToken`

### Identity.Application
- Commands: `RegisterCommand`, `LoginCommand`, `RefreshTokenCommand`, `LogoutCommand`, `ChangePasswordCommand`, `UpdateProfileCommand`
- Queries: `GetCurrentUserQuery`, `GetUserByIdQuery`
- Validators for all commands/queries
- Handlers orchestrate the domain, return `Result<T>`
- JWT claims construction

### Identity.Infrastructure
- EF Core `IdentityDbContext` with `User` and `RefreshToken` entities
- Direct DbContext (`IdentityDbContext`) access for all data operations
- `PasswordHasher` (BCrypt/Argon2)
- `JwtTokenService` implementation (signing, validation, refresh token storage)
- PostgreSQL migrations
- Outbox publisher for `UserRegistered` integration event

### Identity.Api
- REST endpoints:
  - `POST /api/identity/register`
  - `POST /api/identity/login`
  - `POST /api/identity/refresh`
  - `POST /api/identity/logout`
  - `GET /api/identity/me`
  - `PUT /api/identity/me`
  - `POST /api/identity/change-password`
- Global exception middleware → RFC7807 ProblemDetails
- Swagger/OpenAPI with XML docs
- JWT authentication scheme

---

## Lobby Service

### Lobby.Domain
- `Room` entity: `Id`, `Name`, `GameType`, `MaxPlayers`, `IsPrivate`, `Status` (Waiting/Started/Closed), `HostId`, `CreatedAt`
- `RoomPlayer` entity: `Id`, `RoomId`, `UserId`, `DisplayName`, `IsReady`, `JoinedAt`, `ConnectionId` (SignalR)
- `RoomSettings` entity: game-specific settings (serialized JSON)
- Room rules: max players, host transfer logic, ready state validation

### Lobby.Application
- Commands: `CreateRoomCommand`, `JoinRoomCommand`, `LeaveRoomCommand`, `SetReadyCommand`, `KickPlayerCommand`, `TransferHostCommand`, `StartGameCommand`, `CloseRoomCommand`
- Queries: `GetRoomQuery`, `GetRoomListQuery`, `GetMyRoomsQuery`
- Validators for all commands/queries
- Handlers enforce lobby rules
- Publishes integration events via outbox: `RoomCreated`, `PlayerJoinedRoom`, `PlayerLeftRoom`, `GameStarted`, `RoomClosed`

### Lobby.Infrastructure
- EF Core `LobbyDbContext` with `Room`, `RoomPlayer` entities
- Redis cache for active room list (fast reads)
- SignalR `LobbyHub` — real-time room updates (joined, left, ready, host changed, closed)
- PostgreSQL migrations
- Outbox processor

### Lobby.Api
- REST endpoints:
  - `POST /api/lobby/rooms`
  - `GET /api/lobby/rooms`
  - `GET /api/lobby/rooms/{id}`
  - `POST /api/lobby/rooms/{id}/join`
  - `POST /api/lobby/rooms/{id}/leave`
  - `POST /api/lobby/rooms/{id}/ready`
  - `POST /api/lobby/rooms/{id}/unready`
  - `POST /api/lobby/rooms/{id}/kick/{playerId}`
  - `POST /api/lobby/rooms/{id}/transfer-host/{playerId}`
  - `POST /api/lobby/rooms/{id}/start`
  - `POST /api/lobby/rooms/{id}/close`
- SignalR hub endpoint: `/hubs/lobby`
- Swagger/OpenAPI with XML docs
- JWT authentication, authorization policies

---

## Game Service

### Game.Domain
- `GameSession` entity: `Id`, `RoomId`, `GameType`, `Status` (Active/Paused/Finished), `CurrentStateJson` (serialized), `CreatedAt`, `StartedAt`, `FinishedAt`
- `GamePlayer` entity: `Id`, `GameSessionId`, `UserId`, `DisplayName`, `Position`, `IsConnected`, `ConnectionId` (SignalR)
- `GameActionLog` — persisted player action data: `PlayerId`, `ActionType`, `Payload` (JSON), `Timestamp`, `SequenceNumber`
- Game logic is reached through `IGameEngineProvider` → `IGame` (GameEngine.Core); the domain holds no rules

### Game.Application
- Commands: `CreateGameSessionCommand`, `ProcessGameActionCommand`, `ReconnectPlayerCommand`, `PauseGameCommand`, `ResumeGameCommand`
- Queries: `GetGameSessionQuery`, `GetGameStateQuery`, `GetPlayerGameSessionsQuery`
- Validators
- Handlers orchestrate: resolve the engine via `IGameEngineProvider` by `GameType`, load state from `CurrentStateJson`/Redis, process action via engine, persist state, publish events
- `GameEngineProvider` resolves the DI-registered `IGame` implementation matching the session's game type

### Game.Infrastructure
- EF Core `GameDbContext` with `GameSession`, `GamePlayer`, `GameActionLog` entities
- Redis for active game session state (low-latency reads/writes)
- SignalR `GameHub` — real-time game updates (state changes, player actions, connection status)
- `GameRealTimeNotifier` — translates `GameStateChanged` MediatR notifications into typed `IGameHubClient` calls
- `TurnTimeoutService` — 1s background sweep dispatching `TurnTimeout` / `GameTimeExpired` system actions for expired `NextActionDeadlineUtc` / `GameEndsAtUtc` deadlines
- `IGame` implementations registered via DI (see Critical Decisions)
- PostgreSQL migrations
- Outbox processor for `GameStarted`, `GameFinished` integration events

### Game.Api
- REST endpoints:
  - `GET /api/game/games` (catalog of DI-registered games: `GameType`, `MinPlayers`, `MaxPlayers`)
  - `POST /api/game/sessions` (internal, called by Lobby when starting game)
  - `GET /api/game/sessions/{id}`
  - `GET /api/game/sessions/mine`
  - `GET /api/game/sessions/{id}/state`
  - `POST /api/game/sessions/{id}/actions`
  - `POST /api/game/sessions/{id}/reconnect`
  - `POST /api/game/sessions/{id}/pause`
  - `POST /api/game/sessions/{id}/resume`
- SignalR hub endpoint: `/hubs/game`
- Swagger/OpenAPI with XML docs
- JWT authentication

---

## Game Engine

### GameEngine.Core
**Pure C# library — zero dependencies on ASP.NET Core, EF Core, PostgreSQL, Redis, RabbitMQ, SignalR.**

- `IGame` interface:
  - `GameType : string`, `MinPlayers : int`, `MaxPlayers : int`
  - `CreateGame(GameOptions options) : GameState`
  - `ProcessAction(GameState state, GameAction action) : GameResult`
  - `GetValidActions(GameState state, PlayerId playerId) : IReadOnlyList<GameAction>`
  - `IsGameOver(GameState state) : bool`
  - `GetWinner(GameState state) : PlayerId?`
- `IGame` implementations registered via DI (see Critical Decisions)
- `GameOptions` — game-type-specific configuration (`GameType`, `Players`, `Settings` JSON)
- `GameState` — serializable state root: `SessionId`, `GameType`, `Players`, `CurrentPlayerIndex`, `IsOver`, `Winner`, `Version`, `NextActionDeadlineUtc`, `GameEndsAtUtc`, plus a game-specific `Data` dictionary; `TryGetString` handles the JSON-string/`JsonElement` round-trip
- `GameAction` — player action (`PlayerId`, `ActionType`, `Payload` JSON, `Timestamp`, `SequenceNumber`)
- `GameResult` — `{ NewState, Events[], IsValid, Error?, ErrorCode?, ErrorArgs[], GameEnded }`; error codes are part of the platform localization contract
- `GameEvent` — things that happened during action processing (for UI/notifications)
- Base classes: `GameBase`, `TurnBasedGame`, `RealTimeGame` (optional helpers)
- **Player-specific state views:** for games with hidden information the engine may additionally implement an optional player-view capability (see the Silver section) so the Game Service can project per-viewer state without Silver-specific code. This is the only anticipated engine-contract extension; do not build a generic visibility framework.

---

## Games

### UNO *(implemented)*
- Implements `IGame` from GameEngine.Core
- Contains ONLY UNO rules: deck (108 cards), discard pile, player hands, actions (play card, draw card, call UNO, challenge Wild Draw 4)
- No HTTP, no SignalR, no EF Core, no database
- Registered in DI as an `IGame` implementation (see Critical Decisions)

### Silver *(implemented — engine + frontend view)*
- Implements `IGame` and `IPlayerViewGame` from GameEngine.Core exactly as UNO implements `IGame`; the complete rules, action model, hidden-information requirements, and integration checklist are specified in the dedicated **Silver — Next Game Implementation** section below
- `Games/Silver` references only `GameEngine.Core`, registered via one DI line next to UNO (see Critical Decisions 2026-09-11)
- No Silver-specific code leaks into the Game Service, Lobby, BuildingBlocks, or the generic engine model

---

# Silver — Next Game Implementation

> **Status: fully implemented (2026-09-11 — see Critical Decisions): engine + Game Service projection + React view + EN/FA localization.**
>
> **Amended 2026-09-12 (see the re-audit Critical Decision at the end of this file):** the implementation was re-checked line-by-line against the exact-rules specification supplied by the project owner and several rule details below are now **superseded**: character naming (2 Enchanter, 3 Guard, 4 Trickster, 7 Apprentice Seer, 8 Seer, 9 Beholder), the Squire is a passive per-turn reveal with a takeable display area (not an activated once-per-turn ability), Trickster draws one extra per face-up copy and returns the rest to the **top** of the deck in drawn order, the Guard is an explicit card→card protection with Move/Remove actions, Robber may steal face-up cards, the Witch resolves in two steps (peek, then exchange-or-decline), the Master supports multi-card replacement and a decline, the Revealer's *target* chooses which of their cards flips, a matching set may contain at most one Doppelgänger (two match only each other), a village ending the round with exactly two Doppelgängers scores 13, setup removes 5×(4−players) cards from the game so deck+discard is always 32, the Amulet is assigned to the starting player in round 1, "calling for a vote" is called **census** in copy, and replacement-card orientation follows the source (owner clarification 2026-09-12, superseding the earlier literal reading of §9): a card drawn **from the deck** enters the village **face down, known only to the drawer** — including the Witch's peeked card; cards from the **discard pile or the Squire display** were public and enter **face up**. The optional 100-point scoring mode and Kamikaze rule from the owner's specification are explicitly **out of scope** (owner directive 2026-09-12). Where this section and the 2026-09-12 Critical Decisions disagree, the newest Critical Decision wins.

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
6. Verification harness per Testing expectations; record results and any resolved interpretation decisions under Critical Decisions.

---

# Critical Decisions

Record significant choices here so they persist across sessions and agents.

Format for each entry:

- **Date** — short title
- **Context** — what problem or choice prompted the decision
- **Decision** — what was chosen
- **Rationale** — why, and what alternatives were rejected (if any)

<!-- Add new decisions below this line -->

- **2026-07-03** — Critical decisions live in AGENTS.md  - **Context:** Architectural and technical choices need to survive beyond a single chat session.
  - **Decision:** Every critical decision must be recorded in the **Critical Decisions** section of `AGENTS.md`.
  - **Rationale:** Keeps the spec as the single source of truth for humans and AI agents working on the project.

- **2026-07-03** — Frontend: React + Vite
  - **Context:** Need a frontend technology for the board game platform.
  - **Decision:** React with Vite as the build tool.
  - **Rationale:** Fast dev server, modern tooling, strong ecosystem for real-time UI with SignalR.

- **2026-07-04** — No DDD, simplified domain model
  - **Context:** AGENTS.md originally specified full DDD (aggregates, value objects, domain events). For Phase 1 the team preferred a leaner approach.
  - **Decision:** Drop DDD constructs. Entities are plain POCOs inheriting a single `Entity` / `AuditableEntity` base. No aggregates, value objects, or domain events.
  - **Rationale:** Faster to build, less ceremony, sufficient for the platform foundation. DDD patterns can be introduced per-service later if a domain genuinely needs them.

- **2026-07-04** — No Repository pattern; direct DbContext access
  - **Context:** AGENTS.md originally specified repository interfaces in every service's Domain layer.
  - **Decision:** Services access the database directly through their `XxxDbContext` (registered via DI). No `IRepository<T>` abstractions.
  - **Rationale:** EF Core already provides the Unit-of-Work + repository abstraction (`DbContext` + `DbSet`). A custom repository layer would duplicate it without adding value at this stage.

- **2026-07-04** — CQRS via MediatR
  - **Context:** Need a clean separation between commands (writes) and queries (reads).
  - **Decision:** Use **MediatR** as the in-process mediator. Marker interfaces `ICommand<T>`, `ICommand`, `IQuery<T>` live in `BuildingBlocks.Application.CQRS`. All requests return `Result<T>` / `Result`. Pipeline behaviors `ValidationBehavior` and `LoggingBehavior` run automatically.
  - **Rationale:** Industry standard, minimal boilerplate, excellent fit for Clean Architecture handlers. Chosen over hand-rolled dispatcher and over full event-sourcing CQRS.

- **2026-07-04** — AutoMapper for object mapping
  - **Context:** Handlers need to map between entities and DTOs.
  - **Decision:** Use **AutoMapper** with profiles per service. Profiles are auto-discovered via `AddAutoMapper(assemblies)` in `AddAppInfrastructure`.
  - **Rationale:** Chosen over Mapster (team familiarity) and manual mapping (too much boilerplate across many handlers).

- **2026-07-04** — Shared base `AppDbContext` in BuildingBlocks
  - **Context:** Soft-delete, auditing, and query filters are needed identically across all services.
  - **Decision:** `BuildingBlocks.Infrastructure.Persistence.AppDbContext` is an abstract base DbContext. Each service's `XxxDbContext` inherits it. Auditing (`CreatedAt`/`UpdatedAt`) and soft-delete (`IsDeleted`/`DeletedAt` + global query filter) are implemented once in the base.
  - **Rationale:** Single source of truth for cross-cutting persistence concerns, no duplication across Identity/Lobby/Game. Alternatives (interceptors only, or per-service duplication) were rejected as more error-prone.

- **2026-07-04** — Soft delete is mandatory
  - **Context:** Records (users, rooms, game sessions) must be recoverable / retained for audit.
  - **Decision:** All entities inheriting `AuditableEntity` are soft-deleted. `DbContext.SaveChangesAsync` intercepts `EntityState.Deleted` and converts it to an update that sets `IsDeleted = true` and `DeletedAt = now`. A global EF query filter automatically excludes deleted rows from queries.
  - **Rationale:** Enforces an auditable, non-destructive data model platform-wide without each service reimplementing it.

- **2026-09-01** — `IUnitOfWork` kept as a thin `SaveChangesAsync` wrapper
  - **Context:** With the decision to drop the repository pattern, the question arose whether `IUnitOfWork` / `EfCoreUnitOfWork` should remain in the codebase.
  - **Decision:** Keep `IUnitOfWork` as a thin transaction-boundary abstraction implemented by `EfCoreUnitOfWork`, which simply forwards to `XxxDbContext.SaveChangesAsync`. It is not a repository wrapper or persistence facade.
  - **Rationale:** Provides a stable, minimal transaction boundary for handlers while avoiding the redundant repository layer the "No Repository pattern" decision removed. Dropped the more elaborate transaction-manager designs as unnecessary for Phase 1.

- **2026-09-01** — `IGame` implementations registered via DI, no plugin loading yet
  - **Context:** The original spec described a `GameEngineFactory` that loads game assemblies (Splendor, etc.) from `Games/` plugins at runtime.
  - **Decision:** For Phase 1, defer runtime `AssemblyLoadContext` plugin loading. `IGame` implementations are registered directly via the DI container, with a single `IGame` interface used to model games.
  - **Rationale:** Runtime plugin loading adds complexity (hot-swap, sandboxing, versioning) with no concrete need until there are multiple games. DI registration is simpler and sufficient now; plugin loading can be introduced later when a real requirement emerges.

- **2026-09-01** — Consistency pass reconciling spec with Critical Decisions
  - **Context:** The original spec sections and the Critical Decisions log had diverged, leaving contradictions for any agent reading top to bottom.
  - **Decision:** Reconcile the document for self-consistency by resolving five contradictions:
    1. Removed `ValueObject`, `DomainEvent`, `IDomainEventDispatcher`, and all repository interfaces from `BuildingBlocks.Domain`, `Identity.Domain`, `Lobby.Domain`, and `Game.Domain` (No DDD + No Repository pattern decisions).
    2. Rewrote `BuildingBlocks.Application` to reflect MediatR: `ICommand<T>`, `ICommand`, `IQuery<T>` are marker interfaces composing with `IRequest`/`IRequestHandler`, and pipeline behaviors are MediatR's `IPipelineBehavior` mechanism (ValidationBehavior, LoggingBehavior).
    3. Updated the mapping reference to simply say AutoMapper (registered via `AddAutoMapper`), removing the Mapster/AutoMapper wrapper framing.
    4. Explicitly decided that `IUnitOfWork` / `EfCoreUnitOfWork` is kept as a thin `SaveChangesAsync` wrapper, documented in both the BuildingBlocks sections and this decisions log.
    5. Replaced `GameEngineFactory` runtime assembly loading with DI-based registration of `IGame` implementations, with a new decision deferring plugin loading.
  - **Rationale:** Ensures the document is internally consistent and actionable for any agent or developer reading it in order. No scope, stack, or service boundary changes were made.

- **2026-09-05** — First game implementation: UNO
  - **Context:** Need to choose the first game to implement in Phase 2. The original spec mentioned Splendor, but UNO was chosen instead.
  - **Decision:** Implement UNO (base game, no expansions) as the first game instead of Splendor.
  - **Rationale:** UNO has simpler rules (no complex card interactions like Splendor's nobles/tokens), well-known mechanics, and is easier to validate the platform's game engine infrastructure. The turn-based structure maps cleanly to the `IGame` interface. Splendor will follow as the second game. *(Superseded 2026-09-10: Silver is the second game — see the 2026-09-10 decision below.)*

- **2026-09-05** — UNO `Card` is a readonly struct; display names persist in `GameState.Data`
  - **Context:** During the UNO implementation, `Card` was modeled as a `readonly record struct`. Code used `(Card?)null` for a nullable card, and attempted to access `topCardBeforeWild!.Color`. The `!` null-forgiving operator suppresses nullable *warnings* but does NOT unwrap `Nullable<T>` for a struct, producing `CS1061: 'Card?' does not contain a definition for 'Color'`. Separately, `UnoGameState.PlayerNames`/`PlayerIds` are `[JsonIgnore]` (never serialized), so the display-name map was silently lost after the first action.
  - **Decision:** (1) For nullable struct members, access via `.Value` (or the null-checked value) instead of `!`. (2) `PlayerNames` is persisted beside the serialized `UnoState` as the `PlayerNames` key in `GameState.Data`; `ProcessAction` rehydrates it into `UnoGameState` before processing. `PlayerIds` is always re-derived from `state.Players`.
  - **Rationale:** `.Value` on `Nullable<T>` is the only correct way to access members of a nullable struct; `!` only silences analyzer warnings. Persisting names in `GameState.Data` keeps display names accurate across actions while keeping `UnoGameState`'s runtime-only dictionaries unserialized. Avoided serializing `PlayerNames` inside `UnoState` itself to keep the persisted state free of private user-id → display-name mappings.

- **2026-09-06** — Explicitly track new child entities on DB-loaded tracked principals
  - **Context:** `POST /api/lobby/rooms/{id}/join` failed with `DbUpdateConcurrencyException` ("expected to affect 1 row(s), but actually affected 0"). The new `RoomPlayer` was being saved as an `UPDATE ... WHERE id=@newGuid` (0 rows) instead of an `INSERT`; `[JOINDIAG]` tracing showed EF had tracked it as `EntityState.Modified` right after `Room.AddPlayer(...)` added it to the private backing-field collection `_players` (`IReadOnlyList<RoomPlayer> Players => _players;`) of a Room that had been loaded from the DB with `.Include(r => r.Players)`. The same `AddPlayer` path during `Room.Create` inserts correctly (the Room there is not yet tracked), so the bug only appears when mutating the collection of a **tracked, DB-materialized** principal.
  - **Decision:** `Room.AddPlayer` now returns the created `RoomPlayer`, and `JoinRoomCommandHandler` explicitly registers it with `_dbContext.RoomPlayers.Add(membership)` before `SaveChangesAsync`. `Room.Create` continues to ignore the return value.
  - **Rationale:** Forceful, explicit `DbContext.Add` guarantees `EntityState.Added` regardless of what EF's relationship fixup inferred, making the intent unambiguous and immune to EF version behavior changes. Do not rely on collection-fixup alone to infer `Added` for new children of DB-loaded tracked principals; register entities explicitly when their lifecycle transitions from "new" to "persisted".

- **2026-09-06** — `GameState.Data` string values become `JsonElement` after a round-trip
  - **Context:** After creating and starting a UNO session, the first player action (`DrawCard`) failed with `ConflictException "Invalid game state: missing UNO state"`. The `game_sessions.current_state_json` row had a valid `Data.UnoState` JSON string. An isolated System.Text.Json test proved the cause: `GameState.Data` is a `Dictionary<string, object?>`; on `Deserialize<GameState>`, a value that is a JSON string is materialized as a `JsonElement` with `ValueKind=String`, NOT a CLR `string`. So the original checks (`state.Data.TryGetValue("UnoState", out var v) && v is string`) always failed after any persist/load cycle.
  - **Decision:** Added `GameState.TryGetString(string key, out string? value)` to `GameEngine.Core`, which accepts both a CLR `string` and a `JsonElement` (`GetString()` for string-kind elements, `GetRawText()` otherwise). `UNOGame` now reads `UnoState` and `PlayerNames` exclusively via `TryGetString`, and re-canonicalizes `PlayerNames` on write. Verified with a scratch harness: `CreateGame → ToJson → FromJson → ProcessAction(DrawCard)` succeeds repeatedly (including on the new state after an action).
  - **Rationale:** The engine model must own the serialization semantics of its own `Data` payloads. A custom `JsonConverter` or typed properties for every game payload would be over-engineering; one tolerant helper fixes any game that stores JSON strings in `Data`. Keeps JSON-string storage (per the 2026-09-05 decision) instead of embedding raw objects, which would also deserialize back as `JsonElement`.

- **2026-09-06** — `POST /api/game/sessions/{id}/actions` returns a `GameState`, not a `GameResult`
  - **Context:** After the missing-UNO-state fix made actions succeed, the frontend crashed with `Cannot read properties of undefined (reading 'length')`. `GameController.Action` returns `Ok(result.Value)` where `result.Value` is `GameState` (the handler returns `Result<GameState>`), but the frontend typed the response as `GameResult` (`{ newState, events, gameEnded }`) and called `result.events.length`, which was undefined.
  - **Decision:** The action endpoint's response contract is a full `GameState` — the updated state is the response body itself, events are delivered via SignalR, and `isOver` is the game-end signal. Frontend: `gameApi.processAction` is typed `GameState`, the mutation's `onSuccess` does `setState(state)` and invalidates session queries when `state.isOver`. Removed the dead `GameResult` interface.
  - **Rationale:** Single canonical state shape across `getState`, `actions`, and SignalR broadcasts keeps the frontend mapping trivial and avoids wrapper DTOs. Rejected introducing a `GameResult`-shaped response on the backend because the broadcast already delivers the same state and events to the group.

- **2026-09-06** — Turn timers: `NextActionDeadlineUtc` + service-side `TurnTimeoutService`
  - **Context:** Need per-game turn timers with time banking, overshoot penalties, auto-skip, and AFK kick/lose. The Game Engine must stay pure (no HTTP/SignalR/EF/Redis), so the mechanism that *fires* a timeout must live in the Game Service.
  - **Decision:** `GameState.NextActionDeadlineUtc` (`DateTime?`) is a game-agnostic root property set by the engine whenever a turn starts. UNO tracks per-game config (`Timer` key in settings: `BaseTurnSeconds`/`MaxBankSeconds`/`MaxOverrunSeconds`/`MaxAfkTurns`, defaults 30/120/15/3) and per-player `UnoPlayerTimer` (bank, deferred penalty, consecutive timeouts) inside `UnoState`. On a finished turn the engine banks unused time (capped at the bank max), charges overruns against the bank first then a capped deferred penalty, and reports the next deadline. A new `TurnTimeout` action (rejected unless the deadline actually passed) charges the standard overrun, skips the turn, counts a consecutive timeout, and at `MaxAfkTurns` eliminates the player (2 players → game over with the survivor winning; >2 → removed, game continues; `AdvancePlayer` skips eliminated seats). The Game service hosts `TurnTimeoutService` (`Game.Infrastructure`, polled every 1s), which dispatches `TurnTimeout` for expired deadlines through the same engine/persist/cache/broadcast path as player actions.
  - **Rationale:** Keeps the engine pure while centralizing time enforcement in one place; the engine remains the single source of truth for *what* a timeout does per game. Verified with a scratch harness (create/round-trip/deadline/bank/penalty/skip/elimination) — 20/20 checks passed.

- **2026-09-06** — Single-tab enforcement via `SessionTakenOver`
  - **Context:** A player opening the same game in a second tab created two SignalR connections for one seat; both tabs would send actions and every client (including the orphaned tab) kept receiving broadcasts.
  - **Decision:** `ReconnectPlayerCommandHandler` captures the seat's previous `ConnectionId` before `SetConnection`, then publishes a `GameChangeType.PlayerTakenOver` notification carrying it. `GameRealTimeNotifier` sends the new `IGameHubClient.SessionTakenOver(gameSessionId, playerId)` to only that superseded connection. The frontend subscribes, marks the store `takenOver`, stops the hub (disabling auto-reconnect so the old tab can't re-join and kick the new one), and navigates to `/lobby`. (2026-09-07 addition) Closed tabs can no longer strand a game: the Lobby page lists the user's `Active` sessions (`GET /api/game/sessions/mine`, polled every 15s) in a "Your active games" section with a Rejoin button that navigates to `/game/{id}` — the existing `JoinSession` flow re-registers the seat and supersedes the dead connection. Also added a gameHub `onreconnected` handler that re-invokes `joinSession` for the current session after SignalR auto-reconnect, restoring lost group membership (and re-registering the new connection id on the seat).
  - **Rationale:** Backend stays stateless about tabs — one new connection replaces the prior one and the platform informs the loser. Rejected a Redis connection map (no cross-instance need yet); note this relies on a single Game.Api instance, so a Redis-backed seat map should be added if the Game service is ever scaled out.

- **2026-09-06** — Lobby abandoned-room cleanup
  - **Context:** Rooms whose browser tab was closed without leaving (or whose last player quit) would sit in `Waiting` forever; `Closed` rooms accumulated.
  - **Decision:** `AbandonedRoomCleanupService` (`Lobby.Infrastructure`, config `Lobby:RoomCleanup`) closes `Waiting` rooms older than `AbandonedRoomMinutes` (default 60), broadcasting `RoomClosed` like a normal close, and soft-deletes `Closed` rooms older than `ClosedRoomRetentionMinutes` (default 1440). Frontend `RoomPage` leaves the hub and redirects to `/lobby` when it sees `status == 'Closed'`.
  - **Rationale:** Age-based because the `RoomPlayer.ConnectionId` is not reliably cleared on disconnect (LobbyHub has no `OnDisconnectedAsync`). Keeps the Lobby ignorant of game rules while still preventing dead rooms from leaking indefinitely.

- **2026-09-06** — `Room.GameSessionId` persisted; StartGame response carries it
  - **Context:** Bug: after clicking Start, the room "disappeared". The room list only shows `Waiting` rooms, and the only carrier of the new game session id was the `GameStarted` SignalR broadcast. If a client missed that event (drop, race, reload), `currentGameSessionId` stayed null, the redirect to `/game/{id}` never fired, and the host — who has no manual "Go to Game" control — was stranded on a `Started` room that no longer appears anywhere.
  - **Decision:** (1) `Room` stores `GameSessionId` (set via `Room.Start(gameSessionId)`); EF migration `AddRoomGameSessionId` adds `rooms.game_session_id`. (2) `LobbyRoomDto.GameSessionId` is populated automatically by AutoMapper on every read path (`GetRoom`, `GetRoomList`, `GetMyRooms`, StartGame response). (3) Frontend: the StartGame REST response seeds `currentGameSessionId` immediately (host navigates without waiting for SignalR); `RoomPage` also seeds it from a fetched `Started` room (reload/rejoin fallback); a "Go to Game" button is shown to *all* players (was guest-only) when the room is `Started`.
  - **Rationale:** The SignalR broadcast alone is a lossy single point of failure for the lobby→game transition. Persisting the session id on the room makes every recovery path (REST response, room refetch, reload) deterministic while keeping the Lobby unaware of game rules (it stores an opaque id). The signal remains for real-time push, not as the only transport.

- **2026-09-06** — Seat takeover must be idempotent for the same connection
  - **Context:** Bug: when the host started a game, guests were kicked back to `/lobby` moments after the game page rendered, and the Started room had vanished from the lobby list (the list only returns `Waiting` rooms), leaving no way back. Cause: React `StrictMode` double-invokes `GamePage`'s mount effect, so `JoinSession` was invoked **twice on the same SignalR connection**. `ReconnectPlayerCommandHandler` treated *any* stored `seat.ConnectionId` as a superseded tab — including the very same connection id — so the second `ReconnectPlayer` published `PlayerTakenOver(connectionId=X)` **to connection X itself**. The tab received its own `SessionTakenOver`, set `takenOver`, and navigated to `/lobby`. The host survived only by timing luck (the frontend handler ignores the event while `currentSession` is still null).
  - **Decision:** (1) Backend: `ReconnectPlayerCommandHandler` only supersedes when the stored connection id **differs** from the incoming one (`existing != request.ConnectionId`); a same-connection re-join is a no-op for takeover. (2) Frontend: `gameHub.joinSession` is idempotent — it tracks the `joinedConnectionId` per session and skips the `JoinSession` invoke when the current connection already joined that session (StrictMode double-mount / effect re-runs). After a SignalR auto-reconnect the connection id changes, so a later `joinSession` correctly re-invokes.
  - **Rationale:** Hub method invocations are not guaranteed to be exactly-once from the client's perspective (double-mounted effects, retries); takeover semantics must therefore key on connection *identity*, not mere existence of a stored connection id. Rejected disabling React StrictMode — it exposes real idempotency gaps like this one.

- **2026-09-07** — Turn bank capped at 120s total per turn; game time limit with fewest-cards finish
  - **Context:** (1) Time banking let a turn's allowance grow to `BaseTurnSeconds + MaxBankSeconds` (30 + 120 = 150s), exceeding the intended ceiling. (2) UNO games could run indefinitely; a hard finish with a fair outcome was needed. (3) The draw pile could empty late in a game.
  - **Decision:** (1) `MaxBankSeconds` now caps the **total turn allowance**: `MaxTurnSeconds = max(floor, min(MaxBankSeconds, base + bank − penalty))`, and the bank's effective capacity is `MaxBankSeconds − BaseTurnSeconds` (90s with defaults). Formula per spec: next turn = `min(120, saved + bank + 30)`. (2) `GameState.GameEndsAtUtc` is a new game-agnostic root deadline (mirrors `NextActionDeadlineUtc`); UNO sets it at creation (`Timer.TotalGameTimeMinutes`, default 60). `TurnTimeoutService` sweeps it and dispatches a `GameTimeExpired` system action; the engine rejects it before the limit, then force-finishes: single fewest-cards player wins, a shared minimum is a **draw** (`Winner = null`, `IsOver = true`). (3) Deck reshuffle already existed (`Deck.ReshuffleDiscard`, keeping the top card); hardened by ignoring the client-sent `DrawCard` count (server derives `PendingDrawCount > 0 ? pending : 1`) — closes a forged-count cheat.
  - **Rationale:** Capping the allowance (not just the bank) matches the requested formula exactly; the deadline stays on the state root so the service remains game-agnostic and the engine owns outcome rules (same pattern as turn timeouts). Verified with a 20-check scratch harness (bank saturation, allowance ceiling, expiry winner/draw, pre-limit rejection, reshuffle counts, forged-count rejection, JSON round-trips).

- **2026-09-07** — Draw debts survive turn skips and accumulate; ownership-tracked penalties
  - **Context:** With the timeout system, a player hit with +2/+4 could dodge the penalty entirely by timing out (the skip previously auto-drew it), and the "can't play while pending" guard wrongly blocked EVERY player — including the penalizer on their next turn. Requested semantics: the debt survives the skip, the debtor still owes it on their next turn and cannot play until it is accepted, penalties from later rounds accumulate (+2 then +4 → draw 6), and such merged debts cannot be challenged.
  - **Decision:** New `UnoGameState.PendingDrawTargetIndex` records who owes the debt (set after `AdvancePlayer` when a Draw Two/WDF is played; also for the initial-card Draw Two). The play/draw/challenge/accept guards now apply only when the target is the current player (`null` = legacy state, conservatively treated as "current player owes"). `ProcessTurnTimeout` no longer auto-draws the debt — it survives the skip and keeps its target; if the debtor is AFK-eliminated, a challenge-offender debt is paid by the offender immediately, otherwise the debt is dropped. `ProcessAcceptDraw` enforces ownership, accepts the full accumulated amount, and forfeits the turn. New `PendingDrawChallengeable` flag is true only when the debt is exactly a freshly played Wild Draw Four's 4 cards (a WDF stacked onto existing debt sets it false, so merged debts cannot be challenged); challenge offered/rejected accordingly. Frontend: `iOweDraw` (target == me or null) gates the hand, draw/pass buttons, and the pending banner; other players play normally while someone's debt is outstanding.
  - **Rationale:** Timeout-skip must never erase a penalty, and the penalizer must be able to keep playing (their turn is not the debtor's). Ownership tracking is the minimal way to scope the guards; the challengeable flag prevents challenging a merged debt where 2 of the 4 cards came from an unrelated +2. Verified with a 25-check scratch harness reproducing the exact reported scenario (+2 → skip → +4 → skip → 6 owed, no challenge, accept clears and forfeits) plus the pure-+4 control (challengeable).

- **2026-09-08** — Bilingual UI (EN/FA) with RTL support
  - **Context:** The platform needed to serve English and Farsi users with the option to add more languages later. Farsi requires right-to-left layout mirroring across every page.
  - **Decision:** Custom dependency-free i18n under `frontend/src/i18n`: `I18nProvider` (context) exposes `{ lang, dir, setLanguage, t(key, params), d }`. Dictionaries are typed objects in `locales/en.ts` (reference) and `locales/fa.ts`; `fa: Dict` makes a missing translation a compile error. `t` resolves dot-paths with `{param}` interpolation and falls back to English. Selection persists in `localStorage` (`bgp.lang`); the provider sets `<html lang dir>` and a `lang-fa` class on change. RTL is achieved by (1) `dir="rtl"` flipping all flex/grid layouts, (2) Tailwind logical utilities (`ms-`, `me-`, `start-`, `end-`, `text-start`) instead of physical ones in the markup, (3) `marginInlineStart` for the dynamic hand-fan overlap, and (4) the Vazirmatn webfont applied via `html.lang-fa` (with LTR-isolated `.font-mono`/`.tabular-nums` fragments for codes/numbers). A `LanguageSwitcher` sits in every page header. Per-game copy (descriptions, rules, action cards) lives in the dictionaries under `games.<gameType>` and is surfaced through the `useGameInfo` hook — adding a third language is one new locale file plus one `LANGUAGES` entry; adding a game's copy is one dictionary section.
  - **Rationale:** A typed dictionary avoids a runtime i18n dependency while giving compile-time coverage of missing keys; Tailwind's built-in logical utilities plus the `dir` attribute cover mirroring without parallel stylesheets. Backend-generated strings (game event log, server error messages) remain English — translating them would require server-side i18n and is deferred.
  - **(2026-09-08 addition — backend strings localized without server i18n)** The engine now emits event-log entries as JSON envelopes `{"c":"code","d":{params}}` (`UnoEvent.Build`, `Games/UNO`); the client's `formatEvent` renders them via the `events.*` dictionary (color params → localized color names, card codes like "RDraw2" → "Red Draw Two"; legacy plain-text entries render unchanged, so old games don't break). Backend error messages reaching the UI (`ProblemDetails.detail` from engine `Failure(...)` strings and application handler exceptions) are translated client-side via `i18n/backendMessages.ts`, which maps the finite, developer-controlled message table to `unoErrors.*`/`serverErrors.*` keys (regex-matching parameterized ones like "You must draw N cards first"); the API client translates at the single `request()` throw site using a module-level `getActiveT()` accessor the provider keeps in sync (works outside the React tree). **(2026-09-10 update:** superseded for HTTP errors — the backend now localizes coded messages itself from embedded JSON catalogs (see the server-side localization decision) and the client sends the app language as `X-Language`/`Accept-Language`; the `backendMessages.ts` table remains only as a fallback for English payloads.)** Remaining known-English surfaces: display names, the UNO brand wordmark, and card face glyphs — all language-neutral by design. RTL: the game-page hero's decorative floating cards use `rtl:` variants to swap physical sides (trailing side in both directions) and mirror their rotation, so they never collide with the text block.

- **2026-09-07** — Negative countdown grace window; hard deadline = allowance + MaxOverrun
  - **Context:** The turn timer skipped a player exactly at 0 and the UI clamped at 0. Desired UX: the countdown goes negative (-1 … -15), the player may still act inside that window, the overshoot shortens their NEXT allowance (act at -3 → next turn 30-3=27), a full -15 skip charges the whole grace, and 3 consecutive skipped turns = AFK (2 players → other player instantly wins; 3+ → removed, game continues).
  - **Decision:** `SetTurnClock` now stores the **hard** deadline on `GameState.NextActionDeadlineUtc`: `TurnStartUtc + MaxTurnSeconds + MaxOverrunSeconds`. The `TurnTimeoutService` therefore fires at the end of the grace window with no service change. Voluntary actions inside the window are accepted (no deadline check on player actions) and `ApplyTurnTimeAccounting` charges `elapsed − allowance` to the actor's bank-first/deferred-penalty, flooring their next allowance at `Base − MaxOverrun` (15s). `TimerRing` in the UNO view computes the soft end client-side (`TurnStartUtc + allowance`, both derivable from state) and renders a signed countdown: emerald while positive, red `-Ns` with pulse in overtime; AFK/elimination semantics were already in `ProcessTurnTimeout` (unchanged). `useNow` hook added for raw (signed) ticking.
  - **Rationale:** Keeping the grace inside the state deadline keeps the Game service game-agnostic (it still fires exactly at the stored deadline) while the engine owns the soft/hard split and the per-player accounting; the UI derives the soft end deterministically from persisted state. Verified with a 21-check harness (45s hard deadline, -3s action → 27s next allowance, skip at -15 → 15s allowance, 3×AFK elimination in 3p game-continues and 2p instant win, counter reset on action).

- **2026-09-07** — Classic-rule draw penalties: no playing while owing, official +4 challenge, one draw + Pass per turn
  - **Context:** Critical bug: when a player played a +2/+4, the next player could simply **play a card** (and a played +2 even stacked via `PendingDrawCount += 2`), transferring the penalty onward — `ProcessPlayCard` never checked `PendingDrawCount`. Related rule breaks: the WDF challenge set `PendingDrawCount = 6` for **both** outcomes (official: success → offender draws 4, challenger keeps the turn; failure → challenger draws 6); a penalty draw let the drawer keep the turn when a drawn card was playable; and `GetValidActions` offered `DrawCard` unconditionally (`|| true`), enabling infinite re-draws.
  - **Decision:** Classic UNO rules, no stacking: (1) `ProcessPlayCard`/voluntary `DrawCard` are rejected while `PendingDrawCount > 0`; `GetValidActions` offers only `AcceptDraw` (+ `ChallengeWildDrawFour` when pending == 4). (2) Challenge success records `PendingDrawOffenderIndex`; the subsequent `AcceptDraw` draws the 4 cards into the **offender's** hand and the challenger keeps the turn. Failure keeps the challenger drawing 6 and forfeiting the turn. A challenge may be decided **once**: `ProcessChallengeWildDrawFour` rejects attempts while `PendingDrawOffenderIndex` is set (repeats otherwise re-run the challenge and only spam the log). (2026-09-07 refinement) The challenge now **resolves immediately** — success draws the 4 into the offender's hand right away (challenger keeps the turn), failure draws 6 into the challenger's hand and forfeits the turn — removing the intermediate "resolved but not applied" state whose stale-UI repeat clicks surfaced 409s. (3) A penalty `AcceptDraw` (or a `TurnTimeout` with a pending penalty — the penalty is enforced even on skip) always forfeits the turn; drawn penalty cards may not be played. (4) New `DrawnThisTurn` flag (reset in `AdvancePlayer`): one voluntary draw per turn, and a new `Pass` action ends the turn after drawing instead of playing. (5) Refinement: an **unplayable** drawn card auto-passes inside `ProcessDrawCard` (turn ends immediately, no manual Pass); `Pass` remains only for declining a *playable* drawn card. Frontend: cards render unclickable while a penalty is pending, the Challenge button hides once the challenge is resolved, and the banner names the offender with an "Apply draw & continue" action.
  - **Rationale:** The play-through-pending hole let players dodge +2/+4 entirely; official challenge semantics prevent the 6-either-way exploit; the draw flag removes the draw-again loop and gives the classic "draw, then play or pass" choice a concrete action. Verified with a 34-check scratch harness (pending rejections, exact draw counts, offender/challenger hand deltas, turn advance/keep, timeout-with-penalty, DrawnThisTurn lifecycle).
- **2026-09-10** — Server-side error localization via embedded JSON catalogs (completes multi-language support)
  - **Context:** Multi-language was frontend-only: backend errors returned English and were re-translated client-side from a hardcoded English-string table. Half-finished backend work existed (an ErrorCatalog with a C# dictionary, coded UNO failures, a `code` field in ProblemDetails), but the `errors.en.json`/`errors.fa.json` files were unreferenced duplicates, Identity/Lobby/Game handlers still threw raw English text, every `UnauthorizedAccessException` detail was swallowed into a generic message, and the app's language toggle never reached the backend.
  - **Decision:** Translation data lives in per-language JSON files: `Localization/errors.{language}.json` (currently `en`, `fa`; add a language = add a file) embedded in BuildingBlocks.Domain as `WithCulture=false` resources (otherwise the SDK compiles `errors.en.json`-style names into satellite assemblies and the main DLL carries nothing). `ErrorCatalog` lazily loads the embedded dictionaries (lookup: exact tag → `fa-IR`-style prefix → English → raw code); `ErrorCodes` constants declare every code so call sites never drift from the catalog. All user-facing throws migrated: handlers use coded `DomainExceptionBase` subclasses (`ConflictException`/`NotFoundException`/`UnauthorizedException` — the latter replaces the detail-losing `UnauthorizedAccessException` at call sites; status mapping unchanged), validators emit `validation.*` codes instead of FluentValidation's English defaults, and the 3 leftover raw UNO strings became `uno.*` codes. `GlobalExceptionHandlerMiddleware` is the single localization boundary: language resolved from the `X-Language` header (app selection) falling back to `Accept-Language` (raw API clients), and it localizes ProblemDetails `title`, `detail`, the not-found entity name (`entity.*` codes), and the field-level `errors` dictionary. Frontend: `I18nProvider` exposes `getActiveLang()` and the API client sends it as `X-Language`/`Accept-Language` on every request. English catalog templates are kept byte-identical to the old raw strings, so the legacy client-side `backendMessages.ts` table still matches when no header arrives; unknown codes fall back to raw text, so old persisted English event/error payloads keep rendering.
  - **Rationale:** Plain per-language JSON fulfils "a json for each language" with zero tooling; a single middleware boundary keeps Domain/Application layers framework-free and the Game Engine pure (it only emits codes; `GameResult.ErrorCode`/`ErrorArgs` carry them through `ProcessGameActionCommandHandler`). Rejected: .resx/satellite assemblies (SDK culture-inference fights the `{lang}` filenames, tooling overhead), DB/Redis-backed catalogs (translations are build artifacts, not data), and FluentValidation's built-in LanguageManager (.resx-based; can't read our JSON). SignalR hub exception texts remain English (outside the HTTP pipeline); game event-log rendering stays client-side via the `events.*` envelopes. Verified with a scratch harness: resource embedding, en/fa lookup + prefix fallback, {0}-arg formatting, unknown-code passthrough, exception English messages, 97/97 key parity, and all 66 ErrorCodes constants mapped.

- **2026-09-10** — Next game implementation: Silver
  - **Context:** UNO has been implemented and validated as the first game, proving the engine/lobby/frontend pipeline. The next game should expand the platform's game-engine capabilities while remaining reasonably simple to implement. The 2026-09-05 decision had anticipated Splendor as the second game; that expectation is superseded.
  - **Decision:** Silver (Bézier Games, 2019 — the original standalone base game, internally the Amulet card set) is selected as the next game implementation. Silver base game only; no Silver Bullet / Silver Coin / Silver Dagger decks, no combining decks, no expansions, variants, or house rules unless separately approved. The complete rules, action model, hidden-information requirements, and integration checklist live in the **Silver — Next Game Implementation** section of this document. One small generic engine capability is anticipated and specified there: an optional player-specific state view (`GetPlayerView`) so hidden-information games can project per-viewer state — it must be recorded as its own Critical Decision when implemented.
  - **Rationale:** Silver exercises engine capabilities UNO never touched while staying far simpler than large Eurogames (Splendor/Azul/Wingspan/Terraforming Mars): hidden information and private player state (memory element), per-player knowledge tracking, card abilities with strict activation timings, draw/discard decisions, exchange/replacement mechanics (single and multi-card), player interaction (Witch/Robber target other villages), round-ending calls with penalties, and scoring across four rounds with amulet/tie-break rules. It is a useful architectural test after UNO — above all for player-specific state projection, which today's full-state broadcast architecture does not provide — and it requires no changes to the platform's microservice boundaries: the engine stays pure, the Game Service stays game-agnostic, and registration is one DI line under the existing DI-registration/no-plugin-loading decisions.

- **2026-09-11** — Player-specific state projection via optional `IPlayerViewGame`
  - **Context:** The Game Service served and broadcast the full authoritative `GameState` on every path (`GetGameStateQueryHandler`, the action response, `ReconnectPlayerCommandHandler`, and the `GameStateUpdated` SignalR group broadcast). UNO ships everything, including all hands — an accepted limitation there — but Silver's hidden villages, deck order, and per-player knowledge would leak to every client. The Silver spec (2026-09-10) anticipated a small generic engine capability and required it to be recorded as its own Critical Decision.
  - **Decision:** `GameEngine.Core` gains the optional interface `IPlayerViewGame { GameState GetPlayerView(GameState authoritativeState, PlayerId viewer); }`. `SilverGame` implements it: the projection (a dedicated `SilverView` model serialized into `Data["SilverState"]`) contains public information plus the viewer's own private cards and knowledge; other players' face-down values, the deck order, buried discard cards, and other players' knowledge are never exposed (hidden values are `null` in the view). Game Service integration lives in one helper (`PlayerViewProjection.Project(engine, state, viewer)`): `GetGameStateQueryHandler` projects for the requesting user, `ProcessGameActionCommandHandler` projects the action response for the actor (the authoritative state is still what gets persisted to PostgreSQL/Redis), `ReconnectPlayerCommandHandler` projects the reconnect response, and `GameRealTimeNotifier` sends per-connection projected states to each `GamePlayer`'s tracked `ConnectionId` when the session's engine implements the interface — engines that do not implement it (UNO) keep the single group broadcast exactly as before. `GameStateChanged` now carries `GameType` so the notifier can resolve the engine. No Silver-specific code exists in the Game Service; it only knows "engine projects per viewer".
  - **Rationale:** The smallest backward-compatible extension that keeps the engine authoritative and the Game Service game-agnostic; future hidden-information games inherit it by implementing one interface. Rejected alternatives (as anticipated by the spec): a generic visibility/annotation framework (over-engineering), Silver-specific projection services inside the Game Service (violates the game-agnostic Game Service), and per-seat pushes for all games (unnecessary churn for full-state games).

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

- **2026-09-12** — Turn-timeout broadcasts leaked the authoritative state; GameType is required on StateUpdated
  - **Context:** Skipping a turn (timeout) in Silver crashed the client (`Cannot read properties of undefined (reading 'length')`) and, worse, exposed hidden information. `GameRealTimeNotifier.PushStateAsync` resolves the per-viewer projection via `notification.GameType`; `TurnTimeoutService` published `StateUpdated` **without** `GameType`, so the notifier treated the session as a full-state engine and group-broadcast the raw authoritative `SilverState` (every face-down card, deck order, knowledge). Other publishers (`ProcessGameActionCommandHandler`, `CreateGameSessionCommandHandler`) already carried it — only the timeout path missed it.
  - **Decision:** (1) `TurnTimeoutService` now sets `GameType = session.GameType` on its `StateUpdated` notification. (2) Defense in depth on the client: `parseSilverState` rejects any payload that isn't the projected view shape (`DeckSize` number + `DiscardPile` array), rendering "waiting for state" instead of crashing on a leaked/foreign schema — a raw authoritative blob must never render. (3) `GamePlayersPanel` keyed its list by `player.id` (undefined at runtime) — now keyed by `userId` with a fallback. Also per owner directive: Silver timer defaults raised to 90s base / 180s max allowance (`SilverTurnTimerConfig` defaults 90/180/15/3/60). (4) Discard-pile audit follow-up: every card sent to the discard pile lands on TOP, face up; a turn timeout with a pending exchange now returns the taken card to the area it came from (discard top or Squire display) instead of dumping display cards into the discard pile.
  - **Rationale:** The notifier cannot infer projection capability without a game type, so `GameType` is a required field of every `StateUpdated` publish (it is now consistent across all four publishers). The client guard converts any future schema drift from a white-screen crash + potential leak into a safe "waiting" screen. Restarting `Game.Api` and starting fresh sessions is required for games persisted under pre-2026-09-12 schemas (known dev-stage limitation: old Silver sessions are not resumable).

- **2026-09-12** — Replacement orientation follows the source (owner correction; supersedes the §9 "face-up" ruling)
  - **Context:** Playtesting showed the deck-drawn replacement card appearing face up on the opponent's screen. The 2026-09-12 re-audit had applied §9 of the supplied spec literally ("the replacement card becomes face-up") for ALL sources, which destroys the memory element for deck draws. The owner confirmed the correct rule: a card coming from the **deck** (including the Witch's peeked deck-top card) enters the village **face down, known only to the player who drew/peeked it**; cards that were already public — the **discard pile top** and **Squire-displayed** cards — enter **face up**.
  - **Decision:** `ApplyReplacement` takes an `incomingFaceUp` parameter derived from the source: `false` for `ExchangeWithDrawn` and both Witch exchanges, `true` for `ExchangeWithDiscard` (discard or Squire display) and the Master (discard-sourced). Failed-match additions follow the same source orientation; the 3+ penalty card stays face-down/unknown. Deck-sourced incoming cards are added to the drawer's knowledge map so the projection shows them to their owner only. Harness checks updated accordingly (new: opponent view cannot see a face-down replacement; the Witch victim does not learn the inserted card; the actor knows it). EN/FA hints updated ("enters face down, known only to you").
  - **Rationale:** This restores official Silver behavior and matches the 2026-09-11 interpretation decision that the re-audit had temporarily overridden. Orientation is a property of the information the card carried before entering the village, so passing it as a parameter at the single replacement core keeps every entry point consistent and testable.

- **2026-09-12** — Census final-turn countdown keys on turn ownership, not the acting seat
  - **Context:** In a live 2-player game a round hung after a census: the last-turn player used the Revealer against the census caller, the caller answered the `ChooseRevealCard` prompt (which ends the actor's turn), but `RemainingCensusTurns` never decremented — the countdown compared the acting seat against the caller, and the acting seat WAS the caller. The caller then received another turn, which a census forbids.
  - **Decision:** The countdown block in `ProcessAction` resolves the owner of the just-ended turn first (`PlayerAdvanced` timeouts → the acting seat; everything else → `CurrentPlayerIndex`, e.g. a chooser answering the actor's prompt) and decrements when that owner is not the caller. Regression check added: "census: caller answering the final-turn Revealer ends the round" (harness now 246/246). A game already in the stuck state self-heals after the fix: the caller's extra turn ends without a decrement, and the next non-caller turn ends the round.
  - **Rationale:** The census rule counts turns, and the Revealer prompt proved that an action's submitter is not always the player whose turn it completes; keying on the submitter conflated the two roles.
