using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Infrastructure.CurrentUser;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Common;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lobby.Application.Commands.CreateRoom;

/// <summary>
/// Handler that creates a room and publishes the <c>RoomCreated</c> integration event.
/// </summary>
public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IOutbox _outbox;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoomCommandHandler"/> class.
    /// </summary>
    public CreateRoomCommandHandler(
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
        CreateRoomCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var displayName = _currentUser.DisplayName ?? "Player";

        var roomCode = await GenerateUniqueRoomCodeAsync(_dbContext, cancellationToken);

        var room = Room.Create(
            request.Name,
            roomCode,
            request.GameType,
            request.MaxPlayers,
            request.IsPrivate,
            userId,
            displayName);

        if (!string.IsNullOrWhiteSpace(request.SettingsJson))
        {
            room.SetSettings(RoomSettings.Create(room.Id, request.SettingsJson));
        }

        _dbContext.Rooms.Add(room);

        await _outbox.AddAsync(new RoomCreated
        {
            RoomId = room.Id,
            GameType = room.GameType,
            HostId = room.HostId
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.PlayerJoined,
            PlayerId = userId,
            DisplayName = displayName,
            PlayerCount = room.Players.Count
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }

    private static async Task<string> GenerateUniqueRoomCodeAsync(
        LobbyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = RoomCodeGenerator.Generate();
            var taken = await dbContext.Rooms
                .AsNoTracking()
                .AnyAsync(r => r.RoomCode == code, cancellationToken);

            if (!taken)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique room code.");
    }
}