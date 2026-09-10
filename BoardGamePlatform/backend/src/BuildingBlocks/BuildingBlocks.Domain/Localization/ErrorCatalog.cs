using System.Globalization;
using System.Text.Json;

namespace BuildingBlocks.Domain.Localization;

/// <summary>
/// Server-side catalog of user-facing error templates.
/// The single source of truth is the embedded <c>errors.{language}.json</c>
/// resource files in this assembly — one JSON file per supported language,
/// mapping a stable error code to its message template. Positional arguments
/// use string.Format slots ({0}, {1}, ...).
/// Unknown codes resolve to the raw code text, so legacy message-style codes
/// keep rendering their original (English) text unchanged.
/// Adding a new language means adding an <c>errors.&lt;lang&gt;.json</c> file.
/// </summary>
public static class ErrorCatalog
{
    /// <summary>
    /// Language used for exception messages, logs, and as the fallback
    /// when a request language or a code has no translation.
    /// </summary>
    public const string DefaultLanguage = "en";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ByLanguage =
        new(Load, isThreadSafe: true);

    /// <summary>
    /// English template (used for exception messages / logs). Falls back to the
    /// raw code when the code is unknown.
    /// </summary>
    public static string English(string code, object?[] args)
    {
        return Format(code, args, Template(DefaultLanguage, code) ?? code);
    }

    /// <summary>
    /// Localizes a code for the given language tag (e.g. "fa", "fa-IR").
    /// Falls back to English, then to the raw code.
    /// </summary>
    public static string Localize(string code, object?[] args, string languageTag)
    {
        var template =
            Template(ResolveLanguageKey(languageTag), code)
            ?? Template(DefaultLanguage, code)
            ?? code;

        return Format(code, args, template);
    }

    /// <summary>
    /// Whether a code exists in any language (used to distinguish codes from
    /// raw fallback text before composing localization arguments).
    /// </summary>
    public static bool Contains(string code)
    {
        foreach (var entries in ByLanguage.Value.Values)
        {
            if (entries.ContainsKey(code))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages => ByLanguage.Value;

    private static string? Template(string language, string code)
    {
        return Languages.TryGetValue(language, out var entries) && entries.TryGetValue(code, out var template)
            ? template
            : null;
    }

    /// <summary>
    /// Maps a request language tag onto a loaded resource language ("fa-IR" → "fa"),
    /// falling back to the default language.
    /// </summary>
    private static string ResolveLanguageKey(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return DefaultLanguage;
        }

        var tag = languageTag.Trim();

        if (Languages.ContainsKey(tag))
        {
            return tag;
        }

        var prefix = tag.Split('-')[0];
        return Languages.ContainsKey(prefix) ? prefix : DefaultLanguage;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(ErrorCatalog).Assembly;

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var language = TryGetResourceLanguage(resourceName);
            if (language is null)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            try
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (entries is not null)
                {
                    result[language] = entries;
                }
            }
            catch (JsonException)
            {
                // A malformed resource must never break the error path;
                // codes from that language fall back to English, then raw text.
            }
        }

        return result;
    }

    private static string? TryGetResourceLanguage(string manifestResourceName)
    {
        if (!manifestResourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Embedded names are dot-separated, e.g.
        // "BuildingBlocks.Domain.Localization.errors.en.json".
        var markerIndex = manifestResourceName.LastIndexOf(".errors.", StringComparison.OrdinalIgnoreCase);
        var start = markerIndex >= 0
            ? markerIndex + ".errors.".Length
            : manifestResourceName.StartsWith("errors.", StringComparison.OrdinalIgnoreCase)
                ? "errors.".Length
                : -1;

        if (start < 0)
        {
            return null;
        }

        var language = manifestResourceName[start..^".json".Length];
        return language.Length == 0 ? null : language;
    }

    private static string Format(string code, object?[] args, string template)
    {
        try
        {
            return args.Length == 0
                ? template
                : string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            // Malformed template/args must never break the error path.
            return code;
        }
    }
}
