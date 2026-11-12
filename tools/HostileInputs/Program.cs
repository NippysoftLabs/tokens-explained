using System.Globalization;
using TokensExplained;

// Sends unusual or malformed inputs to the "after" code and prints what happens with each one.
// It shows the limits of the examples; it is not part of the test run.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var counter = new TokenCounter();
var price = PriceList.Load(Path.Combine(AppContext.BaseDirectory, "config", "prices.json"))[0];
var number = 0;

void Run(string name, Func<object?> action)
{
    number++;
    string result;
    try
    {
        result = action() switch
        {
            IEnumerable<string> list => string.Join(" | ", list.Select(p => $"[{p}]")),
            var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null",
        };
    }
    catch (Exception ex)
    {
        result = $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
    }
    Console.WriteLine($"{number}. {name}");
    Console.WriteLine($"    -> {result}");
}

Console.WriteLine("A. Token counter (o200k_base)");
Run("empty string", () => counter.Count(""));
Run("five spaces", () => counter.Count("     "));
Run("null", () => counter.Count(null!));
Run("\"canción\" with the accent as one character (NFC)", () => counter.Count("canción"));
Run("\"canción\" with the accent as a separate mark (NFD)", () => counter.Count("canción".Normalize(System.Text.NormalizationForm.FormD)));
Run("pieces of the NFD form", () => counter.Pieces("canción".Normalize(System.Text.NormalizationForm.FormD)));
Run("thumbs up with skin tone emoji", () => counter.Pieces("\U0001F44D\U0001F3FD"));
Run("family emoji joined with ZWJ", () => counter.Count("\U0001F468‍\U0001F469‍\U0001F467"));
Run("Chinese \"hello world\"", () => counter.Pieces("你好世界"));
Run("20-digit number", () => counter.Pieces("12345678901234567890"));
Run("same word 100 times", () => counter.Count(string.Join(' ', Enumerable.Repeat("factura", 100))));
Run("lone surrogate", () => counter.Pieces("a\uD800b"));
Run("real U+FFFD in the text (badly decoded file)", () => counter.Pieces("Error � in the file name."));
Run("real U+FFFD followed by 8,000 words, pieces timed", () =>
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var count = counter.Pieces("�" + string.Concat(Enumerable.Repeat(" word", 8_000))).Count;
    return $"{count} pieces in {watch.ElapsedMilliseconds} ms";
});
Run("three lines with Windows (CRLF) and Unix (LF) line endings", () => counter.Count("a\r\nb\r\nc") + " and " + counter.Count("a\nb\nc"));

Run("'a' repeated 5,000 times, timed (times vary by machine)", () => Timed(() => counter.Count(new string('a', 5_000))));
Run("'a' repeated 10,000 times, timed", () => Timed(() => counter.Count(new string('a', 10_000))));
Run("'a' repeated 10,001 times (over the run limit)", () => counter.Count(new string('a', 10_001)));
Run("'a' repeated 20,000 times with a counter that allows it, timed", () => Timed(() => new TokenCounter(maxRunLength: 20_000).Count(new string('a', 20_000))));
Run("10,000 words \"hello \" (with spaces), timed", () => Timed(() => counter.Count(string.Concat(Enumerable.Repeat("hello ", 10_000)))));

