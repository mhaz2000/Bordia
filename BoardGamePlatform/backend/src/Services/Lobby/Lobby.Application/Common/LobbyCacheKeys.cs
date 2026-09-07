namespace Lobby.Application.Common;

/// <summary>
/// Redis cache keys used by the Lobby service.
/// </summary>
public static class LobbyCacheKeys
{
    /// <summary>
    /// Cache key for the list of joinable (waiting) rooms.
    /// </summary>
    public const string RoomsList = "lobby:rooms";

    /// <summary>
    /// Cache key for a single room's DTO.
    /// </summary>
    public static string Room(Guid roomId) => $"lobby:room:{roomId}";

    /// <summary>
    /// Cache key for a user's open rooms.
    /// </summary>
    public static string MyRooms(Guid userId) => $"lobby:my-rooms:{userId}";
}