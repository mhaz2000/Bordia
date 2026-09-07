namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a game session finishes.
/// Published by the Game Service.
/// </summary>
public class GameFinished : IntegrationEvent
{
    /// <summary>
    /// The id of the finished game session.
    /// </summary>
    public Guid GameSessionId { get; set; }

    /// <summary>
    /// The id of the room the game belonged to.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Optional id of the winning player, if any.
    /// </summary>
    public Guid? WinnerId { get; set; }
}
