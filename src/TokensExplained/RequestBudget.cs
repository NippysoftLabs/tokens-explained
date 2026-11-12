namespace TokensExplained;

/// <summary>The decision for one request, with the most it can cost.</summary>
public sealed record BudgetDecision(bool Allowed, decimal WorstCaseUsd, string Reason);

/// <summary>
/// AFTER: checks a request before it is sent. The input is known (count it first); the output is not,
/// so the check assumes the model writes up to maxOutputTokens, the cap the request sets (max_tokens).
/// </summary>
public static class RequestBudget
{
    public static BudgetDecision Check(long inputTokens, int maxOutputTokens, Price price, decimal limitUsd)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        // Without an output cap there is no worst case to compute, so the request is not allowed.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(limitUsd);

        var worstCase = RequestCost.Of(new Usage(inputTokens, maxOutputTokens), price);
        return worstCase <= limitUsd
            ? new BudgetDecision(true, worstCase, "within the limit")
            : new BudgetDecision(false, worstCase, $"up to {worstCase:0.######} USD, over the limit of {limitUsd:0.######} USD");
    }
}
