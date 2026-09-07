namespace Game.Domain.Entities;

/// <summary>
/// A player seat within a game session.
/// </summary>
public class GamePlayer : BuildingBlocks.Domain.Base.AuditableEntity
{
    private GamePlayer()
    {
    }

    /// <summary>
    /// Creates a player seat for a session.
    /// </summary>
    public static GamePlayer Create(
        GameSession session,
        Guid userId,
        string displayName,
        int position)
    {
        return new GamePlayer
        {
            GameSessionId = session.Id,
            UserId = userId,
            DisplayName = displayName,
            Position = position,
            IsConnected = false
        };
    }

    /// <summary>
    /// The session this seat belongs to.
    /// </summary>
    public Guid GameSessionId { get; private set; }

    /// <summary>
    /// The user id occupying the seat.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The display name of the player.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Zero-based seat order.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Whether the player currently has an active SignalR connection.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// The current SignalR connection id for this seat, if any.
    /// </summary>
    public string? ConnectionId { get; private set; }

    /// <summary>
    /// Associates a SignalR connection with this seat.
    /// </summary>
    public void SetConnection(string connectionId)
    {
        ConnectionId = connectionId;
        IsConnected = true;
    }

    /// <summary>
    /// Clears the seat's SignalR connection.
    /// </summary>
    public void ClearConnection()
    {
        ConnectionId = null;
        IsConnected = false;
    }
}