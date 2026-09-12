using GameEngine.Core.Models;

namespace GameEngine.Core;

/// <summary>
/// Optional capability for games that contain hidden information. Implementations
/// return the state as seen by a single viewer: public information plus the
/// viewer's own private information and knowledge, with every secret belonging
/// to other players removed.
/// </summary>
/// <remarks>
/// The Game Service projects per-viewer states through this interface when the
/// resolved engine implements it (state queries, action responses, reconnect
/// responses, and per-connection real-time pushes). Engines that do not
/// implement it keep serving their full state exactly as before.
/// </remarks>
public interface IPlayerViewGame
{
    /// <summary>
    /// Returns the state as seen by <paramref name="viewer"/>: public info plus
    /// the viewer's own private info and knowledge. Secrets belonging to others
    /// are removed. The authoritative state is never mutated.
    /// </summary>
    /// <param name="authoritativeState">The full game state.</param>
    /// <param name="viewer">The player whose view is requested.</param>
    GameState GetPlayerView(GameState authoritativeState, PlayerId viewer);
}
