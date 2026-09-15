using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.CurrentUser;
using BuildingBlocks.Domain.Localization;
using Game.Application.Common;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using Game.Domain.Enums;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Game.Application.Commands.ProcessGameAction;

/// <summary>
/// Handler that validates a player, processes the action through the Game Engine,
/// persists the new state, and broadcasts the update.
/// </summary>
public class ProcessGameActionCommandHandler : IRequestHandler<ProcessGameActionCommand, Result<GameState>>
{
    private readonly GameDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGameEngineProvider _engineProvider;
    private readonly RedisCacheService _cache;
    private readonly IPublisher _publisher;
    private readonly ILogger<ProcessGameActionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessGameActionCommandHandler"/> class.
    /// </summary>
    public ProcessGameActionCommandHandler(
        GameDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGameEngineProvider engineProvider,
        RedisCacheService cache,
        IPublisher publisher,
        ILogger<ProcessGameActionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _engineProvider = engineProvider;
        _cache = cache;
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<GameState>> Handle(
        ProcessGameActionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        // Optimistic concurrency (xmin) makes session state writes race-free
        // across instances and against the timeout sweeper. On conflict the
        // action is re-applied to the freshly reloaded state (up to 3 attempts).
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            _dbContext.ChangeTracker.Clear();
            try
            {
                return await TryProcessAsync(userId, request, attempt, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                _logger.LogInformation(
                    "Concurrent state change on session {SessionId}; retrying action {ActionType} (attempt {Attempt})",
                    request.SessionId,
                    request.ActionType,
                    attempt + 1);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(ErrorCodes.Game.ActionRejected);
            }
        }
    }

    private async Task<Result<GameState>> TryProcessAsync(
        Guid userId,
        ProcessGameActionCommand request,
        int attempt,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

        if (session.GetPlayer(userId) is null)
        {
            throw new UnauthorizedException(ErrorCodes.Game.NotAPlayer);
        }

        if (session.Status != GameSessionStatus.Active)
        {
            throw new ConflictException(ErrorCodes.Game.NotAccepting);
        }

        var engine = _engineProvider.Get(session.GameType);

        var currentState = GameState.FromJson(session.CurrentStateJson);

        long nextSequence;
        var lastSequence = await _dbContext.GameActionLogs
            .AsNoTracking()
            .Where(a => a.GameSessionId == session.Id)
            .Select(a => (long?)a.SequenceNumber)
            .MaxAsync(cancellationToken);

        nextSequence = (lastSequence ?? 0) + 1;

        var action = new GameAction
        {
            PlayerId = new PlayerId(userId),
            ActionType = request.ActionType,
            Payload = request.Payload,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = nextSequence
        };

        var result = engine.ProcessAction(currentState, action);

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Rejected action {ActionType} for session {SessionId}: {Error}",
                request.ActionType,
                session.Id,
                result.Error);

            // result.Error is a stable error code; the middleware localizes it
            // (Accept-Language) using the ErrorCatalog at the response boundary.
            throw new ConflictException(
                result.Error ?? ErrorCodes.Game.ActionRejected,
                result.ErrorArgs ?? Array.Empty<object?>());
        }

        if (result.NewState is null)
        {
            throw new ConflictException(ErrorCodes.Game.NoEngineState);
        }

        var newStateJson = result.NewState.ToJson();
        session.ApplyState(newStateJson);

        _dbContext.GameActionLogs.Add(GameActionLog.Create(
            session.Id,
            userId,
            request.ActionType,
            request.Payload,
            nextSequence));

        if (result.GameEnded || result.NewState.IsOver)
        {
            session.Finish();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.SetAsync(GameCacheKeys.State(session.Id), newStateJson, TimeSpan.FromMinutes(120), cancellationToken);

        await _publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.StateUpdated,
            State = result.NewState,
            GameType = session.GameType
        }, cancellationToken);

        await _publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.ActionProcessed,
            PlayerId = userId,
            ActionType = request.ActionType
        }, cancellationToken);

        if (session.Status == GameSessionStatus.Finished)
        {
            await _publisher.Publish(new GameStateChanged
            {
                SessionId = session.Id,
                ChangeType = GameChangeType.GameFinished,
                WinnerId = result.NewState.Winner?.UserId
            }, cancellationToken);
        }

        // Hidden-information games project the state per viewer; engines
        // without the capability return their full state unchanged.
        return Result<GameState>.Success(
            PlayerViewProjection.Project(engine, result.NewState, userId));
    }
}