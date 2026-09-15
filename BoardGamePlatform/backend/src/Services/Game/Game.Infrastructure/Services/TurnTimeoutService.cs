using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.Persistence;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using Game.Domain.Enums;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Services;

/// <summary>
/// Background service that enforces per-game turn timers.
/// Queries ONLY sessions whose mirrored deadline columns have passed (indexed,
/// no per-second deserialization of every active state); when a deadline has
/// passed it dispatches a <c>TurnTimeout</c>/<c>GameTimeExpired</c> action to
/// the Game Engine, which applies the game's timeout/Skip/AFK rules and returns
/// an updated state. The result is persisted, cached, and broadcast through the
/// same real-time path as player-submitted actions. Each sweep cycle runs under
/// a PostgreSQL advisory lock so replicas never double-process timeouts.
/// </summary>
public class TurnTimeoutService : BackgroundService
{
    /// <summary>Advisory-lock key electing the single timeout sweeper.</summary>
    public const long LockKey = 7002;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TurnTimeoutService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="TurnTimeoutService"/> class.
    /// </summary>
    public TurnTimeoutService(
        IServiceScopeFactory scopeFactory,
        ILogger<TurnTimeoutService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Turn timeout service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turn timeout sweep encountered an error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task SweepExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (connectionString is null)
        {
            return;
        }

        // Single-sweeper election across replicas.
        await using var lease = await PostgresAdvisoryLock.TryAcquireAsync(connectionString, LockKey, cancellationToken);
        if (lease is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var engineProvider = scope.ServiceProvider.GetRequiredService<IGameEngineProvider>();
        var cache = scope.ServiceProvider.GetRequiredService<RedisCacheService>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var now = DateTime.UtcNow;

        // Indexed: only sessions whose mirrored deadlines already passed.
        var expiredIds = await db.GameSessions
            .Where(s => s.Status == GameSessionStatus.Active && !s.IsDeleted
                && ((s.NextActionDeadlineUtc != null && s.NextActionDeadlineUtc <= now)
                    || (s.GameEndsAtUtc != null && s.GameEndsAtUtc <= now)))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in expiredIds)
        {
            var session = await db.GameSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            if (session is null)
            {
                continue;
            }

            try
            {
                await ProcessExpiredSessionAsync(
                    session,
                    now,
                    db,
                    engineProvider,
                    cache,
                    publisher,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A player action (or another would-be sweeper) won the race on
                // this row; the mirrored deadline is re-evaluated next cycle.
                db.Entry(session).State = EntityState.Detached;
                _logger.LogDebug("Timeout sweep lost an optimistic race for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turn timeout processing failed for session {SessionId}", sessionId);
            }
        }
    }

    private async Task ProcessExpiredSessionAsync(
        GameSession session,
        DateTime now,
        GameDbContext db,
        IGameEngineProvider engineProvider,
        RedisCacheService cache,
        IPublisher publisher,
        CancellationToken cancellationToken)
    {
        var current = GameState.FromJson(session.CurrentStateJson);

        if (current.IsOver)
        {
            return;
        }

        // Priority: an expired turn deadline is handled first (it may end the game
        // via AFK elimination); the overall game time limit is checked on the next
        // sweep one second later if the game is still running.
        string actionType;
        if (current.NextActionDeadlineUtc is { } deadline && now > deadline)
        {
            actionType = "TurnTimeout";
        }
        else if (current.GameEndsAtUtc is { } gameEndsAt && now >= gameEndsAt)
        {
            actionType = "GameTimeExpired";
        }
        else
        {
            return;
        }

        if (current.CurrentPlayerIndex is not { } currentIndex
            || currentIndex < 0
            || currentIndex >= current.Players.Count)
        {
            return;
        }

        var expiredUserId = current.Players[currentIndex].UserId;
        var engine = engineProvider.Get(session.GameType);

        var lastSequence = await db.GameActionLogs
            .AsNoTracking()
            .Where(a => a.GameSessionId == session.Id)
            .Select(a => (long?)a.SequenceNumber)
            .MaxAsync(cancellationToken);
        var nextSequence = (lastSequence ?? 0) + 1;

        var action = new GameAction
        {
            PlayerId = new PlayerId(expiredUserId),
            ActionType = actionType,
            Payload = "{}",
            Timestamp = now,
            SequenceNumber = nextSequence
        };

        var result = engine.ProcessAction(current, action);

        if (!result.IsValid || result.NewState is null)
        {
            // Usually a race with a real action submitted just before the deadline.
            _logger.LogInformation(
                "Discarding {ActionType} for session {SessionId}: {Reason}",
                actionType,
                session.Id,
                result.Error ?? "no new state");
            return;
        }

        var newStateJson = result.NewState.ToJson();
        session.ApplyState(newStateJson);
        db.GameActionLogs.Add(GameActionLog.Create(
            session.Id,
            expiredUserId,
            actionType,
            "{}",
            nextSequence));

        if (result.GameEnded || result.NewState.IsOver)
        {
            session.Finish();
        }

        await db.SaveChangesAsync(cancellationToken);

        await cache.SetAsync(
            GameCacheKeys.State(session.Id),
            newStateJson,
            TimeSpan.FromMinutes(120),
            cancellationToken);

        await publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.StateUpdated,
            State = result.NewState,
            GameType = session.GameType
        }, cancellationToken);

        await publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.ActionProcessed,
            PlayerId = expiredUserId,
            ActionType = actionType
        }, cancellationToken);

        if (session.Status == GameSessionStatus.Finished)
        {
            await publisher.Publish(new GameStateChanged
            {
                SessionId = session.Id,
                ChangeType = GameChangeType.GameFinished,
                WinnerId = result.NewState.Winner?.UserId
            }, cancellationToken);
        }

        _logger.LogInformation(
            "{ActionType} processed for session {SessionId}, player {PlayerId}",
            actionType,
            session.Id,
            expiredUserId);
    }
}