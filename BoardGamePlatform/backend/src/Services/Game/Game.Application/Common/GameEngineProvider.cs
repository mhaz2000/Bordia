using GameEngine.Core;

namespace Game.Application.Common;

/// <summary>
/// Resolves games from the set of DI-registered <see cref="IGame"/> implementations.
/// </summary>
public class GameEngineProvider : IGameEngineProvider
{
    private readonly IEnumerable<IGame> _games;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEngineProvider"/> class.
    /// </summary>
    public GameEngineProvider(IEnumerable<IGame> games)
    {
        _games = games;
    }

    /// <inheritdoc />
    public IGame Get(string gameType)
    {
        return _games.FirstOrDefault(g => g.GameType == gameType)
            ?? throw new KeyNotFoundException($"No game registered for game type '{gameType}'.");
    }
}