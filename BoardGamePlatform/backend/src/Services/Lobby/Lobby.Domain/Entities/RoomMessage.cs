namespace Lobby.Domain.Entities;

/// <summary>
/// A chat message sent by a player in a room (waiting room and the game
/// started from it). Messages are append-only.
/// </summary>
public class RoomMessage : BuildingBlocks.Domain.Base.AuditableEntity
{
    private RoomMessage()
    {
    }

    /// <summary>
    /// Creates a chat message for a room.
    /// </summary>
    public static RoomMessage Create(Guid roomId, Guid userId, string displayName, string text)
    {
        return new RoomMessage
        {
            RoomId = roomId,
            UserId = userId,
            DisplayName = displayName,
            Text = text,
            SentAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// The room the message was sent in.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>
    /// The author's user id.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The author's display name at the time of sending.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// The message text.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was sent.
    /// </summary>
    public DateTime SentAt { get; private set; }
}
