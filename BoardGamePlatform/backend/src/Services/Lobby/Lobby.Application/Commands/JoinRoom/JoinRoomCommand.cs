using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.JoinRoom;

/// <summary>
/// Adds the current user to a room, addressed by id or by its public room
/// code. Code joins are how private rooms are entered (by invitation), so
/// <see cref="AllowPrivate"/> is set on that path; joining a private room by
/// id is rejected for non-members.
/// </summary>
public record JoinRoomCommand(Guid? RoomId = null, string? Code = null, bool AllowPrivate = false)
    : ICommand<LobbyRoomDto>;