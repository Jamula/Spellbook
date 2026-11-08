namespace Spellbook.Services;

public sealed record VideoOcrFrame(double Time, string Text, int? StackCountEstimate, int SampleIndex = -1);

public sealed class VideoCardCandidate
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public double LastSeen { get; set; }
    public int LastSampleIndex { get; set; } = -1;
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
            if (lastByName.TryGetValue(key, out var prior) &&
                (frame.Time - prior.LastSeen <= 2.25 || frame.SampleIndex >= 0 && prior.LastSampleIndex >= 0 && frame.SampleIndex - prior.LastSampleIndex <= 1))
            {
                prior.LastSeen = frame.Time;
                prior.LastSampleIndex = frame.SampleIndex;
                ApplyEstimate(prior, frame.StackCountEstimate);
                continue;
            }
            var candidate = new VideoCardCandidate { Name = key, LastSeen = frame.Time, LastSampleIndex = frame.SampleIndex };
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
