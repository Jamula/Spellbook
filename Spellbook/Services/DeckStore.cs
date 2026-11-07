using System.Text.Json;
using Microsoft.JSInterop;
using Spellbook.Models;

namespace Spellbook.Services;

public sealed class DeckStore(IJSRuntime js)
{
    private const string Key = "spellbook.decks.v1";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<List<Deck>> LoadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("spellbookStorage.get", Key);
            return string.IsNullOrWhiteSpace(json) ? [] : ParseImport(json);
        }
        catch (JSException) { return []; }
        catch (JsonException) { return []; }
        catch (InvalidDataException) { return []; }
    }

    public Task SaveAsync(List<Deck> decks) =>
        js.InvokeVoidAsync("spellbookStorage.set", Key, JsonSerializer.Serialize(decks, Options)).AsTask();

    public Task ExportAsync(List<Deck> decks) =>
        js.InvokeVoidAsync("spellbookStorage.download", "spellbook-decks.json", JsonSerializer.Serialize(decks, Options)).AsTask();

    public List<Deck> ParseImport(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The deck file is empty.");

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Expected a JSON array of decks.");

        var data = JsonSerializer.Deserialize<List<Deck>>(json, Options)
            ?? throw new InvalidDataException("Invalid deck file.");

        if (data.Count > 500)
            throw new InvalidDataException("A backup can contain at most 500 decks.");

        foreach (var deck in data)
        {
            if (deck is null)
                throw new InvalidDataException("A deck entry is missing.");
            if (string.IsNullOrWhiteSpace(deck.Name) || deck.Name.Length > 160)
                throw new InvalidDataException("Each deck needs a name of at most 160 characters.");
            if (string.IsNullOrWhiteSpace(deck.Format) || deck.Format.Length > 80)
                throw new InvalidDataException("Each deck needs a valid format.");
            if (deck.Cards is null || deck.Cards.Count > 1000)
                throw new InvalidDataException("A deck cannot contain more than 1,000 distinct card entries.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in deck.Cards)
            {
                if (card is null || string.IsNullOrWhiteSpace(card.Name) || card.Name.Length > 200)
                    throw new InvalidDataException("Every card entry must have a valid name.");
                if (card.Quantity is < 1 or > 250)
                    throw new InvalidDataException("Card quantity must be between 1 and 250.");
                if (string.IsNullOrWhiteSpace(card.Section) || card.Section.Length > 80)
                    throw new InvalidDataException("Every card entry needs a valid section.");

                // A repeated name in the same section is ambiguous for the quantity editor.
                if (!seen.Add(card.Section.Trim() + "\u001f" + card.Name.Trim()))
                    throw new InvalidDataException("Duplicate card entries were found in a deck.");
            }
        }

        return data;
    }
}
