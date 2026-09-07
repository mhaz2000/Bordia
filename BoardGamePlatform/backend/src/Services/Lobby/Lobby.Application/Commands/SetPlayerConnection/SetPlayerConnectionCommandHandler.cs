using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.CurrentUser;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.SetPlayerConnection;

/// <summary>
/// Handler that stores the current user's SignalR connection id for a room.
/// </summary>
public class SetPlayerConnectionCommandHandler
    : IRequestHandler<SetPlayerConnectionCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetPlayerConnectionCommandHandler"/> class.
    /// </summary>
    public SetPlayerConnectionCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<LobbyRoomDto>> Handle(
        SetPlayerConnectionCommand request,
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

        room.GetPlayer(userId)?.SetConnectionId(request.ConnectionId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}