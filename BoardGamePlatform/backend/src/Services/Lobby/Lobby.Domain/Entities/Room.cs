using Lobby.Domain.Enums;

namespace Lobby.Domain.Entities;

/// <summary>
/// A lobby room players gather in before starting a game.
/// </summary>
public class Room : BuildingBlocks.Domain.Base.AuditableEntity
{
    private readonly List<RoomPlayer> _players = new();

    private Room()
    {
    }

    /// <summary>
    /// Creates a new room with the generating user as host.
    /// </summary>
    public static Room Create(
        string name,
        string roomCode,
        string gameType,
        int maxPlayers,
        bool isPrivate,
        Guid hostId,
        string hostDisplayName)
    {
        var room = new Room
        {
            Name = name,
            RoomCode = roomCode,
            GameType = gameType,
            MaxPlayers = maxPlayers,
            IsPrivate = isPrivate,
            HostId = hostId,
            Status = RoomStatus.Waiting
        };

        room.AddPlayer(hostId, hostDisplayName);
        room.ChangeHost(hostId);

        return room;
    }

    /// <summary>
    /// The room's display name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The room's short, unique join code (e.g. "K7RM2X").
    /// </summary>
    public string RoomCode { get; private set; } = string.Empty;

    /// <summary>
    /// The game type this room plays (e.g. "Splendor"). Lobby never interprets it.
    /// </summary>
    public string GameType { get; private set; } = string.Empty;

    /// <summary>
    /// Maximum number of players allowed.
    /// </summary>
    public int MaxPlayers { get; private set; }

    /// <summary>
    /// Indicates whether the room is invite-only (private).
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>
    /// Current status of the room.
    /// </summary>
    public RoomStatus Status { get; private set; }

    /// <summary>
    /// The id of the current host.
    /// </summary>
    public Guid HostId { get; private set; }

    /// <summary>
    /// UTC timestamp when the game started, if the room has started.
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>
    /// The id of the game session created when the room started, if any.
    /// </summary>
    public Guid? GameSessionId { get; private set; }

    /// <summary>
    /// The players currently in the room.
    /// </summary>
    public IReadOnlyList<RoomPlayer> Players => _players;

    /// <summary>
    /// The game-specific settings for this room, if any.
    /// </summary>
    public RoomSettings? Settings { get; set; }

    /// <summary>
    /// Sets the room's game-specific settings.
    /// </summary>
    public void SetSettings(RoomSettings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Adds a player to the room.
    /// </summary>
    public RoomPlayer AddPlayer(Guid playerId, string displayName)
    {
        var membership = RoomPlayer.Create(this, playerId, displayName);
        _players.Add(membership);
        return membership;
    }

    /// <summary>
    /// Removes a player from the room.
    /// </summary>
    /// <returns>True if the player was present and removed.</returns>
    public bool RemovePlayer(Guid playerId)
    {
        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
        {
            return false;
        }

        _players.Remove(player);
        return true;
    }

    /// <summary>
    /// Returns a player in the room by user id, or null.
    /// </summary>
    public RoomPlayer? GetPlayer(Guid playerId)
        => _players.FirstOrDefault(p => p.UserId == playerId);

    /// <summary>
    /// Changes the host of the room.
    /// </summary>
    public void ChangeHost(Guid newHostId)
    {
        if (_players.All(p => p.UserId != newHostId))
        {
            throw new InvalidOperationException("The new host must be a member of the room.");
        }

        HostId = newHostId;
    }

    /// <summary>
    /// Marks the room as started and records the created game session.
    /// </summary>
    public void Start(Guid gameSessionId)
    {
        Status = RoomStatus.Started;
        StartedAt = DateTime.UtcNow;
        GameSessionId = gameSessionId;
    }

    /// <summary>
    /// Marks the room as closed.
    /// </summary>
    public void Close()
    {
        Status = RoomStatus.Closed;
    }

    /// <summary>
    /// Copies the player's ready state changes back into the room collection.
    /// </summary>
    public void ApplyReadyState(Guid playerId, bool isReady)
    {
        _players.FirstOrDefault(p => p.UserId == playerId)?.SetReady(isReady);
    }
}