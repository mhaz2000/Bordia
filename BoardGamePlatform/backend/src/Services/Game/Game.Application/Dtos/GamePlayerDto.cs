namespace Game.Application.Dtos;

/// <summary>
/// A player seat within a game session.
/// </summary>
public class GamePlayerDto
{
    /// <summary>
    /// The user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based seat position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Whether the player is currently connected.
    /// </summary>
    public bool IsConnected { get; set; }
}