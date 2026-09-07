using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Queries.GetRoomList;

/// <summary>
/// Fetches the list of rooms currently open for joining.
/// </summary>
public record GetRoomListQuery : IQuery<List<LobbyRoomDto>>;