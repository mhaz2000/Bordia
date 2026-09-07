namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a player joins a room in the Lobby Service.
/// </summary>
public class PlayerJoinedRoom : IntegrationEvent
{
    /// <summary>
    /// The id of the room the player joined.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The id of the player who joined.
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// The display name of the joining player.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
