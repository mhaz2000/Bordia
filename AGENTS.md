# Board Game Platform - Implementation Specification

## Current Status

| Component | Status |
|---|---|
| Foundation (services, BuildingBlocks, gateway, docker, auth, lobby) | COMPLETE |
| UNO (first game) | COMPLETE — see [docs/games/uno.md](docs/games/uno.md) |
| **Silver** | **COMPLETE — see [docs/games/silver.md](docs/games/silver.md)** |
| **Splendor** | **COMPLETE — see [docs/games/splendor.md](docs/games/splendor.md)** |
| **Azul** | **COMPLETE — see [docs/games/azul.md](docs/games/azul.md)** |

## Objective

Build a production-ready online multiplayer board game platform.

The foundation phase established the architecture that all future development builds upon: infrastructure, communication, authentication, rooms, and the game engine foundation. That work is **complete**, and four games (**UNO**, **Silver**, **Splendor**, **Azul**) have been implemented end to end on top of it (Azul: backend 2026-09-15, frontend 2026-09-16).

The current goal is implementing further games on the existing architecture, with **zero architectural changes per game** — see *Game Documentation* and the integration checklist for the pattern.

The platform is designed to allow implementing games like:

- UNO *(implemented)*
- Silver *(implemented)*
- Splendor
- Wingspan
- Azul *(implemented — backend 2026-09-15, frontend 2026-09-16)*
- Terraforming Mars
- Ticket to Ride
- etc.

without requiring architectural changes.

The codebase should be maintainable for many years.

### Historical note

Early revisions of this document described only the foundation phase ("the goal is NOT to create a playable game yet") and named Splendor as the first planned game. Those statements are historical: UNO was chosen and shipped first (see Critical Decisions 2026-09-05), and Silver followed (see the game documentation index below). Do not re-implement UNO or Silver, and do not treat Splendor as the next game without a new decision.

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
                Silver/     (implemented)

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

- `Games/UNO/` — implemented end to end (engine, registration, frontend view, localization); see [docs/games/uno.md](docs/games/uno.md).
- `Games/Silver/` — implemented end to end (engine, player-view projection, frontend view, localization); see [docs/games/silver.md](docs/games/silver.md).
- `Games/Splendor/` — implemented end to end (engine, viewer-independent player-view projection, registration, frontend views with original SVG card/noble/token art, localization); see [docs/games/splendor.md](docs/games/splendor.md).
- `Games/Azul/` — implemented end to end (engine, viewer-independent player-view projection that hides the bag order, registration, error catalogs, verification harness, frontend views with original SVG Persian haft-rangi tile art, the printed wall mosaic and §29 flight animations, localization); see [docs/games/azul.md](docs/games/azul.md).

Splendor implementation was explicitly started by owner decision 2026-09-13 (see the Critical Decisions of [docs/games/splendor.md](docs/games/splendor.md)); the frontend followed by owner decision 2026-09-14. Both are complete.

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

- Splendor rules beyond the base game (no Cities of Splendor / Trading Posts / The Orient / The Strongholds modules, no Splendor Duel / Marvel variants, no expansions, fan variants, or house rules) — see [docs/games/splendor.md](docs/games/splendor.md)
- Wingspan
- ~~Azul~~ — *implemented (engine 2026-09-15 by owner decision; frontend 2026-09-16 — see [docs/games/azul.md](docs/games/azul.md))*
- Silver rules beyond the base game (no Silver Bullet / Silver Coin / Silver Dagger decks, no combining decks, no variants or house rules)
- ~~Chat~~ — *implemented 2026-09-14 by owner decision; see Critical Decisions*
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
- Broad unit/integration test suites (the Silver engine verification harness required by [docs/games/silver.md](docs/games/silver.md) is the exception; see its Testing expectations)

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

The foundation deliverables are complete and validated by the UNO implementation. The Silver implementation followed without architectural refactoring — the pattern established by the two shipped games (see *Game Documentation* and the integration checklist) is the template for all future games.

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

