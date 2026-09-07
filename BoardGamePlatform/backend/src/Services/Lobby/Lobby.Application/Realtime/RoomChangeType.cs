namespace Lobby.Application.Realtime;

/// <summary>
/// Types of room changes broadcast to the waiting room.
/// </summary>
public enum RoomChangeType
{
    /// <summary>
    /// A player joined the room.
    /// </summary>
    PlayerJoined,

    /// <summary>
    /// A player left the room.
    /// </summary>
    PlayerLeft,

    /// <summary>
    /// A player's ready state changed.
    /// </summary>
    PlayerReady,

    /// <summary>
    /// A player's unready state changed.
    /// </summary>
    PlayerUnready,

    /// <summary>
    /// The room host changed.
    /// </summary>
    HostChanged,

    /// <summary>
    /// The room was closed.
    /// </summary>
    RoomClosed,

    /// <summary>
    /// The game started from this room.
    /// </summary>
    GameStarted
}