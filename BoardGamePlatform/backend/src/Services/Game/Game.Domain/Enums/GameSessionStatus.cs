namespace Game.Domain.Enums;

/// <summary>
/// Lifecycle status of a game session.
/// </summary>
public enum GameSessionStatus
{
    /// <summary>
    /// The game is running and accepting actions.
    /// </summary>
    Active,

    /// <summary>
    /// The game is paused; actions are rejected.
    /// </summary>
    Paused,

    /// <summary>
    /// The game has finished.
    /// </summary>
    Finished
}