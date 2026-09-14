namespace Lobby.Application.Dtos;

/// <summary>
/// A room chat message as delivered to clients.
/// </summary>
public class RoomMessageDto
{
    /// <summary>
    /// The message id (used by clients to dedupe the hub echo against a sent reply).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The room the message belongs to.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The author's user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The author's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The message text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was sent.
    /// </summary>
    public DateTime SentAt { get; set; }
}
