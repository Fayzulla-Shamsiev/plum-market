using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// What the merchant's bot answers a customer: a greeting and the button that opens that shop inside Telegram.
/// Both ways of hearing from Telegram — the webhook and polling — end up here, so the bot says the same thing.
/// </summary>
public class TelegramGreeter(TelegramBotApi telegram, StoreLinks links, ILogger<TelegramGreeter> log)
{
    /// <summary>The greeting a store starts with, until the administrator writes their own.</summary>
    public static string DefaultGreeting(string storeName) =>
        $"Здравствуйте, {{name}}! Это магазин «{storeName}». Нажмите кнопку ниже — каталог, корзина и " +
        "оформление заказа откроются прямо здесь, в Telegram.";

    /// <summary>What an empty chat shows before «Начать» (Telegram's bot description).</summary>
    public static string DefaultAbout(string storeName) =>
        $"Магазин «{storeName}». Нажмите «Открыть магазин», чтобы выбрать товары и оформить заказ.";

    /// <summary>Fills in who is being greeted; an unknown name simply disappears from the sentence.</summary>
    public static string Render(string text, string? firstName, string storeName)
    {
        var name = firstName?.Trim();
        var rendered = text.Replace("{store}", storeName);
        rendered = string.IsNullOrEmpty(name)
            // "Здравствуйте, {name}!" → "Здравствуйте!" rather than a dangling comma.
            ? System.Text.RegularExpressions.Regex.Replace(rendered, @",?\s*\{name\}", "")
            : rendered.Replace("{name}", name);
        return rendered.Trim();
    }

    public async Task ReplyAsync(Store store, long chatId, string? firstName, string? text, CancellationToken ct = default)
    {
        if (store.BotToken is not { Length: > 0 } token) return;

        var template = store.BotGreeting is { Length: > 0 } custom ? custom : DefaultGreeting(store.Name);
        var body = Render(template, firstName, store.Name);

        try
        {
            await telegram.SendShopMessageAsync(token, chatId, body, "🛍 Открыть магазин", links.MiniAppUrl(store), ct);
        }
        catch (Exception e)
        {
            // A bot that can't answer must not stop the rest of the shop from working.
            log.LogWarning(e, "Could not answer a Telegram message for store {StoreId}", store.Id);
        }
    }
}
