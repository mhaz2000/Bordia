using Game.Domain.Enums;

namespace Game.Domain.Entities;

/// <summary>
/// A running game session, decoupled from the lobby room it originated from.
/// </summary>
public class GameSession : BuildingBlocks.Domain.Base.AuditableEntity
{
    private readonly List<GamePlayer> _players = new();

    private GameSession()
    {
    }

    /// <summary>
    /// Creates a game session in the Active state.
    /// </summary>
    public static GameSession Create(
        Guid roomId,
        string gameType,
        string initialStateJson,
        IReadOnlyList<(Guid UserId, string DisplayName)> players)
    {
        var session = new GameSession
        {
            RoomId = roomId,
            GameType = gameType,
            Status = GameSessionStatus.Active,
            CurrentStateJson = initialStateJson,
            StartedAt = DateTime.UtcNow
        };

        for (var i = 0; i < players.Count; i++)
        {
            session._players.Add(GamePlayer.Create(
                session,
                players[i].UserId,
                players[i].DisplayName,
                i));
        }

        return session;
    }

    /// <summary>
    /// The id of the lobby room this session was started from.
    /// </summary>
    public Guid RoomId { get; private set; }

    /// <summary>
    /// The type of game being played (e.g. "Splendor").
    /// </summary>
    public string GameType { get; private set; } = string.Empty;

    /// <summary>
    /// The current lifecycle status.
    /// </summary>
    public GameSessionStatus Status { get; private set; }

    /// <summary>
    /// The current game state, serialized as JSON by the Game Engine.
    /// </summary>
    public string CurrentStateJson { get; private set; } = "{}";

    /// <summary>
    /// UTC timestamp when the session started.
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the session finished, if finished.
    /// </summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>
    /// The players in the session, in seat order.
    /// </summary>
    public IReadOnlyList<GamePlayer> Players => _players;

    /// <summary>
    /// Replaces the serialized game state after an action is processed.
    /// </summary>
    public void ApplyState(string stateJson)
    {
        CurrentStateJson = stateJson;
    }

    /// <summary>
    /// Pauses an active session.
    /// </summary>
    public void Pause()
    {
        if (Status == GameSessionStatus.Active)
        {
            Status = GameSessionStatus.Paused;
        }
    }

    /// <summary>
    /// Resumes a paused session.
    /// </summary>
    public void Resume()
    {
        if (Status == GameSessionStatus.Paused)
        {
            Status = GameSessionStatus.Active;
        }
    }

    /// <summary>
    /// Marks an active or paused session as finished.
    /// </summary>
    public void Finish()
    {
        if (Status != GameSessionStatus.Finished)
        {
            Status = GameSessionStatus.Finished;
            FinishedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Returns a player seat by user id, or null.
    /// </summary>
    public GamePlayer? GetPlayer(Guid userId)
        => _players.FirstOrDefault(p => p.UserId == userId);
}