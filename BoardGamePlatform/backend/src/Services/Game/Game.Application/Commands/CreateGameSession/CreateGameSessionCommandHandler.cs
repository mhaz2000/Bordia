using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Outbox;
using Game.Application.Common;
using Game.Application.Dtos;
using Game.Application.Persistence;
using Game.Application.Realtime;
using Game.Domain.Entities;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Game.Application.Commands.CreateGameSession;

/// <summary>
/// Handler that creates a game session by asking the Game Engine for the initial state.
/// </summary>
public class CreateGameSessionCommandHandler : IRequestHandler<CreateGameSessionCommand, Result<GameSessionDto>>
{
    private readonly GameDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameEngineProvider _engineProvider;
    private readonly IMapper _mapper;
    private readonly IOutbox _outbox;
    private readonly IPublisher _publisher;
    private readonly ILogger<CreateGameSessionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateGameSessionCommandHandler"/> class.
    /// </summary>
    public CreateGameSessionCommandHandler(
        GameDbContext dbContext,
        IUnitOfWork unitOfWork,
        IGameEngineProvider engineProvider,
        IMapper mapper,
        IOutbox outbox,
        IPublisher publisher,
        ILogger<CreateGameSessionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _engineProvider = engineProvider;
        _mapper = mapper;
        _outbox = outbox;
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<GameSessionDto>> Handle(
        CreateGameSessionCommand request,
        CancellationToken cancellationToken)
    {
        var engine = _engineProvider.Get(request.GameType);

        var playerDict = request.Players
            .ToDictionary(p => p.UserId, p => p.DisplayName);

        var players = request.Players
            .Select(p => (p.UserId, p.DisplayName))
            .ToList();

        var options = new GameOptions
        {
            GameType = request.GameType,
            Players = request.Players
                .Select(p => new PlayerId(p.UserId))
                .ToList(),
            Settings = System.Text.Json.JsonSerializer.Serialize(new { PlayerNames = playerDict })
        };

        var session = GameSession.Create(
            request.RoomId,
            request.GameType,
            "{}",
            players);

        var initialState = engine.CreateGame(options);
        initialState.SessionId = session.Id;
        session.ApplyState(initialState.ToJson());

        _dbContext.GameSessions.Add(session);

        await _outbox.AddAsync(new GameStarted
        {
            GameSessionId = session.Id,
            RoomId = request.RoomId,
            GameType = session.GameType
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new GameStateChanged
        {
            SessionId = session.Id,
            ChangeType = GameChangeType.StateUpdated,
            State = initialState,
            GameType = session.GameType
        }, cancellationToken);

        _logger.LogInformation("Game session {SessionId} created for {GameType}",
            session.Id, session.GameType);

        return Result<GameSessionDto>.Success(_mapper.Map<GameSessionDto>(session));
    }
}