using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using GameEngine.Core.Models;

namespace Game.Application.Commands.ReconnectPlayer;

/// <summary>
/// Associates a SignalR connection with the current user's seat in a session.
/// Invoked by the game hub; contains no business rules.
/// </summary>
public record ReconnectPlayerCommand(Guid SessionId, string ConnectionId) : ICommand<GameState>;