using GameEngine.Core.Models;

namespace GameEngine.Core.Abstractions;

/// <summary>
/// Convenience base class for real-time games (no turn order).
/// Games with simultaneous play or timer-driven rounds derive from this class.
/// It removes turn-based accounting and leaves action ordering purely event-driven.
/// </summary>
public abstract class RealTimeGame : GameBase
{
    /// <summary>
    /// Initializes a new instance with the game's type name.
    /// </summary>
    protected RealTimeGame(string gameType, int minPlayers, int maxPlayers)
        : base(gameType, minPlayers, maxPlayers)
    {
    }

    /// <inheritdoc />
    public override GameState CreateGame(GameOptions options)
    {
        var state = CreateRealTimeGame(options);
        state.CurrentPlayerIndex = null;
        return state;
    }

    /// <summary>
    /// Creates the initial state for a real-time game.
    /// </summary>
    protected abstract GameState CreateRealTimeGame(GameOptions options);
}