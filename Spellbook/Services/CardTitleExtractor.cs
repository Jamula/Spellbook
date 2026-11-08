using System.Text.RegularExpressions;

namespace Spellbook.Services;

/// <summary>
/// Conservative first-line OCR title cleanup. Never changes the raw OCR text:
/// users can edit and confirm every suggested name.
/// </summary>
public static partial class CardTitleExtractor
{
    public static string FromOcr(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var title = line.Trim().Trim('\r');
            // Strip only a recognizable trailing mana-cost cluster. Ordinary
            // numbers and names remain unchanged when no mana symbols follow.
            title = ManaSuffix().Replace(title, "").Trim();
            if (title.Length is >= 2 and <= 200) return title;
        }
        return "";
    }

    [GeneratedRegex(@"(?:\s+(?:\{(?:[0-9]{1,2}|[WUBRGCXS]|[WUBRG]/[WUBRG])\}\s*)+|\s+[0-9]{1,2}\s*[WUBRGC]{1,8}|\s+[WUBRGC]{2,8}|\s+[0-9]{1,2}(?:\s+[WUBRGC]){1,8})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ManaSuffix();
}
