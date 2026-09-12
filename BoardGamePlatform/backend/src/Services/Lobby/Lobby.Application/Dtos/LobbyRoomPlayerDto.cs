namespace Lobby.Application.Dtos;

/// <summary>
/// A player inside a lobby room.
/// </summary>
public class LobbyRoomPlayerDto
{
    /// <summary>
    /// The user id of the player.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The display name of the player.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the player is ready.
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// UTC timestamp when the player joined.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Whether the player currently has a live SignalR connection to the room.
    /// </summary>
    public bool IsConnected { get; set; }
}