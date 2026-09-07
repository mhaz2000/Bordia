namespace Game.Domain.Entities;

/// <summary>
/// A persisted record of a processed player action for a session.
/// </summary>
public class GameActionLog : BuildingBlocks.Domain.Base.AuditableEntity
{
    private GameActionLog()
    {
    }

    /// <summary>
    /// Records a processed action.
    /// </summary>
    public static GameActionLog Create(
        Guid gameSessionId,
        Guid playerId,
        string actionType,
        string payloadJson,
        long sequenceNumber)
    {
        return new GameActionLog
        {
            GameSessionId = gameSessionId,
            PlayerId = playerId,
            ActionType = actionType,
            PayloadJson = payloadJson,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = sequenceNumber
        };
    }

    /// <summary>
    /// The session the action belongs to.
    /// </summary>
    public Guid GameSessionId { get; private set; }

    /// <summary>
    /// The user who submitted the action.
    /// </summary>
    public Guid PlayerId { get; private set; }

    /// <summary>
    /// The action type, as interpreted by the game.
    /// </summary>
    public string ActionType { get; private set; } = string.Empty;

    /// <summary>
    /// The action payload as JSON.
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the action was accepted.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    /// Monotonic sequence number used to order actions within a session.
    /// </summary>
    public long SequenceNumber { get; private set; }
}