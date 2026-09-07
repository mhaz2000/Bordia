using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;

namespace Game.Application.Commands.CreateGameSession;

/// <summary>
/// A player joining a new game session.
/// </summary>
public record CreateGamePlayerCommand(Guid UserId, string DisplayName);

/// <summary>
/// Creates a game session (internal endpoint invoked by the Lobby service).
/// </summary>
public record CreateGameSessionCommand(
    Guid RoomId,
    string GameType,
    IReadOnlyList<CreateGamePlayerCommand> Players) : ICommand<GameSessionDto>;