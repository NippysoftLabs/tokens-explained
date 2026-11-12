using Microsoft.ML.Tokenizers;

namespace TokensExplained;

/// <summary>
/// Counts tokens with o200k_base, the public encoding that OpenAI's tiktoken uses for GPT-4o and later
/// OpenAI models. It runs offline and calls no API. Claude and Gemini use their own tokenizers, so the
/// same text gives a different count there: use each provider's count endpoint for those models.
/// </summary>
public sealed class TokenCounter
{
    public const string EncodingName = "o200k_base";

    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding(EncodingName);

    public int Count(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// The text of each token, in order. A token can hold part of a character: an emoji or an accented
    /// letter is several bytes in UTF-8 and can be split across tokens. Tokens that are not readable text
    /// on their own are joined until they form whole characters, shown as "text (n tokens)".
    /// </summary>
    public IReadOnlyList<string> Pieces(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pieces = new List<string>();
        var pending = new List<int>();
        foreach (var id in _tokenizer.EncodeToIds(text))
        {
            pending.Add(id);
            var decoded = _tokenizer.Decode(pending) ?? "";
            if (decoded.Contains(Replacement)) continue; // still part of a character
            pieces.Add(pending.Count == 1 ? decoded : $"{decoded} ({pending.Count} tokens)");
            pending.Clear();
        }
        if (pending.Count > 0) pieces.Add($"{_tokenizer.Decode(pending)} ({pending.Count} tokens)");
        return pieces;
    }

    // U+FFFD, what UTF-8 decoding gives for an incomplete character.
    private const char Replacement = '\uFFFD';
}
