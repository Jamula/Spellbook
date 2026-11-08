using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Microsoft.JSInterop;
using Spellbook.Models;
using Spellbook.Services;

namespace Spellbook.Tests;

[TestClass]
public sealed class DeckStoreTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task LoadReturnsEmptyForMissingStorage(string? json)
    {
        var js = new StorageRuntime { StoredJson = json };

        TestChecks.Empty(await new DeckStore(js).LoadAsync());
        TestChecks.AreEqual("spellbookStorage.get", js.LastCall);
        TestChecks.AreEqual("spellbook.decks.v1", TestChecks.Single(js.LastArguments));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("not json")]
    [DataRow("{}")]
    public async Task LoadRejectsInvalidStorageWithoutSaving(string json)
    {
        var js = new StorageRuntime { StoredJson = json };

        await TestChecks.ThrowsAsync<InvalidDataException>(() => new DeckStore(js).LoadAsync());
        TestChecks.AreEqual("spellbookStorage.get", js.LastCall);
    }

    [TestMethod]
    public async Task LoadRejectsUnavailableBrowserStorage()
    {
        var js = new StorageRuntime { Failure = new JSException("Storage unavailable") };

        await TestChecks.ThrowsAsync<InvalidDataException>(() => new DeckStore(js).LoadAsync());
        TestChecks.AreEqual("spellbookStorage.get", js.LastCall);
    }

    [TestMethod]
    public async Task SaveAndLoadPreserveDecksAndCardData()
    {
        var js = new StorageRuntime();
        var store = new DeckStore(js);
        var deck = new Deck
        {
            Name = "Bolt & friends",
            Format = "Modern",
            UpdatedUtc = DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
            Cards = [new DeckCard { Name = "Lightning Bolt", Quantity = 4, Section = "Sideboard" }]
        };

        await store.SaveAsync([deck]);

        TestChecks.AreEqual("spellbookStorage.set", js.LastCall);
        TestChecks.AreEqual("spellbook.decks.v1", js.LastArguments[0]);
        var saved = TestChecks.Single(store.ParseImport(js.StoredJson!));
        TestChecks.AreEqual(deck.Id, saved.Id);
        var loaded = TestChecks.Single(await store.LoadAsync());
        TestChecks.AreEqual(deck.Id, loaded.Id);
        TestChecks.AreEqual(deck.Name, loaded.Name);
        TestChecks.AreEqual(deck.Format, loaded.Format);
        TestChecks.AreEqual(deck.UpdatedUtc, loaded.UpdatedUtc);
        var card = TestChecks.Single(loaded.Cards);
        TestChecks.AreEqual("Lightning Bolt", card.Name);
        TestChecks.AreEqual(4, card.Quantity);
        TestChecks.AreEqual("Sideboard", card.Section);
        TestChecks.AreEqual(4, loaded.CardCount);
    }

    [TestMethod]
    public async Task ExportDownloadsImportableJson()
    {
        var js = new StorageRuntime();
        var store = new DeckStore(js);
        var deck = new Deck { Name = "Commander" };

        await store.ExportAsync([deck]);

        TestChecks.AreEqual("spellbookStorage.download", js.LastCall);
        TestChecks.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            TestChecks.IsType<string>(js.LastArguments[0]),
            @"^\d{4}-\d{2}-\d{2}-local-decks-[A-Za-z0-9._-]+\.json$"));
        var imported = TestChecks.Single(store.ParseImport(TestChecks.IsType<string>(js.LastArguments[1])));
        TestChecks.AreEqual(deck.Id, imported.Id);
        TestChecks.AreEqual(deck.Name, imported.Name);
    }

    [TestMethod]
    public void BackupFileNameUsesLocalDateSanitizedIdentityAndAppVersion()
    {
        var localDate = new DateTime(2026, 2, 3, 23, 59, 0, DateTimeKind.Local);
        TestChecks.AreEqual("2026-02-03-local-decks-1.2.3.json",
            DeckBackupFileName.Create(null, "1.2.3+build.45", localDate));
        TestChecks.AreEqual("2026-02-03-Alice_7-unsafe-decks-1.2.3.json",
            DeckBackupFileName.Create(" Alice_7/unsafe ", "1.2.3", localDate));

        var currentVersion = DeckBackupFileName.CurrentAppVersion();
        TestChecks.IsFalse(string.IsNullOrWhiteSpace(currentVersion));
        TestChecks.IsFalse(currentVersion.Contains('+'));
    }

    [TestMethod]
    public async Task SaveReportsStorageFailureRatherThanSilentlyLosingChanges()
    {
        var store = new DeckStore(new StorageRuntime { Failure = new JSException("Quota exceeded") });

        await TestChecks.ThrowsAsync<JSException>(() => store.SaveAsync([new Deck()]));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("not json")]
    [DataRow("{}")]
    public void ImportRejectsInvalidCollections(string json)
    {
        var store = new DeckStore(new StorageRuntime());

        if (json == "not json")
            TestChecks.ThrowsAny<JsonException>(() => store.ParseImport(json));
        else
            TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    public void ImportRejectsMissingRequiredNamesAndCards()
    {
        var store = new DeckStore(new StorageRuntime());

        TestChecks.Throws<InvalidDataException>(() => store.ParseImport("[{\"cards\":[]}]"));
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport("[{\"name\":null,\"cards\":[]}]"));
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport("[{\"name\":\"Imported deck\",\"cards\":null}]"));
        TestChecks.Empty(store.ParseImport("[]"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(251)]
    public void ImportRejectsOutOfRangeCardQuantities(int quantity)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(new[] { new Deck { Cards = [new DeckCard { Quantity = quantity }] } });

        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(250)]
    public void ImportAcceptsBoundaryCardQuantities(int quantity)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(new[] { new Deck { Cards = [new DeckCard { Name = "Card", Quantity = quantity }] } });

        TestChecks.AreEqual(quantity, TestChecks.Single(store.ParseImport(json)).CardCount);
    }

    [TestMethod]
    [DataRow(500, true)]
    [DataRow(501, false)]
    public void ImportEnforcesDeckLimit(int count, bool accepted)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(Enumerable.Range(0, count).Select(_ => new Deck()));

        if (accepted) TestChecks.AreEqual(count, store.ParseImport(json).Count);
        else TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    [DataRow(1000, true)]
    [DataRow(1001, false)]
    public void ImportEnforcesCardsPerDeckLimit(int count, bool accepted)
    {
        var store = new DeckStore(new StorageRuntime());
        var deck = new Deck { Cards = Enumerable.Range(0, count).Select(i => new DeckCard { Name = $"Card {i}" }).ToList() };
        var json = JsonSerializer.Serialize(new[] { deck });

        if (accepted) TestChecks.AreEqual(count, TestChecks.Single(store.ParseImport(json)).Cards.Count);
        else TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    public void CardCountSumsQuantitiesAcrossCards()
    {
        var deck = new Deck();
        TestChecks.AreEqual(0, deck.CardCount);

        deck.Cards = [new DeckCard { Quantity = 4 }, new DeckCard { Quantity = 2 }];

        TestChecks.AreEqual(6, deck.CardCount);
    }


    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void ImportRejectsBlankContent(string json)
    {
        var store = new DeckStore(new StorageRuntime());
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    [DataRow("[null]")]
    [DataRow("[10]")]
    [DataRow("[{}]")]
    [DataRow("[{\"name\":\"\",\"cards\":[]}]")]
    [DataRow("[{\"name\":\"Deck\",\"format\":\"\",\"cards\":[]}]")]
    [DataRow("[{\"name\":\"Deck\",\"cards\":[null]}]")]
    [DataRow("[{\"name\":\"Deck\",\"cards\":[{\"name\":\"\",\"quantity\":1}]}]")]
    [DataRow("[{\"name\":\"Deck\",\"cards\":[{\"name\":\"Bolt\",\"quantity\":1,\"section\":\"\"}]}]")]
    public void ImportRejectsCorruptDeckAndCardRecords(string json)
    {
        var store = new DeckStore(new StorageRuntime());
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [TestMethod]
    public void ImportRejectsOversizedNamesAndFormats()
    {
        var store = new DeckStore(new StorageRuntime());
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Name = new string('N', 161) }
        })));
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Format = new string('F', 81) }
        })));
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Cards = [new DeckCard { Name = new string('C', 201) }] }
        })));
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Cards = [new DeckCard { Section = new string('S', 81) }] }
        })));
    }

    [TestMethod]
    public void ImportRejectsDuplicateCardNamesIgnoringCaseWithinASection()
    {
        var store = new DeckStore(new StorageRuntime());
        var deck = new Deck
        {
            Cards = [
                new DeckCard { Name = "Lightning Bolt", Section = "Main" },
                new DeckCard { Name = "lightning bolt", Section = "Main" }
            ]
        };
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[] { deck })));

        deck.Cards[1].Section = "Sideboard";
        TestChecks.AreEqual(2, TestChecks.Single(store.ParseImport(JsonSerializer.Serialize(new[] { deck }))).Cards.Count);
    }

    [TestMethod]
    public void ImportAcceptsScryfallImageMetadataAndRejectsUntrustedImageUrls()
    {
        var store = new DeckStore(new StorageRuntime());
        var deck = new Deck
        {
            Cards = [new DeckCard
            {
                Name = "Baneslayer Angel",
                Details = new CardDetails
                {
                    ManaValue = 5,
                    ManaCost = "{3}{W}{W}",
                    TypeLine = "Creature",
                    Colors = "W",
                    ImageUrl = "https://cards.scryfall.io/small/front/a/b/angel.jpg"
                }
            }]
        };

        var importedCard = TestChecks.Single(TestChecks.Single(store.ParseImport(JsonSerializer.Serialize(new[] { deck }))).Cards);
        TestChecks.AreEqual("{3}{W}{W}", importedCard.Details!.ManaCost);
        TestChecks.AreEqual("https://cards.scryfall.io/small/front/a/b/angel.jpg", importedCard.Details.ImageUrl);

        deck.Cards[0].Details!.ImageUrl = "https://example.com/card.jpg";
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[] { deck })));
        deck.Cards[0].Details!.ImageUrl = "http://cards.scryfall.io/card.jpg";
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[] { deck })));
        deck.Cards[0].Details!.ImageUrl = "https://cards.scryfall.io/card.jpg";
        deck.Cards[0].Details!.ManaCost = new string('X', 101);
        TestChecks.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[] { deck })));
    }

    [TestMethod]
    public async Task RecoveryDownloadDoesNotOverwriteEmptyOrUnavailableStorage()
    {
        var runtime = new StorageRuntime { StoredJson = null };
        var store = new DeckStore(runtime);
        await store.ExportDamagedAsync();
        TestChecks.AreEqual("spellbookStorage.get", runtime.LastCall);

        runtime.StoredJson = "  ";
        await store.ExportDamagedAsync();
        TestChecks.AreEqual("spellbookStorage.get", runtime.LastCall);

        runtime.StoredJson = "corrupted backup";
        await store.ExportDamagedAsync();
        TestChecks.AreEqual("spellbookStorage.download", runtime.LastCall);
        TestChecks.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            TestChecks.IsType<string>(runtime.LastArguments[0]),
            @"^\d{4}-\d{2}-\d{2}-local-decks-[A-Za-z0-9._-]+\.json$"));
        TestChecks.AreEqual("corrupted backup", runtime.LastArguments[1]);
    }

    [TestMethod]
    public async Task ExportSurfacesBrowserDownloadExceptions()
    {
        var store = new DeckStore(new StorageRuntime { Failure = new JSException("download denied") });
        await TestChecks.ThrowsAsync<JSException>(() => store.ExportAsync([new Deck()]));
    }

    private sealed class StorageRuntime : IJSRuntime
    {
        public string? StoredJson { get; set; }
        public Exception? Failure { get; init; }
        public string? LastCall { get; private set; }
        public object?[] LastArguments { get; private set; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            LastCall = identifier;
            LastArguments = args ?? [];
            if (Failure is not null) return ValueTask.FromException<TValue>(Failure);
            switch (identifier)
            {
                case "spellbookStorage.get":
                    return ValueTask.FromResult((TValue)(object?)StoredJson!);
                case "spellbookStorage.set":
                    StoredJson = (string)LastArguments[1]!;
                    break;
                case "spellbookStorage.download":
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected JS call: {identifier}");
            }
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
