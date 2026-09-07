namespace Game.Application.Dtos;

/// <summary>
/// A game session as seen by clients.
/// </summary>
public class GameSessionDto
{
    /// <summary>
    /// The session id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The room id the session was started from.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// The game type.
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// The session status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the session started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the session finished.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// The players in seat order.
    /// </summary>
    public List<GamePlayerDto> Players { get; set; } = new();
}