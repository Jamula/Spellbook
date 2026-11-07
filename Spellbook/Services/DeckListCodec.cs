using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using Spellbook.Models;

namespace Spellbook.Services;

public sealed record DeckListImport(Deck Deck, IReadOnlyList<string> Warnings);

/// <summary>Local interchange only: card names are syntax-checked, never looked up remotely.</summary>
public static partial class DeckListCodec
{
    private const int MaxLength = 2_000_000;
    private static readonly Dictionary<string, string> Zones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["main"] = "Main", ["mainboard"] = "Main", ["main deck"] = "Main", ["deck"] = "Main",
        ["sideboard"] = "Sideboard", ["side board"] = "Sideboard",
        ["commander"] = "Commander", ["commanders"] = "Commander", ["command zone"] = "Commander",
        ["maybeboard"] = "Maybeboard", ["maybe"] = "Maybeboard", ["considering"] = "Maybeboard"
    };

    public static DeckListImport Parse(string text, string deckName)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Paste a deck list or choose a text/CSV file.");
        if (text.Length > MaxLength) throw new InvalidDataException("Deck list must be smaller than 2 MB.");
        if (string.IsNullOrWhiteSpace(deckName) || deckName.Trim().Length > 160)
            throw new InvalidDataException("Deck name must be 1–160 characters.");
        var deck = new Deck { Name = deckName.Trim() };
        var warnings = new HashSet<string>();
        var firstLine = text.TrimStart('\uFEFF', ' ', '\r', '\n').Split('\n')[0];
        if (firstLine.Contains(',') && HeaderName().IsMatch(firstLine)) ParseCsv(text.TrimStart('\uFEFF'), deck, warnings);
        else ParseText(text, deck, warnings);
        if (deck.Cards.Count == 0) throw new InvalidDataException("No cards were found in the deck list.");
        return new(deck, warnings.ToList());
    }

    private static void ParseText(string text, Deck deck, HashSet<string> warnings)
    {
        var section = "Main";
        var number = 0;
        foreach (var raw in text.Split('\n'))
        {
            number++;
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("//"))
            {
                if (Zones.TryGetValue(line[2..].Trim().TrimEnd(':'), out var commentZone)) section = commentZone;
                continue;
            }
            var heading = line.TrimEnd(':').Trim('[', ']');
            if (Zones.TryGetValue(heading, out var zone)) { section = zone; continue; }
            var rowSection = section;
            if (line.StartsWith("SB:", StringComparison.OrdinalIgnoreCase)) { rowSection = "Sideboard"; line = line[3..].Trim(); }
            var match = CardLine().Match(line);
            if (!match.Success) throw new InvalidDataException($"Line {number}: expected a quantity followed by a card name.");
            var name = match.Groups[2].Value.Trim();
            // Archidekt text includes categories in brackets; only recognized board names change zones.
            var taggedZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (TrailingTag().IsMatch(name))
            {
                var tag = TrailingTag().Match(name);
                var category = tag.Groups[1].Value;
                if (Zones.TryGetValue(category, out zone)) taggedZones.Add(zone);
                else warnings.Add("Non-board categories and tags are omitted; review the imported cards and zones.");
                name = name[..tag.Index].Trim();
            }
            if (taggedZones.Count > 1)
                throw new InvalidDataException($"Line {number}: conflicting board tags; choose one board before importing.");
            rowSection = taggedZones.FirstOrDefault() ?? rowSection;
            var printing = PrintingSuffix().Match(name);
            if (printing.Success)
            {
                name = name[..printing.Index].Trim();
                warnings.Add("Set/collector and foil details are omitted; Spellbook stores card names, quantities, and zones.");
            }
            Add(deck, name, match.Groups[1].Value, rowSection, number);
        }
    }

    private static void ParseCsv(string text, Deck deck, HashSet<string> warnings)
    {
        using var parser = new TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        try
        {
            var headers = parser.ReadFields() ?? [];
            int Find(params string[] names) => Array.FindIndex(headers, h => names.Contains(h, StringComparer.OrdinalIgnoreCase));
            if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                throw new InvalidDataException("CSV column names must be unique.");
            var nameIndex = Find("Name", "Card Name", "Card");
            var quantityIndex = Find("Quantity", "Count", "Qty");
            var zoneIndex = Find("Board", "Section", "Category", "Categories");
            var commanderIndex = Find("Commander");
            if (nameIndex < 0 || quantityIndex < 0) throw new InvalidDataException("CSV needs Name and Quantity (or Count) columns.");
            if (headers.Any(h => !new[] { nameIndex, quantityIndex, zoneIndex, commanderIndex }.Contains(Array.IndexOf(headers, h))))
                warnings.Add("Additional CSV columns are omitted; only names, quantities, and zones are stored.");
            while (!parser.EndOfData)
            {
                var line = (int)parser.LineNumber;
                var row = parser.ReadFields() ?? [];
                if (row.Length != headers.Length) throw new InvalidDataException($"CSV row {line}: column count does not match the header.");
                var section = zoneIndex >= 0 ? ResolveCategories(row[zoneIndex], warnings) : "Main";
                if (commanderIndex >= 0 && (row[commanderIndex].Equals("true", StringComparison.OrdinalIgnoreCase) || row[commanderIndex] == "1")) section = "Commander";
                Add(deck, row[nameIndex], row[quantityIndex], section, line);
            }
        }
        catch (MalformedLineException) { throw new InvalidDataException("Malformed CSV: check quoted fields and column separators."); }
    }

    private static string ResolveCategories(string value, HashSet<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Main";
        if (Zones.TryGetValue(value, out var zone)) return zone;
        var parts = value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var recognized = parts.Where(Zones.ContainsKey).Select(p => Zones[p]).Distinct().ToList();
        if (recognized.Count > 1) throw new InvalidDataException("A card has conflicting board categories; choose one board before importing.");
        warnings.Add("Non-board categories and tags are omitted; review the imported cards and zones.");
        return recognized.FirstOrDefault() ?? "Main";
    }

    private static void Add(Deck deck, string name, string quantity, string section, int line)
    {
        if (!int.TryParse(quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 1 or > 250)
            throw new InvalidDataException($"Line/row {line}: quantity must be between 1 and 250.");
        name = name.Trim();
        if (name.Length is < 1 or > 200 || name.Any(char.IsControl))
            throw new InvalidDataException($"Line/row {line}: card name must be 1–200 characters without control characters.");
        var existing = deck.Cards.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && c.Section == section);
        if (existing is not null)
        {
            if (existing.Quantity + count > 250) throw new InvalidDataException($"Line/row {line}: combined card quantity exceeds 250.");
            existing.Quantity += count;
        }
        else deck.Cards.Add(new DeckCard { Name = name, Quantity = count, Section = section });
        if (deck.Cards.Count > 1000) throw new InvalidDataException("A deck can contain at most 1,000 distinct card entries.");
    }

    public static string ExportText(Deck deck)
    {
        ValidateExport(deck);
        var text = new StringBuilder();
        foreach (var section in deck.Cards.GroupBy(c => c.Section))
        {
            var zone = Zones[section.Key];
            text.AppendLine(zone == "Main" ? "Mainboard" : zone);
            foreach (var card in section) text.AppendLine($"{card.Quantity} {card.Name}");
            text.AppendLine();
        }
        return text.ToString();
    }

    public static string ExportCsv(Deck deck)
    {
        ValidateExport(deck);
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Quantity,Name,Board\n");
        foreach (var card in deck.Cards) text.AppendLine($"{card.Quantity},{Quote(card.Name)},{Quote(Zones[card.Section])}");
        return text.ToString();
    }

    private static void ValidateExport(Deck deck)
    {
        if (deck.Cards.Count == 0) throw new InvalidDataException("This deck has no cards to export. Use JSON backup to preserve an empty deck.");
        foreach (var card in deck.Cards)
        {
            if (!Zones.ContainsKey(card.Section)) throw new InvalidDataException("This deck has a custom zone. Use JSON backup to preserve it.");
            if (string.IsNullOrWhiteSpace(card.Name) || card.Name.Length > 200 || card.Name.Any(char.IsControl) || card.Quantity is < 1 or > 250)
                throw new InvalidDataException("A card cannot be represented safely in a deck list. Use JSON backup to preserve it.");
        }
    }

    [GeneratedRegex("(?:^|,)\\s*\"?(?:Name|Card Name|Card|Quantity|Count|Qty)\"?\\s*(?:,|$)", RegexOptions.IgnoreCase)] private static partial Regex HeaderName();
    [GeneratedRegex(@"^(\d+)x?\s+(.+)$", RegexOptions.IgnoreCase)] private static partial Regex CardLine();
    [GeneratedRegex(@"\s+\[([^\]]+)\]$")] private static partial Regex TrailingTag();
    [GeneratedRegex(@"\s+\([A-Za-z0-9]+\)(?:\s+[A-Za-z0-9★*-]+)?(?:\s+\*F\*)?$")] private static partial Regex PrintingSuffix();
}
