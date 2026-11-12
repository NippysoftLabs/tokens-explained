using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokensExplained;

/// <summary>
/// Reads config/prices.json. Prices change: each entry names the page it was copied from and the day it
/// was checked, and reading fails if either is missing, a property is unknown or repeated, a price is
/// negative or written as text, or a model appears twice.
/// </summary>
public static class PriceList
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        // The Web defaults read "2" as the number 2; a price written as text is rejected instead.
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static IReadOnlyList<Price> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var prices = JsonSerializer.Deserialize<List<Price>>(json, Options)
                     ?? throw new JsonException("The price list is null.");
        foreach (var price in prices) price.Validate();
        var duplicate = prices.GroupBy(p => p.Model, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new JsonException($"The model '{duplicate.Key}' appears more than once.");
        return prices;
    }

    public static IReadOnlyList<Price> Load(string path) => Parse(File.ReadAllText(path));
}
