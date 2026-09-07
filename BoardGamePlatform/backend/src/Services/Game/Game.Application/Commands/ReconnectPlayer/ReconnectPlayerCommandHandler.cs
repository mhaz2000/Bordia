using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Game.Application.Commands.ReconnectPlayer;

/// <summary>
/// Handler that marks the current user's seat as connected and returns the current state.
/// </summary>
public class ReconnectPlayerCommandHandler : IRequestHandler<ReconnectPlayerCommand, Result<GameState>>
{
    private readonly GameDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnectPlayerCommandHandler"/> class.
    /// </summary>
    public ReconnectPlayerCommandHandler(
        GameDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task<Result<GameState>> Handle(
        ReconnectPlayerCommand request,
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

        var seat = session.GetPlayer(userId)
            ?? throw new UnauthorizedAccessException("You are not a player in this game.");

        // Single-tab enforcement: if the seat already had an active connection on a
        // DIFFERENT connection id, that tab is superseded and must be kicked after
        // this one takes over. The same connection re-invoking JoinSession (idempotent
        // re-join, React StrictMode double-mount, hub retries) must never kick itself.
        var supersededConnectionId =
            seat.ConnectionId is { Length: > 0 } existing && existing != request.ConnectionId
                ? existing
                : null;

        seat.SetConnection(request.ConnectionId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.PlayerConnectionChanged,
            PlayerId = userId,
            IsConnected = true
        }, cancellationToken);

        if (supersededConnectionId is not null)
        {
            await _publisher.Publish(new GameStateChanged
            {
                SessionId = session.Id,
                ChangeType = GameChangeType.PlayerTakenOver,
                PlayerId = userId,
                ConnectionId = supersededConnectionId
            }, cancellationToken);
        }

        return Result<GameState>.Success(GameState.FromJson(session.CurrentStateJson));
    }
}