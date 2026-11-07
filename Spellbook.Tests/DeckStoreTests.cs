using System.Text.Json;
using Microsoft.JSInterop;
using Spellbook.Models;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckStoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task LoadReturnsEmptyForMissingStorage(string? json)
    {
        var js = new StorageRuntime { StoredJson = json };

        Assert.Empty(await new DeckStore(js).LoadAsync());
        Assert.Equal("spellbookStorage.get", js.LastCall);
        Assert.Equal("spellbook.decks.v1", Assert.Single(js.LastArguments));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("{}")]
    public async Task LoadRejectsInvalidStorageWithoutSaving(string json)
    {
        var js = new StorageRuntime { StoredJson = json };

        await Assert.ThrowsAsync<InvalidDataException>(() => new DeckStore(js).LoadAsync());
        Assert.Equal("spellbookStorage.get", js.LastCall);
    }

    [Fact]
    public async Task LoadRejectsUnavailableBrowserStorage()
    {
        var js = new StorageRuntime { Failure = new JSException("Storage unavailable") };

        await Assert.ThrowsAsync<InvalidDataException>(() => new DeckStore(js).LoadAsync());
        Assert.Equal("spellbookStorage.get", js.LastCall);
    }

    [Fact]
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

        Assert.Equal("spellbookStorage.set", js.LastCall);
        Assert.Equal("spellbook.decks.v1", js.LastArguments[0]);
        var saved = Assert.Single(store.ParseImport(js.StoredJson!));
        Assert.Equal(deck.Id, saved.Id);
        var loaded = Assert.Single(await store.LoadAsync());
        Assert.Equal(deck.Id, loaded.Id);
        Assert.Equal(deck.Name, loaded.Name);
        Assert.Equal(deck.Format, loaded.Format);
        Assert.Equal(deck.UpdatedUtc, loaded.UpdatedUtc);
        var card = Assert.Single(loaded.Cards);
        Assert.Equal("Lightning Bolt", card.Name);
        Assert.Equal(4, card.Quantity);
        Assert.Equal("Sideboard", card.Section);
        Assert.Equal(4, loaded.CardCount);
    }

    [Fact]
    public async Task ExportDownloadsImportableJson()
    {
        var js = new StorageRuntime();
        var store = new DeckStore(js);
        var deck = new Deck { Name = "Commander" };

        await store.ExportAsync([deck]);

        Assert.Equal("spellbookStorage.download", js.LastCall);
        Assert.Equal("spellbook-decks.json", js.LastArguments[0]);
        var imported = Assert.Single(store.ParseImport(Assert.IsType<string>(js.LastArguments[1])));
        Assert.Equal(deck.Id, imported.Id);
        Assert.Equal(deck.Name, imported.Name);
    }

    [Fact]
    public async Task SaveReportsStorageFailureRatherThanSilentlyLosingChanges()
    {
        var store = new DeckStore(new StorageRuntime { Failure = new JSException("Quota exceeded") });

        await Assert.ThrowsAsync<JSException>(() => store.SaveAsync([new Deck()]));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void ImportRejectsInvalidCollections(string json)
    {
        var store = new DeckStore(new StorageRuntime());

        if (json == "not json")
            Assert.ThrowsAny<JsonException>(() => store.ParseImport(json));
        else
            Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Fact]
    public void ImportRejectsMissingRequiredNamesAndCards()
    {
        var store = new DeckStore(new StorageRuntime());

        Assert.Throws<InvalidDataException>(() => store.ParseImport("[{\"cards\":[]}]"));
        Assert.Throws<InvalidDataException>(() => store.ParseImport("[{\"name\":null,\"cards\":[]}]"));
        Assert.Throws<InvalidDataException>(() => store.ParseImport("[{\"name\":\"Imported deck\",\"cards\":null}]"));
        Assert.Empty(store.ParseImport("[]"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(251)]
    public void ImportRejectsOutOfRangeCardQuantities(int quantity)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(new[] { new Deck { Cards = [new DeckCard { Quantity = quantity }] } });

        Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    public void ImportAcceptsBoundaryCardQuantities(int quantity)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(new[] { new Deck { Cards = [new DeckCard { Name = "Card", Quantity = quantity }] } });

        Assert.Equal(quantity, Assert.Single(store.ParseImport(json)).CardCount);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void ImportEnforcesDeckLimit(int count, bool accepted)
    {
        var store = new DeckStore(new StorageRuntime());
        var json = JsonSerializer.Serialize(Enumerable.Range(0, count).Select(_ => new Deck()));

        if (accepted) Assert.Equal(count, store.ParseImport(json).Count);
        else Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void ImportEnforcesCardsPerDeckLimit(int count, bool accepted)
    {
        var store = new DeckStore(new StorageRuntime());
        var deck = new Deck { Cards = Enumerable.Range(0, count).Select(i => new DeckCard { Name = $"Card {i}" }).ToList() };
        var json = JsonSerializer.Serialize(new[] { deck });

        if (accepted) Assert.Equal(count, Assert.Single(store.ParseImport(json)).Cards.Count);
        else Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Fact]
    public void CardCountSumsQuantitiesAcrossCards()
    {
        var deck = new Deck();
        Assert.Equal(0, deck.CardCount);

        deck.Cards = [new DeckCard { Quantity = 4 }, new DeckCard { Quantity = 2 }];

        Assert.Equal(6, deck.CardCount);
    }


    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ImportRejectsBlankContent(string json)
    {
        var store = new DeckStore(new StorageRuntime());
        Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[10]")]
    [InlineData("[{}]")]
    [InlineData("[{\"name\":\"\",\"cards\":[]}]")]
    [InlineData("[{\"name\":\"Deck\",\"format\":\"\",\"cards\":[]}]")]
    [InlineData("[{\"name\":\"Deck\",\"cards\":[null]}]")]
    [InlineData("[{\"name\":\"Deck\",\"cards\":[{\"name\":\"\",\"quantity\":1}]}]")]
    [InlineData("[{\"name\":\"Deck\",\"cards\":[{\"name\":\"Bolt\",\"quantity\":1,\"section\":\"\"}]}]")]
    public void ImportRejectsCorruptDeckAndCardRecords(string json)
    {
        var store = new DeckStore(new StorageRuntime());
        Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
    }

    [Fact]
    public void ImportRejectsOversizedNamesAndFormats()
    {
        var store = new DeckStore(new StorageRuntime());
        Assert.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Name = new string('N', 161) }
        })));
        Assert.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Format = new string('F', 81) }
        })));
        Assert.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Cards = [new DeckCard { Name = new string('C', 201) }] }
        })));
        Assert.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[]
        {
            new Deck { Cards = [new DeckCard { Section = new string('S', 81) }] }
        })));
    }

    [Fact]
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
        Assert.Throws<InvalidDataException>(() => store.ParseImport(JsonSerializer.Serialize(new[] { deck })));

        deck.Cards[1].Section = "Sideboard";
        Assert.Equal(2, Assert.Single(store.ParseImport(JsonSerializer.Serialize(new[] { deck }))).Cards.Count);
    }

    [Fact]
    public async Task RecoveryDownloadDoesNotOverwriteEmptyOrUnavailableStorage()
    {
        var runtime = new StorageRuntime { StoredJson = null };
        var store = new DeckStore(runtime);
        await store.ExportDamagedAsync();
        Assert.Equal("spellbookStorage.get", runtime.LastCall);

        runtime.StoredJson = "  ";
        await store.ExportDamagedAsync();
        Assert.Equal("spellbookStorage.get", runtime.LastCall);

        runtime.StoredJson = "corrupted backup";
        await store.ExportDamagedAsync();
        Assert.Equal("spellbookStorage.download", runtime.LastCall);
        Assert.Equal("spellbook-recovery-raw.json", runtime.LastArguments[0]);
        Assert.Equal("corrupted backup", runtime.LastArguments[1]);
    }

    [Fact]
    public async Task ExportSurfacesBrowserDownloadExceptions()
    {
        var store = new DeckStore(new StorageRuntime { Failure = new JSException("download denied") });
        await Assert.ThrowsAsync<JSException>(() => store.ExportAsync([new Deck()]));
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