16. Document critical decisions where they belong: platform-wide architectural/technical decisions in this file under **Critical Decisions** (see below); game-specific rules, interpretation, and implementation decisions in the **Critical Decisions** section of that game's document (`docs/games/<game>.md`). Do not rely on chat history alone.

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
- **Player-specific state views:** for games with hidden information the engine may additionally implement an optional player-view capability (`IPlayerViewGame`, see Critical Decisions 2026-09-11; first exercised by [docs/games/silver.md](docs/games/silver.md)) so the Game Service can project per-viewer state without game-specific code. This is the only anticipated engine-contract extension; do not build a generic visibility framework.

---

## Games

### UNO *(implemented)*
- Implements `IGame` from GameEngine.Core
- Contains ONLY UNO rules: deck (108 cards), discard pile, player hands, actions (play card, draw card, call UNO, challenge Wild Draw Four)
- No HTTP, no SignalR, no EF Core, no database
- Registered in DI as an `IGame` implementation (see Critical Decisions); rules & game decisions: [docs/games/uno.md](docs/games/uno.md)

### Silver *(implemented — engine + frontend view)*
- Implements `IGame` and `IPlayerViewGame` from GameEngine.Core exactly as UNO implements `IGame`; the complete rules, action model, hidden-information requirements, and integration checklist live in [docs/games/silver.md](docs/games/silver.md)
- `Games/Silver` references only `GameEngine.Core`, registered via one DI line next to UNO (see Critical Decisions 2026-09-11)
- No Silver-specific code leaks into the Game Service, Lobby, BuildingBlocks, or the generic engine model

---

# Game Documentation

Per-game rules specifications, implementation notes, and game-specific critical decisions
live in `docs/games/`, not in this file:

| Game | Status | Document |
|---|---|---|
| UNO | Implemented | [docs/games/uno.md](docs/games/uno.md) |
| Silver | Implemented | [docs/games/silver.md](docs/games/silver.md) |
| Splendor | Implemented (backend 2026-09-13, frontend 2026-09-14) | [docs/games/splendor.md](docs/games/splendor.md) |
| Azul | Implemented (backend 2026-09-15, frontend 2026-09-16) | [docs/games/azul.md](docs/games/azul.md) |

This file remains authoritative for the platform: architecture, service boundaries, the
`IGame` / `IPlayerViewGame` engine contracts, shared persistence/localization/SignalR
patterns, the integration checklist below, and platform-wide critical decisions.

## Adding a Game (Integration Checklist)

1. New `backend/src/Games/<Game>` project (net9.0, references **only** `GameEngine.Core`,
   added to the solution) plus a verification harness console project under `tests/`.
2. `<Game>Game : IGame` - and also `IPlayerViewGame` when the game has hidden information;
   `CreateGame` validates the configured player count.
3. One DI registration line in `Game.Infrastructure` next to the existing games; the
   `GET /api/game/games` catalog and `GameEngineProvider` resolution then need zero changes.
4. Engine error codes use a `<game>.*` namespace in the game project's own `Errors` class
   and are added to both server-side localization catalogs (`errors.en.json` / `errors.fa.json`).
5. Frontend: `<game>.ts` typed state mirror + parser, `<Game>GameView.tsx`, `GamePage` view
   switch, `GAME_THEME` entry, and `games.<Game>` + `events.<game>.*` copy in **both** locale files.
6. Create `docs/games/<game>.md` (rules specification, design notes, game-specific Critical
   Decisions in the same dated format) and point this file's status table and index at it.


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

