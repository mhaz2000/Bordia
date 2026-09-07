namespace BuildingBlocks.Contracts.SignalR;

/// <summary>
/// Defines the set of client-invokable methods exposed by the Game hub.
/// Implemented by the SignalR JavaScript/TypeScript client for type-safe calls.
/// </summary>
public interface IGameHubClient
{
    /// <summary>
    /// Notifies clients that the game state changed.
    /// </summary>
    Task GameStateUpdated(Guid gameSessionId, object state);

    /// <summary>
    /// Notifies clients that a player action was processed.
    /// </summary>
    Task ActionProcessed(Guid gameSessionId, Guid playerId, string actionType);

    /// <summary>
    /// Notifies clients that a player's connection status changed.
    /// </summary>
    Task PlayerConnectionChanged(Guid gameSessionId, Guid playerId, bool isConnected);

    /// <summary>
    /// Notifies clients that the game finished.
    /// </summary>
    Task GameFinished(Guid gameSessionId, Guid? winnerId);

    /// <summary>
    /// Notifies a single client that it has been superseded by a newer connection
    /// for the same player (single-tab enforcement). The receiver should stop
    /// listening and redirect away from the session.
    /// </summary>
    Task SessionTakenOver(Guid gameSessionId, Guid playerId);
}
