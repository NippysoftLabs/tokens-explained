namespace TokensExplained;

/// <summary>
/// A model's token prices in US dollars per million tokens, as the provider's pricing page lists them.
/// CacheWrite is null when the provider publishes no separate price for writing to the cache;
/// the simulator then charges those tokens at the base input price.
/// </summary>
public sealed record Price(
    string Model,
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal CacheReadPerMillion,
    decimal? CacheWritePerMillion,
    int MinCacheableTokens,
    string Source,
    string CheckedOn)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Model)) throw new ArgumentException("A price needs a model name.");
        if (string.IsNullOrWhiteSpace(Source)) throw new ArgumentException($"{Model}: a price needs the page it comes from.");
        if (!DateOnly.TryParseExact(CheckedOn, "yyyy-MM-dd", out _))
            throw new ArgumentException($"{Model}: checkedOn must be a date like 2026-11-12, not '{CheckedOn}'.");
        foreach (var (name, value) in new (string, decimal?)[]
                 {
                     ("input", InputPerMillion), ("output", OutputPerMillion),
                     ("cache read", CacheReadPerMillion), ("cache write", CacheWritePerMillion),
                 })
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name, value, $"{Model}: a price cannot be negative.");
        }
        if (MinCacheableTokens < 0) throw new ArgumentOutOfRangeException(nameof(MinCacheableTokens));
    }

    internal static decimal Cost(long tokens, decimal perMillion) => tokens * perMillion / 1_000_000m;
}
