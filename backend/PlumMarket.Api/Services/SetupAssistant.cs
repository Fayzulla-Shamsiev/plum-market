using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

public record AssistantMessageDto(int Id, string Role, string Text, List<string> Suggestions, List<AssistantCard> Cards,
    List<string> Attachments, DateTime CreatedAt);

/// <summary>One line of the «Настройка магазина» list next to the chat.</summary>
public record SetupStep(string Key, string Title, string Hint, bool Done, bool Optional, string Link);

/// <summary>
/// The AI assistant a new administrator meets right after registration, instead of an empty panel. It asks what
/// the business is, then fills the shop in with them — description, catalog, branch, contacts, delivery, return
/// terms, a Telegram bot — through <see cref="AssistantTools"/>, and answers questions about running an online
/// shop along the way. The conversation is stored per store, so it survives reloads and can be reopened later.
///
/// OpenAI is reached through <see cref="AiContentService"/>, which owns the API key (user secrets `OpenAI:ApiKey`
/// locally, `OpenAI__ApiKey` on the server). Nothing secret is ever put into the conversation: Telegram bot tokens
/// go through their own form, and one pasted into the chat by mistake is cut out before it is stored or sent.
/// </summary>
public partial class SetupAssistant(AppDbContext db, StoreContext tenant, AiContentService ai, AssistantTools tools,
    StoreLinks links, IWebHostEnvironment env, ILogger<SetupAssistant> log)
{
    const int MaxRounds = 8;
    const int HistoryWindow = 60;
    static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool Enabled => ai.AiEnabled;

    // ------------------------------------------------------------------ conversation

    /// <summary>The visible conversation. A store's first visit gets the assistant's greeting.</summary>
    public async Task<List<AssistantMessageDto>> HistoryAsync()
    {
        if (!await db.AssistantMessages.AnyAsync())
        {
            db.AssistantMessages.Add(Greeting());
            await db.SaveChangesAsync();
        }
        return Visible(await db.AssistantMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync());
    }

    /// <summary>Starts over: the conversation is forgotten, the shop's data stays as it is.</summary>
    public async Task ResetAsync()
    {
        await db.AssistantMessages.ExecuteDeleteAsync();
    }

    /// <summary>
    /// The administrator's message → the assistant's answer, running as many tools as it needs in between.
    /// Returns the messages to append to the chat (the administrator's own, then the answer with its cards).
    /// </summary>
    public async Task<List<AssistantMessageDto>> SendAsync(string text, List<string> attachments, CancellationToken ct)
    {
        await HistoryAsync(); // makes sure the greeting is part of what the model sees
        var firstNewId = await db.AssistantMessages.MaxAsync(m => (int?)m.Id) ?? 0;

        text = BotToken().Replace(text.Trim(), "[токен скрыт]");
        if (text.Length > 4000) text = text[..4000];
        var images = attachments.Where(IsOwnImage).Distinct().Take(8).ToList();

        var user = new AssistantMessage
        {
            Role = "user", Content = text, CreatedAt = DateTime.Now,
            Attachments = images.Count > 0 ? JsonSerializer.Serialize(images) : null,
        };
        db.AssistantMessages.Add(user);
        await db.SaveChangesAsync();

        if (!ai.AiEnabled)
        {
            Reply("ИИ-помощник сейчас недоступен: на сервере не настроен ключ OpenAI. Магазин можно настроить вручную — " +
                  "все разделы есть в админ-панели (кнопка вверху справа).", []);
            await db.SaveChangesAsync();
            return await NewSince(firstNewId);
        }

        try
        {
            await RunTurnAsync(user, images, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Setup assistant turn failed");
            Reply($"Не получилось ответить: {e.Message}. Попробуйте отправить сообщение ещё раз.", ["Повторить"]);
            await db.SaveChangesAsync();
        }
        return await NewSince(firstNewId);
    }

    async Task RunTurnAsync(AssistantMessage user, List<string> images, CancellationToken ct)
    {
        var history = await db.AssistantMessages.AsNoTracking()
            .Where(m => m.Id < user.Id).OrderByDescending(m => m.Id).Take(HistoryWindow).ToListAsync();
        history.Reverse();
        history = Consistent(history);

        var conversation = new List<object> { new { role = "system", content = await SystemPromptAsync() } };
        conversation.AddRange(history.Select(ToOpenAi));
        conversation.Add(CurrentUserMessage(user, images));

        var toolsRan = false;
        var nudged = false;
        for (var round = 0; round < MaxRounds; round++)
        {
            var message = await ai.ChatAsync(conversation, AssistantTools.Definitions, ReplyFormat, ct);
            var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

            if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array && calls.GetArrayLength() > 0)
            {
                var said = content is null ? null : ParseReply(content).Text;
                db.AssistantMessages.Add(new AssistantMessage
                {
                    Role = "assistant", Content = said ?? "", ToolCalls = calls.GetRawText(), CreatedAt = DateTime.Now,
                });
                conversation.Add(new { role = "assistant", content, tool_calls = calls });

                foreach (var call in calls.EnumerateArray())
                {
                    var id = call.GetProperty("id").GetString() ?? "";
                    var fn = call.GetProperty("function");
                    var outcome = await tools.RunAsync(fn.GetProperty("name").GetString() ?? "", fn.GetProperty("arguments").GetString() ?? "{}");
                    db.AssistantMessages.Add(new AssistantMessage
                    {
                        Role = "tool", ToolCallId = id, Content = outcome.Result, CreatedAt = DateTime.Now,
                        Card = outcome.Card is null ? null : JsonSerializer.Serialize(outcome.Card, Json),
                    });
                    conversation.Add(new { role = "tool", tool_call_id = id, content = outcome.Result });
                }
                toolsRan = true;
                await db.SaveChangesAsync();
                // The model sees the shop as it is now, not as it was when the turn started.
                conversation[0] = new { role = "system", content = await SystemPromptAsync() };
                continue;
            }

            var reply = ParseReply(content ?? "");
            // "Сохранил / добавлю" with no tool call means nothing happened — the model gets one chance to act on it.
            if (!toolsRan && !nudged && round < MaxRounds - 1 && ClaimsAction().IsMatch(reply.Text))
            {
                nudged = true;
                conversation.Add(new { role = "assistant", content });
                conversation.Add(new
                {
                    role = "system",
                    content = "Your reply says something was (or will be) saved, added or created, but you called no tool, so " +
                              "nothing changed. If the administrator already gave the data, call the tool now and then answer. " +
                              "If you are waiting for their confirmation, rewrite the reply as a question without claiming anything was done.",
                });
                continue;
            }
            Reply(reply.Text.Length > 0 ? reply.Text : "Готово.", reply.Suggestions);
            await db.SaveChangesAsync();
            return;
        }

        Reply("Изменения применены. Что делаем дальше?", ["Что осталось настроить?"]);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// OpenAI rejects a conversation where a tool call has no result, or a result has no call. Either can happen at
    /// the edge of the history window or after a turn that failed half-way, so such pairs are left out.
    /// </summary>
    static List<AssistantMessage> Consistent(List<AssistantMessage> history)
    {
        static IEnumerable<string> CallIds(string toolCalls) =>
            JsonDocument.Parse(toolCalls).RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetString() ?? "");

        var answered = history.Where(m => m.Role == "tool").Select(m => m.ToolCallId ?? "").ToHashSet();
        var complete = history.Where(m => m.Role != "assistant" || m.ToolCalls is null || CallIds(m.ToolCalls).All(answered.Contains)).ToList();
        var asked = complete.Where(m => m.ToolCalls is not null).SelectMany(m => CallIds(m.ToolCalls!)).ToHashSet();
        return complete.Where(m => m.Role != "tool" || asked.Contains(m.ToolCallId ?? "")).ToList();
    }

    void Reply(string text, List<string> suggestions) => db.AssistantMessages.Add(new AssistantMessage
    {
        Role = "assistant", Content = text, CreatedAt = DateTime.Now,
        Suggestions = suggestions.Count > 0 ? JsonSerializer.Serialize(suggestions, Json) : null,
    });

    async Task<List<AssistantMessageDto>> NewSince(int id) =>
        Visible(await db.AssistantMessages.AsNoTracking().Where(m => m.Id > id).OrderBy(m => m.Id).ToListAsync());

    /// <summary>
    /// What the administrator sees: their messages and the assistant's answers. Cards produced by tools during a
    /// turn are attached to the answer that closes it.
    /// </summary>
    static List<AssistantMessageDto> Visible(List<AssistantMessage> messages)
    {
        var result = new List<AssistantMessageDto>();
        var cards = new List<AssistantCard>();
        foreach (var m in messages)
        {
            if (m.Role == "tool")
            {
                if (m.Card is { } card && JsonSerializer.Deserialize<AssistantCard>(card, Json) is { } parsed) cards.Add(parsed);
                continue;
            }
            if (m.Role == "assistant" && m.ToolCalls is not null) continue; // "let me do that" before the tools ran
            if (m.Content.Length == 0 && m.Role != "user") continue;
            var (text, fromJson) = m.Role == "assistant" && m.Content.TrimStart().StartsWith('{') ? ParseReply(m.Content) : (m.Content, []);
            result.Add(new AssistantMessageDto(m.Id, m.Role, text,
                m.Suggestions is null ? fromJson : JsonSerializer.Deserialize<List<string>>(m.Suggestions)?.Where(IsFitSuggestion).ToList() ?? [],
                m.Role == "assistant" ? [.. cards] : [],
                m.Attachments is null ? [] : JsonSerializer.Deserialize<List<string>>(m.Attachments) ?? [],
                m.CreatedAt));
            if (m.Role == "assistant") cards.Clear();
        }
        return result;
    }

    // ------------------------------------------------------------------ OpenAI message shapes

    /// <summary>The final answer is JSON: the text plus quick replies the panel shows as buttons.</summary>
    static readonly object ReplyFormat = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "assistant_reply",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    message = new { type = "string", description = "The reply shown to the administrator" },
                    suggestions = new
                    {
                        type = "array",
                        description = "0–4 short quick replies the administrator may tap, written as they would say them",
                        items = new { type = "string" },
                    },
                },
                required = new[] { "message", "suggestions" },
                additionalProperties = false,
            },
        },
    };

    /// <summary>
    /// The answer's JSON. Models occasionally send the same object twice in a row (or add text around it), so only
    /// the first complete object counts — the administrator must never see raw JSON.
    /// </summary>
    static (string Text, List<string> Suggestions) ParseReply(string content)
    {
        var start = content.IndexOf('{');
        if (start < 0) return (content.Trim(), []);
        try
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(content[start..]),
                new JsonReaderOptions { AllowMultipleValues = true, AllowTrailingCommas = true });
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("message", out var m)) return (content.Trim(), []);
            var suggestions = root.TryGetProperty("suggestions", out var s) && s.ValueKind == JsonValueKind.Array
                ? s.EnumerateArray().Select(x => x.GetString() ?? "").Where(IsFitSuggestion).Take(4).ToList()
                : [];
            return ((m.GetString() ?? "").Trim(), suggestions);
        }
        catch (JsonException)
        {
            // Cut off mid-object: salvage the message text rather than show braces.
            var match = Regex.Match(content, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (match.Success) return (Regex.Unescape(match.Groups[1].Value).Trim(), []);
            var open = Regex.Match(content, "\"message\"\\s*:\\s*\"(.*)$", RegexOptions.Singleline);
            return open.Success ? (open.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"").Trim() + "…", []) : (content.Trim(), []);
        }
    }

    /// <summary>
    /// A quick reply is something the administrator would say — never a fact only they know. An address, a phone
    /// number or a price put into their mouth would be saved as if they had typed it.
    /// </summary>
    static bool IsFitSuggestion(string s) =>
        s.Length is > 0 and <= 60 && !MadeUpFact().IsMatch(s);

    [GeneratedRegex(@"(ул\.|улиц|проспект|пр-т|переул|массив|квартал|дом\s*\d|\bд\.\s*\d|\d+\s*(-?й)?\s*кв|\+?\d[\d\s()-]{7,}|\d[\d\s]*\s*(сум|so'm|soʻm|uzs)|https?://|\.uz\b|\.com\b)", RegexOptions.IgnoreCase)]
    private static partial Regex MadeUpFact();

    object ToOpenAi(AssistantMessage m) => m.Role switch
    {
        "tool" => new { role = "tool", tool_call_id = m.ToolCallId, content = m.Content },
        "assistant" when m.ToolCalls is not null => new
        {
            role = "assistant",
            content = m.Content.Length > 0 ? m.Content : null,
            tool_calls = JsonDocument.Parse(m.ToolCalls).RootElement,
        },
        // Earlier answers are replayed in the same JSON shape the model is asked to answer in.
        "assistant" => new
        {
            role = "assistant",
            content = JsonSerializer.Serialize(new
            {
                message = m.Content.TrimStart().StartsWith('{') ? ParseReply(m.Content).Text : m.Content,
                suggestions = m.Suggestions is null ? [] : JsonSerializer.Deserialize<List<string>>(m.Suggestions) ?? [],
            }, Json),
        },
        _ => new { role = "user", content = WithAttachmentNote(m) },
    };

    static string WithAttachmentNote(AssistantMessage m)
    {
        if (m.Attachments is null) return m.Content;
        var urls = JsonSerializer.Deserialize<List<string>>(m.Attachments) ?? [];
        return urls.Count == 0 ? m.Content : $"{m.Content}\n\n[Прикреплённые фото: {string.Join(", ", urls)}]";
    }

    /// <summary>
    /// The newest message carries its images themselves, so the model can read a menu, a price list or a product
    /// photo. Older messages only mention the file names, to keep every turn small.
    /// </summary>
    object CurrentUserMessage(AssistantMessage user, List<string> images)
    {
        var text = WithAttachmentNote(user);
        if (text.Length == 0) text = "(фото без подписи)";
        if (images.Count == 0) return new { role = "user", content = text };
        var parts = new List<object> { new { type = "text", text } };
        foreach (var url in images)
            if (DataUrl(url) is { } data) parts.Add(new { type = "image_url", image_url = new { url = data, detail = "auto" } });
        return new { role = "user", content = parts };
    }

    bool IsOwnImage(string url) =>
        url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.Contains("..")
        && Path.GetExtension(url).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"
        && File.Exists(UploadPath(url));

    string UploadPath(string url) => Path.Combine(env.ContentRootPath, "uploads", url["/uploads/".Length..]);

    string? DataUrl(string url)
    {
        var file = new FileInfo(UploadPath(url));
        if (!file.Exists || file.Length > 8 * 1024 * 1024) return null;
        var mime = file.Extension.ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg" };
        return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(file.FullName))}";
    }

    /// <summary>Words that claim a change: past ("сохранил", "добавлены") or promised ("добавлю", "сохраню").</summary>
    [GeneratedRegex(@"\b(сохрани(л|ла|ло|ли|лось)|сохранен[аоы]?|сохранён|сохраню|добавил[аи]?|добавлен[аоы]?|добавлю|создал[аи]?|создан[аоы]?|создам|обновил[аи]?|обновлен[аоы]?|обновлён|обновлю|установил[аи]?|установлен[аоы]?|записал[аи]?|записан[аоы]?|saved|added|created|updated|saqlandi|qoʻshildi|yaratildi)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ClaimsAction();

    /// <summary>A Telegram bot token («123456789:AA…»): it controls the bot, so it never enters the conversation.</summary>
    [GeneratedRegex(@"\b\d{6,12}:[A-Za-z0-9_-]{30,}\b")]
    public static partial Regex BotToken();

    // ------------------------------------------------------------------ greeting

    AssistantMessage Greeting()
    {
        var store = tenant.Store!;
        var first = (tenant.Admin?.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var hello = first is null ? "Здравствуйте!" : $"Здравствуйте, {first}!";
        if (store.OnboardedAt is not null)
            return new AssistantMessage
            {
                Role = "assistant", CreatedAt = DateTime.Now,
                Content = $"{hello} Я ИИ-помощник Plum Market. Могу добавить товары и категории, поправить описание, " +
                          "доставку и контакты, подключить Telegram-бота или подсказать, как продавать больше. Чем помочь?",
                Suggestions = JsonSerializer.Serialize(new[] { "Что ещё стоит настроить?", "Добавить товары", "Как получить первые заказы?" }, Json),
            };
        return new AssistantMessage
        {
            Role = "assistant", CreatedAt = DateTime.Now,
            Content = $"{hello} Я ИИ-помощник Plum Market и помогу настроить магазин.\n\n" +
                      $"Магазин **«{store.Name}»** уже создан и работает — посмотреть его можно кнопкой «Открыть магазин» " +
                      "вверху справа. Но пока в нём пусто. " +
                      "Давайте за 10–15 минут подготовим его к первым заказам: я задам несколько вопросов, сам заполню " +
                      "настройки, создам категории и товары, а вы только проверите.\n\n" +
                      "Для начала расскажите: **что вы продаёте и где?** Например, «пекарня в Ташкенте» или «магазин детской одежды в Самарканде».",
            Suggestions = JsonSerializer.Serialize(new[] { "Пекарня и кондитерская", "Одежда и обувь", "Кафе с доставкой", "Что нужно настроить?" }, Json),
        };
    }

    // ------------------------------------------------------------------ progress

    public async Task<List<SetupStep>> ProgressAsync()
    {
        var store = tenant.Store!;
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var branch = await db.Branches.AsNoTracking().OrderBy(b => b.Id).FirstOrDefaultAsync();
        var accepted = store.SetupAccepted.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var adminPhone = tenant.Admin?.Phone;

        return
        [
            new("profile", "Описание магазина", "Название и «О нас»",
                accepted.Contains("profile") || (!string.IsNullOrWhiteSpace(s.AboutText) && !s.AboutText.Contains(StoreProvisioner.AboutPlaceholder)),
                false, "/platforms/website"),
            new("branch", "Филиалы", "Адреса, откуда забирают и везут заказы",
                branch is not null && branch.Address.Length > 0, false, "/store"),
            new("categories", "Категории", "Разделы каталога", await db.Categories.AnyAsync(), false, "/products/categories"),
            new("products", "Товары", "Названия, цены и фото", await db.Products.AnyAsync(), false, "/products/items"),
            new("contacts", "Контакты", "Телефон и время работы",
                accepted.Contains("contacts") || s.WorkingHours != StoreProvisioner.DefaultHours || s.Phone != adminPhone,
                false, "/store"),
            new("delivery", "Доставка", "Стоимость и условия",
                accepted.Contains("delivery") || s.DeliveryFee != StoreProvisioner.DefaultDeliveryFee
                || s.FreeDeliveryFrom != StoreProvisioner.DefaultFreeDeliveryFrom || s.DeliveryTerms != StoreProvisioner.DefaultDeliveryTerms,
                false, "/store"),
            new("returns", "Условия возврата", "Что покупатель увидит в профиле",
                accepted.Contains("returns") || !(s.ReturnTerms ?? "").Contains(StoreProvisioner.ReturnsPlaceholder),
                false, "/platforms/website"),
            new("telegram", "Telegram-бот", "Магазин прямо в Telegram", store.BotUsername is { Length: > 0 }, true, "/platforms/telegram"),
        ];
    }

    // ------------------------------------------------------------------ system prompt

    async Task<string> SystemPromptAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Instructions);
        sb.AppendLine();
        sb.AppendLine("# The administrator's shop right now");
        sb.AppendLine(await SnapshotAsync());
        return sb.ToString();
    }

    const string Instructions = """
        You are the setup assistant of Plum Market — a platform where small and medium businesses in Uzbekistan open an
        online shop: a website plus the same shop inside Telegram (a Telegram Mini App opened from the shop's own bot).
        The administrator has just registered. Instead of an empty admin panel they talk to you. Your job: guide and
        consult them until the shop is ready to take its first orders, and do the configuration yourself with your tools.

        Behave like a capable employee this merchant just hired — their right hand and e-commerce consultant, not a form
        that saves whatever it is given. You care whether the shop sells: you check what they give you, say honestly
        when something will not work for customers, suggest a better way and explain why in one sentence, and remind
        them of what is still missing. You stay polite and brief, and the final decision is always theirs.

        # Quality check — before saving anything the administrator wrote
        Review every text, name and price they give you before it reaches customers:
        - Vague or empty descriptions ("хороший", "свежий", "вкусный", "качественный", "лучший", "красивый", one or two
          words) tell a customer nothing. Don't save it straight away: say kindly that it is too vague and what a customer
          would want to know instead (size, weight, composition, what is included, how it is used), show an improved
          version, and ask one or two concrete questions to fill the gaps. The improved version may use ONLY facts they
          gave you — never add delivery speed, origin, farms, quality promises or anything else they did not say; where a
          fact is missing, ask for it instead of making it up. Example (a different shop): for "Футболка — хорошая"
          answer that «хорошая» doesn't help choosing, ask about fabric and sizes, and once told "хлопок, S–XL" write
          «Футболка из 100% хлопка, размеры S–XL». Use their facts in their words — "около недели" stays "около недели".
        - Names: flag names that are generic ("Товар", "Букет 1"), in CAPS, with typos, or too long for a phone screen, and
          propose a clean one.
        - Prices: question anything that looks like a slip — 25 instead of 25 000 сум, a single flower pricier than a
          bouquet, an old price lower than the new one.
        - Offer both options as quick replies (e.g. "Сохранить ваш вариант" / "Сохранить мой вариант"). If they insist on
          their own wording, save it without arguing.
        - When the text is already good, don't nitpick — save it and move on.

        # How to run the conversation
        - Follow this plan, one step at a time, skipping what is already done (see the progress list below):
          1. Business: what they sell and where → write a short «О нас» (update_store_profile); rename the shop if they want.
          2. Branches — BEFORE the catalog, because every product belongs to branches: ask how many points (shops,
             kitchens, warehouses) they have and the address of each. The shop starts with one branch «Основной
             филиал» with an empty address: fill it in (save_branch with its id, give it a proper name such as the district)
             and create the others (save_branch with branch_id null). Use your best coordinates and say the pins can be
             checked on the map (open_section "store"). Phone and hours given for the main branch also become the shop's.
          3. Catalog: propose 3–8 categories that fit the business, then add products with real prices. Offer to build a
             starter catalog from their description, or from a photo of their menu / price list / products.
             With more than one branch, ask where the products are sold ("во всех филиалах или только в некоторых?") and
             pass branch_ids per product. Ask whether they keep counted stock (e.g. 20 pieces) or make to order
             (unlimited) and pass quantity. After creating, mention in one line that the table under the answer lets them
             tick branches and type quantities for each product.
             A number of pieces is STOCK, never separate products: "3 розы", "3 отдельные розы" or "роза, 3 штуки" is ONE
             product «Роза» with quantity 3 — never «Роза 1», «Роза 2», «Роза 3», and never unlimited. Whenever the
             administrator gives a count of a product, pass it as quantity; leave quantity null only when they gave no count
             or said it is made to order / always available.
             Stock counts and prices never go into names or descriptions («5 штук», «в наличии») — the shop shows them itself. Separate products only for really different items (colour,
             size, variety); if unsure whether "red and white roses" are one product or two, ask.
          3b. Photos — right after categories and products are created, always ask for photos. They are optional, but a
             product without a photo sells far worse, so say so in one sentence. Two ways: the photo button next to each
             product / category in the card under the answer, or several photos attached here in the chat (the 🖼 button)
             with a word on which is which — you can see attached photos, match them to products and set image_url with
             update_product / create_categories. Photo tips when relevant: square, good daylight, plain background, the
             product fills the frame. If they postpone, move on and remind once near the end.
          4. Contacts: public phone and working hours (update_contacts), unless already set with the branch.
          5. Delivery: courier price and free-delivery threshold; write the delivery terms text (update_delivery).
             Customers currently pay in cash on delivery or at pickup — online payments (Click, Payme) come later.
          6. Return terms (update_store_profile return_terms) — offer a sensible text for their kind of goods.
          7. Optional: Telegram bot, loyalty points, a welcome promo code. As soon as the administrator wants a bot, call
             request_telegram_bot in that same turn — the form it shows already lists the @BotFather steps
             (/newbot → name → username ending in «bot» → copy the token), so just add one line of context.
          8. When the required steps are done, give a short summary and, if they agree, call finish_setup.
        - Ask at most one or two questions per message. Keep messages short (usually under 110 words), warm and practical.
          Use **bold** sparingly and "- " lists for options. No headings, no tables, no emoji spam.
        - Never say you created, saved or changed something unless you called the tool for it in this same turn. When the
          administrator confirms a proposal ("да", "подтверждаю", "добавьте ещё …"), call the tool right away in that turn.
        - When the administrator states a fact (address, phone, hours, a price, delivery cost), save it right away with a
          tool — no need to ask "shall I save?". When YOU propose content (a list of categories, draft products, prices you
          are unsure about), show it briefly and ask to confirm before creating. You may write and save texts («О нас»,
          delivery and return terms) directly, then offer to adjust them.
        - After a tool runs, the panel shows a card with the change and a link, so don't repeat every detail — confirm in
          one line and move to the next step.
        - Never invent prices, addresses or phone numbers. If you need them, ask. Example prices must be confirmed first.
          In texts («О нас», descriptions) stay with what the administrator told you: no made-up claims about quality,
          ingredients, years in business, awards or guarantees.
        - Product and category names: always both Russian (name_ru) and Uzbek in the Latin script (name_uz), plus a 1–2
          sentence description in both (see the Uzbek rule at the end).
        - Photos the administrator attaches arrive as /uploads/... URLs (and you can see the newest ones). Use them as
          image_url for products or categories — look at each photo to decide which product it shows; if you can't tell,
          ask. If a photo is a menu or price list, read the items and prices and propose them as products.
        - After each step add at most one short practical tip that fits what they just did (not a lecture), and point out
          what is still missing (e.g. products without photos or descriptions, a branch without a pin check).
        - Delete things only after the administrator explicitly confirms. Never ask for passwords, card numbers or the bot
          token in the chat — the token goes only into the form shown by request_telegram_bot. If you see "[токен скрыт]",
          explain that the token must go into the form and call request_telegram_bot.
        - Some sections of the full platform are not available yet: online payments, external delivery services
          (Fargo, Uzpost, Yandex), staff and roles, the tariff plan and integrations (POS, CRM, Instagram). If asked, say
          they are coming soon — never pretend to configure them.
        - Consult like an experienced e-commerce advisor for Uzbekistan: prices in сум; Telegram is where most customers
          shop; typical courier delivery in Tashkent is 10 000–25 000 сум with free delivery above a threshold; square
          photos on a clean background sell better; short clear names; start with the best-selling 10–20 products; share
          the shop link and bot in Instagram/Telegram channels; a first-order promo code helps the first sales.
        - Use open_section when something is easier by hand in the panel (many photos, the map pin, stock, banners).
        - Reply in the language of the administrator's last message (Russian by default; Uzbek → Uzbek Latin; English → English).
        - Your final answer is JSON: "message" (the reply) and "suggestions" — 0–4 short quick replies (max ~35 characters)
          the administrator might tap next, phrased as they would say them. A tapped suggestion is sent to you as their
          message, so never suggest navigation ("В админ-панель", "Открыть магазин") — the panel has its own buttons.
          Suggestions must never contain facts only the administrator knows — no addresses, street names, phone numbers,
          prices, names or dates. When you ask for such a fact, offer at most a neutral reply like "Напишу адрес" or none.
          In your questions don't give made-up examples of those facts either (no sample addresses or phone numbers).
        - Never write the shop's web address or any URL in your messages. To show the shop, say it opens with the
          «Открыть магазин» button in the top right corner.

        # Admin panel sections (for links and explanations)
        Дашборд /dashboard — sales, orders, balance, first-steps checklist. Заказы /orders — order board by status
        (Новый → В сборке → Готов → Передан в доставку → В пути → Доставлен → Завершён), auto-reply texts. Клиенты
        /customers — customers and the loyalty points settings. Чат /chat — messages from customers. Каталог:
        Категории /products/categories, Товары /products/items (photos, prices, variants, AI translate/describe buttons),
        Скидки /products/discounts, Склад /products/stock (stock per branch). Маркетинг: Промокоды /marketing/promocodes,
        Баннеры /marketing/banners, Отзывы /marketing/reviews. Платформы: Веб-сайт /platforms/website (name, «О нас»,
        return terms), Telegram-бот /platforms/telegram. Магазин /store — contacts, delivery price and terms, branches on a map.

        # Uzbek rule
        """ + AiContentService.UzbekRule;

    async Task<string> SnapshotAsync()
    {
        var store = tenant.Store!;
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var sb = new StringBuilder();
        sb.AppendLine($"Administrator: {tenant.Admin?.Name} ({tenant.Admin?.Phone})");
        sb.AppendLine($"Shop: «{store.Name}», address {links.ShopUrl(store)}, registered {store.CreatedAt:dd.MM.yyyy}, first-run setup {(store.OnboardedAt is null ? "in progress" : "finished")}");
        sb.AppendLine($"«О нас»: {Short(s.AboutText, 400)}");
        sb.AppendLine($"Public phone: {s.Phone ?? "—"}; working hours: {s.WorkingHours ?? "—"}");
        sb.AppendLine($"Delivery: {(s.DeliveryFee == 0 ? "free" : AssistantTools.Money(s.DeliveryFee))}, free from {(s.FreeDeliveryFrom is { } f ? AssistantTools.Money(f) : "never")}; terms: {Short(s.DeliveryTerms, 300)}");
        sb.AppendLine($"Return terms: {Short(s.ReturnTerms, 300)}");
        sb.AppendLine($"Loyalty points: {(s.BonusEnabled ? $"on, 1 point per {AssistantTools.Money(s.SpendPerPoint)}" : "off")}; promo codes: {await db.PromoCodes.CountAsync()}");
        sb.AppendLine($"Telegram bot: {(store.BotUsername is { Length: > 0 } bot ? "@" + bot : "not connected")}");

        sb.AppendLine("Branches:");
        foreach (var b in await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync())
            sb.AppendLine($"- id={b.Id} «{b.Name}», address: {(b.Address.Length > 0 ? b.Address : "EMPTY")}, phone {b.Phone ?? "—"}, hours {b.WorkingHours ?? "—"}, pin {b.Lat:0.####},{b.Lng:0.####}");

        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();
        var counts = await db.Products.AsNoTracking().Where(p => p.CategoryId != null).GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        sb.AppendLine(categories.Count == 0 ? "Categories: none yet" : $"Categories ({categories.Count}):");
        foreach (var c in categories.Take(60))
            sb.AppendLine($"- id={c.Id} {c.Name.Get()} / {c.Name.GetValueOrDefault("uz") ?? "—"}{(c.ParentId is { } p ? $" (in id={p})" : "")}, products: {counts.GetValueOrDefault(c.Id)}");

        var products = await db.Products.AsNoTracking().OrderBy(p => p.SortOrder).Take(80)
            .Select(p => new
            {
                p.Id, p.Name, p.Price, p.OldPrice, p.Unit, p.IsActive, Category = p.Category != null ? p.Category.Name : null, p.Media,
                Stock = p.Stock.Select(s => new { s.BranchId, s.Status, s.Quantity }).ToList(),
            }).ToListAsync();
        var total = await db.Products.CountAsync();
        sb.AppendLine(total == 0 ? "Products: none yet" : $"Products ({total}{(total > products.Count ? $", first {products.Count} shown" : "")}):");
        foreach (var p in products)
            sb.AppendLine($"- id={p.Id} {p.Name.Get()} — {AssistantTools.Money(p.Price)}/{p.Unit}{(p.OldPrice is { } o ? $" (was {AssistantTools.Money(o)})" : "")}, " +
                          $"category: {p.Category?.Get() ?? "—"}, photo: {(p.Media.Count > 0 ? "yes" : "no")}{(p.IsActive ? "" : ", hidden")}, " +
                          $"stock: {(p.Stock.Count == 0 ? "NOT SOLD ANYWHERE" : string.Join("; ", p.Stock.Select(s => $"branch {s.BranchId} " + (s.Status == StockStatus.Unlimited ? "unlimited" : $"{s.Quantity} pcs"))))}");

        sb.AppendLine("Setup progress:");
        foreach (var step in await ProgressAsync())
            sb.AppendLine($"- [{(step.Done ? "x" : " ")}] {step.Key}: {step.Title}{(step.Optional ? " (optional)" : "")}");
        return sb.ToString();
    }

    static string Short(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "—";
        var flat = Regex.Replace(text, @"\s+", " ").Trim();
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
