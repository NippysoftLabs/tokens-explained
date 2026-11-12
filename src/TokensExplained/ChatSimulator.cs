namespace TokensExplained;

/// <summary>One turn of a chat: the tokens of the user's message and of the model's answer.</summary>
public sealed record Turn(int UserTokens, int AssistantTokens);

/// <summary>What one turn sends and costs.</summary>
public sealed record TurnCost(int Number, Usage Usage, decimal Cost);

/// <summary>
/// AFTER: a chat as the API sees it. The API keeps no memory between requests, so turn n sends the
/// system prompt, every earlier message and answer, and the new message. Prices come from a Price.
/// </summary>
public static class ChatSimulator
{
    /// <param name="useCache">
    /// Models automatic prompt caching as the Claude API documents it: each request marks its last
    /// message, so turn n reads what turn n-1 sent from the cache and writes the rest (the previous
    /// answer and the new message). A request shorter than the price's MinCacheableTokens is not cached.
    /// The cache is assumed not to expire between turns.
    /// </param>
    public static IReadOnlyList<TurnCost> Run(int systemTokens, IReadOnlyList<Turn> turns, Price price, bool useCache)
    {
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentOutOfRangeException.ThrowIfNegative(systemTokens);
        price.Validate();

        var result = new List<TurnCost>(turns.Count);
        long history = systemTokens; // what every later request sends again
        long cached = 0;             // the prefix already in the cache
        for (var i = 0; i < turns.Count; i++)
        {
            var turn = turns[i] ?? throw new ArgumentException($"Turn {i + 1} is null.", nameof(turns));
            ArgumentOutOfRangeException.ThrowIfNegative(turn.UserTokens, $"turns[{i}].UserTokens");
            ArgumentOutOfRangeException.ThrowIfNegative(turn.AssistantTokens, $"turns[{i}].AssistantTokens");

            var sent = history + turn.UserTokens;
            var usage = useCache && sent >= price.MinCacheableTokens
                ? new Usage(Input: 0, Output: turn.AssistantTokens, CacheRead: cached, CacheWrite: sent - cached)
                : new Usage(Input: sent, Output: turn.AssistantTokens);
            if (useCache && sent >= price.MinCacheableTokens) cached = sent;

            result.Add(new TurnCost(i + 1, usage, RequestCost.Of(usage, price)));
            history = sent + turn.AssistantTokens;
        }
        return result;
    }
}
