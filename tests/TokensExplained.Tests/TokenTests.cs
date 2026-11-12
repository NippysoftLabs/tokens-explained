using System.Text.Json;
using TokensExplained.Before;

namespace TokensExplained.Tests;

public class TokenTests
{
    private static readonly TokenCounter Counter = new();

    private static List<ParallelText> Texts() => JsonSerializer.Deserialize<List<ParallelText>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "parallel-texts.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void The_count_matches_the_example_in_the_Microsoft_documentation()
    {
        // learn.microsoft.com/dotnet/ai/how-to/use-tokenizers counts this sentence with gpt-5 (o200k_base): 16.
        Assert.Equal(16, Counter.Count("Text tokenization is the process of splitting a string into a list of tokens."));
    }

    [Fact]
    public void Before_words_are_not_tokens()
    {
        const string prompt = "Summarize this contract in five points and point out the penalty clauses for late delivery.";

        Assert.Equal(15, WordEstimate.Count(prompt));
        Assert.Equal(18, Counter.Count(prompt));
    }

    [Fact]
    public void After_the_tokenizer_cuts_long_or_rare_words_into_pieces()
    {
        const string prompt = "Resume este contrato en cinco puntos y señala las cláusulas de penalización por retraso en la entrega.";
        var pieces = Counter.Pieces(prompt);

        Assert.Equal(21, pieces.Count);
        Assert.Equal(["Resume", " este", " contrato"], pieces.Take(3));
        Assert.Contains(" cláus", pieces);
        Assert.Contains("ulas", pieces);
        Assert.Equal(prompt, string.Concat(pieces));
    }

    [Fact]
    public void A_character_split_across_tokens_is_shown_whole()
    {
        // The skin tone modifier is two tokens; Pieces joins them so the text stays readable.
        Assert.Equal(["\U0001F44D", "\U0001F3FD (2 tokens)"], Counter.Pieces("\U0001F44D\U0001F3FD"));
    }

    [Fact]
    public void A_real_replacement_character_does_not_swallow_the_rest_of_the_text()
    {
        const string text = "Error � in the file name, please check the other twenty words here.";

        var pieces = Counter.Pieces(text);

        Assert.Equal(15, Counter.Count(text));
        Assert.Equal(14, pieces.Count);
        Assert.Equal(" � in (2 tokens)", pieces[1]);
        Assert.Equal(" file", pieces[3]);
    }

    [Fact]
    public void The_same_text_in_Spanish_takes_more_tokens_than_in_English()
    {
        var comparisons = Texts().Select(t => LanguageComparison.Of(Counter, t)).ToList();

        Assert.Equal(
            [("udhr-article-1", 38, 33), ("udhr-article-3", 19, 13), ("support-reply", 38, 38), ("meeting-summary", 32, 26), ("prompt", 21, 18)],
            comparisons.Select(c => (c.Name, c.SpanishTokens, c.EnglishTokens)));
        // Not every text: the support reply is a tie. Over the five, Spanish takes 148 tokens and English 128.
        Assert.Equal(148, comparisons.Sum(c => c.SpanishTokens));
        Assert.Equal(128, comparisons.Sum(c => c.EnglishTokens));
    }

    [Fact]
    public void A_long_stretch_without_whitespace_is_rejected_instead_of_counted_slowly()
    {
        Assert.Equal(1_250, Counter.Count(new string('a', TokenCounter.DefaultMaxRunLength)));
        var ex = Assert.Throws<ArgumentException>(() => Counter.Count(new string('a', TokenCounter.DefaultMaxRunLength + 1)));
        Assert.Contains("without whitespace", ex.Message);
        // Whitespace resets the stretch: two halves separated by a space are counted.
        Assert.Equal(1_251, Counter.Count(new string('a', 6_000) + " " + new string('a', 4_001)));
    }

    [Fact]
    public void Null_text_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Counter.Count(null!));
        Assert.Throws<ArgumentNullException>(() => Counter.Pieces(null!));
    }
}
