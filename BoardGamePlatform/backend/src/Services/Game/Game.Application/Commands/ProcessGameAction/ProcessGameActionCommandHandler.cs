using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.CurrentUser;
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
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var session = await _dbContext.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(GameSession), request.SessionId);

        if (session.GetPlayer(userId) is null)
        {
            throw new UnauthorizedAccessException("You are not a player in this game.");
        }

        if (session.Status != GameSessionStatus.Active)
        {
            throw new ConflictException("This game is not accepting actions right now.");
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

            throw new ConflictException(result.Error ?? "The action was not accepted.");
        }

        if (result.NewState is null)
        {
            throw new ConflictException("The game engine returned no state for a valid action.");
        }

        session.ApplyState(result.NewState.ToJson());

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

        await _cache.SetAsync(GameCacheKeys.State(session.Id), result.NewState.ToJson(), cancellationToken);

        await _publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.StateUpdated,
            State = result.NewState
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

        return Result<GameState>.Success(result.NewState);
    }
}