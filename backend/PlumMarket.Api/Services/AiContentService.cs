using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

public record AiResult(Dictionary<string, Dictionary<string, string>> Values, string Provider, string? Note = null);

/// <summary>
/// "Перевести" and "Сгенерировать и заполнить автоматически" for catalog forms.
/// Russian ⇄ Uzbek goes through OpenAI when an API key is configured; Uzbek Latin ⇄ Cyrillic is always done by
/// <see cref="UzTransliterator"/> (same language, exact mapping). Without a key the service degrades to an
/// offline mode: script conversion + template descriptions.
///
/// The key is read from configuration only — `dotnet user-secrets set "OpenAI:ApiKey" …` while developing,
/// the OpenAI__ApiKey environment variable in production. It never appears in a file that is committed, is
/// never logged, and never leaves the server: the browser only ever calls our own /api/ai endpoints.
/// </summary>
public class AiContentService(IConfiguration config, IHttpClientFactory factory, ILogger<AiContentService> log)
{
    /// <summary>Overridable with OpenAI:Model, so the model can change without a deploy of new code.</summary>
    string Model => config["OpenAI:Model"] is { Length: > 0 } m ? m : "gpt-4.1-mini";

    /// <summary>
    /// The key, from the user secrets store while developing (`OpenAI:ApiKey`) or from the environment in
    /// production (`OpenAI__ApiKey`; the conventional `OPENAI_API_KEY` is accepted too, so a host that dislikes
    /// double underscores still works). Never written to a file in this repository, never logged, never sent
    /// to the browser.
    /// </summary>
    string? ApiKey => First(config["OpenAI:ApiKey"], config["OPENAI_API_KEY"]);

    static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    /// <summary>
    /// Uzbek is where these models slip: they reach for a Turkish word or transliterate the Russian one.
    /// Naming the trap is what makes "миндаль" come out as "bodom" rather than "badem" or "minal".
    /// </summary>
    const string UzbekRule =
        "Uzbek must be the Uzbek of Uzbekistan, not Turkish and not transliterated Russian: use the everyday " +
        "word a shopper in Tashkent would use — bodom (almond), qovoq (pumpkin), yongʻoq (walnut), qaymoq " +
        "(cream), sariyogʻ (butter), asal (honey), tovuq (chicken), goʻsht (meat), xamir (dough), somsa (not " +
        "\"samsa\"), non (bread), qahva (coffee), kartoshka, piyoz. If you are unsure of an ingredient's Uzbek " +
        "name, use the culinary term an Uzbek bakery menu would print. Never invent a word from the Russian " +
        "one, and re-read the result as a native speaker would.";

