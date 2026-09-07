using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.SetPlayerConnection;

/// <summary>
/// Associates a SignalR connection id with the current user's seat in a room.
/// Invoked by the lobby hub; contains no business rules.
/// </summary>
public record SetPlayerConnectionCommand(Guid RoomId, string ConnectionId) : ICommand<LobbyRoomDto>;