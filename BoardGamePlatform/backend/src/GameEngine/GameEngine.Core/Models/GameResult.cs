namespace GameEngine.Core.Models;

/// <summary>
/// The outcome of processing a player action.
/// </summary>
public class GameResult
{
    /// <summary>
    /// Indicates whether the action was accepted and applied.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The updated game state after processing a valid action.
    /// May be equal to the input state when the action was invalid.
    /// </summary>
    public GameState? NewState { get; set; }

    /// <summary>
    /// Events describing what happened during action processing.
    /// Empty when the action was invalid.
    /// </summary>
    public IReadOnlyList<GameEvent> Events { get; set; } = [];

    /// <summary>
    /// Human-readable error when the action is invalid. Null on success.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Indicates whether the game ended as a result of this action.
    /// </summary>
    public bool GameEnded { get; set; }
}