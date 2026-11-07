using Spellbook.Models;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckListCodecTests
{
    [Theory]
    [InlineData("moxfield.txt")]
    [InlineData("archidekt.csv")]
    [InlineData("archidekt.txt")]
    public void SampleExportsPreserveQuantitiesAndZonesAndRoundTrip(string sample)
    {
        var imported = DeckListCodec.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Samples", sample)), "Sample");
        Assert.Equal(4, imported.Deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Main").Quantity);
        Assert.Contains(imported.Deck.Cards, c => c.Name == "Negate" && c.Quantity == 2 && c.Section == "Sideboard");
        Assert.Contains(imported.Deck.Cards, c => c.Name == "Niv-Mizzet, Parun" && c.Section == "Commander");
        Assert.Contains(imported.Deck.Cards, c => c.Name == "Opt" && c.Section == "Maybeboard");
        Assert.NotEmpty(imported.Warnings);
        var roundTrips = new[] { DeckListCodec.ExportText(imported.Deck), DeckListCodec.ExportCsv(imported.Deck) }
            .Select(exported => DeckListCodec.Parse(exported, "Round trip"));
        foreach (var result in roundTrips) Assert.Equal(Rows(imported.Deck), Rows(result.Deck));
    }

    [Fact]
    public void TextMergesDuplicatesWithinEachZoneAndPreservesSplitAndUnicodeNames()
    {
        var deck = DeckListCodec.Parse("# exported list\n4 Lightning Bolt\n2 lightning bolt\nSB: 1 Lightning Bolt\n[Commander]\n1 Éowyn, Shieldmaiden\n// Main Deck:\n1 Fire // Ice\n1 Sol Ring [Ramp]", " Trimmed ").Deck;
        Assert.Equal("Trimmed", deck.Name);
        Assert.Equal(6, deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Main").Quantity);
        Assert.Equal(1, deck.Cards.Single(c => c.Name == "Lightning Bolt" && c.Section == "Sideboard").Quantity);
        Assert.Contains(deck.Cards, c => c.Name == "Fire // Ice");
        Assert.Contains(deck.Cards, c => c.Name == "Éowyn, Shieldmaiden" && c.Section == "Commander");
    }

    [Theory]
    [InlineData("[Mainboard] [Sideboard]")]
    [InlineData("[Sideboard] [Mainboard]")]
    public void TextRejectsConflictingBoardTagsRegardlessOfOrder(string tags) =>
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse($"1 Opt {tags}", "Conflict"));

    [Theory]
    [InlineData("[Main] [Mainboard]", "Main")]
    [InlineData("[Sideboard] [Side board]", "Sideboard")]
    public void TextAllowsMultipleAliasesForTheSameBoard(string tags, string section) =>
        Assert.Equal(section, Assert.Single(DeckListCodec.Parse($"1 Opt {tags}", "Aliases").Deck.Cards).Section);

    [Fact]
    public void CsvHandlesBomQuotedCommasQuotesColumnAliasesAndCategories()
    {
        var result = DeckListCodec.Parse("\uFEFFCount,Card Name,Section\r\n2,\"Name, with \"\"quotes\"\"\",Sideboard\r\n1,Opt,\"Ramp; Mainboard\"\r\n1,Island,\r\n", "CSV");
        Assert.Equal("Name, with \"quotes\"", result.Deck.Cards[0].Name);
        Assert.Equal("Sideboard", result.Deck.Cards[0].Section);
        Assert.Equal("Main", result.Deck.Cards[1].Section);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal(Rows(result.Deck), Rows(DeckListCodec.Parse(DeckListCodec.ExportCsv(result.Deck), "Again").Deck));
        Assert.Equal("Main", DeckListCodec.Parse("Quantity,Name\n1,Opt", "No zones").Deck.Cards[0].Section);
        Assert.Equal("Main", DeckListCodec.Parse("Quantity,Name,Categories\n1,Opt,Ramp", "Category").Deck.Cards[0].Section);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# only comments")]
    [InlineData("Opt")]
    [InlineData("0 Opt")]
    [InlineData("251 Opt")]
    [InlineData("99999999999 Opt")]
    [InlineData("250 Opt\n1 opt")]
    [InlineData("1 Card\tname")]
    [InlineData("Quantity,Name\n1")]
    [InlineData("Quantity,Wrong\n1,Opt")]
    [InlineData("Quantity,Name,Name\n1,Opt,Opt")]
    [InlineData("Quantity,Name\n1,\"unclosed")]
    [InlineData("Quantity,Name,Board\n1,Opt,\"Mainboard;Sideboard\"")]
    public void InvalidInputsAreRejectedWithoutPartialDecks(string text) =>
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse(text, "Deck"));

    [Fact]
    public void SizeNameAndEntryLimitsAreEnforced()
    {
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse(new string('a', 2_000_001), "Deck"));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 Opt", " "));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 Opt", new string('a', 161)));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse("1 " + new string('a', 201), "Deck"));
        var thousand = string.Join('\n', Enumerable.Range(0, 1000).Select(i => $"1 Card {i}"));
        Assert.Equal(1000, DeckListCodec.Parse(thousand, "Deck").Deck.Cards.Count);
        Assert.Throws<InvalidDataException>(() => DeckListCodec.Parse(thousand + "\n1 Extra", "Deck"));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportText(new Deck()));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(new Deck()));
        var custom = new Deck { Cards = [new DeckCard { Name = "Opt", Section = "Custom" }] };
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportText(custom));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(custom));
        custom.Cards[0].Section = "Main";
        custom.Cards[0].Name = "Opt\n2 Island";
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportText(custom));
        Assert.Throws<InvalidDataException>(() => DeckListCodec.ExportCsv(custom));
    }

    private static string[] Rows(Deck deck) => deck.Cards.Select(c => $"{c.Section}|{c.Name}|{c.Quantity}").Order().ToArray();
}
