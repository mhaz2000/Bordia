using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.JoinRoom;

/// <summary>
/// Adds the current user to a room.
/// </summary>
public record JoinRoomCommand(Guid RoomId) : ICommand<LobbyRoomDto>;