using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Lobby.Application.Dtos;

namespace Lobby.Application.Commands.SendRoomMessage;

/// <summary>
/// Sends a chat message to a room on behalf of the current user.
/// Rate-limited to <see cref="MaxPerMinute"/> messages per user per minute.
/// </summary>
public record SendRoomMessageCommand(Guid RoomId, string Text) : ICommand<RoomMessageDto>
{
    /// <summary>
    /// The maximum number of messages a player may send per minute.
    /// </summary>
    public const int MaxPerMinute = 10;

    /// <summary>
    /// The maximum length of a single message.
    /// </summary>
    public const int MaxTextLength = 300;
}
