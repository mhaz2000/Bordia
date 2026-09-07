namespace GameEngine.Core.Models;

/// <summary>
/// A noteworthy occurrence that happened while processing an action.
/// Used to drive UI notifications and client-side reactions (e.g. "Player X reserved a card").
/// </summary>
public class GameEvent
{
    /// <summary>
    /// The type of the event, interpreted by the client.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The player the event relates to, if any.
    /// </summary>
    public PlayerId? PlayerId { get; set; }

    /// <summary>
    /// Free-form event payload as JSON.
    /// </summary>
    public string Payload { get; set; } = "{}";
}