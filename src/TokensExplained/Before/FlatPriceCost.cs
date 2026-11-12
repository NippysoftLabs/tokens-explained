namespace TokensExplained.Before;

/// <summary>
/// BEFORE: prices every token the same, at the input price shown first on the pricing page.
/// Output tokens cost more than input tokens, so a long answer is underestimated.
/// </summary>
public static class FlatPriceCost
{
    public static decimal Of(long inputTokens, long outputTokens, Price price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return (inputTokens + outputTokens) * price.InputPerMillion / 1_000_000m;
    }
}
