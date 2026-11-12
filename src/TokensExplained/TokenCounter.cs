using Microsoft.ML.Tokenizers;

namespace TokensExplained;

/// <summary>
/// Counts tokens with o200k_base, the public encoding that OpenAI's tiktoken maps to GPT-4o. It runs
/// offline and calls no API. Other models, Claude and Gemini among them, use their own tokenizers, so the
/// same text gives a different count there: use each provider's count endpoint for those models.
/// </summary>
public sealed class TokenCounter
{
    public const string EncodingName = "o200k_base";

    /// <summary>The default for the longest stretch of text without whitespace that is counted.</summary>
    public const int DefaultMaxRunLength = 10_000;

    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding(EncodingName);
    private readonly int _maxRunLength;

    /// <param name="maxRunLength">
    /// Counting time grows with the square of the length of a stretch without whitespace: 'a' repeated
    /// 20,000 times took 3.5 to 3.7 times as long as 10,000 in the runs for this repository. Text from users (a pasted base64 image, a
    /// minified file) can block the thread, so a longer stretch is rejected instead of counted.
    /// </param>
    public TokenCounter(int maxRunLength = DefaultMaxRunLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRunLength);
        _maxRunLength = maxRunLength;
    }

    public int Count(string text)
    {
        CheckInput(text);
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// The text of each token, in order. A token can hold part of a character: an emoji or an accented
    /// letter is several bytes in UTF-8 and can be split across tokens. Tokens that are not readable text
    /// on their own are joined until they form whole characters, shown as "text (n tokens)".
    /// </summary>
    public IReadOnlyList<string> Pieces(string text)
    {
        CheckInput(text);
        var pieces = new List<string>();
        var pending = new List<int>();
        foreach (var id in _tokenizer.EncodeToIds(text))
        {
            pending.Add(id);
            var decoded = _tokenizer.Decode(pending) ?? "";
            // Still part of a character: the decoded text ends in U+FFFD. A UTF-8 character has at most 4
            // bytes, so after 4 tokens the U+FFFD is real text (a badly decoded file has them) and is shown.
            if (decoded.EndsWith(Replacement) && pending.Count < 4) continue;
            pieces.Add(pending.Count == 1 ? decoded : $"{decoded} ({pending.Count} tokens)");
            pending.Clear();
        }
        if (pending.Count > 0) pieces.Add($"{_tokenizer.Decode(pending)} ({pending.Count} tokens)");
        return pieces;
    }

    private void CheckInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var run = 0;
        foreach (var c in text)
        {
            run = char.IsWhiteSpace(c) ? 0 : run + 1;
            if (run > _maxRunLength)
                throw new ArgumentException(
                    $"The text has more than {_maxRunLength} characters in a row without whitespace; counting it would be slow.",
                    nameof(text));
        }
    }

    // U+FFFD, what UTF-8 decoding gives for an incomplete character.
    private const char Replacement = '\uFFFD';
}
