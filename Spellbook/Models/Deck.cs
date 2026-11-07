namespace Spellbook.Models;

public sealed class Deck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Deck";
    public string Format { get; set; } = "Casual 60-card";
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<DeckCard> Cards { get; set; } = [];
    public int CardCount => Cards.Sum(c => c.Quantity);
}

public sealed class DeckCard
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Section { get; set; } = "Main";
}
