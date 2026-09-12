using BuildingBlocks.Application;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.ClearPlayerConnection;

/// <summary>
/// Handler that marks seats offline when their SignalR connection drops.
/// </summary>
public class ClearPlayerConnectionCommandHandler
    : IRequestHandler<ClearPlayerConnectionCommand, Result>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearPlayerConnectionCommandHandler"/> class.
    /// </summary>
    public ClearPlayerConnectionCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(ClearPlayerConnectionCommand request, CancellationToken cancellationToken)
    {
        var affected = await _dbContext.RoomPlayers
            .Where(p => p.ConnectionId == request.ConnectionId)
            .ToListAsync(cancellationToken);
        if (affected.Count == 0) return Result.Success();

        foreach (var player in affected)
        {
            player.SetConnectionId(null);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var group in affected.GroupBy(p => p.RoomId))
        {
            foreach (var player in group)
            {
                await _publisher.Publish(new LobbyRoomChanged
                {
                    RoomId = group.Key,
                    ChangeType = RoomChangeType.PresenceChanged,
                    PlayerId = player.UserId,
                    IsConnected = false,
                }, cancellationToken);
            }
        }

        return Result.Success();
    }
}
