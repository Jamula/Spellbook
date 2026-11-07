using Spellbook.Models;

namespace Spellbook.Services;

public sealed class DeckAnalysis
{
    public int Total { get; private set; }
    public int OutsideMain { get; private set; }
    public int Lands { get; private set; }
    public int UnknownTypes { get; private set; }
    public int UnknownMana { get; private set; }
    public int UnknownColors { get; private set; }
    public int UnknownRoles { get; private set; }
    public int Draw { get; private set; }
    public int Ramp { get; private set; }
    public int Removal { get; private set; }
    public int[] Curve { get; } = new int[8];
    public Dictionary<char, int> Colors { get; } = "WUBRGC".ToDictionary(c => c, _ => 0);
    public List<string> Advice { get; } = [];

    public static DeckAnalysis Calculate(Deck deck)
    {
        var result = new DeckAnalysis();
        foreach (var card in deck.Cards)
        {
            if (!string.Equals(card.Section.Trim(), "Main", StringComparison.OrdinalIgnoreCase))
            {
                result.OutsideMain += card.Quantity;
                continue;
            }
            var count = card.Quantity;
            result.Total += count;
            var details = card.Details;
            var type = details?.TypeLine;
            var knownType = !string.IsNullOrWhiteSpace(type) && !type.Contains("//", StringComparison.Ordinal);
            // Only the type portion before the em dash contains card types.
            var isLand = knownType && type!.Split('—')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("Land", StringComparer.OrdinalIgnoreCase);
            if (!knownType) result.UnknownTypes += count;
            if (isLand) result.Lands += count;
            else if (knownType && details!.ManaValue is double mana && double.IsFinite(mana) && mana >= 0)
                result.Curve[(int)Math.Min(7, Math.Floor(mana))] += count;
            else result.UnknownMana += count;

            var colors = details?.Colors?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(colors) || colors.Any(c => !"WUBRGC".Contains(c)) ||
                (colors.Contains('C') && colors.Length != 1)) result.UnknownColors += count;
            else foreach (var color in colors.Distinct()) result.Colors[color] += count;
            if (details?.RolesReviewed != true) result.UnknownRoles += count;
            else
            {
                if (details.Draw) result.Draw += count;
                if (details.Ramp) result.Ramp += count;
                if (details.Removal) result.Removal += count;
            }
        }
        if (result.Total == 0) result.Advice.Add("Add cards to the main deck to begin a local analysis.");
        else
        {
            if (result.UnknownTypes + result.UnknownMana + result.UnknownColors + result.UnknownRoles > 0)
                result.Advice.Add("Some details are unknown. Review card details before using these partial counts to tune the deck.");
            if (result.UnknownTypes == 0)
            {
                var fraction = (double)result.Lands / result.Total;
                result.Advice.Add(fraction < .33
                    ? $"Lands are {result.Lands} of {result.Total} main-deck cards (below 33%). Try more lands if opening hands miss land drops; mana costs and playtesting may justify a different balance."
                    : fraction > .45
                        ? $"Lands are {result.Lands} of {result.Total} main-deck cards (above 45%). Playtest for flooding before replacing any lands."
                        : $"Lands are {result.Lands} of {result.Total} main-deck cards (33–45%). This is a starting point, not a guarantee of reliable mana.");
            }
            if (result.UnknownMana == 0 && result.Curve.Sum() > 0 && result.Curve.Skip(4).Sum() * 2 > result.Curve.Sum())
                result.Advice.Add("More than half of the known nonland cards cost 4+ mana. Playtest early turns and consider cheaper plays if the deck starts slowly.");
            if (result.UnknownRoles == 0)
                result.Advice.Add($"Your reviewed roles total {result.Draw} draw, {result.Ramp} ramp and {result.Removal} removal copies. For casual one-on-one, test whether you can refill your hand and answer an opposing threat; roles can overlap and are not a power rating.");
            result.Advice.Add(deck.Format == "Two-Headed Giant"
                ? "For Two-Headed Giant, compare these counts with your teammate's deck and agree who handles early defense and answers threats. Shared life does not make card effects automatically apply to a teammate; read each card's wording."
                : "For casual one-on-one, agree on power level with your opponent and use opening-hand playtests to check this deck's plan.");
        }
        return result;
    }
}