    static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["ru"] = "Russian",
        ["uz"] = "Uzbek in the Latin script (official 2023 orthography: oʻ, gʻ, sh, ch)",
    };

    /// <summary>Whether a key is configured at all. Only ever reports yes/no — never the key.</summary>
    public bool AiEnabled => ApiKey is not null;

    public string Provider => AiEnabled ? "openai" : "offline";

    /// <summary>Fills every other catalog language from the fields written in <paramref name="source"/>.</summary>
    public async Task<AiResult> TranslateAsync(string source, Dictionary<string, string> fields, CancellationToken ct)
    {
        fields = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToDictionary(f => f.Key, f => f.Value.Trim());
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (fields.Count == 0) return new AiResult(result, Provider, "Нет текста для перевода");

        // Normalise Uzbek Cyrillic input to Latin: the model translates Latin, Cyrillic is then derived exactly.
        var pivotLang = source == "oz" ? "uz" : source;
        var pivot = source == "oz" ? fields.ToDictionary(f => f.Key, f => UzTransliterator.ToLatin(f.Value)) : fields;
        if (source == "oz") result["uz"] = pivot;

        string? note = null;
        var other = pivotLang == "ru" ? "uz" : "ru";
        if (AiEnabled)
        {
            try
            {
                result[other] = await TranslateWithAi(pivotLang, other, pivot, ct);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "AI translation failed");
                note = $"Перевод на {LangLabel(other)} не удался: {e.Message}";
            }
        }
        else
        {
            note = $"Перевод на {LangLabel(other)} недоступен: не настроен ключ OpenAI. Кириллица/латиница заполнены автоматически.";
        }

        // Uzbek Latin → Cyrillic is a script conversion, not a translation.
        if (result.TryGetValue("uz", out var uzTranslated)) result["oz"] = uzTranslated.ToDictionary(f => f.Key, f => UzTransliterator.ToCyrillic(f.Value));
        else if (source == "uz") result["oz"] = fields.ToDictionary(f => f.Key, f => UzTransliterator.ToCyrillic(f.Value));

        result.Remove(source);
        return new AiResult(result, Provider, note);
    }

    /// <summary>Writes a storefront description in every catalog language.</summary>
    public async Task<AiResult> DescribeAsync(DescribeRequest req, CancellationToken ct)
    {
        Dictionary<string, string> texts;
        string? note = null;
        if (AiEnabled)
        {
            try
            {
                texts = await DescribeWithAi(req, ct);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "AI description failed");
                texts = TemplateDescription(req);
                note = $"Генерация не удалась ({e.Message}) — использован шаблон.";
            }
        }
        else
        {
            texts = TemplateDescription(req);
            note = "Описание собрано по шаблону: не настроен ключ OpenAI.";
        }
        texts["oz"] = UzTransliterator.ToCyrillic(texts["uz"]);
        return new AiResult(texts.ToDictionary(t => t.Key, t => new Dictionary<string, string> { ["description"] = t.Value }),
            AiEnabled && note is null ? "openai" : "offline", note);
    }

    // ------------------------------------------------------------------ OpenAI

    async Task<Dictionary<string, string>> TranslateWithAi(string from, string to, Dictionary<string, string> fields, CancellationToken ct)
    {
        var system =
            "You translate product catalog content for an online store in Uzbekistan (bakery, café, retail). " +
            "Translate each field's value faithfully and naturally for shoppers. Keep brand names, numbers, units " +
            "and sizes as they are. Keep the same line breaks. Return only the translated fields.\n" + UzbekRule;
        var user = $"Translate from {LanguageNames[from]} to {LanguageNames[to]}.\n\n" +
                   JsonSerializer.Serialize(fields, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var json = await CallAsync("translation", system, user, ObjectSchema(fields.Keys), ct);
        return fields.Keys.ToDictionary(k => k, k => json.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "");
    }

    async Task<Dictionary<string, string>> DescribeWithAi(DescribeRequest req, CancellationToken ct)
    {
        var what = req.Kind == "category" ? "product category" : "product";
        var system =
            $"You write short, appetising storefront descriptions for an online store in Uzbekistan. " +
            $"Write 2–3 sentences (max ~350 characters) for the {what}. Be concrete, no invented facts about " +
            "ingredients, certifications or prices that are not given. No emojis, no markdown. " +
            "Write the same description once in Russian (ru) and once in Uzbek Latin script (uz, official " +
            "orthography).\n" + UzbekRule;
        var facts = new List<string> { $"Name: {req.Name}" };
        if (!string.IsNullOrWhiteSpace(req.Category)) facts.Add($"Category: {req.Category}");
        if (req.Attributes is { Count: > 0 }) facts.Add("Attributes: " + string.Join("; ", req.Attributes.Select(a => $"{a.Name}: {a.Value}")));
        if (!string.IsNullOrWhiteSpace(req.Unit)) facts.Add($"Sold per: {req.Unit}");
        if (req.WeightGrams is > 0) facts.Add($"Weight: {req.WeightGrams} g");
        if (!string.IsNullOrWhiteSpace(req.Existing)) facts.Add($"Merchant's notes / current text: {req.Existing}");

        var json = await CallAsync("description", system, string.Join("\n", facts), ObjectSchema(["ru", "uz"]), ct);
        return new Dictionary<string, string>
        {
            ["ru"] = json.GetProperty("ru").GetString() ?? "",
            ["uz"] = json.GetProperty("uz").GetString() ?? "",
        };
    }

    /// <summary>
    /// One chat completion that must answer with an object matching <paramref name="schema"/>. The key is
    /// attached to this request and nowhere else; failures are logged without it.
    /// </summary>
    async Task<JsonElement> CallAsync(string schemaName, string system, string user,
        Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        if (ApiKey is not { } key) throw new InvalidOperationException("ключ OpenAI не настроен");

        var payload = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = schemaName, strict = true, schema },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var client = factory.CreateClient(nameof(AiContentService));
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("OpenAI {Status}: {Body}", (int)response.StatusCode, Trim(body));
            throw new InvalidOperationException(Explain(response.StatusCode, body));
        }

        var content = JsonDocument.Parse(body).RootElement
            .GetProperty("choices")[0].GetProperty("message");
        // A refusal comes back in its own field rather than as content.
        if (content.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException("запрос отклонён моделью");
        return JsonDocument.Parse(content.GetProperty("content").GetString() ?? "{}").RootElement.Clone();
    }

    /// <summary>What the merchant sees. OpenAI's own wording is for the log, not for a catalog form.</summary>
    static string Explain(System.Net.HttpStatusCode status, string body) => status switch
    {
        System.Net.HttpStatusCode.Unauthorized => "ключ OpenAI отклонён",
        System.Net.HttpStatusCode.TooManyRequests => "лимит запросов OpenAI исчерпан, попробуйте позже",
        System.Net.HttpStatusCode.NotFound when body.Contains("model", StringComparison.OrdinalIgnoreCase) =>
            "выбранная модель недоступна для этого ключа",
        _ => $"OpenAI ответил ошибкой {(int)status}",
    };

    static string Trim(string body) => body.Length <= 500 ? body : body[..500] + "…";

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