- **2026-09-08** — Bilingual UI (EN/FA) with RTL support
  - **Context:** The platform needed to serve English and Farsi users with the option to add more languages later. Farsi requires right-to-left layout mirroring across every page.
  - **Decision:** Custom dependency-free i18n under `frontend/src/i18n`: `I18nProvider` (context) exposes `{ lang, dir, setLanguage, t(key, params), d }`. Dictionaries are typed objects in `locales/en.ts` (reference) and `locales/fa.ts`; `fa: Dict` makes a missing translation a compile error. `t` resolves dot-paths with `{param}` interpolation and falls back to English. Selection persists in `localStorage` (`bgp.lang`); the provider sets `<html lang dir>` and a `lang-fa` class on change. RTL is achieved by (1) `dir="rtl"` flipping all flex/grid layouts, (2) Tailwind logical utilities (`ms-`, `me-`, `start-`, `end-`, `text-start`) instead of physical ones in the markup, (3) `marginInlineStart` for the dynamic hand-fan overlap, and (4) the Vazirmatn webfont applied via `html.lang-fa` (with LTR-isolated `.font-mono`/`.tabular-nums` fragments for codes/numbers). A `LanguageSwitcher` sits in every page header. Per-game copy (descriptions, rules, action cards) lives in the dictionaries under `games.<gameType>` and is surfaced through the `useGameInfo` hook — adding a third language is one new locale file plus one `LANGUAGES` entry; adding a game's copy is one dictionary section.
  - **Rationale:** A typed dictionary avoids a runtime i18n dependency while giving compile-time coverage of missing keys; Tailwind's built-in logical utilities plus the `dir` attribute cover mirroring without parallel stylesheets. Backend-generated strings (game event log, server error messages) remain English — translating them would require server-side i18n and is deferred.
  - **(2026-09-08 addition — backend strings localized without server i18n)** The engine now emits event-log entries as JSON envelopes `{"c":"code","d":{params}}` (`UnoEvent.Build`, `Games/UNO`); the client's `formatEvent` renders them via the `events.*` dictionary (color params → localized color names, card codes like "RDraw2" → "Red Draw Two"; legacy plain-text entries render unchanged, so old games don't break). Backend error messages reaching the UI (`ProblemDetails.detail` from engine `Failure(...)` strings and application handler exceptions) are translated client-side via `i18n/backendMessages.ts`, which maps the finite, developer-controlled message table to `unoErrors.*`/`serverErrors.*` keys (regex-matching parameterized ones like "You must draw N cards first"); the API client translates at the single `request()` throw site using a module-level `getActiveT()` accessor the provider keeps in sync (works outside the React tree). **(2026-09-10 update:** superseded for HTTP errors — the backend now localizes coded messages itself from embedded JSON catalogs (see the server-side localization decision) and the client sends the app language as `X-Language`/`Accept-Language`; the `backendMessages.ts` table remains only as a fallback for English payloads.)** Remaining known-English surfaces: display names, the UNO brand wordmark, and card face glyphs — all language-neutral by design. RTL: the game-page hero's decorative floating cards use `rtl:` variants to swap physical sides (trailing side in both directions) and mirror their rotation, so they never collide with the text block.

- **2026-09-07** — Negative countdown grace window; hard deadline = allowance + MaxOverrun
  - **Context:** The turn timer skipped a player exactly at 0 and the UI clamped at 0. Desired UX: the countdown goes negative (-1 … -15), the player may still act inside that window, the overshoot shortens their NEXT allowance (act at -3 → next turn 30-3=27), a full -15 skip charges the whole grace, and 3 consecutive skipped turns = AFK (2 players → other player instantly wins; 3+ → removed, game continues).
  - **Decision:** `SetTurnClock` now stores the **hard** deadline on `GameState.NextActionDeadlineUtc`: `TurnStartUtc + MaxTurnSeconds + MaxOverrunSeconds`. The `TurnTimeoutService` therefore fires at the end of the grace window with no service change. Voluntary actions inside the window are accepted (no deadline check on player actions) and `ApplyTurnTimeAccounting` charges `elapsed − allowance` to the actor's bank-first/deferred-penalty, flooring their next allowance at `Base − MaxOverrun` (15s). `TimerRing` in the UNO view computes the soft end client-side (`TurnStartUtc + allowance`, both derivable from state) and renders a signed countdown: emerald while positive, red `-Ns` with pulse in overtime; AFK/elimination semantics were already in `ProcessTurnTimeout` (unchanged). `useNow` hook added for raw (signed) ticking.
  - **Rationale:** Keeping the grace inside the state deadline keeps the Game service game-agnostic (it still fires exactly at the stored deadline) while the engine owns the soft/hard split and the per-player accounting; the UI derives the soft end deterministically from persisted state. Verified with a 21-check harness (45s hard deadline, -3s action → 27s next allowance, skip at -15 → 15s allowance, 3×AFK elimination in 3p game-continues and 2p instant win, counter reset on action).

