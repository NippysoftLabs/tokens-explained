namespace TokensExplained.Before;

/// <summary>
/// BEFORE: estimates the size of a prompt by counting words, the way a person reads it.
/// Models are billed and limited in tokens, and a word can be one token or several.
/// </summary>
public static class WordEstimate
{
    public static int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
