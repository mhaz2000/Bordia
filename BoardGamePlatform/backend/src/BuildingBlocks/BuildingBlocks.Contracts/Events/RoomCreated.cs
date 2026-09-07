namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a room is created in the Lobby Service.
/// </summary>
public class RoomCreated : IntegrationEvent
{
    /// <summary>
    /// The id of the created room.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The id of the user hosting the room.
    /// </summary>
    public Guid HostId { get; set; }

    /// <summary>
    /// The game type the room is configured for.
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the host.
    /// </summary>
    public string HostDisplayName { get; set; } = string.Empty;
}
