namespace Lobby.Domain.Entities;

/// <summary>
/// Game-specific settings for a room, stored as opaque JSON.
/// The Lobby Service never interprets the payload; messages stay free.
/// </summary>
public class RoomSettings : BuildingBlocks.Domain.Base.AuditableEntity
{
    private RoomSettings()
    {
    }

    /// <summary>
    /// Creates settings for the specified room.
    /// </summary>
    public static RoomSettings Create(Guid roomId, string json)
    {
        return new RoomSettings
        {
            RoomId = roomId,
            Json = json
        };
    }

    /// <summary>
    /// The id of the room these settings belong to.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>
    /// The settings payload serialized as JSON.
    /// </summary>
    public string Json { get; private set; } = string.Empty;
}