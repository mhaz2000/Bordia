using GameEngine.Core;

namespace Game.Application.Common;

/// <summary>
/// Resolves the engine implementation for a given game type.
/// </summary>
public interface IGameEngineProvider
{
    /// <summary>
    /// Returns the <see cref="IGame"/> implementation for the requested game type.
    /// </summary>
    /// <exception cref="KeyNotFoundException">When no game is registered for the type.</exception>
    IGame Get(string gameType);
}