namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Raised when a game session starts.
/// Published by the Game Service (or initiated by the Lobby Service's Start Game).
/// </summary>
public class GameStarted : IntegrationEvent
{
    /// <summary>
    /// The id of the started game session.
    /// </summary>
    public Guid GameSessionId { get; set; }

    /// <summary>
    /// The id of the room the game was started from.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The game type being played.
    /// </summary>
    public string GameType { get; set; } = string.Empty;
}
