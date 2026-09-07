using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.TransferHost;

/// <summary>
/// Transfers host privileges to another member of the room (host only).
/// </summary>
public record TransferHostCommand(Guid RoomId, Guid PlayerId) : ICommand<LobbyRoomDto>;