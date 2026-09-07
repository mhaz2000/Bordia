using MediatR;

namespace Lobby.Application.Realtime;

/// <summary>
/// Notification raised whenever a room changes. Consumed by the Lobby
/// Infrastructure real-time notifier, which pushes updates to the SignalR hub.
/// </summary>
public class LobbyRoomChanged : INotification
{
    /// <summary>
    /// The room that changed.
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// The type of change.
    /// </summary>
    public RoomChangeType ChangeType { get; init; }

    /// <summary>
    /// The affected player, when the change is player-scoped.
    /// </summary>
    public Guid? PlayerId { get; init; }

    /// <summary>
    /// The affected player's display name, when applicable.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The player count at the time of the change.
    /// </summary>
    public int PlayerCount { get; init; }

    /// <summary>
    /// The affected player's new ready state, when applicable.
    /// </summary>
    public bool IsReady { get; init; }

    /// <summary>
    /// The new host id, when the host changed.
    /// </summary>
    public Guid? NewHostId { get; init; }

    /// <summary>
    /// The created game session id, when a game started.
    /// </summary>
    public Guid? GameSessionId { get; init; }
}