namespace Spellbook.Models;

/// <summary>Optional details explicitly supplied by the user or a confirmed lookup.</summary>
public sealed class CardDetails
{
    public double? ManaValue { get; set; }
    public string? TypeLine { get; set; }
    // W/U/B/R/G, or C for colorless. Missing/invalid values remain unknown.
    public string? Colors { get; set; }
    public bool RolesReviewed { get; set; }
    public bool Draw { get; set; }
    public bool Ramp { get; set; }
    public bool Removal { get; set; }
}
