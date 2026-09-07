using System.Text.Json;

namespace GameEngine.Core.Models;

/// <summary>
/// The full state of an in-progress game.
/// Designed to be JSON-serializable without circular references so it can be stored
/// directly in the database and sent over SignalR. Game-specific fields live in
/// <see cref="Data"/>; platform-level metadata lives on dedicated properties.
/// </summary>
public class GameState
{
    /// <summary>
    /// The identifier of the game session this state belongs to.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// The game type (e.g. "Splendor").
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// The players in the game, in seat order.
    /// </summary>
    public IReadOnlyList<PlayerId> Players { get; set; } = [];

    /// <summary>
    /// The index of the player whose turn it currently is. Null in non-turn-based games.
    /// </summary>
    public int? CurrentPlayerIndex { get; set; }

    /// <summary>
    /// Indicates whether the game is over.
    /// </summary>
    public bool IsOver { get; set; }

    /// <summary>
    /// The id of the winning player, when <see cref="IsOver"/> is true.
    /// </summary>
    public PlayerId? Winner { get; set; }

    /// <summary>
    /// A monotonically increasing revision number incremented on every state change.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// UTC deadline by which the current player must submit an action, when the game
    /// enforces turn timers. <see langword="null"/> means the game does not currently
    /// enforce a deadline. The Game Service polls this field to emit turn timeouts,
    /// so it must stay a plain, game-agnostic value on the state root.
    /// </summary>
    public DateTime? NextActionDeadlineUtc { get; set; }

    /// <summary>
    /// Dictionary of game-specific state, keyed by game-defined names.
    /// Values must be JSON-serializable and contain no circular references.
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new();

    /// <summary>
    /// Serializes this state to JSON for persistence or transport.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserializes a JSON-encoded game state.
    /// </summary>
    public static GameState FromJson(string json)
        => JsonSerializer.Deserialize<GameState>(json) ?? new GameState();

    /// <summary>
    /// Tries to read a string value from <see cref="Data"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Data"/> is a <see cref="Dictionary{TKey,TValue}"/> with
    /// <c>object?</c> values, so values written as JSON strings are sometimes
    /// materialized by System.Text.Json as <see cref="JsonElement"/> instead of as
    /// a plain <see cref="string"/> during a serialize/deserialize round-trip.
    /// This member handles both representations so games can read their payloads
    /// reliably regardless of where the state came from.
    /// </remarks>
    public bool TryGetString(string key, out string? value)
    {
        if (Data.TryGetValue(key, out var raw) && raw is not null)
        {
            switch (raw)
            {
                case string str:
                    value = str;
                    return true;
                case JsonElement element:
                    value = element.ValueKind == JsonValueKind.String
                        ? element.GetString()
                        : element.GetRawText();
                    return true;
                default:
                    value = raw.ToString();
                    return true;
            }
        }

        value = null;
        return false;
    }
}