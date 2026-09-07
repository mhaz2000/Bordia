using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.KickPlayer;

/// <summary>
/// Handler that removes a member from a room on behalf of the host.
/// </summary>
public class KickPlayerCommandHandler : IRequestHandler<KickPlayerCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="KickPlayerCommandHandler"/> class.
    /// </summary>
    public KickPlayerCommandHandler(
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
        KickPlayerCommand request,
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
            throw new UnauthorizedAccessException("Only the host can kick players.");
        }

        if (request.PlayerId == hostId)
        {
            throw new ConflictException("The host cannot kick themselves.");
        }

        if (room.GetPlayer(request.PlayerId) is null)
        {
            throw new ConflictException("The player is not a member of this room.");
        }

        room.RemovePlayer(request.PlayerId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.PlayerLeft,
            PlayerId = request.PlayerId,
            PlayerCount = room.Players.Count
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}