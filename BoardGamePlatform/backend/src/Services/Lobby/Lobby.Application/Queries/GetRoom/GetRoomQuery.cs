using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Queries.GetRoom;

/// <summary>
/// Fetches a single room by id.
/// </summary>
public record GetRoomQuery(Guid RoomId) : IQuery<LobbyRoomDto>;