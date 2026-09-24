using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// What the merchant's bot answers a customer: a greeting and the button that opens that shop inside Telegram.
/// Both ways of hearing from Telegram — the webhook and polling — end up here, so the bot says the same thing.
/// </summary>
public class TelegramGreeter(TelegramBotApi telegram, StoreLinks links, ILogger<TelegramGreeter> log)
{
    public async Task ReplyAsync(Store store, long chatId, string? firstName, string? text, CancellationToken ct = default)
    {
        if (store.BotToken is not { Length: > 0 } token) return;

        var name = firstName?.Trim();
        var greeting = string.IsNullOrEmpty(name) ? "Здравствуйте!" : $"Здравствуйте, {name}!";
        var body = (text ?? "").TrimStart().StartsWith("/start", StringComparison.OrdinalIgnoreCase)
            ? $"{greeting} Это магазин «{store.Name}». Нажмите кнопку ниже — каталог, корзина и оформление " +
              "заказа откроются прямо здесь, в Telegram."
            : $"{greeting} Чтобы посмотреть товары и оформить заказ, откройте магазин «{store.Name}» кнопкой ниже.";

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
