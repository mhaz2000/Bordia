using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Game.Application.Dtos;

namespace Game.Application.Commands.PauseGame;

/// <summary>
/// Pauses an active game session.
/// </summary>
public record PauseGameCommand(Guid SessionId) : ICommand<GameSessionDto>;