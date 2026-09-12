using BuildingBlocks.Application.CQRS;

namespace Lobby.Application.Commands.ClearPlayerConnection;

/// <summary>
/// Clears the stored SignalR connection for every seat that held the given
/// connection id, so presence indicators go offline when a client disconnects.
/// Invoked by the lobby hub's OnDisconnectedAsync; contains no business rules.
/// </summary>
public record ClearPlayerConnectionCommand(string ConnectionId) : ICommand;
