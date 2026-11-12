namespace TokensExplained.Before;

/// <summary>
/// BEFORE: charges each turn only for the new message and its answer, as if the model remembered the
/// rest of the chat. The API sends the whole conversation again on every turn, so this undercounts.
/// </summary>
public static class NaiveChatCost
{
    public static IReadOnlyList<TurnCost> Run(int systemTokens, IReadOnlyList<Turn> turns, Price price)
    {
        ArgumentNullException.ThrowIfNull(turns);
        return turns
            .Select((turn, i) =>
            {
                var usage = new Usage(Input: (i == 0 ? systemTokens : 0) + turn.UserTokens, Output: turn.AssistantTokens);
                return new TurnCost(i + 1, usage, RequestCost.Of(usage, price));
            })
            .ToList();
    }
}
