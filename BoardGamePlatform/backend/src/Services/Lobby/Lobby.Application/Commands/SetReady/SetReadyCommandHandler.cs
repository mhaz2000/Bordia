using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.SetReady;

/// <summary>
/// Handler that toggles the current user's ready state.
/// </summary>
public class SetReadyCommandHandler : IRequestHandler<SetReadyCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetReadyCommandHandler"/> class.
    /// </summary>
    public SetReadyCommandHandler(
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
        SetReadyCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.Status != RoomStatus.Waiting)
        {
            throw new ConflictException("This room is no longer accepting players.");
        }

        var player = room.GetPlayer(userId)
            ?? throw new ConflictException("You are not a member of this room.");

        room.ApplyReadyState(player.UserId, request.IsReady);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = request.IsReady ? RoomChangeType.PlayerReady : RoomChangeType.PlayerUnready,
            PlayerId = userId,
            IsReady = request.IsReady,
            PlayerCount = room.Players.Count
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}