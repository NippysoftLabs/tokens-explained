# AI tokens explained

This repository is the example code for a blog post on nippysoft.com:

- English: [AI tokens: what they are and how they work](https://nippysoft.com/en/blog/artificial-intelligence/ai-tokens-what-they-are-how-they-work)
- Español: [Tokens de la IA: qué son y cómo funcionan](https://nippysoft.com/es/blog/inteligencia-artificial/tokens-ia-que-son-como-funcionan)

**This is teaching code, not production code.** It is small on purpose so each idea fits on one screen. It calls no AI service and needs no account or key: it counts tokens with a public tokenizer that runs on your machine and simulates what a chat costs from a price list. Read it to understand how tokens are counted and billed; do not use it to forecast a real bill. The section "What this does not check" lists what it leaves out.

## What it shows

A token is a piece of text: a whole word, part of a word, a space plus a word, a punctuation mark or part of a character. AI models read and write tokens, and the APIs bill and limit them in tokens. The repository shows five ideas, each with a "before" (the usual mistake) and an "after":

| Idea | Before | After | What checks it |
|---|---|---|---|
| A token is not a word | `WordEstimate` counts words: 15 for the English prompt in `samples/parallel-texts.json`. | `TokenCounter` counts tokens with `o200k_base`: 18. `Pieces` shows where it cuts ("Summ", "ar", "ize"). | `TokenTests`, including the count from the Microsoft documentation example (16). |
| Spanish takes more tokens than English | | `LanguageComparison` counts the same text in both languages: 148 Spanish tokens and 128 English tokens over the five samples; one sample is a tie. | `TokenTests` |
| Input and output have different prices | `FlatPriceCost` prices every token at the input price. | `RequestCost` prices input, output, cache reads and cache writes separately. | `CostTests` |
| A chat sends the whole conversation again on every turn | `NaiveChatCost` charges each turn for its new message only. | `ChatSimulator` sends the system prompt, every earlier message and answer, and the new message. Turn 20 of the example sends 9,800 input tokens; turn 1 sends 1,060. | `CostTests` |
| Prompt caching | | `ChatSimulator` with `useCache: true`: the first turn costs more (writing to the cache) and the whole chat costs less. | `CostTests` |
| Measure before paying | `InputOnlyBudget` checks the input only. | `RequestBudget` adds the most the answer can cost, from the output cap (`max_tokens`). | `CostTests` |

The chat in the example has a 1,000-token system prompt, 20 turns, 60-token messages and 400-token answers. Those sizes are an assumption for the example, not a measurement.

## How it is organized

```
src/TokensExplained          token counter, language comparison, prices, chat simulator, budget check
src/TokensExplained/Before   the "before" versions
config/prices.json           prices per million tokens, each with its source page and the day it was checked
samples/parallel-texts.json  the same texts in Spanish and English, each with its source
tests/TokensExplained.Tests  TokenTests and CostTests
tools/TokensDemo             prints the comparisons and the chat table
tools/HostileInputs          prints unusual inputs and their results
```

## How to run it

You need the .NET 10 SDK; the version is pinned in `global.json`. The tokenizer comes from two NuGet packages (`Microsoft.ML.Tokenizers` and `Microsoft.ML.Tokenizers.Data.O200kBase`), which `dotnet` downloads on the first build. Nothing else: no network calls at run time, no AI account and no key.

```
git clone https://github.com/NippysoftLabs/tokens-explained.git
cd tokens-explained
dotnet test
```

The last line of `dotnet test` should report 25 passed and 0 failed.

Two more things you can run, from the repository root:

```
dotnet run --project tools/TokensDemo
dotnet run --project tools/HostileInputs
```

The same `dotnet test` runs in CI (`.github/workflows/test.yml`).

## The tokenizer

`TokenCounter` uses `o200k_base`, the encoding that OpenAI's tiktoken maps to GPT-4o, through Microsoft's `Microsoft.ML.Tokenizers` library. Claude and Gemini use their own tokenizers, which are not published as a library: the same text gives a different count there. To count for those models, use each provider's count endpoint (Claude: `/v1/messages/count_tokens`; Gemini: `count_tokens`; OpenAI: `/v1/responses/input_tokens`).

## What this does not check

- **Counts for other models.** Every count here is `o200k_base`. Claude, Gemini and other models can cut text differently, and Anthropic documents that Claude Opus 4.7 and later produce about 30% more tokens for the same text than earlier Claude models.
- **Long text without spaces.** Counting time grows with the square of the length of a stretch without whitespace: on the machine used for this repository, 'a' repeated 10,000 times took about 0.1 s and 20,000 times between 0.4 and 0.5 s (times vary by run; `tools/HostileInputs` prints them). `TokenCounter` rejects a stretch longer than 10,000 characters (configurable) instead of counting it; a pasted base64 image, a minified file, or more than 10,000 characters of text in a language written without spaces, such as Chinese or Japanese, hit that limit even when the text is normal.
- **The full request.** A real request adds tokens for message roles, tools, images and files. `TokenCounter` counts plain text only.
- **Unicode forms.** "canción" typed with a separate accent mark (NFD) is cut differently from the usual form (NFC); in this tokenizer both give 3 tokens, but the pieces differ. Text is not normalized before counting. `Pieces` joins tokens that end in half a character (U+FFFD when decoded), up to 4 tokens, the most a UTF-8 character needs; a real U+FFFD in the text (a badly decoded file has them) is shown joined to the next token.
- **Prices.** `config/prices.json` was copied by hand from each provider's pricing page on the date in `checkedOn`. Prices change: Google's page already lists a higher Gemini 4 Carbon price from December 1, 2026. Batch discounts, long-context prices, regional multipliers and taxes are not modelled.
- **Cache rules.** The simulator follows the automatic caching of the Claude API and assumes the cache never expires between turns; the Claude cache lasts 5 minutes by default. OpenAI and Gemini cache on their own terms (minimums, lifetimes, write prices); Gemini publishes no separate write price, so writes are charged as input.
- **Context window.** The simulator does not stop when the chat outgrows the model's context window: 1,000 turns of 2,000 + 2,000 tokens send almost 4 million tokens on the last turn. Real chat apps summarize or drop old messages first.
- **Reasoning tokens.** Models that think before answering bill those tokens as output; the simulator only counts the visible answer.
- **Chat apps with a subscription.** The ChatGPT, Claude and Gemini apps are paid by plan, not by token, so this cost does not reach the user as a bill. Claude's help center says the length of a conversation affects usage, and that long conversations that trigger automatic context management use more of the usage limit.

## License

MIT. See `LICENSE`.
