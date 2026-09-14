using Lobby.Application.Dtos;
using MediatR;

namespace Lobby.Application.Realtime;

/// <summary>
/// Notification raised when a chat message is sent to a room. Consumed by the
/// Lobby Infrastructure real-time notifier, which pushes it to the room group.
/// </summary>
public class RoomChatMessageSent : INotification
{
    /// <summary>
    /// The room the message belongs to.
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// The message payload delivered to clients.
    /// </summary>
    public RoomMessageDto Message { get; init; } = new();
}
