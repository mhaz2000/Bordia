using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.KickPlayer;

/// <summary>
/// Kicks a player from a room (host only).
/// </summary>
public record KickPlayerCommand(Guid RoomId, Guid PlayerId) : ICommand<LobbyRoomDto>;