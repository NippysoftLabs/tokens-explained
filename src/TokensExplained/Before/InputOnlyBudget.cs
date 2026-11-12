namespace TokensExplained.Before;

/// <summary>
/// BEFORE: checks the budget with the input alone. The answer is billed too, at a higher price,
/// so a request that passes this check can still go over the limit.
/// </summary>
public static class InputOnlyBudget
{
    public static bool Allows(long inputTokens, Price price, decimal limitUsd)
    {
        ArgumentNullException.ThrowIfNull(price);
        return inputTokens * price.InputPerMillion / 1_000_000m <= limitUsd;
    }
}