- **2026-09-10** — Server-side error localization via embedded JSON catalogs (completes multi-language support)
  - **Context:** Multi-language was frontend-only: backend errors returned English and were re-translated client-side from a hardcoded English-string table. Half-finished backend work existed (an ErrorCatalog with a C# dictionary, coded UNO failures, a `code` field in ProblemDetails), but the `errors.en.json`/`errors.fa.json` files were unreferenced duplicates, Identity/Lobby/Game handlers still threw raw English text, every `UnauthorizedAccessException` detail was swallowed into a generic message, and the app's language toggle never reached the backend.
  - **Decision:** Translation data lives in per-language JSON files: `Localization/errors.{language}.json` (currently `en`, `fa`; add a language = add a file) embedded in BuildingBlocks.Domain as `WithCulture=false` resources (otherwise the SDK compiles `errors.en.json`-style names into satellite assemblies and the main DLL carries nothing). `ErrorCatalog` lazily loads the embedded dictionaries (lookup: exact tag → `fa-IR`-style prefix → English → raw code); `ErrorCodes` constants declare every code so call sites never drift from the catalog. All user-facing throws migrated: handlers use coded `DomainExceptionBase` subclasses (`ConflictException`/`NotFoundException`/`UnauthorizedException` — the latter replaces the detail-losing `UnauthorizedAccessException` at call sites; status mapping unchanged), validators emit `validation.*` codes instead of FluentValidation's English defaults, and the 3 leftover raw UNO strings became `uno.*` codes. `GlobalExceptionHandlerMiddleware` is the single localization boundary: language resolved from the `X-Language` header (app selection) falling back to `Accept-Language` (raw API clients), and it localizes ProblemDetails `title`, `detail`, the not-found entity name (`entity.*` codes), and the field-level `errors` dictionary. Frontend: `I18nProvider` exposes `getActiveLang()` and the API client sends it as `X-Language`/`Accept-Language` on every request. English catalog templates are kept byte-identical to the old raw strings, so the legacy client-side `backendMessages.ts` table still matches when no header arrives; unknown codes fall back to raw text, so old persisted English event/error payloads keep rendering.
  - **Rationale:** Plain per-language JSON fulfils "a json for each language" with zero tooling; a single middleware boundary keeps Domain/Application layers framework-free and the Game Engine pure (it only emits codes; `GameResult.ErrorCode`/`ErrorArgs` carry them through `ProcessGameActionCommandHandler`). Rejected: .resx/satellite assemblies (SDK culture-inference fights the `{lang}` filenames, tooling overhead), DB/Redis-backed catalogs (translations are build artifacts, not data), and FluentValidation's built-in LanguageManager (.resx-based; can't read our JSON). SignalR hub exception texts remain English (outside the HTTP pipeline); game event-log rendering stays client-side via the `events.*` envelopes. Verified with a scratch harness: resource embedding, en/fa lookup + prefix fallback, {0}-arg formatting, unknown-code passthrough, exception English messages, 97/97 key parity, and all 66 ErrorCodes constants mapped.

