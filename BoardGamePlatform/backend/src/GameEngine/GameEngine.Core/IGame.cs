using GameEngine.Core.Models;

namespace GameEngine.Core;

/// <summary>
/// Contract every board game implements.
/// Implementations are pure business logic: no HTTP, SignalR, EF Core, PostgreSQL,
/// Redis, or RabbitMQ dependencies. They are registered via the DI container.
/// </summary>
public interface IGame
{
    /// <summary>
    /// The name identifying this game (e.g. "Splendor").
    /// </summary>
    string GameType { get; }

    /// <summary>
    /// The minimum number of players a room for this game can host.
    /// </summary>
    int MinPlayers { get; }

    /// <summary>
    /// The maximum number of players a room for this game can host.
    /// </summary>
    int MaxPlayers { get; }

    /// <summary>
    /// Creates a new game in its initial state.
    /// </summary>
    /// <param name="options">The configuration for the new game.</param>
    GameState CreateGame(GameOptions options);

    /// <summary>
    /// Processes a player action against the current state and returns the result.
    /// The input state is never mutated; a new state is returned when the action is valid.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <param name="action">The player action to process.</param>
    GameResult ProcessAction(GameState state, GameAction action);

    /// <summary>
    /// Returns the actions a given player may currently take.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <param name="playerId">The player whose valid actions are requested.</param>
    IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId);

    /// <summary>
    /// Indicates whether the game has ended.
    /// </summary>
    bool IsGameOver(GameState state);

    /// <summary>
    /// Returns the winning player, or null if the game is not over / has no winner.
    /// </summary>
    PlayerId? GetWinner(GameState state);
}