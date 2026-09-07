using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Caching;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using Game.Domain.Enums;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Services;

/// <summary>
/// Background service that enforces per-game turn timers.
/// Polls active sessions; when a session's <see cref="GameState.NextActionDeadlineUtc"/>
/// has passed it dispatches a <c>TurnTimeout</c> action to the Game Engine, which
/// applies the game's timeout/Skip/AFK rules and returns an updated state. The
/// result is persisted, cached, and broadcast through the same real-time path as
/// player-submitted actions.
/// </summary>
public class TurnTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TurnTimeoutService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="TurnTimeoutService"/> class.
    /// </summary>
    public TurnTimeoutService(
        IServiceScopeFactory scopeFactory,
        ILogger<TurnTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var engineProvider = scope.ServiceProvider.GetRequiredService<IGameEngineProvider>();
        var cache = scope.ServiceProvider.GetRequiredService<RedisCacheService>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var now = DateTime.UtcNow;

        var sessions = await db.GameSessions
            .Where(s => s.Status == GameSessionStatus.Active && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turn timeout processing failed for session {SessionId}", session.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
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

        if (current.IsOver
            || current.NextActionDeadlineUtc is not { } deadline
            || now <= deadline)
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
            ActionType = "TurnTimeout",
            Payload = "{}",
            Timestamp = now,
            SequenceNumber = nextSequence
        };

        var result = engine.ProcessAction(current, action);

        if (!result.IsValid || result.NewState is null)
        {
            // Usually a race with a real action submitted just before the deadline.
            _logger.LogInformation(
                "Discarding turn timeout for session {SessionId}: {Reason}",
                session.Id,
                result.Error ?? "no new state");
            return;
        }

        session.ApplyState(result.NewState.ToJson());
        db.GameActionLogs.Add(GameActionLog.Create(
            session.Id,
            expiredUserId,
            "TurnTimeout",
            "{}",
            nextSequence));

        if (result.GameEnded || result.NewState.IsOver)
        {
            session.Finish();
        }

        await cache.SetAsync(
            GameCacheKeys.State(session.Id),
            result.NewState.ToJson(),
            cancellationToken);

        await publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.StateUpdated,
            State = result.NewState
        }, cancellationToken);

        await publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.ActionProcessed,
            PlayerId = expiredUserId,
            ActionType = "TurnTimeout"
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
            "Turn timed out for session {SessionId}, player {PlayerId}",
            session.Id,
            expiredUserId);
    }
}