namespace Game.Application.Common;

/// <summary>
/// Redis cache keys used by the Game service.
/// </summary>
public static class GameCacheKeys
{
    /// <summary>
    /// Cache key for a session's serialized game state.
    /// </summary>
    public static string State(Guid sessionId) => $"game:state:{sessionId}";
}