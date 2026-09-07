namespace Lobby.Domain.Enums;

/// <summary>
/// Lifecycle status of a lobby room.
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// The room is open and players can join.
    /// </summary>
    Waiting,

    /// <summary>
    /// The game has started; the room is no longer joinable.
    /// </summary>
    Started,

    /// <summary>
    /// The room has been closed.
    /// </summary>
    Closed
}