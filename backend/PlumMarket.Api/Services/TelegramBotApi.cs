using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlumMarket.Api.Services;

/// <summary>
/// The merchant's own Telegram bot (spec «Подключение Telegram-бота»). The administrator creates it in
/// @BotFather with /newbot and pastes the token here; we check the token with getMe — which also gives us the
/// bot's name and username, so nothing else has to be typed — and attach the shop to the bot's menu button with
/// setChatMenuButton, which is the "Open Shop" button customers press inside Telegram.
/// </summary>
public partial class TelegramBotApi(IHttpClientFactory factory, IConfiguration configuration, ILogger<TelegramBotApi> log)
{
    /// <summary>Telegram's own address; overridable (Telegram:ApiBase) so the flow can be exercised against a stub.</summary>
    string ApiBase => (configuration["Telegram:ApiBase"] ?? "https://api.telegram.org").TrimEnd('/');

    public record BotInfo(long Id, string Name, string Username);

    /// <summary>"8123456789:AAF3...": the id from @BotFather, a colon, then the secret.</summary>
    [GeneratedRegex(@"^\d{5,}:[A-Za-z0-9_\-]{20,}$")]
    private static partial Regex TokenShape();

    public static bool LooksLikeToken(string? token) => token is not null && TokenShape().IsMatch(token.Trim());

    /// <summary>Checks the token and reads who the bot is. Returns the reason when it can't.</summary>
    public async Task<(BotInfo? Bot, string? Error)> GetMeAsync(string token, CancellationToken ct = default)
    {
        if (!LooksLikeToken(token))
            return (null, "Токен не похож на настоящий — скопируйте его целиком из @BotFather.");

        var (result, error) = await CallAsync<GetMeResult>(token, "getMe", null, ct);
        if (result is null) return (null, error);
        if (!result.IsBot) return (null, "Этот токен принадлежит не боту.");
        return (new BotInfo(result.Id, result.FirstName, result.Username ?? ""), null);
    }

