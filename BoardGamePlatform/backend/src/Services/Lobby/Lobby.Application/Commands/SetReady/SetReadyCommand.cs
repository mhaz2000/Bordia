using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.SetReady;

/// <summary>
/// Sets the current user's ready state in a room.
/// Also used by the <c>unready</c> endpoint with <c>IsReady = false</c>.
/// </summary>
public record SetReadyCommand(Guid RoomId, bool IsReady) : ICommand<LobbyRoomDto>;