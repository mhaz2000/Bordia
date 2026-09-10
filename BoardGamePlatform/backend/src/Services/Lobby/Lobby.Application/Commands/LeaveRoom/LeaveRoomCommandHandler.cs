using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using BuildingBlocks.Domain.Localization;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.LeaveRoom;

/// <summary>
/// Handler that removes the current user from a room, transferring or closing
/// the room when the host or last player leaves.
/// </summary>
public class LeaveRoomCommandHandler : IRequestHandler<LeaveRoomCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaveRoomCommandHandler"/> class.
    /// </summary>
    public LeaveRoomCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task<Result<LobbyRoomDto>> Handle(
        LeaveRoomCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.GetPlayer(userId) is null)
        {
            throw new ConflictException(ErrorCodes.Lobby.NotMember);
        }

        var wasHost = room.HostId == userId;
        var remainingPlayers = room.Players.Count - 1;

        room.RemovePlayer(userId);

        if (room.Players.Count == 0)
        {
            room.Close();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(new LobbyRoomChanged
            {
                RoomId = room.Id,
                ChangeType = RoomChangeType.RoomClosed
            }, cancellationToken);

            return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
        }

        Guid? newHostId = null;
        if (wasHost)
        {
            var nextHost = room.Players.OrderBy(p => p.JoinedAt).First();
            room.ChangeHost(nextHost.UserId);
            newHostId = nextHost.UserId;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.PlayerLeft,
            PlayerId = userId,
            DisplayName = _currentUser.DisplayName,
            PlayerCount = remainingPlayers
        }, cancellationToken);

        if (newHostId is not null)
        {
            await _publisher.Publish(new LobbyRoomChanged
            {
                RoomId = room.Id,
                ChangeType = RoomChangeType.HostChanged,
                NewHostId = newHostId
            }, cancellationToken);
        }

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}