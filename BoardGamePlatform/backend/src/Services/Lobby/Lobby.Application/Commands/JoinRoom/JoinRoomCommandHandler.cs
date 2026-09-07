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

namespace Lobby.Application.Commands.JoinRoom;

/// <summary>
/// Handler that joins the current user to a room.
/// </summary>
public class JoinRoomCommandHandler : IRequestHandler<JoinRoomCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="JoinRoomCommandHandler"/> class.
    /// </summary>
    public JoinRoomCommandHandler(
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
        JoinRoomCommand request,
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

        if (room.GetPlayer(userId) is not null)
        {
            return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            throw new ConflictException("This room is full.");
        }

        var membership = room.AddPlayer(userId, _currentUser.DisplayName ?? "Player");
        _dbContext.RoomPlayers.Add(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.PlayerJoined,
            PlayerId = userId,
            DisplayName = _currentUser.DisplayName,
            PlayerCount = room.Players.Count
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}