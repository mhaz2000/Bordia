namespace GameEngine.Core.Models;

/// <summary>
/// Game-type-specific configuration used to create a new game.
/// The payload is interpreted by the implementing game (number of players, variants, etc.).
/// </summary>
public class GameOptions
{
    /// <summary>
    /// The identifier of the game type being created (e.g. "Splendor").
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// The players joining the new game, in seat order.
    /// </summary>
    public IReadOnlyList<PlayerId> Players { get; set; } = [];

    /// <summary>
    /// Free-form, game-specific settings stored as JSON.
    /// </summary>
    public string Settings { get; set; } = "{}";
}