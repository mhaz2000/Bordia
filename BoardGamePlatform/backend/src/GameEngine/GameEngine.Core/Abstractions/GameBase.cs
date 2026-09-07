using GameEngine.Core.Models;

namespace GameEngine.Core.Abstractions;

/// <summary>
/// Convenience base class for <see cref="IGame"/> implementations.
/// Provides a <see cref="GameType"/> property and safe defaults for game-over checks.
/// Games are free to override all members; this base only removes boilerplate.
/// </summary>
public abstract class GameBase : IGame
{
    /// <summary>
    /// Initializes a new instance with the game's type name.
    /// </summary>
    protected GameBase(string gameType, int minPlayers, int maxPlayers)
    {
        GameType = gameType;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
    }

    /// <inheritdoc />
    public string GameType { get; }

    /// <inheritdoc />
    public int MinPlayers { get; }

    /// <inheritdoc />
    public int MaxPlayers { get; }

    /// <inheritdoc />
    public abstract GameState CreateGame(GameOptions options);

    /// <inheritdoc />
    public abstract GameResult ProcessAction(GameState state, GameAction action);

    /// <inheritdoc />
    public abstract IReadOnlyList<GameAction> GetValidActions(GameState state, PlayerId playerId);

    /// <inheritdoc />
    public virtual bool IsGameOver(GameState state) => state.IsOver;

    /// <inheritdoc />
    public virtual PlayerId? GetWinner(GameState state) => state.Winner;
}