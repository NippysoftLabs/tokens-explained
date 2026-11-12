using System.Text.Json;
using TokensExplained.Before;

namespace TokensExplained.Tests;

public class CostTests
{
    private static readonly IReadOnlyList<Price> Prices =
        PriceList.Load(Path.Combine(AppContext.BaseDirectory, "config", "prices.json"));

    private static Price Sonnet => Prices.Single(p => p.Model == "claude-sonnet-5-5");

    // A chat of 20 turns: 1,000-token system prompt, 60-token messages, 400-token answers (assumed sizes).
    private const int SystemTokens = 1_000;
    private static readonly List<Turn> Turns = Enumerable.Repeat(new Turn(60, 400), 20).ToList();

    [Fact]
    public void Every_price_names_its_source_and_the_day_it_was_checked()
    {
        Assert.Equal(["claude-sonnet-5-5", "gpt-6.1-sol", "gemini-4-carbon"], Prices.Select(p => p.Model));
        Assert.All(Prices, p => Assert.StartsWith("https://", p.Source));
        Assert.All(Prices, p => Assert.Equal("2026-11-12", p.CheckedOn));
    }

    [Fact]
    public void Output_costs_five_times_input_in_all_three_listed_models()
    {
        Assert.All(Prices, p => Assert.Equal(5m, p.OutputPerMillion / p.InputPerMillion));
    }

    [Fact]
    public void Before_a_flat_price_undercounts_the_answer()
    {
        var usage = new Usage(Input: 1_000, Output: 2_000);

        Assert.Equal(0.006m, FlatPriceCost.Of(usage.Input, usage.Output, Sonnet));
        Assert.Equal(0.022m, RequestCost.Of(usage, Sonnet));
    }

    [Fact]
    public void After_each_turn_sends_the_whole_chat_again()
    {
        var turns = ChatSimulator.Run(SystemTokens, Turns, Sonnet, useCache: false);

        Assert.Equal(1_060, turns[0].Usage.Input);
        Assert.Equal(1_520, turns[1].Usage.Input);   // system + message 1 + answer 1 + message 2
        Assert.Equal(9_800, turns[19].Usage.Input);
        Assert.Equal(108_600, turns.Sum(t => t.Usage.Input));
        Assert.Equal(0.00612m, turns[0].Cost);
        Assert.Equal(0.0236m, turns[19].Cost);
        Assert.Equal(0.2972m, turns.Sum(t => t.Cost));
    }

    [Fact]
    public void Before_charging_only_the_new_message_misses_most_of_the_cost()
    {
        var naive = NaiveChatCost.Run(SystemTokens, Turns, Sonnet);

        Assert.Equal(0.00412m, naive[19].Cost);
        Assert.Equal(0.0844m, naive.Sum(t => t.Cost));
    }

    [Fact]
    public void With_cache_the_first_turn_costs_more_and_the_chat_costs_less()
    {
        var plain = ChatSimulator.Run(SystemTokens, Turns, Sonnet, useCache: false);
        var cached = ChatSimulator.Run(SystemTokens, Turns, Sonnet, useCache: true);

        Assert.Equal(new Usage(Input: 0, Output: 400, CacheRead: 0, CacheWrite: 1_060), cached[0].Usage);
        Assert.Equal(new Usage(Input: 0, Output: 400, CacheRead: 1_060, CacheWrite: 460), cached[1].Usage);
        Assert.Equal(0.00665m, cached[0].Cost);          // writing costs 1.25 times the input price
        Assert.True(cached[0].Cost > plain[0].Cost);
        Assert.Equal(0.11438m, cached.Sum(t => t.Cost));
        Assert.Equal(plain.Select(t => t.Usage.TotalInput), cached.Select(t => t.Usage.TotalInput));
    }

    [Fact]
    public void A_prompt_below_the_minimum_is_not_cached()
    {
        var turns = ChatSimulator.Run(100, [new Turn(20, 30), new Turn(20, 30)], Sonnet, useCache: true);

        Assert.All(turns, t => Assert.Equal(0, t.Usage.CacheWrite + t.Usage.CacheRead));
    }

    [Fact]
    public void Without_a_published_write_price_cache_writes_are_charged_as_input()
    {
        var gemini = Prices.Single(p => p.Model == "gemini-4-carbon");

        Assert.Null(gemini.CacheWritePerMillion);
        Assert.Equal(0.002m, RequestCost.Of(new Usage(0, 0, CacheWrite: 1_000), gemini));
    }

    [Fact]
    public void Before_a_budget_on_input_alone_lets_an_expensive_request_through()
    {
        Assert.True(InputOnlyBudget.Allows(9_800, Sonnet, 0.02m));

        var decision = RequestBudget.Check(9_800, maxOutputTokens: 4_000, Sonnet, 0.02m);

        Assert.False(decision.Allowed);
        Assert.Equal(0.0596m, decision.WorstCaseUsd);
    }

    [Fact]
    public void A_budget_check_needs_an_output_cap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestBudget.Check(9_800, maxOutputTokens: 0, Sonnet, 1m));
    }

    [Fact]
    public void A_price_built_in_code_is_validated_too()
    {
        var typo = Sonnet with { InputPerMillion = -2m };

        Assert.Throws<ArgumentOutOfRangeException>(() => RequestCost.Of(new Usage(1_000, 0), typo));
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestBudget.Check(1_000_000, 1, typo, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatSimulator.Run(1_000, [], typo, useCache: false));
    }

    [Theory]
    [InlineData("\"source\":\"https://example.com/pricing\",", "")]                       // no source
    [InlineData("\"outputPerMillion\":10", "\"outputPerMillion\":-10")]                   // negative
    [InlineData("\"inputPerMillion\":2", "\"inputPerMillion\":\"2\"")]                    // number as text
    [InlineData("\"checkedOn\":\"2026-11-12\"", "\"checkedOn\":\"12/11/2026\"")]          // not ISO date
    [InlineData("\"model\":\"m\",", "\"model\":\"m\",\"currency\":\"EUR\",")]             // unknown property
    [InlineData("\"model\":\"m\",", "\"model\":\"m\",\"model\":\"n\",")]                  // repeated property
    public void A_malformed_price_entry_is_rejected(string find, string replace)
    {
        const string entry = "{\"model\":\"m\",\"inputPerMillion\":2,\"outputPerMillion\":10,\"cacheReadPerMillion\":0.1,\"cacheWritePerMillion\":2.5,\"minCacheableTokens\":512,\"source\":\"https://example.com/pricing\",\"checkedOn\":\"2026-11-12\"}";
        Assert.Single(PriceList.Parse($"[{entry}]"));

        var ex = Record.Exception(() => PriceList.Parse($"[{entry.Replace(find, replace)}]"));

        Assert.True(ex is JsonException or ArgumentException, ex?.GetType().Name ?? "no exception");
    }
}
