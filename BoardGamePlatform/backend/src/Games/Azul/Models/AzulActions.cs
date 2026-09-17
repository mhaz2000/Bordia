using System.Text.Json.Serialization;

namespace Azul.Models;

/// <summary>
/// Azul action types, dispatched by <c>AzulGame.ProcessAction</c>. Every player
/// action is atomic: it names the draft source, the color, and the destination
/// pattern line (or the floor sentinel) so the engine resolves the whole turn
/// in one pass — there is deliberately no separate "place" action (spec §19).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AzulActionType
{
    /// <summary>Take all tiles of one color from a factory; the factory's rest move to the center; place the taken tiles into one pattern line (§8.1).</summary>
    DraftFromFactory,

    /// <summary>Take all tiles of one color from the center; the first such draft of the round also takes the first-player marker (§8.2).</summary>
    DraftFromCenter,

    /// <summary>Take only the first-player marker as the whole turn, when it is in the center (spec §15 / OD-12).</summary>
    TakeFirstPlayer,

    /// <summary>System action: the current player's turn timed out (skips the turn; never auto-drafts, spec §24).</summary>
    TurnTimeout,

    /// <summary>System action: the total game time limit expired (force-finish, spec §24).</summary>
    GameTimeExpired
}

/// <summary>Sentinel <c>LineIndex</c> value meaning "draft everything to the floor" (legal only when no pattern line can take the color, §8.3).</summary>
public static class AzulFloorSentinel
{
    /// <summary>The <see cref="DraftFromFactoryPayload.LineIndex"/> / center value that routes the draft entirely to the floor line.</summary>
    public const int Value = -1;
}

/// <summary>Payload for <see cref="AzulActionType.DraftFromFactory"/>.</summary>
public sealed class DraftFromFactoryPayload
{
    /// <summary>The factory display index (0-based, within the active factory count).</summary>
    public int FactoryIndex { get; set; } = -1;

    /// <summary>The color to draft (0..4), as a name or digit.</summary>
    public string? Color { get; set; }

    /// <summary>Destination pattern line (0..4), or <see cref="AzulFloorSentinel.Value"/> to floor everything.</summary>
    public int LineIndex { get; set; } = -2;
}

/// <summary>Payload for <see cref="AzulActionType.DraftFromCenter"/>.</summary>
public sealed class DraftFromCenterPayload
{
    /// <summary>The color to draft from the center (0..4), as a name or digit.</summary>
    public string? Color { get; set; }

    /// <summary>Destination pattern line (0..4), or <see cref="AzulFloorSentinel.Value"/> to floor everything.</summary>
    public int LineIndex { get; set; } = -2;
}

/// <summary>
/// Builds localized-friendly event log entries as JSON envelopes:
/// {"c":"code","d":{...params}}. The client renders them in the active
/// language through the events.azul.* dictionary. Entries are public-safe:
/// they never expose the bag draw order (only counts), and color params are
/// canonical names resolved through the color name map (spec §23).
/// </summary>
internal static class AzulEvent
{
    /// <summary>Serializes an event code with optional parameters into a log entry.</summary>
    public static string Build(string code, object? data = null)
    {
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["c"] = code,
            ["d"] = data
        });
    }
}

/// <summary>
/// Internal carrier for the outcome of processing one Azul action. Player
/// processors mutate the state and record their events; the shared pipeline in
/// <c>AzulGame.ProcessAction</c> then performs the round-end (tiling) decision,
/// advances play, and applies the timer based on the flags below.
/// </summary>
internal sealed class AzulActionResult
{
    public bool IsValid { get; set; }

    public string? Error { get; set; }

    public object?[] ErrorArgs { get; set; } = Array.Empty<object?>();

    /// <summary>Public-safe event entries produced by this action (appended to the log at the end).</summary>
    public List<string> Events { get; } = new();

    /// <summary>Whether the action completed a turn.</summary>
    public bool TurnEnded { get; set; }

    /// <summary>True for player actions (bank time then start the next clock); false for system timeouts (overrun already charged).</summary>
    public bool IsTurnAction { get; set; }

    /// <summary>Set when the action itself ended the game (time expiry, 2-player AFK, or the round-end tiling trigger).</summary>
    public bool GameEnded { get; set; }

    /// <summary>Winning seat when the game ended, or -1 (a -1 with GameEnded and no shared flag means no winner).</summary>
    public int WinnerSeat { get; set; } = -1;

    /// <summary>Whether the game ended in a shared victory (null winner).</summary>
    public bool SharedVictory { get; set; }
}
