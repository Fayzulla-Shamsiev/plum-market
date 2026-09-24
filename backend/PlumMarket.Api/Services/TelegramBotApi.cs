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