- **2026-09-11** — Player-specific state projection via optional `IPlayerViewGame`
  - **Context:** The Game Service served and broadcast the full authoritative `GameState` on every path (`GetGameStateQueryHandler`, the action response, `ReconnectPlayerCommandHandler`, and the `GameStateUpdated` SignalR group broadcast). UNO ships everything, including all hands — an accepted limitation there — but Silver's hidden villages, deck order, and per-player knowledge would leak to every client. The Silver spec (2026-09-10) anticipated a small generic engine capability and required it to be recorded as its own Critical Decision.
  - **Decision:** `GameEngine.Core` gains the optional interface `IPlayerViewGame { GameState GetPlayerView(GameState authoritativeState, PlayerId viewer); }`. `SilverGame` implements it: the projection (a dedicated `SilverView` model serialized into `Data["SilverState"]`) contains public information plus the viewer's own private cards and knowledge; other players' face-down values, the deck order, buried discard cards, and other players' knowledge are never exposed (hidden values are `null` in the view). Game Service integration lives in one helper (`PlayerViewProjection.Project(engine, state, viewer)`): `GetGameStateQueryHandler` projects for the requesting user, `ProcessGameActionCommandHandler` projects the action response for the actor (the authoritative state is still what gets persisted to PostgreSQL/Redis), `ReconnectPlayerCommandHandler` projects the reconnect response, and `GameRealTimeNotifier` sends per-connection projected states to each `GamePlayer`'s tracked `ConnectionId` when the session's engine implements the interface — engines that do not implement it (UNO) keep the single group broadcast exactly as before. `GameStateChanged` now carries `GameType` so the notifier can resolve the engine. No Silver-specific code exists in the Game Service; it only knows "engine projects per viewer".
  - **Rationale:** The smallest backward-compatible extension that keeps the engine authoritative and the Game Service game-agnostic; future hidden-information games inherit it by implementing one interface. Rejected alternatives (as anticipated by the spec): a generic visibility/annotation framework (over-engineering), Silver-specific projection services inside the Game Service (violates the game-agnostic Game Service), and per-seat pushes for all games (unnecessary churn for full-state games).

- **2026-09-12** — Turn-timeout broadcasts leaked the authoritative state; GameType is required on StateUpdated
  - **Context:** Skipping a turn (timeout) in Silver crashed the client (`Cannot read properties of undefined (reading 'length')`) and, worse, exposed hidden information. `GameRealTimeNotifier.PushStateAsync` resolves the per-viewer projection via `notification.GameType`; `TurnTimeoutService` published `StateUpdated` **without** `GameType`, so the notifier treated the session as a full-state engine and group-broadcast the raw authoritative `SilverState` (every face-down card, deck order, knowledge). Other publishers (`ProcessGameActionCommandHandler`, `CreateGameSessionCommandHandler`) already carried it — only the timeout path missed it.
  - **Decision:** (1) `TurnTimeoutService` now sets `GameType = session.GameType` on its `StateUpdated` notification. (2) Defense in depth on the client: `parseSilverState` rejects any payload that isn't the projected view shape (`DeckSize` number + `DiscardPile` array), rendering "waiting for state" instead of crashing on a leaked/foreign schema — a raw authoritative blob must never render. (3) `GamePlayersPanel` keyed its list by `player.id` (undefined at runtime) — now keyed by `userId` with a fallback. Also per owner directive: Silver timer defaults raised to 90s base / 180s max allowance (`SilverTurnTimerConfig` defaults 90/180/15/3/60). (4) Discard-pile audit follow-up: every card sent to the discard pile lands on TOP, face up; a turn timeout with a pending exchange now returns the taken card to the area it came from (discard top or Squire display) instead of dumping display cards into the discard pile.
  - **Rationale:** The notifier cannot infer projection capability without a game type, so `GameType` is a required field of every `StateUpdated` publish (it is now consistent across all four publishers). The client guard converts any future schema drift from a white-screen crash + potential leak into a safe "waiting" screen. Restarting `Game.Api` and starting fresh sessions is required for games persisted under pre-2026-09-12 schemas (known dev-stage limitation: old Silver sessions are not resumable).



