namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a room is closed in the Lobby Service.
/// </summary>
public class RoomClosed : IntegrationEvent
{
    /// <summary>
    /// The id of the closed room.
    /// </summary>
    public Guid RoomId { get; set; }
}
