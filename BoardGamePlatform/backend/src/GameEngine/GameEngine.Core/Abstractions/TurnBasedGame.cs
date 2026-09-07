using GameEngine.Core.Models;

namespace GameEngine.Core.Abstractions;

/// <summary>
/// Convenience base class for turn-based games.
/// Adds current-player tracking and helpers to advance the turn.
/// Assumes <see cref="GameState.CurrentPlayerIndex"/> drives play order.
/// </summary>
public abstract class TurnBasedGame : GameBase
{
    /// <summary>
    /// Initializes a new instance with the game's type name.
    /// </summary>
    protected TurnBasedGame(string gameType, int minPlayers, int maxPlayers)
        : base(gameType, minPlayers, maxPlayers)
    {
    }

    /// <summary>
    /// Returns the player currently taking their turn, or null if unknown.
    /// </summary>
    protected static PlayerId? CurrentPlayer(GameState state)
    {
        if (state.CurrentPlayerIndex is null or < 0)
        {
            return null;
        }

        var index = state.CurrentPlayerIndex.Value;
        return index < state.Players.Count ? state.Players[index] : null;
    }

    /// <summary>
    /// Returns whether the given player is the current player.
    /// Returns false when there is no active current player.
    /// </summary>
    protected static bool NotCurrentPlayer(GameState state, PlayerId playerId)
    {
        var current = CurrentPlayer(state);
        return current is null || !EqualityComparer<PlayerId>.Default.Equals(current.Value, playerId);
    }

    /// <summary>
    /// Advances <see cref="GameState.CurrentPlayerIndex"/> to the next player.
    /// </summary>
    protected static void AdvanceTurn(GameState state)
    {
        if (state.Players.Count == 0)
        {
            return;
        }

        state.CurrentPlayerIndex = ((state.CurrentPlayerIndex ?? 0) + 1) % state.Players.Count;
    }
}