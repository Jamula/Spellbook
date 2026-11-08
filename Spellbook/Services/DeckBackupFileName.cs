using System.Globalization;
using System.Reflection;

namespace Spellbook.Services;

public static class DeckBackupFileName
{
    public static string Create(string? userIdentifier, string? appVersion, DateTime localDate)
    {
        var user = Sanitize(userIdentifier, "local");
        var baseVersion = appVersion?.Split('+')[0];
        var version = Sanitize(baseVersion, "unknown");
        return $"{localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}-{user}-decks-{version}.json";
    }

    public static string CurrentAppVersion()
    {
        var version = typeof(DeckStore).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version.Split('+')[0];
    }

    private static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var safe = new string(value
            .Trim()
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-')
            .Take(64)
            .ToArray())
            .Trim('.', '-', '_');

        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
