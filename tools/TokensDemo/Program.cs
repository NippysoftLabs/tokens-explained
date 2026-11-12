using System.Globalization;
using System.Text.Json;
using TokensExplained;
using TokensExplained.Before;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var counter = new TokenCounter();
var prices = PriceList.Load(Path.Combine(AppContext.BaseDirectory, "config", "prices.json"));
var texts = JsonSerializer.Deserialize<List<ParallelText>>(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "parallel-texts.json")),
    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

Console.WriteLine($"Tokenizer: {TokenCounter.EncodingName} (OpenAI's public encoding). Claude and Gemini count differently.");
Console.WriteLine();

// 1. What a token is: the pieces the tokenizer cuts.
foreach (var sample in new[] { texts[4].English, texts[4].Spanish })
{
    var pieces = counter.Pieces(sample);
    Console.WriteLine($"{WordEstimate.Count(sample)} words, {pieces.Count} tokens: {string.Join(" | ", pieces.Select(p => $"[{p}]"))}");
}
Console.WriteLine();

// 2. Spanish and English, same tokenizer.
Console.WriteLine($"{"text",-16} {"words es",9} {"words en",9} {"tokens es",10} {"tokens en",10} {"es/en",6}");
int totalEs = 0, totalEn = 0;
foreach (var text in texts)
{
    var c = LanguageComparison.Of(counter, text);
    totalEs += c.SpanishTokens;
    totalEn += c.EnglishTokens;
    Console.WriteLine($"{c.Name,-16} {c.SpanishWords,9} {c.EnglishWords,9} {c.SpanishTokens,10} {c.EnglishTokens,10} {c.Ratio,6:0.00}");
}
Console.WriteLine($"{"all",-16} {"",9} {"",9} {totalEs,10} {totalEn,10} {Math.Round((decimal)totalEs / totalEn, 2),6:0.00}");
Console.WriteLine();

// 3 to 5. A chat that grows. The sizes are an assumption for the example, not a measurement.
var price = prices.First(p => p.Model == "claude-sonnet-5-5");
const int systemTokens = 1_000;
var turns = Enumerable.Repeat(new Turn(UserTokens: 60, AssistantTokens: 400), 20).ToList();
Console.WriteLine($"Chat of {turns.Count} turns: system prompt {systemTokens} tokens, each message 60, each answer 400 (assumed sizes).");
Console.WriteLine($"Prices: {price.Model}, USD per million tokens: input {price.InputPerMillion}, output {price.OutputPerMillion}, " +
                  $"cache write {price.CacheWritePerMillion}, cache read {price.CacheReadPerMillion} ({price.Source}, checked {price.CheckedOn}).");

var naive = NaiveChatCost.Run(systemTokens, turns, price);
var full = ChatSimulator.Run(systemTokens, turns, price, useCache: false);
var cached = ChatSimulator.Run(systemTokens, turns, price, useCache: true);
Console.WriteLine($"{"turn",4} {"input sent",11} {"cost",10} {"naive cost",11} {"cached cost",12}");
foreach (var n in new[] { 1, 2, 5, 10, 20 })
{
    var i = n - 1;
    Console.WriteLine($"{n,4} {full[i].Usage.TotalInput,11} {full[i].Cost,10:0.000000} {naive[i].Cost,11:0.000000} {cached[i].Cost,12:0.000000}");
}
Console.WriteLine($"{"sum",4} {full.Sum(t => t.Usage.TotalInput),11} {full.Sum(t => t.Cost),10:0.000000} {naive.Sum(t => t.Cost),11:0.000000} {cached.Sum(t => t.Cost),12:0.000000}");
Console.WriteLine();

// Flat price versus separate prices, for turn 20.
var last = full[^1].Usage;
Console.WriteLine($"Turn 20 priced flat at the input price: {FlatPriceCost.Of(last.Input, last.Output, price):0.000000} USD; " +
                  $"with the output price: {RequestCost.Of(last, price):0.000000} USD.");

// Measure before paying.
var input = full[^1].Usage.TotalInput;
Console.WriteLine($"Budget 0.02 USD for turn 20 with max_tokens 4000: input only says {(InputOnlyBudget.Allows(input, price, 0.02m) ? "allowed" : "refused")}; " +
                  $"worst case says {RequestBudget.Check(input, 4_000, price, 0.02m) switch { { Allowed: true } d => $"allowed ({d.WorstCaseUsd:0.000000} USD)", var d => $"refused ({d.Reason})" }}.");
