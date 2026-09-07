namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a player leaves a room in the Lobby Service.
/// </summary>
public class PlayerLeftRoom : IntegrationEvent
{
    /// <summary>
    /// The id of the room the player left.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The id of the player who left.
    /// </summary>
    public Guid PlayerId { get; set; }
}
