namespace TokensExplained;

/// <summary>
/// Token usage of one request, split the way providers bill it. Input counts only the input tokens that
/// were neither read from nor written to the cache, as the usage of the Claude API reports input_tokens.
/// </summary>
public sealed record Usage(long Input, long Output, long CacheRead = 0, long CacheWrite = 0)
{
    public long TotalInput => Input + CacheRead + CacheWrite;

    public void Validate()
    {
        if (Input < 0 || Output < 0 || CacheRead < 0 || CacheWrite < 0)
            throw new ArgumentOutOfRangeException(nameof(Usage), this, "Token counts cannot be negative.");
    }
}

/// <summary>AFTER: each kind of token at its own price.</summary>
public static class RequestCost
{
    public static decimal Of(Usage usage, Price price)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(price);
        usage.Validate();
        price.Validate(); // a Price built in code skips PriceList, which is where prices are otherwise checked
        return Price.Cost(usage.Input, price.InputPerMillion)
             + Price.Cost(usage.Output, price.OutputPerMillion)
             + Price.Cost(usage.CacheRead, price.CacheReadPerMillion)
             + Price.Cost(usage.CacheWrite, price.CacheWritePerMillion ?? price.InputPerMillion);
    }
}
