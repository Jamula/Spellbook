using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spellbook.Models;
using Spellbook.Services;

namespace Spellbook.Tests;

[TestClass]
public sealed class DeckListCodecTests
{
    [TestMethod]
    [DataRow("moxfield.txt")]
    [DataRow("archidekt.csv")]
    [DataRow("archidekt.txt")]
    public void SampleExportsPreserveQuantitiesAndZonesAndRoundTrip(string sample)
    {
        var imported = DeckListCodec.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Samples", sample)), "Sample");
        TestChecks.AreEqual(4, imported.Deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Main").Quantity);
        TestChecks.Contains(imported.Deck.Cards, c => c.Name == "Negate" && c.Quantity == 2 && c.Section == "Sideboard");
        TestChecks.Contains(imported.Deck.Cards, c => c.Name == "Niv-Mizzet, Parun" && c.Section == "Commander");
        TestChecks.Contains(imported.Deck.Cards, c => c.Name == "Opt" && c.Section == "Maybeboard");
        TestChecks.NotEmpty(imported.Warnings);
        var roundTrips = new[] { DeckListCodec.ExportText(imported.Deck), DeckListCodec.ExportCsv(imported.Deck) }
            .Select(exported => DeckListCodec.Parse(exported, "Round trip"));
        foreach (var result in roundTrips) TestChecks.AreEqual(Rows(imported.Deck), Rows(result.Deck));
    }

    [TestMethod]
    public void TextMergesDuplicatesWithinEachZoneAndPreservesSplitAndUnicodeNames()
    {
        var deck = DeckListCodec.Parse("# exported list\n4 Lightning Bolt\n2 lightning bolt\nSB: 1 Lightning Bolt\n[Commander]\n1 Éowyn, Shieldmaiden\n// Main Deck:\n1 Fire // Ice\n1 Sol Ring [Ramp]", " Trimmed ").Deck;
        TestChecks.AreEqual("Trimmed", deck.Name);
        TestChecks.AreEqual(6, deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Main").Quantity);
        TestChecks.AreEqual(1, deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Sideboard").Quantity);
        TestChecks.Contains(deck.Cards, c => c.Name == "Fire // Ice");
        TestChecks.Contains(deck.Cards, c => c.Name == "Éowyn, Shieldmaiden" && c.Section == "Commander");
    }

    [TestMethod]
    [DataRow("[Mainboard] [Sideboard]")]
    [DataRow("[Sideboard] [Mainboard]")]
    public void TextRejectsConflictingBoardTagsRegardlessOfOrder(string tags) =>
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse($"1 Opt {tags}", "Conflict"));

    [TestMethod]
    [DataRow("[Main] [Mainboard]", "Main")]
    [DataRow("[Sideboard] [Side board]", "Sideboard")]
    public void TextAllowsMultipleAliasesForTheSameBoard(string tags, string section) =>
        TestChecks.AreEqual(section, TestChecks.Single(DeckListCodec.Parse($"1 Opt {tags}", "Aliases").Deck.Cards).Section);

    [TestMethod]
    public void TextParsesManyTrailingCategoryTags()
    {
        var text = "1 Opt" + string.Concat(Enumerable.Repeat(" [Ramp]", 5000));
        var result = DeckListCodec.Parse(text, "Many tags");

        TestChecks.AreEqual("Opt", TestChecks.Single(result.Deck.Cards).Name);
        TestChecks.NotEmpty(result.Warnings);
    }

    [TestMethod]
    public void CsvHandlesBomQuotedCommasQuotesColumnAliasesAndCategories()
    {
        var result = DeckListCodec.Parse("\uFEFFCount,Card Name,Section\r\n2,\"Name, with \"\"quotes\"\"\",Sideboard\r\n1,Opt,\"Ramp; Mainboard\"\r\n1,Island,\r\n", "CSV");
        TestChecks.AreEqual("Name, with \"quotes\"", result.Deck.Cards[0].Name);
        TestChecks.AreEqual("Sideboard", result.Deck.Cards[0].Section);
        TestChecks.AreEqual("Main", result.Deck.Cards[1].Section);
        TestChecks.NotEmpty(result.Warnings);
        TestChecks.AreEqual(Rows(result.Deck), Rows(DeckListCodec.Parse(DeckListCodec.ExportCsv(result.Deck), "Again").Deck));
        TestChecks.AreEqual("Main", DeckListCodec.Parse("Quantity,Name\n1,Opt", "No zones").Deck.Cards[0].Section);
        TestChecks.AreEqual("Main", DeckListCodec.Parse("Quantity,Name,Categories\n1,Opt,Ramp", "Category").Deck.Cards[0].Section);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("# only comments")]
    [DataRow("Opt")]
    [DataRow("0 Opt")]
    [DataRow("251 Opt")]
    [DataRow("99999999999 Opt")]
    [DataRow("250 Opt\n1 opt")]
    [DataRow("1 Card\tname")]
    [DataRow("Quantity,Name\n1")]
    [DataRow("Quantity,Wrong\n1,Opt")]
    [DataRow("Quantity,Name,Name\n1,Opt,Opt")]
    [DataRow("Quantity,Name\n1,\"unclosed")]
    [DataRow("Quantity,Name,Board\n1,Opt,\"Mainboard;Sideboard\"")]
    public void InvalidInputsAreRejectedWithoutPartialDecks(string text) =>
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse(text, "Deck"));

    [TestMethod]
    public void SizeNameAndEntryLimitsAreEnforced()
    {
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse(new string('a', 2_000_001), "Deck"));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 Opt", " "));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 Opt", new string('a', 161)));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 " + new string('a', 201), "Deck"));
        var thousand = string.Join('\n', Enumerable.Range(0, 1000).Select(i => $"1 Card {i}"));
        TestChecks.AreEqual(1000, DeckListCodec.Parse(thousand, "Deck").Deck.Cards.Count);
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse(thousand + "\n1 Extra", "Deck"));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportText(new Deck()));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(new Deck()));
        var custom = new Deck { Cards = [new DeckCard { Name = "Opt", Section = "Custom" }] };
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportText(custom));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(custom));
        custom.Cards[0].Section = "Main";
        custom.Cards[0].Name = "Opt\n2 Island";
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportText(custom));
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(custom));
    }

    [TestMethod]
    public void CsvRejectsSideboardCommandersAndTextPreservesUnrecognizedTrailingBrackets()
    {
        TestChecks.Throws<InvalidDataException>(() => DeckListCodec.Parse(
            "Quantity,Name,Board,Commander\n1,Opt,Sideboard,true", "Conflict"));
        var parsed = DeckListCodec.Parse("1 Unusual card name]", "Odd bracket");
        TestChecks.AreEqual("Unusual card name]", TestChecks.Single(parsed.Deck.Cards).Name);
    }

    private static string[] Rows(Deck deck) => deck.Cards.Select(c => $"{c.Section}|{c.Name}|{c.Quantity}").Order().ToArray();
}
