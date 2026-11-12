namespace TokensExplained;

/// <summary>The same text in Spanish and English, counted with the same tokenizer.</summary>
public sealed record LanguageComparison(
    string Name,
    int SpanishWords,
    int EnglishWords,
    int SpanishTokens,
    int EnglishTokens)
{
    /// <summary>Spanish tokens per English token. Above 1, the Spanish version costs more.</summary>
    public decimal Ratio => EnglishTokens == 0 ? 0m : Math.Round((decimal)SpanishTokens / EnglishTokens, 2);

    public static LanguageComparison Of(TokenCounter counter, ParallelText text)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentNullException.ThrowIfNull(text);
        return new LanguageComparison(
            text.Name,
            Before.WordEstimate.Count(text.Spanish),
            Before.WordEstimate.Count(text.English),
            counter.Count(text.Spanish),
            counter.Count(text.English));
    }
}

/// <summary>One text in both languages, with where it comes from.</summary>
public sealed record ParallelText(string Name, string Source, string Spanish, string English);
