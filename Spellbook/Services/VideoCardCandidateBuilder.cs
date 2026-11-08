namespace Spellbook.Services;

public sealed record VideoOcrFrame(double Time, string Text, int? StackCountEstimate);

public sealed class VideoCardCandidate
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public double LastSeen { get; set; }
    public bool HasStackEstimate { get; set; }
    public string EstimateLabel => HasStackEstimate
        ? $"Estimated from visible edges: {Quantity} · low confidence, correct before adding"
        : "Stack estimate unavailable · enter quantity manually";
}

public static class VideoCardCandidateBuilder
{
    public static List<VideoCardCandidate> Build(IEnumerable<VideoOcrFrame> frames)
    {
        var results = new List<VideoCardCandidate>();
        var lastByName = new Dictionary<string, VideoCardCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in frames.OrderBy(f => f.Time))
        {
            var name = CardTitleExtractor.FromOcr(frame.Text).Trim();
            if (name.Length is < 2 or > 200) continue;
            var key = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (lastByName.TryGetValue(key, out var prior) && frame.Time - prior.LastSeen <= 2.25)
            {
                prior.LastSeen = frame.Time;
                ApplyEstimate(prior, frame.StackCountEstimate);
                continue;
            }
            var candidate = new VideoCardCandidate { Name = key, LastSeen = frame.Time };
            ApplyEstimate(candidate, frame.StackCountEstimate);
            results.Add(candidate);
            lastByName[key] = candidate;
        }
        return results;
    }

    private static void ApplyEstimate(VideoCardCandidate candidate, int? estimate)
    {
        if (estimate is >= 2 and <= 250)
        {
            candidate.Quantity = estimate.Value;
            candidate.HasStackEstimate = true;
        }
    }
}