    /// <summary>
    /// Points the bot's menu button at the shop, so customers open it as a Mini App. Returns the reason when
    /// Telegram refuses — most often because the shop has no https address yet (a local run).
    /// </summary>
    public async Task<string?> SetMenuButtonAsync(string token, string shopUrl, string buttonText, CancellationToken ct = default)
    {
        if (!shopUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "Telegram открывает Mini App только по https-адресу, а магазин сейчас доступен по " +
                $"«{shopUrl}». Кнопка появится, когда магазин будет опубликован.";

        var body = new
        {
            menu_button = new
            {
                type = "web_app",
                text = buttonText,
                web_app = new { url = shopUrl },
            },
        };
        var (result, error) = await CallAsync<bool>(token, "setChatMenuButton", body, ct);
        return result ? null : error ?? "Telegram не принял кнопку меню.";
    }

    /// <summary>
    /// Everything that makes the bot greet a customer on its own: a /start command, the text shown in an empty
    /// chat (so the very first screen already explains what this bot is), and the webhook that lets us answer.
    /// Returns the reason when the webhook could not be set — the bot still works, it just stays silent.
    /// </summary>
    public async Task<string?> SetUpGreetingAsync(string token, string about, string? webhookUrl, string? secret,
        CancellationToken ct = default)
    {
        await CallAsync<bool>(token, "setMyCommands", new
        {
            commands = new[] { new { command = "start", description = "Открыть магазин" } },
        }, ct);

        await SetAboutAsync(token, about, ct);

        // No public address for Telegram to call: drop any old webhook so the bot can be polled instead.
        if (webhookUrl is null || secret is null)
        {
            await CallAsync<bool>(token, "deleteWebhook", new { drop_pending_updates = true }, ct);
            return null;
        }

        var (ok, error) = await CallAsync<bool>(token, "setWebhook", new
        {
            url = webhookUrl,
            secret_token = secret,
            allowed_updates = new[] { "message" },
            drop_pending_updates = true,
        }, ct);
        return ok ? null : error ?? "Telegram не принял адрес для обновлений.";
    }

    /// <summary>The greeting itself: a message with the button that opens the shop inside Telegram.</summary>
    public Task SendShopMessageAsync(string token, long chatId, string text, string buttonText, string shopUrl,
        CancellationToken ct = default) =>
        CallAsync<object>(token, "sendMessage", new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                inline_keyboard = new[] { new[] { new { text = buttonText, web_app = new { url = shopUrl } } } },
            },
        }, ct);

    /// <summary>The text an empty chat with the bot shows, before the customer presses «Начать».</summary>
    public async Task SetAboutAsync(string token, string about, CancellationToken ct = default)
    {
        await CallAsync<bool>(token, "setMyShortDescription", new { short_description = Cut(about, 120) }, ct);
        await CallAsync<bool>(token, "setMyDescription", new { description = Cut(about, 512) }, ct);
    }

    /// <summary>
    /// Checks that the launch data a Mini App page hands us really came from Telegram for this bot: the hash
    /// is an HMAC over the other fields, keyed by the bot token. Without this anyone could claim any chat id.
    /// </summary>
    public static long? VerifiedUserId(string botToken, string initData)
    {
        if (string.IsNullOrWhiteSpace(initData)) return null;
        var pairs = initData.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        if (!pairs.Remove("hash", out var hash)) return null;
        pairs.Remove("signature");

        var check = string.Join('\n', pairs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        var secret = System.Security.Cryptography.HMACSHA256.HashData("WebAppData"u8.ToArray(), Encoding.UTF8.GetBytes(botToken));
        var expected = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check)));
        if (!expected.Equals(hash, StringComparison.OrdinalIgnoreCase)) return null;

        // Stale data is as good as forged: a page kept open for days shouldn't keep proving who its user is.
        if (pairs.TryGetValue("auth_date", out var authDate) && long.TryParse(authDate, out var seconds)
            && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds) > TimeSpan.FromDays(1)) return null;

        if (!pairs.TryGetValue("user", out var user)) return null;
        return JsonDocument.Parse(user).RootElement.TryGetProperty("id", out var id) && id.TryGetInt64(out var value)
            ? value
            : null;
    }

    public record UpdateDto(
        [property: JsonPropertyName("update_id")] long UpdateId,
        MessageDto? Message);

    public record MessageDto(ChatDto Chat, SenderDto? From, string? Text);
    public record ChatDto(long Id);
    public record SenderDto([property: JsonPropertyName("first_name")] string? FirstName);

    /// <summary>
    /// Asks Telegram for new messages. Used for bots we can't give a webhook to — a shop running on a local
    /// machine has no address Telegram could call — so the bot still answers while it's being tried out.
    /// </summary>
    public async Task<List<UpdateDto>> GetUpdatesAsync(string token, long offset, CancellationToken ct = default)
    {
        var (updates, _) = await CallAsync<List<UpdateDto>>(token, "getUpdates", new
        {
            offset,
            timeout = 5,
            allowed_updates = new[] { "message" },
        }, ct);
        return updates ?? [];
    }

    public async Task StopGreetingAsync(string token, CancellationToken ct = default)
    {
        await CallAsync<bool>(token, "deleteWebhook", new { drop_pending_updates = true }, ct);
        await CallAsync<bool>(token, "setMyCommands", new { commands = Array.Empty<object>() }, ct);
    }

    static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    /// <summary>Puts the bot's menu button back to its default (the commands list).</summary>
    public async Task ResetMenuButtonAsync(string token, CancellationToken ct = default) =>
        await CallAsync<bool>(token, "setChatMenuButton", new { menu_button = new { type = "commands" } }, ct);

    async Task<(T? Result, string? Error)> CallAsync<T>(string token, string method, object? body, CancellationToken ct)
    {
        try
        {
            var client = factory.CreateClient(nameof(TelegramBotApi));
            var url = $"{ApiBase}/bot{token.Trim()}/{method}";
            // A serialized string (not PostAsJsonAsync) so the request carries a Content-Length instead of
            // being chunked — some HTTP front ends are picky about that.
            using var content = body is null
                ? null
                : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = content is null
                ? await client.GetAsync(url, ct)
                : await client.PostAsync(url, content, ct);
            var payload = await response.Content.ReadFromJsonAsync<Response<T>>(ct);

            if (payload is { Ok: true }) return (payload.Result, null);
            var description = payload?.Description ?? $"HTTP {(int)response.StatusCode}";
            log.LogWarning("Telegram {Method} failed: {Description}", method, description);
            return (default, Explain(description));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(e, "Telegram {Method} unreachable", method);
            return (default, "Не удалось связаться с Telegram. Попробуйте ещё раз через минуту.");
        }
    }

    /// <summary>Telegram answers in English; the administrator shouldn't have to decode it.</summary>
    static string Explain(string description) => description switch
    {
        var d when d.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) =>
            "Telegram не принял токен. Проверьте, что скопировали его полностью, и что бот не удалён в @BotFather.",
        var d when d.Contains("BUTTON_TEXT_INVALID", StringComparison.OrdinalIgnoreCase) =>
            "Telegram не принял название кнопки.",
        var d when d.Contains("WEB_APP_URL_INVALID", StringComparison.OrdinalIgnoreCase) =>
            "Telegram не принял адрес магазина: нужен публичный https-адрес.",
        _ => $"Telegram ответил: {description}",
    };

    record Response<T>(bool Ok, T? Result, string? Description);

    record GetMeResult(
        long Id,
        [property: JsonPropertyName("is_bot")] bool IsBot,
        [property: JsonPropertyName("first_name")] string FirstName,
        string? Username);
}
