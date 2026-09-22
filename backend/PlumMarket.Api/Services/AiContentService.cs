using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

public record AiResult(Dictionary<string, Dictionary<string, string>> Values, string Provider, string? Note = null);

/// <summary>
/// "Перевести" and "Сгенерировать и заполнить автоматически" for catalog forms.
/// Russian ⇄ Uzbek goes through Claude when API credentials are configured; Uzbek Latin ⇄ Cyrillic is
/// always done by <see cref="UzTransliterator"/> (same language, exact mapping). Without credentials the
/// service degrades to an offline mode: script conversion + template descriptions.
/// </summary>
public class AiContentService(IConfiguration config, ILogger<AiContentService> log)
{
    const string Model = "claude-opus-5";

    static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["ru"] = "Russian",
        ["uz"] = "Uzbek in the Latin script (official 2023 orthography: oʻ, gʻ, sh, ch)",
    };

    /// <summary>Claude is used when an API key/token is present or explicitly enabled (e.g. an `ant auth login` profile).</summary>
    public bool ClaudeEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN")) ||
        config.GetValue<bool>("Ai:UseClaude");

    public string Provider => ClaudeEnabled ? "claude" : "offline";

    /// <summary>Fills every other catalog language from the fields written in <paramref name="source"/>.</summary>
    public async Task<AiResult> TranslateAsync(string source, Dictionary<string, string> fields, CancellationToken ct)
    {
        fields = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToDictionary(f => f.Key, f => f.Value.Trim());
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (fields.Count == 0) return new AiResult(result, Provider, "Нет текста для перевода");

        // Normalise Uzbek Cyrillic input to Latin: Claude translates Latin, Cyrillic is then derived exactly.
        var pivotLang = source == "oz" ? "uz" : source;
        var pivot = source == "oz" ? fields.ToDictionary(f => f.Key, f => UzTransliterator.ToLatin(f.Value)) : fields;
        if (source == "oz") result["uz"] = pivot;

        string? note = null;
        var other = pivotLang == "ru" ? "uz" : "ru";
        if (ClaudeEnabled)
        {
            try
            {
                result[other] = await TranslateWithClaude(pivotLang, other, pivot, ct);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Claude translation failed");
                note = $"Перевод {LangLabel(other)} недоступен: {e.Message}";
            }
        }
        else
        {
            note = $"Для перевода на {LangLabel(other)} подключите Claude (переменная ANTHROPIC_API_KEY). Кириллица/латиница заполнены автоматически.";
        }

        // Uzbek Latin → Cyrillic is a script conversion, not a translation.
        if (result.TryGetValue("uz", out var uzFromClaude)) result["oz"] = uzFromClaude.ToDictionary(f => f.Key, f => UzTransliterator.ToCyrillic(f.Value));
        else if (source == "uz") result["oz"] = fields.ToDictionary(f => f.Key, f => UzTransliterator.ToCyrillic(f.Value));

        result.Remove(source);
        return new AiResult(result, Provider, note);
    }

    /// <summary>Writes a storefront description in every catalog language.</summary>
    public async Task<AiResult> DescribeAsync(DescribeRequest req, CancellationToken ct)
    {
        Dictionary<string, string> texts;
        string? note = null;
        if (ClaudeEnabled)
        {
            try
            {
                texts = await DescribeWithClaude(req, ct);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Claude description failed");
                texts = TemplateDescription(req);
                note = $"Claude недоступен ({e.Message}) — использован шаблон.";
            }
        }
        else
        {
            texts = TemplateDescription(req);
            note = "Описание собрано по шаблону. Подключите Claude (ANTHROPIC_API_KEY) для генерации ИИ.";
        }
        texts["oz"] = UzTransliterator.ToCyrillic(texts["uz"]);
        return new AiResult(texts.ToDictionary(t => t.Key, t => new Dictionary<string, string> { ["description"] = t.Value }),
            ClaudeEnabled && note is null ? "claude" : "offline", note);
    }

    // ------------------------------------------------------------------ Claude

    async Task<Dictionary<string, string>> TranslateWithClaude(string from, string to, Dictionary<string, string> fields, CancellationToken ct)
    {
        var system =
            "You translate product catalog content for an online store in Uzbekistan (bakery, café, retail). " +
            "Translate each field's value faithfully and naturally for shoppers. Keep brand names, numbers, units " +
            "and sizes as they are. Keep the same line breaks. Return only the translated fields.";
        var user = $"Translate from {LanguageNames[from]} to {LanguageNames[to]}.\n\n" +
                   JsonSerializer.Serialize(fields, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var schema = ObjectSchema(fields.Keys);
        var json = await CallClaude(system, user, schema, ct);
        return fields.Keys.ToDictionary(k => k, k => json.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "");
    }

    async Task<Dictionary<string, string>> DescribeWithClaude(DescribeRequest req, CancellationToken ct)
    {
        var what = req.Kind == "category" ? "product category" : "product";
        var system =
            $"You write short, appetising storefront descriptions for an online store in Uzbekistan. " +
            $"Write 2–3 sentences (max ~350 characters) for the {what}. Be concrete, no invented facts about " +
            "ingredients, certifications or prices that are not given. No emojis, no markdown. " +
            "Write the same description once in Russian (ru) and once in Uzbek Latin script (uz, official orthography).";
        var facts = new List<string> { $"Name: {req.Name}" };
        if (!string.IsNullOrWhiteSpace(req.Category)) facts.Add($"Category: {req.Category}");
        if (req.Attributes is { Count: > 0 }) facts.Add("Attributes: " + string.Join("; ", req.Attributes.Select(a => $"{a.Name}: {a.Value}")));
        if (!string.IsNullOrWhiteSpace(req.Unit)) facts.Add($"Sold per: {req.Unit}");
        if (req.WeightGrams is > 0) facts.Add($"Weight: {req.WeightGrams} g");
        if (!string.IsNullOrWhiteSpace(req.Existing)) facts.Add($"Merchant's notes / current text: {req.Existing}");

        var json = await CallClaude(system, string.Join("\n", facts), ObjectSchema(["ru", "uz"]), ct);
        return new Dictionary<string, string>
        {
            ["ru"] = json.GetProperty("ru").GetString() ?? "",
            ["uz"] = json.GetProperty("uz").GetString() ?? "",
        };
    }

    async Task<JsonElement> CallClaude(string system, string user, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        AnthropicClient client = new();
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 16000,
            System = system,
            // Short, well-specified writing task: low effort keeps it fast and cheap.
            OutputConfig = new BetaOutputConfig
            {
                Effort = Effort.Low,
                Format = new BetaJsonOutputFormat { Schema = schema },
            },
            // If a safety classifier declines, re-serve on the recommended fallback model instead of failing.
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            Messages = [new() { Role = Role.User, Content = user }],
        }, ct);

        if (response.StopReason == "refusal") throw new InvalidOperationException("запрос отклонён моделью");
        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    static Dictionary<string, JsonElement> ObjectSchema(IEnumerable<string> keys)
    {
        var list = keys.ToList();
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(list.ToDictionary(k => k, _ => new { type = "string" })),
            ["required"] = JsonSerializer.SerializeToElement(list),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        };
    }

    // ------------------------------------------------------------------ offline fallback

    static Dictionary<string, string> TemplateDescription(DescribeRequest req)
    {
        var attrsRu = req.Attributes is { Count: > 0 } ? " " + string.Join(", ", req.Attributes.Select(a => $"{a.Name.ToLowerInvariant()}: {a.Value}")) + "." : "";
        var weight = req.WeightGrams is > 0 ? $" Вес — {req.WeightGrams} г." : "";
        var weightUz = req.WeightGrams is > 0 ? $" Ogʻirligi — {req.WeightGrams} g." : "";
        var nameUz = string.IsNullOrWhiteSpace(req.NameUz) ? req.Name : req.NameUz;
        if (req.Kind == "category")
            return new()
            {
                ["ru"] = $"Раздел «{req.Name}»: свежий ассортимент, который мы обновляем каждый день. Выберите любимое и оформите заказ с доставкой или самовывозом.",
                ["uz"] = $"«{nameUz}» boʻlimi: har kuni yangilanadigan yangi assortiment. Sevimli mahsulotingizni tanlang va yetkazib berish yoki olib ketish bilan buyurtma bering.",
            };
        var cat = string.IsNullOrWhiteSpace(req.Category) ? "" : $" из раздела «{req.Category}»";
        return new()
        {
            ["ru"] = $"{req.Name}{cat} — готовим из свежих продуктов в день заказа.{attrsRu}{weight} Закажите с доставкой или заберите в ближайшем филиале.",
            ["uz"] = $"{nameUz} — buyurtma kuni yangi mahsulotlardan tayyorlaymiz.{weightUz} Yetkazib berish bilan buyurtma qiling yoki eng yaqin filialdan olib keting.",
        };
    }

    static string LangLabel(string lang) => lang switch { "ru" => "русский", "uz" => "узбекский", _ => lang };
}

public record DescribeRequest(
    string Name,
    string? NameUz,
    string Kind,
    string? Category,
    List<ProductAttribute>? Attributes,
    string? Unit,
    int? WeightGrams,
    string? Existing);
