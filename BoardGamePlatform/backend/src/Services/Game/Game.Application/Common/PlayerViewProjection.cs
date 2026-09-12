using GameEngine.Core;
using GameEngine.Core.Models;

namespace Game.Application.Common;

/// <summary>
/// Projects game states per viewer when the resolved engine supports hidden
/// information (<see cref="IPlayerViewGame"/>). Engines that do not implement
/// the capability keep serving their full state exactly as before.
/// </summary>
public static class PlayerViewProjection
{
    /// <summary>
    /// Returns the state as seen by <paramref name="viewer"/> when the engine
    /// implements <see cref="IPlayerViewGame"/>; otherwise the unchanged state.
    /// </summary>
    public static GameState Project(IGame engine, GameState state, Guid viewerUserId)
    {
        if (state is null)
        {
            return state!;
        }

        return engine is IPlayerViewGame viewGame
            ? viewGame.GetPlayerView(state, new PlayerId(viewerUserId))
            : state;
    }
}
