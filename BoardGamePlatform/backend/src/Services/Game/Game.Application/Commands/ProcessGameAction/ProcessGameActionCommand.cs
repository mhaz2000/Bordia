using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using GameEngine.Core.Models;

namespace Game.Application.Commands.ProcessGameAction;

/// <summary>
/// Submits a player action against a game session.
/// </summary>
public record ProcessGameActionCommand(
    Guid SessionId,
    string ActionType,
    string Payload) : ICommand<GameState>;