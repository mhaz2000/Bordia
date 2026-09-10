using System.Text.Json;

namespace UNO.Models;

/// <summary>
/// Builds localized-friendly event log entries as JSON envelopes:
/// {"c":"code","d":{...params}}. The client renders them in the active
/// language; legacy plain-text entries from older games render as-is.
/// </summary>
internal static class UnoEvent
{
    /// <summary>
    /// Serializes an event code with optional parameters into a log entry.
    /// </summary>
    public static string Build(string code, object? data = null)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["c"] = code,
            ["d"] = data
        });
    }
}
