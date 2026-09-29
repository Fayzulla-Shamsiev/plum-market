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
    internal const string UzbekRule =
        "Uzbek must be the Uzbek of Uzbekistan, not Turkish and not transliterated Russian: use the everyday " +
        "word a shopper in Tashkent would use — bodom (almond), qovoq (pumpkin), yongʻoq (walnut), qaymoq " +
        "(cream), sariyogʻ (butter), asal (honey), tovuq (chicken), goʻsht (meat), xamir (dough), non (лепёшка, not " +
        "\"lepyoshka\"), qarsildoq (хрустящий), somsa (not " +
        "\"samsa\"), non (bread), qahva (coffee), kartoshka, piyoz. If you are unsure of an ingredient's Uzbek " +
        "name, use the culinary term an Uzbek bakery menu would print. Never leave a Russian ending (-ый, -ой, " +
        "-ая, -ое) or a half-translated Russian word in Uzbek: «демисезонная куртка» is «mavsumiy kurtka», not " +
        "\"demisezonnoy kurtka\". Never invent a word from the Russian " +
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

    /// <summary>
    /// Writes one of the bot's own texts — what an empty chat shows, or how the bot greets a customer after
    /// «Начать». Russian only: these are read inside Telegram, where the storefront language doesn't apply.
    /// </summary>
    public async Task<(string? Text, string? Note)> BotTextAsync(string kind, string storeName, string? existing, CancellationToken ct)
    {
        if (!AiEnabled) return (null, "Не настроен ключ OpenAI.");
        var greeting = kind == "greeting";
        var system =
            "You write the wording of a Telegram bot that sells for a small shop in Uzbekistan. Russian only, " +
            "warm but businesslike, no emoji, no markdown, no links. " +
            (greeting
                ? "Write the bot's answer to /start: 1–3 short sentences. Say hello, say whose shop it is and " +
                  "that the catalog, cart and checkout open right here in Telegram with the button below. " +
                  "Use {name} exactly once where the customer's first name goes."
                : "Write what an empty chat with the bot shows before the customer presses «Начать»: 1–2 " +
                  "sentences, at most 400 characters, saying whose shop it is and what can be done here.");
        var facts = $"Shop name: {storeName}";
        if (!string.IsNullOrWhiteSpace(existing)) facts += $"\nCurrent text the merchant wants improved: {existing}";

        try
        {
            var json = await CallAsync("bot_text", system, facts, ObjectSchema(["text"]), ct);
            var text = json.GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return (null, "Модель вернула пустой текст.");
            // The greeting needs its placeholder; a model that forgot it would drop the customer's name.
            if (greeting && !text.Contains("{name}")) text = text.Replace("Здравствуйте", "Здравствуйте, {name}");
            return (text, null);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "AI bot text failed");
            return (null, $"Не удалось сгенерировать текст: {e.Message}");
        }
    }

    /// <summary>
    /// Turns a long text an administrator pasted — a marketplace page, a supplier's spec sheet — into product cards
    /// ready for the catalog: a short name, a readable description, a characteristics table, weight and size. One
    /// focused call, so the setup assistant gets a compact summary instead of pages of raw text.
    /// Returns the JSON, or null when the text describes no product or the call fails.
    /// </summary>
    public async Task<string?> DigestProductsAsync(string text, CancellationToken ct)
    {
        if (!AiEnabled) return null;
        var system = """
            You prepare product cards for an online shop in Uzbekistan from text an administrator pasted (a marketplace
            page, a supplier's spec sheet, a messy description). Extract every product it describes; if it describes no
            product for sale (e.g. text about the shop itself), return an empty products array.

            For each product:
            - name_ru: type + brand + model + the 1–2 specs people choose by, max ~60 characters, no article numbers, no
              marketing words. E.g. «Ноутбук ASUS Vivobook S16 S3607VA, 16", 16/512 ГБ», «Кроссовки Nike Air Max 90, белые».
            - description_ru: 2–3 short paragraphs separated by a blank line, 250–450 characters in total, written for a
              buyer in plain Russian. Paragraph 1: what it is and who it suits. Paragraph 2: the 2–3 benefits that matter
              most in everyday use (explain what a spec gives the buyer, e.g. "144 Гц — плавная картинка", not a list of
              numbers). Paragraph 3 (optional): what is in the box, warranty. Do not list specs that are in attributes.
            - attributes: 8–14 rows of what buyers compare, short «name: value», merged where related ("16 ГБ DDR5";
              "16", 1920×1200, IPS, 144 Гц"; "2× USB-C 3.2, 2× USB-A 3.2, HDMI 2.1, 3,5 мм"). Useful order for
              electronics: процессор, оперативная память, накопитель, экран, видеокарта, аккумулятор, разъёмы,
              беспроводная связь, веб-камера, клавиатура, ОС, цвет, материал, гарантия, страна производства. Adapt the
              list to other kinds of goods. No weight or dimensions here.
            - weight_grams, length_cm/width_cm/height_cm: from the text (mm ÷ 10, rounded; kg × 1000), else null.
            - price: only when the text states the selling price in сум; otherwise null. Never guess.
            - quantity: pieces in stock when the text says so ("есть 6 штук", "в наличии 15"); otherwise null.
            - category_hint: a short Russian category name that fits (e.g. «Ноутбуки»).
            - name_uz, description_uz: the same in Uzbek, Latin script.
            Drop the noise: article numbers, section headers, repeated labels, "Состояние: новый", "Гарантия
            предоставляется продавцом", preinstalled-software flags, plug type. Use only facts from the text.
            Copy brand, model, processor and part names exactly as written — never "correct" or modernise them:
            «Intel Core 5 210H» is a real name and must not become «Core i5»; «S3607VA» stays «S3607VA».
            missing: what a buyer would still need that the text lacks (usually the price) — short Russian phrases.
            """ + "\n" + UzbekRule;

        static JsonElement E(object o) => JsonSerializer.SerializeToElement(o);
        var nstr = new { type = new[] { "string", "null" } };
        var nint = new { type = new[] { "integer", "null" } };
        var product = new
        {
            type = "object",
            properties = new
            {
                name_ru = new { type = "string" }, name_uz = new { type = "string" },
                description_ru = new { type = "string" }, description_uz = new { type = "string" },
                price = nint, quantity = nint, category_hint = nstr,
                attributes = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new { name = new { type = "string" }, value = new { type = "string" } },
                        required = new[] { "name", "value" }, additionalProperties = false,
                    },
                },
                weight_grams = nint, length_cm = nint, width_cm = nint, height_cm = nint,
            },
            required = new[] { "name_ru", "name_uz", "description_ru", "description_uz", "price", "quantity", "category_hint", "attributes", "weight_grams", "length_cm", "width_cm", "height_cm" },
            additionalProperties = false,
        };
        var schema = new Dictionary<string, JsonElement>
        {
            ["type"] = E("object"),
            ["properties"] = E(new { products = new { type = "array", items = product }, missing = new { type = "array", items = new { type = "string" } } }),
            ["required"] = E(new[] { "products", "missing" }),
            ["additionalProperties"] = E(false),
        };

        try
        {
            // The strong model with some reasoning: this is exactly where a weaker one mixed up facts.
            var response = await RespondAsync(system,
                [new { role = "user", content = text.Length > 12_000 ? text[..12_000] : text }], null,
                new { type = "json_schema", name = "product_digest", strict = true, schema }, "medium", ct);
            using var json = JsonDocument.Parse(OutputText(response));
            return json.RootElement.GetProperty("products").GetArrayLength() == 0 ? null
                : JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Product digest failed");
            return null;
        }
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

    /// <summary>
    /// The setup assistant's model. It follows a long playbook, many tools and messy pasted texts, so it gets the
    /// strongest model with reasoning; overridable with OpenAI:AssistantModel.
    /// </summary>
    public string AssistantModel => config["OpenAI:AssistantModel"] is { Length: > 0 } m ? m : "gpt-6-luna";

    /// <summary>Stands in when the main model hits the account's rate limit (OpenAI:AssistantFallbackModel).</summary>
    string FallbackModel => config["OpenAI:AssistantFallbackModel"] is { Length: > 0 } m ? m : "gpt-5.4-mini";

    /// <summary>
    /// One call to OpenAI's Responses API — the API that lets a reasoning model use tools. <paramref name="input"/> is
    /// in its item format (messages, function calls and their outputs); the whole response object comes back, so the
    /// caller can run its function calls and pass the output items on to the next round.
    ///
    /// Nothing is stored at OpenAI (store: false): the model's reasoning travels back encrypted inside the output items
    /// instead. The key is attached here and nowhere else, and never logged.
    /// </summary>
    public async Task<JsonElement> RespondAsync(string instructions, IEnumerable<object> input, IEnumerable<object>? tools,
        object? textFormat, string effort, CancellationToken ct)
    {
        if (ApiKey is not { } key) throw new InvalidOperationException("ключ OpenAI не настроен");
        var model = AssistantModel;
        var body = await SendResponseAsync(key, model, instructions, input, tools, textFormat, effort, ct);

        // A rate limit is usually gone within a second: wait the moment OpenAI asks for, then try the smaller model
        // rather than leave the administrator with an error in the middle of the setup.
        if (body.Status == System.Net.HttpStatusCode.TooManyRequests && !body.Text.Contains("insufficient_quota"))
        {
            if (RetryAfter(body.Text) is { } wait && wait <= TimeSpan.FromSeconds(6))
            {
                await Task.Delay(wait + TimeSpan.FromMilliseconds(250), ct);
                body = await SendResponseAsync(key, model, instructions, input, tools, textFormat, effort, ct);
            }
            if (body.Status == System.Net.HttpStatusCode.TooManyRequests && FallbackModel != model)
            {
                log.LogInformation("OpenAI rate limit on {Model}, falling back to {Fallback}", model, FallbackModel);
                body = await SendResponseAsync(key, FallbackModel, instructions, input, tools, textFormat, effort, ct);
            }
        }

        if (body.Status != System.Net.HttpStatusCode.OK)
        {
            log.LogWarning("OpenAI {Status}: {Body}", (int)body.Status, Trim(body.Text));
            throw new InvalidOperationException(Explain(body.Status, body.Text));
        }
        var root = JsonDocument.Parse(body.Text).RootElement.Clone();
        if (root.TryGetProperty("status", out var status) && status.GetString() == "incomplete")
            log.LogWarning("OpenAI response incomplete: {Details}", root.TryGetProperty("incomplete_details", out var d) ? d.GetRawText() : "");
        return root;
    }

    async Task<(System.Net.HttpStatusCode Status, string Text)> SendResponseAsync(string key, string model, string instructions,
        IEnumerable<object> input, IEnumerable<object>? tools, object? textFormat, string effort, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["instructions"] = instructions,
            ["input"] = input,
            ["store"] = false,
        };
        // Older models (gpt-4.x) have no reasoning settings and reject them.
        if (!model.StartsWith("gpt-4", StringComparison.Ordinal))
        {
            payload["reasoning"] = new { effort };
            payload["include"] = new[] { "reasoning.encrypted_content" };
        }
        if (tools is not null)
        {
            payload["tools"] = tools;
            // Strict tool schemas and parallel calls don't mix; one call per round, arrays inside a call instead.
            payload["parallel_tool_calls"] = false;
        }
        if (textFormat is not null) payload["text"] = new { format = textFormat };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var client = factory.CreateClient(nameof(AiContentService));
        using var response = await client.SendAsync(request, ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>The text of a response's message items (what a Chat Completions reply called "content").</summary>
    public static string OutputText(JsonElement response)
    {
        var sb = new StringBuilder();
        foreach (var item in response.GetProperty("output").EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "message" || !item.TryGetProperty("content", out var parts)) continue;
            foreach (var part in parts.EnumerateArray())
            {
                var type = part.GetProperty("type").GetString();
                if (type == "output_text") sb.Append(part.GetProperty("text").GetString());
                else if (type == "refusal") throw new InvalidOperationException("запрос отклонён моделью");
            }
        }
        return sb.ToString();
    }

    /// <summary>The function calls a response asks for, in order.</summary>
    public static List<JsonElement> FunctionCalls(JsonElement response) =>
        response.GetProperty("output").EnumerateArray().Where(i => i.GetProperty("type").GetString() == "function_call").ToList();

    /// <summary>"Please try again in 752ms" / "in 1.3s" from a rate-limit message.</summary>
    static TimeSpan? RetryAfter(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(body, @"try again in (\d+(?:\.\d+)?)(ms|s)");
        if (!match.Success || !double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) return null;
        return match.Groups[2].Value == "ms" ? TimeSpan.FromMilliseconds(v) : TimeSpan.FromSeconds(v);
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
