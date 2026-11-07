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
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("{}")]
    public async Task LoadReturnsEmptyForMissingOrInvalidStorage(string? json)
    {
        var js = new StorageRuntime { StoredJson = json };

        Assert.Empty(await new DeckStore(js).LoadAsync());
        Assert.Equal("spellbookStorage.get", js.LastCall);
        Assert.Equal("spellbook.decks.v1", Assert.Single(js.LastArguments));
    }

    [Fact]
    public async Task LoadReturnsEmptyWhenBrowserStorageIsUnavailable()
    {
        var js = new StorageRuntime { Failure = new JSException("Storage unavailable") };

        Assert.Empty(await new DeckStore(js).LoadAsync());
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

        if (json == "null") Assert.Throws<InvalidDataException>(() => store.ParseImport(json));
        else Assert.Throws<JsonException>(() => store.ParseImport(json));
    }

    [Fact]
    public void ImportNormalizesMissingNamesAndCards()
    {
        var store = new DeckStore(new StorageRuntime());

        var deck = Assert.Single(store.ParseImport("[{\"name\":null,\"cards\":null}]"));

        Assert.Equal("Imported deck", deck.Name);
        Assert.Empty(deck.Cards);
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
        var json = JsonSerializer.Serialize(new[] { new Deck { Cards = [new DeckCard { Quantity = quantity }] } });

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
        var deck = new Deck { Cards = Enumerable.Range(0, count).Select(_ => new DeckCard()).ToList() };
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
