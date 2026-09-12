namespace BuildingBlocks.Contracts.SignalR;

/// <summary>
/// Defines the set of client-invokable methods exposed by the Lobby hub.
/// Implemented by the SignalR JavaScript/TypeScript client for type-safe calls.
/// </summary>
public interface ILobbyHubClient
{
    /// <summary>
    /// Notifies a player that they joined a room.
    /// </summary>
    Task PlayerJoined(Guid roomId, Guid playerId, string displayName, int playerCount);

    /// <summary>
    /// Notifies a player that they left a room.
    /// </summary>
    Task PlayerLeft(Guid roomId, Guid playerId, int playerCount);

    /// <summary>
    /// Notifies clients that a player's ready state changed.
    /// </summary>
    Task PlayerReadyChanged(Guid roomId, Guid playerId, bool isReady);

    /// <summary>
    /// Notifies clients that the host changed.
    /// </summary>
    Task HostChanged(Guid roomId, Guid newHostId);

    /// <summary>
    /// Notifies clients that the room was closed.
    /// </summary>
    Task RoomClosed(Guid roomId);

    /// <summary>
    /// Notifies clients that the game is starting from this room.
    /// </summary>
    Task GameStarted(Guid roomId, Guid gameSessionId);

    /// <summary>
    /// Notifies clients that a player's connection state changed.
    /// </summary>
    Task PresenceChanged(Guid roomId, Guid playerId, bool isConnected);
}
