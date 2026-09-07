using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Queries.GetMyRooms;

/// <summary>
/// Fetches the rooms the current user is a member of.
/// </summary>
public record GetMyRoomsQuery : IQuery<List<LobbyRoomDto>>;