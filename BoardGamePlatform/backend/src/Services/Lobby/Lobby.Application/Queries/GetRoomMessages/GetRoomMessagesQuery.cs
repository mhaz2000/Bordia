using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Queries.GetRoomMessages;

/// <summary>
/// Loads the most recent chat messages of a room (chronological order).
/// </summary>
public record GetRoomMessagesQuery(Guid RoomId, int Take = 50) : IQuery<List<RoomMessageDto>>;
