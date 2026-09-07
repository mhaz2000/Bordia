namespace Lobby.Domain.Entities;

/// <summary>
/// A player present in a lobby room.
/// </summary>
public class RoomPlayer : BuildingBlocks.Domain.Base.AuditableEntity
{
    private RoomPlayer()
    {
    }

    /// <summary>
    /// Creates a room membership for the specified player.
    /// </summary>
    public static RoomPlayer Create(Room room, Guid userId, string displayName)
    {
        return new RoomPlayer
        {
            RoomId = room.Id,
            UserId = userId,
            DisplayName = displayName,
            IsReady = false,
            JoinedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// The id of the room the player belongs to.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>
    /// The user id of the player.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The display name of the player at the time they joined.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates whether the player is ready to start.
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// UTC timestamp when the player joined.
    /// </summary>
    public DateTime JoinedAt { get; private set; }

    /// <summary>
    /// The SignalR connection id currently associated with the player, if any.
    /// </summary>
    public string? ConnectionId { get; private set; }

    /// <summary>
    /// Sets the player's ready state.
    /// </summary>
    public void SetReady(bool isReady)
    {
        IsReady = isReady;
    }

    /// <summary>
    /// Associates a SignalR connection with this player.
    /// </summary>
    public void SetConnectionId(string? connectionId)
    {
        ConnectionId = connectionId;
    }
}