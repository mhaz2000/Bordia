namespace Lobby.Application.Dtos;

/// <summary>
/// A lobby room as seen by clients.
/// </summary>
public class LobbyRoomDto
{
    /// <summary>
    /// The room id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The room name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The room's unique join code.
    /// </summary>
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>
    /// The game type this room plays.
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of players.
    /// </summary>
    public int MaxPlayers { get; set; }

    /// <summary>
    /// Whether the room is private.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// The room's current status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The id of the host player.
    /// </summary>
    public Guid HostId { get; set; }

    /// <summary>
    /// The players currently in the room.
    /// </summary>
    public List<LobbyRoomPlayerDto> Players { get; set; } = new();

    /// <summary>
    /// The game session id, if the room has started a game.
    /// </summary>
    public Guid? GameSessionId { get; set; }
}