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
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Deck>>(json, Options) ?? [];
        }
        catch (JSException) { return []; }
        catch (JsonException) { return []; }
    }
    public Task SaveAsync(List<Deck> decks) => js.InvokeVoidAsync("spellbookStorage.set", Key, JsonSerializer.Serialize(decks, Options)).AsTask();
    public Task ExportAsync(List<Deck> decks) => js.InvokeVoidAsync("spellbookStorage.download", "spellbook-decks.json", JsonSerializer.Serialize(decks, Options)).AsTask();
    public List<Deck> ParseImport(string json)
    {
        var data = JsonSerializer.Deserialize<List<Deck>>(json, Options) ?? throw new InvalidDataException("Invalid deck file");
        if (data.Count > 500 || data.Any(d => (d.Cards?.Count ?? 0) > 1000 || (d.Cards?.Any(c => c.Quantity < 1 || c.Quantity > 250) ?? false)))
            throw new InvalidDataException("The imported file exceeds the supported size or quantity limits.");
        foreach (var d in data) { d.Cards ??= []; d.Name ??= "Imported deck"; }
        return data;
    }
}
