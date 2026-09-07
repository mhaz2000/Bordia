using System.Text.Json;

namespace GameEngine.Core.Models;

/// <summary>
/// A player action submitted against a game.
/// The action is identified by its type plus a free-form JSON payload interpreted
/// by the implementing game.
/// </summary>
public class GameAction
{
    /// <summary>
    /// The player submitting the action.
    /// </summary>
    public PlayerId PlayerId { get; set; }

    /// <summary>
    /// The type of the action, interpreted by the game (e.g. "TakeTokens", "BuyCard").
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Free-form action payload as JSON.
    /// </summary>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// UTC timestamp when the action was submitted.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Monotonic sequence number used to order actions within a session.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Deserializes the payload into the requested type.
    /// </summary>
    public T? PayloadAs<T>() => JsonSerializer.Deserialize<T>(Payload);
}