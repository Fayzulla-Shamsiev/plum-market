using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// What the merchant's bot says to a customer: a greeting, the button that opens that shop inside Telegram, and
/// an offer to receive order updates here. Both ways of hearing from Telegram — the webhook and polling — end up
/// here, so the bot behaves the same either way.
/// </summary>
public class TelegramGreeter(AppDbContext db, StoreContext tenant, TelegramBotApi telegram, StoreLinks links,
    ILogger<TelegramGreeter> log)
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

    /// <summary>Answers whatever the customer sent: a shared phone number, or anything else.</summary>
    public async Task HandleAsync(Store store, TelegramBotApi.MessageDto message, CancellationToken ct = default)
    {
        if (store.BotToken is not { Length: > 0 }) return;
        // Telegram calls us outside any request, so nothing has said which store this is yet. Without it the
        // per-store filter would hide every customer and a new one would belong to no shop.
        tenant.StoreId ??= store.Id;
        tenant.Store ??= store;
        var chatId = message.Chat.Id;

        if (message.Contact?.PhoneNumber is { Length: > 0 } phone)
        {
            await LinkByPhoneAsync(store, chatId, phone, message.Contact.FirstName ?? message.From?.FirstName, ct);
            return;
        }

        var template = store.BotGreeting is { Length: > 0 } custom ? custom : DefaultGreeting(store.Name);
        // Ask for the number only while we don't know who this chat belongs to.
        var known = await db.Customers.AnyAsync(c => c.TelegramChatId == chatId, ct);
        await SendAsync(store, chatId, Render(template, message.From?.FirstName, store.Name), askForPhone: !known, ct);
    }

    /// <summary>
    /// Ties this chat to the customer with that number, so order updates can reach them here. A number we
    /// haven't seen becomes a customer straight away — they'll be recognised when they order.
    /// </summary>
    async Task LinkByPhoneAsync(Store store, long chatId, string rawPhone, string? firstName, CancellationToken ct)
    {
        if (Phone.Normalize(rawPhone) is not { } phone)
        {
            await SendAsync(store, chatId, "Не удалось прочитать номер. Попробуйте ещё раз.", askForPhone: true, ct);
            return;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                FullName = string.IsNullOrWhiteSpace(firstName) ? "Покупатель" : firstName.Trim(),
                Phone = phone,
                Platform = Platform.Telegram,
                CreatedAt = DateTime.Now,
                LastVisitAt = DateTime.Now,
            };
            db.Customers.Add(customer);
        }
        customer.TelegramChatId = chatId;
        await db.SaveChangesAsync(ct);

        await SendAsync(store, chatId,
            $"Готово! Статусы заказов по номеру {phone} будут приходить сюда.", askForPhone: false, ct);
    }

    async Task SendAsync(Store store, long chatId, string text, bool askForPhone, CancellationToken ct)
    {
        try
        {
            await telegram.SendShopMessageAsync(store.BotToken!, chatId, text, "🛍 Открыть магазин",
                links.MiniAppUrl(store), askForPhone, ct);
        }
        catch (Exception e)
        {
            // A bot that can't answer must not stop the rest of the shop from working.
            log.LogWarning(e, "Could not answer a Telegram message for store {StoreId}", store.Id);
        }
    }
}
