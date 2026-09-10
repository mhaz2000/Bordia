using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Domain.Localization;
using Lobby.Application.Common;
using Lobby.Application.Dtos;
using Lobby.Application.Persistence;
using Lobby.Application.Realtime;
using Lobby.Domain.Entities;
using Lobby.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lobby.Application.Commands.StartGame;

/// <summary>
/// Handler that starts a game from a waiting room.
/// </summary>
public class StartGameCommandHandler : IRequestHandler<StartGameCommand, Result<LobbyRoomDto>>
{
    private readonly LobbyDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGameSessionClient _gameSessionClient;
    private readonly IMapper _mapper;
    private readonly IOutbox _outbox;
    private readonly IPublisher _publisher;
    private readonly ILogger<StartGameCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartGameCommandHandler"/> class.
    /// </summary>
    public StartGameCommandHandler(
        LobbyDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGameSessionClient gameSessionClient,
        IMapper mapper,
        IOutbox outbox,
        IPublisher publisher,
        ILogger<StartGameCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _gameSessionClient = gameSessionClient;
        _mapper = mapper;
        _outbox = outbox;
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<LobbyRoomDto>> Handle(
        StartGameCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } hostId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Room), request.RoomId);

        if (room.HostId != hostId)
        {
            throw new UnauthorizedException(ErrorCodes.Lobby.OnlyHostStart);
        }

        if (room.Status != RoomStatus.Waiting)
        {
            throw new ConflictException(ErrorCodes.Lobby.RoomNotStartable);
        }

        if (room.Players.Count < 2)
        {
            throw new ConflictException(ErrorCodes.Lobby.TwoPlayersRequired);
        }

        if (room.Players.Where(p => p.UserId != hostId).Any(p => !p.IsReady))
        {
            throw new ConflictException(ErrorCodes.Lobby.AllMustBeReady);
        }

        Guid gameSessionId;
        try
        {
            var players = room.Players
                .Select(p => new GamePlayerRequest(p.UserId, p.DisplayName))
                .ToList();

            var result = await _gameSessionClient.CreateAsync(
                room.Id,
                room.GameType,
                players,
                cancellationToken);

            gameSessionId = result.SessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create game session for room {RoomId}", room.Id);
            throw new ConflictException(ErrorCodes.Lobby.GameServiceFailed);
        }

        room.Start(gameSessionId);

        await _outbox.AddAsync(new GameStarted
        {
            GameSessionId = gameSessionId,
            RoomId = room.Id,
            GameType = room.GameType
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new LobbyRoomChanged
        {
            RoomId = room.Id,
            ChangeType = RoomChangeType.GameStarted,
            GameSessionId = gameSessionId,
            PlayerCount = room.Players.Count
        }, cancellationToken);

        return Result<LobbyRoomDto>.Success(_mapper.Map<LobbyRoomDto>(room));
    }
}