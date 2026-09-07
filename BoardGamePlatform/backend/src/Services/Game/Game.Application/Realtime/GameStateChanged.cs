using GameEngine.Core.Models;

namespace Game.Application.Realtime;

/// <summary>
/// Real-time events pushed to game hub clients.
/// </summary>
public enum GameChangeType
{
    /// <summary>
    /// The game state changed for all players.
    /// </summary>
    StateUpdated,

    /// <summary>
    /// A player submitted an accepted action.
    /// </summary>
    ActionProcessed,

    /// <summary>
    /// A player's connection status changed.
    /// </summary>
    PlayerConnectionChanged,

    /// <summary>
    /// The game finished.
    /// </summary>
    GameFinished,

    /// <summary>
    /// A player's older connection was superseded by a newer one in the same session.
    /// </summary>
    PlayerTakenOver
}

/// <summary>
/// Notification raised whenever a game state changes. Consumed by the Game
/// Infrastructure real-time notifier, which pushes updates to the game hub.
/// </summary>
public class GameStateChanged : MediatR.INotification
{
    /// <summary>
    /// The session that changed.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// The type of change.
    /// </summary>
    public GameChangeType ChangeType { get; init; }

    /// <summary>
    /// The full game state, pushed on state updates.
    /// </summary>
    public GameState? State { get; init; }

    /// <summary>
    /// The user who submitted an action, when applicable.
    /// </summary>
    public Guid? PlayerId { get; init; }

    /// <summary>
    /// The action type that was processed, when applicable.
    /// </summary>
    public string? ActionType { get; init; }

    /// <summary>
    /// Whether the connection changed to connected.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// The winner, when the game finished.
    /// </summary>
    public Guid? WinnerId { get; init; }

    /// <summary>
    /// A SignalR connection id for one-to-one notifications, used by
    /// <see cref="GameChangeType.PlayerTakenOver"/> to address the superseded client.
    /// </summary>
    public string? ConnectionId { get; init; }
}