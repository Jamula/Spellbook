namespace Spellbook.Models;

/// <summary>
/// Minimal public Scryfall card details, returned after explicit exact-name lookup.
/// Stored only in page memory; no private deck data is sent to Scryfall.
/// </summary>
public sealed class ScryfallCard
{
    public string Name { get; set; } = "";
    public double ManaValue { get; set; }
    public string TypeLine { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? ScryfallUrl { get; set; }
}