- **2026-09-13** — Private rooms: code-invite mechanism + truthful lobby presence
  - **Context:** `Room.IsPrivate` was stored but never enforced: private rooms appeared in the public list and joined exactly like public ones. Simultaneously, the waiting-room presence/ready display was broken (`LobbyRoomPlayerDto` exposed no connection state while the client read a nonexistent `connectionId`; joining/disconnecting broadcast nothing; the client store and kick/transfer used a `player.id` field the DTO never had, patching by `id` and sending `undefined` userIds).
  - **Decision:** **Privacy:** private rooms are excluded from `GetRoomListQueryHandler`; `JoinRoomCommand` became `(RoomId?, Code?, AllowPrivate)` — by-id joins reject private rooms for non-members (`lobby.roomIsPrivate` error code, EN/FA catalog entries), and a new `POST /api/lobby/rooms/join-by-code { code }` is the invite path (uppercases the 6-char `RoomCodeGenerator` code, sets `AllowPrivate`). **Presence:** `LobbyRoomPlayerDto.IsConnected` (mapped from `ConnectionId != null`); `SetPlayerConnection` and a new `ClearPlayerConnectionCommand` (from `LobbyHub.OnDisconnectedAsync`) publish the new `RoomChangeType.PresenceChanged` → `ILobbyHubClient.PresenceChanged(roomId, playerId, isConnected)`; the lobby hub wrapper re-invokes `JoinRoom` on `onreconnected` so a re-connected socket records its new id. The client store patches players by `userId` (not the nonexistent `id`), kick/transfer send `userId`, and the waiting room was redesigned (themed hero, copy-code button, seat cards with presence dots, dashed empty seats, ready-progress bar, host shown without a misleading "Not ready" pill).
  - **Rationale:** A room code the host shares is the invite secret (no friendship system in scope), so code-join is the only private entry path; GUIDs remain unguessable but are never trusted as invitations. Presence was made server-driven (DTO flag + events) instead of the client guessing from raw connection ids it should never see. Old Redis-cached room lists may show a private room until the next invalidation - acceptable staleness for a lobby list.

