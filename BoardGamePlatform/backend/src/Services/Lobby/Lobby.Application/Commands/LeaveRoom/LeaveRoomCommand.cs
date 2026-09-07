using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.LeaveRoom;

/// <summary>
/// Removes the current user from a room.
/// </summary>
public record LeaveRoomCommand(Guid RoomId) : ICommand<LobbyRoomDto>;