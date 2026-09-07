using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.CloseRoom;

/// <summary>
/// Closes a waiting room (host only).
/// </summary>
public record CloseRoomCommand(Guid RoomId) : ICommand<LobbyRoomDto>;