- **2026-09-13** — Room code mistakes answer 409, and kicked players are told they were removed
  - **Context:** Following the private-room work, two gaps surfaced in testing: entering a wrong room code was an unhandled failure (the join-by-code path fell through to the generic entity-not-found shape), and a player kicked by the host stayed rendered on the waiting-room page, silently vanishing from the player list with no explanation (kick reused the generic `PlayerLeft` broadcast, which carries no "you were removed" semantics for the victim).
  - **Decision:** Wrong/expired codes now throw `ConflictException(ErrorCodes.Lobby.RoomCodeNotFound)` → a localized 409 problem+json ("No waiting room was found with that code", EN/FA catalogs), and the code lookup only matches `Waiting` rooms; both join mutations set `retry: 0` so the error banner appears immediately (react-query's default would retry 3 times). Kicking now emits a dedicated `RoomChangeType.PlayerKicked` → `ILobbyHubClient.PlayerKicked(roomId, playerId)`: the frontend lobby hub wires `onPlayerKicked`, the store keeps a `kickedRoomId` flag, and `RoomPage` shows a blocking "Removed from the room / The host removed you from this room" overlay (leaving the hub group as the side effect) with a Back-to-lobby action; other clients still see the player disappear from the list.
  - **Rationale:** A kick is a different event than a voluntary leave and must be distinguishable client-side, which needs its own SignalR contract method rather than overloading `PlayerLeft`. The 409 keeps invite-code UX within the coded-error localization boundary, and retrying a user-typed code server-side would only mask the error and delay the message.

- **2026-09-13** — Unique indexes over soft-deleted entities must be partial
  - **Context:** Joining a private room, getting kicked, then rejoining threw `DbUpdateException` → Postgres `23505 duplicate key "ix_room_players_room_id_user_id"`. Soft delete keeps membership rows (deleted), and the unique index covered them too, so a rejoin's INSERT collided with the ghost row (queries never see it because of the soft-delete filter — the mismatch made it look impossible). The same trap applies to any re-joinable membership.
  - **Decision:** `RoomPlayer(RoomId, UserId)` unique index now carries `HasFilter("\"is_deleted\" = false")` (mirroring the existing `Room.RoomCode` partial index), with migration `RoomPlayersUniqueIndexIgnoresSoftDeleted` (drop + recreate filtered unique; applied to the dev DB and auto-applied on startup via `MigrateDatabase`). Going forward: every unique index on a soft-deleted entity must be partial on `is_deleted = false` at creation time.
  - **Rationale:** Partial indexes keep the audit-trail guarantee (rows are never purged) while enforcing uniqueness only among live rows — the only semantics consistent with a global soft-delete query filter. The alternative considered (reviving the deleted row on rejoin) would corrupt the membership history and hide the repeated kick/leave pattern the soft-delete exists to preserve.

- **2026-09-13** — Silent token refresh keeps players logged in mid-game
  - **Context:** The Identity service already rotates one-time-use refresh tokens (60-min access / 7-day refresh, replay of a revoked token is rejected), and the client persisted both tokens — but nothing ever used the refresh token: any 401 from `request()` called `logout()` outright, so an hour-long game or an idle tab dropped players to the login screen mid-session.
  - **Decision:** Frontend-only. `client.ts` gains a single-flight `refreshSession()` (one in-flight exchange at a time — concurrent 401s must not replay the rotated token and trip the server's reuse detection; on success it stores the new pair via `setAuth`). `request()` treats a 401 on a protected path as an expiring session: refresh once, replay the original call with the new token, and only `logout()` if the refresh itself fails; credential endpoints (`login/register/refresh/logout`) keep their plain 401 semantics. A `useSessionMaintenance()` hook (mounted in `PrivateLayout`) decodes the JWT `exp`, schedules a refresh 60 s before expiry (rescheduling on each rotation), refreshes immediately on mount if already inside the window, and re-checks on `visibilitychange` after long idle. SignalR hubs already read the live token through `accessTokenFactory`, so reconnects pick up rotated tokens.
  - **Rationale:** The request choke point is the single boundary where every API caller benefits without per-page logic; the proactive timer minimizes how often the retry path fires at all (important for in-flight game actions and SignalR handshakes). Server behavior was verified correct and untouched — rotation with replay revocation is exactly why the client serializes refreshes.

- **2026-09-14** — Room chat implemented (owner reverses the earlier out-of-scope call)
  - **Context:** AGENTS.md listed Chat as out of scope. The owner explicitly directed adding a chat available in the waiting room and in every game: a floating button opening a drawer, closable, with an unread badge on new messages while closed, and a server-enforced rate limit of 10 messages per minute per player.
  - **Decision:** Chat belongs to the **Lobby service** (rooms are the shared concept across lobby and game; the Lobby already owns the `lobby-room-{id}` SignalR group, membership checks and presence): (1) `RoomMessage` domain entity + `room_messages` table (migration `AddRoomMessages`), append-only, soft-deleted per the global convention; (2) `SendRoomMessageCommand` — membership + room-not-closed + Redis fixed-window rate limit (`lobby:chat:{roomId}:{userId}:{yyyyMMddHHmm}`, cap 10, TTL 70s, `lobby.chatRateLimited` coded error in both EN/FA catalogs) + validation codes `validation.chatMessageRequired/TooLong` (≤300 chars); (3) delivery via the **LobbyHub** only: new client contract method `ILobbyHubClient.RoomMessage(...)` broadcast to the room group, hub method `SendRoomMessage` forwarding to the mediator (no logic in the hub, per the AI-agent rules), persisted-first then notified through `RoomChatMessageSent` + `LobbyChatNotifier`; (4) REST `GET /api/lobby/rooms/{id}/chat?take=` (members only, ≤200) loads history when the drawer opens; (5) frontend shared `ChatDrawer` component (FAB with unread badge → slide-in side drawer, Enter-to-send, own/others bubbles, localized) mounted on `RoomPage` (reuses the page's hub group join) and `GamePage` (drawer itself connects + `JoinRoom`/`LeaveRoom` on the lobby hub, so game screens receive chat without the Game service knowing anything about chat). Game sessions keep using their own hub; chat never crosses into Game service, engine, or contracts.
  - **Rationale:** Reuses every existing platform mechanism (hub groups, coded-error localization, EF/migrations, Redis-as-cache) with zero new infrastructure; the lobby is the only service that knows room membership. Rejected: a separate Chat service (overkill for append-only room chatter), Redis pub/sub fan-out (SignalR groups already handle it), storing chat in the room cache (messages are durable history, not cache state). Known dev-stage limits: the rate limit is a soft fixed window (concurrent boundary sends may admit 11/minute; the window is the cap unit anyway) and hub invocation exceptions surface untranslated in the drawer error line (SignalR bypasses the HTTP localization boundary — same accepted precedent as before).


- **2026-09-15** — Scalability & concurrency code-hardening pass (all issues now are deployment knobs, not code)
  - **Context:** Before adding more games, the owner requested a full code-level audit for high usage (200+ rooms) so deployment only needs instance-count decisions. Audit found: `TurnTimeoutService` loading every Active session + full state JSON every second (deadlines lived only inside the JSON, unindexed); `AddSignalR()` had **no Redis backplane** despite the plan, locking every service to one instance; read-modify-write races on `game_sessions.current_state_json` between the sweep and player actions (and across replicas); lost-update-free background workers impossible (outbox/cleanup would double-fire per instance); chat rate limit was a non-atomic get/set; no HTTP abuse backstop; hub connection-id lookups unindexed; 30-min default TTL on active game-state cache.
  - **Decision:** (1) `GameSession` mirrors `NextActionDeadlineUtc`/`GameEndsAtUtc` out of the state JSON on every write (`ApplyState`/`Create`) + partial indexes (`status = 0 AND …`); the sweep is now one indexed query for expired sessions only. (2) Optimistic concurrency via a `version` column (`IsConcurrencyToken`, incremented by every session mutation): the action handler retries up to 3 attempts against the freshly reloaded state; raw `DbUpdateConcurrencyException` is mapped by the middleware to localized 409 `common.concurrencyConflict`. (Deliberate deviation: the classic Npgsql pattern is `Property<uint>("xmin").IsRowVersion()`, but Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4 contains no xmin handling — it generated `ADD COLUMN xmin` which Postgres reserves as a system column — so an app-maintained token was used instead.) (3) SignalR Redis backplane wired in Game.Api + Lobby.Api (the original `instanceName` key-prefix crashed startup: StackExchange.Redis 2.8 REMOVED the instanceName option - both the connection-string keyword and the property - which the SignalR 9.0 backplane resolves; the fix passes the bare connection string, and SignalR's own per-hub channel namespacing is sufficient on a dedicated Redis). (4) New `PostgresAdvisoryLock.TryAcquireAsync` elects ONE instance per background loop: `OutboxProcessor` (key 7001), `TurnTimeoutService` (7002), `AbandonedRoomCleanupService` (7003); losers skip the tick quietly. (5) `RedisCounter` (INCR + first-hit EXPIRE on a shared `IConnectionMultiplexer` registered in `AddAppInfrastructure`) makes the chat fixed window atomic; cap still 10/min. (6) Gateway fixed-window rate limiting per client IP via YARP `RateLimitingPolicy`: auth 30/min, lobby 240/min, game 300/min (generous for NATs), 429 on exceed. (7) Indexes: `game_players(connection_id)`, `room_players(connection_id)`. (8) Game-state cache writes now TTL 120 min (>60-min game cap). (9) Frontend react-query polling stops in background tabs (`refetchIntervalInBackground: false`). Verified: solution build 0 errors, Splendor harness 236/236, Silver harness green, `tsc && vite build` green.
  - **Rationale:** All races are closed with DB-enforced mechanisms (version token + advisory locks), not in-process assumptions, so instances can be added without code change; the only remaining multi-instance caveat is *documented, not silent*: single-tab `SessionTakenOver` + hub group membership now work across replicas via the backplane (connection ids are already persisted), and connection-string pools/Kestrel sizing are pure deployment. Deployment checklist left: replicas behind YARP, Npgsql `Maximum Pool Size`, Redis maxmemory, Postgres connection ceiling (PgBouncer at ~10× scale), and rotating the dev `Jwt:Key`/`Cors` origin.