Console.WriteLine();
Console.WriteLine("B. Price list (config/prices.json format)");
const string ok = "\"inputPerMillion\":2,\"outputPerMillion\":10,\"cacheReadPerMillion\":0.1,\"cacheWritePerMillion\":2.5,\"minCacheableTokens\":512,\"source\":\"https://example.com/pricing\",\"checkedOn\":\"2026-11-12\"";
Run("valid entry", () => PriceList.Parse($"[{{\"model\":\"m\",{ok}}}]").Count);
Run("no source", () => PriceList.Parse($"[{{\"model\":\"m\",{ok.Replace("\"source\":\"https://example.com/pricing\",", "")}}}]").Count);
Run("negative output price", () => PriceList.Parse($"[{{\"model\":\"m\",{ok.Replace("\"outputPerMillion\":10", "\"outputPerMillion\":-10")}}}]").Count);
Run("price as a string", () => PriceList.Parse($"[{{\"model\":\"m\",{ok.Replace("\"inputPerMillion\":2", "\"inputPerMillion\":\"2\"")}}}]").Count);
Run("unknown property \"currency\"", () => PriceList.Parse($"[{{\"model\":\"m\",\"currency\":\"EUR\",{ok}}}]").Count);
Run("property twice", () => PriceList.Parse($"[{{\"model\":\"m\",\"model\":\"n\",{ok}}}]").Count);
Run("same model twice", () => PriceList.Parse($"[{{\"model\":\"m\",{ok}}},{{\"model\":\"M\",{ok}}}]").Count);
Run("date as 12/11/2026", () => PriceList.Parse($"[{{\"model\":\"m\",{ok.Replace("2026-11-12", "12/11/2026")}}}]").Count);
Run("cache write null (no published write price)", () => PriceList.Parse($"[{{\"model\":\"m\",{ok.Replace("\"cacheWritePerMillion\":2.5", "\"cacheWritePerMillion\":null")}}}]")[0].CacheWritePerMillion ?? -1m);
Run("JSON null", () => PriceList.Parse("null").Count);
Run("empty list", () => PriceList.Parse("[]").Count);

Console.WriteLine();
Console.WriteLine("C. Chat simulator");
Run("no turns", () => ChatSimulator.Run(1_000, [], price, useCache: true).Count);
Run("negative message size", () => ChatSimulator.Run(1_000, [new Turn(-5, 10)], price, useCache: false).Count);
Run("null turn", () => ChatSimulator.Run(1_000, [null!], price, useCache: false).Count);
Run("chat below the cache minimum (100-token system prompt, 3 short turns) with cache on", () =>
    string.Join("; ", ChatSimulator.Run(100, [new Turn(20, 30), new Turn(20, 30), new Turn(20, 30)], price, useCache: true)
        .Select(t => $"turn {t.Number}: input {t.Usage.Input}, write {t.Usage.CacheWrite}, read {t.Usage.CacheRead}")));
Run("crosses the minimum on turn 3", () =>
    string.Join("; ", ChatSimulator.Run(400, [new Turn(20, 30), new Turn(20, 30), new Turn(20, 30)], price, useCache: true)
        .Select(t => $"turn {t.Number}: input {t.Usage.Input}, write {t.Usage.CacheWrite}, read {t.Usage.CacheRead}")));
Run("1,000 turns of 2,000 + 2,000 tokens (input sent on the last turn)", () =>
    ChatSimulator.Run(0, Enumerable.Repeat(new Turn(2_000, 2_000), 1_000).ToList(), price, useCache: false)[^1].Usage.TotalInput);

Console.WriteLine();
Console.WriteLine("D. Budget check");
Run("max_tokens 0", () => RequestBudget.Check(1_000, 0, price, 1m));
Run("negative limit", () => RequestBudget.Check(1_000, 100, price, -1m));
Run("worst case exactly at the limit", () => RequestBudget.Check(1_000_000, 100_000, price, 3m));
Run("negative usage", () => RequestCost.Of(new Usage(-1, 0), price));
var handMade = new Price("typo", -2m, 10m, 0.1m, 2.5m, 512, "https://example.com/pricing", "2026-11-12");
Run("price built in code with a negative input price, cost of 1,000 input tokens", () => RequestCost.Of(new Usage(1_000, 0), handMade));
Run("the same price in a budget check with limit 0", () => RequestBudget.Check(1_000_000, 1, handMade, 0m));

static string Timed(Func<int> count)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var tokens = count();
    return $"{tokens} tokens in {watch.ElapsedMilliseconds} ms";
}
