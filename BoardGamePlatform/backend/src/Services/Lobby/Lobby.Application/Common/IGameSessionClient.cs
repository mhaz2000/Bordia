namespace Lobby.Application.Common;

/// <summary>
/// Player participating in a game session being created.
/// </summary>
public record GamePlayerRequest(Guid UserId, string DisplayName);

/// <summary>
/// Result of creating a game session in the Game service.
/// </summary>
public record CreateGameSessionResult(Guid SessionId);

/// <summary>
/// Creates game sessions in the Game service when a lobby starts a game.
/// Implemented in Infrastructure as an HTTP client to the Game API.
/// </summary>
public interface IGameSessionClient
{
    /// <summary>
    /// Creates a game session for the given room and players.
    /// </summary>
    Task<CreateGameSessionResult> CreateAsync(
        Guid roomId,
        string gameType,
        IReadOnlyList<GamePlayerRequest> players,
        CancellationToken cancellationToken);
}