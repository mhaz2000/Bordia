using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using BuildingBlocks.Infrastructure.Outbox;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.CloseRoom;

/// <summary>
/// Handler that closes a waiting room and publishes the <c>RoomClosed</c> event.
/// </summary>
public class CloseRoomCommandHandler : IRequestHandler<CloseRoomCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IOutbox _outbox;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloseRoomCommandHandler"/> class.
    /// </summary>
    public CloseRoomCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper,
        IOutbox outbox,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
        _outbox = outbox;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task<Result<LobbyRoomDto>> Handle(
        CloseRoomCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } hostId)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.HostId != hostId)
        {
            throw new UnauthorizedAccessException("Only the host can close the room.");
        }

        room.Close();

        await _outbox.AddAsync(new RoomClosed
        {
            RoomId = room.Id
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.RoomClosed
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}