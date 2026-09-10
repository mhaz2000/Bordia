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
    /// Kept in sync with <see cref="ErrorCode"/>/<see cref="ErrorArgs"/> for
    /// logging and debugging; clients should prefer the code for localization.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Stable error code (dot-separated, e.g. "uno.notYourTurn") when the action
    /// is invalid. Null on success. Codes are part of the platform contract:
    /// the backend localizes them at the request boundary via Accept-Language,
    /// so adding a new code requires adding its templates to the server catalog.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Localization arguments for <see cref="ErrorCode"/> (e.g. draw counts).
    /// </summary>
    public object?[] ErrorArgs { get; set; } = Array.Empty<object?>();

    /// <summary>
    /// Indicates whether the game ended as a result of this action.
    /// </summary>
    public bool GameEnded { get; set; }
}