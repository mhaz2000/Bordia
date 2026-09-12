using System.Text.Json;

namespace Silver.Models;

/// <summary>
/// Builds localized-friendly event log entries as JSON envelopes:
/// {"c":"code","d":{...params}}. The client renders them in the active
/// language through the events.silver.* dictionary. Entries must stay
/// public-safe: they never contain the value of a hidden card.
/// </summary>
internal static class SilverEvent
{
    /// <summary>Serializes an event code with optional parameters into a log entry.</summary>
    public static string Build(string code, object? data = null)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["c"] = code,
            ["d"] = data
        });
    }
}
