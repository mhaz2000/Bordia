# Board Game Platform - Phase 1 Foundation Specification

## Objective

Build the foundation of a production-ready online multiplayer board game platform.

This phase should establish the architecture that all future development will build upon.

The goal is **NOT** to create a playable game yet.

The goal is to create a scalable platform that allows implementing games like:

- UNO
- Splendor
- Wingspan
- Azul
- Terraforming Mars
- Ticket to Ride
- etc.

without requiring architectural changes.

The implementation should focus on infrastructure, communication, authentication, rooms, and the game engine foundation.

The codebase should be maintainable for many years.

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

                UNO/

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

Create GameSession infrastructure only.

Do NOT implement UNO.

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

Create the folder structure.

Games/

    Splendor/

Only create the project.

Do NOT implement game logic yet.

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

- UNO rules
- Splendor rules
- Wingspan
- Azul
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
- Unit Tests
- Integration Tests

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

The platform should be ready for implementing UNO in the next phase without requiring architectural refactoring.

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
- `GameSession` entity: `Id`, `RoomId`, `GameType`, `Status` (Active/Paused/Finished), `CurrentState` (serialized), `CreatedAt`, `StartedAt`, `FinishedAt`
- `GamePlayer` entity: `Id`, `GameSessionId`, `UserId`, `DisplayName`, `Position`, `IsConnected`
- `GameAction` — player action data: `PlayerId`, `ActionType`, `Payload` (JSON), `Timestamp`, `SequenceNumber`
- `IGameEngine` — interface to GameEngine.Core (LoadGame, CreateGame, ProcessAction, GetState)

### Game.Application
- Commands: `CreateGameSessionCommand`, `ProcessGameActionCommand`, `ReconnectPlayerCommand`, `PauseGameCommand`, `ResumeGameCommand`
- Queries: `GetGameSessionQuery`, `GetGameStateQuery`, `GetPlayerGameSessionsQuery`
- Validators
- Handlers orchestrate: load game from engine, process action via engine, persist state, publish events
- Calls `IGameEngine` (GameEngine.Core) for all game logic

### Game.Infrastructure
- EF Core `GameDbContext` with `GameSession`, `GamePlayer`, `GameAction` entities
- Redis for active game session state (low-latency reads/writes)
- SignalR `GameHub` — real-time game updates (state changes, player actions, connection status)
- `IGame` implementations registered via DI (see Critical Decisions)
- PostgreSQL migrations
- Outbox processor for `GameStarted`, `GameFinished` integration events

### Game.Api
- REST endpoints:
  - `POST /api/game/sessions` (internal, called by Lobby when starting game)
  - `GET /api/game/sessions/{id}`
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
  - `CreateGame(GameOptions options) : GameState`
  - `ProcessAction(GameState state, GameAction action) : GameResult`
  - `GetValidActions(GameState state, PlayerId playerId) : GameAction[]`
  - `IsGameOver(GameState state) : bool`
  - `GetWinner(GameState state) : PlayerId?`
- `IGame` implementations registered via DI (see Critical Decisions)
- `GameOptions` — game-type-specific configuration (number of players, variants, etc.)
- `GameState` — serializable game state (JSON-serializable, no circular refs)
- `GameAction` — player action with type and payload
- `GameResult` — `{ NewState, Events[], IsValid, Error? }`
- `GameEvent` — things that happened during action processing (for UI/notifications)
- Base classes: `GameBase`, `TurnBasedGame`, `RealTimeGame` (optional helpers)

---

## Games

### UNO
- Implements `IGame` from GameEngine.Core
- Contains ONLY UNO rules: deck (108 cards), discard pile, player hands, actions (play card, draw card, call UNO, challenge Wild Draw 4)
- No HTTP, no SignalR, no EF Core, no database
- Registered in DI as an `IGame` implementation (see Critical Decisions); ready for Phase 2 implementation

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
  - **Rationale:** UNO has simpler rules (no complex card interactions like Splendor's nobles/tokens), well-known mechanics, and is easier to validate the platform's game engine infrastructure. The turn-based structure maps cleanly to the `IGame` interface. Splendor will follow as the second game.

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
