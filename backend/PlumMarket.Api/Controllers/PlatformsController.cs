using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Admin «Платформы» — where the shop is open for customers. Every store is a website from the moment it is
/// registered; a Telegram bot is an extra door into the same shop, connected here whenever the merchant wants
/// one. Both show the same catalog, the same cart and the same orders.
/// </summary>
[ApiController]
[Route("api/platforms")]
public class PlatformsController(AppDbContext db, StoreContext tenant, TelegramBotApi telegram, StoreLinks links) : ControllerBase
{
    public record WebsiteDto(string Name, string Slug, string Url, string? About, string? ReturnTerms);
    public record TelegramDto(string Username, string Name, string Url, DateTime? LinkedAt, string? Warning,
        string ButtonUrl, bool ButtonIsFallback, bool Greets, string About, string Greeting, int Subscribers);
    public record PlatformsDto(WebsiteDto Website, TelegramDto? Telegram);

    [HttpGet]
    public ActionResult<PlatformsDto> Get() => Dto();

    public record WebsiteBody(string Name, string? About, string? ReturnTerms);

    /// <summary>
    /// What the shop is called and what it tells customers about itself — «О нас» and «Условия возврата и
    /// обмена», the pages a shopper opens from their profile. The address never changes, so links and a
    /// connected bot's button keep working.
    /// </summary>
    [HttpPut("website")]
    public async Task<ActionResult<PlatformsDto>> PutWebsite(WebsiteBody body)
    {
        var store = tenant.Store!;
        var name = (body.Name ?? "").Trim();
        if (name.Length is < 2 or > 80) return Bad("Название магазина — от 2 до 80 символов.", "name");

        var renamed = store.Name != name;
        store.Name = name;
        var settings = await db.Settings.FirstAsync();
        settings.StoreName = name;
        settings.AboutText = Clean(body.About, 4000);
        settings.ReturnTerms = Clean(body.ReturnTerms, 8000);

        // The bot greets customers by the shop's name, so a rename is worth passing on.
        if (renamed && store.BotToken is { Length: > 0 }) await SetUpGreetingAsync(store);
        await db.SaveChangesAsync();
        return Dto();
    }

    static string? Clean(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > max ? text[..max] : text;
    }

    public record TelegramBody(string? BotToken);

    /// <summary>
    /// Connects the merchant's bot (or re-attaches the current one, which is what you do once the shop has a
    /// public https address) and points its menu button at the shop.
    /// </summary>
    [HttpPut("telegram")]
    public async Task<ActionResult<PlatformsDto>> PutTelegram(TelegramBody body)
    {
        var store = tenant.Store!;
        var token = body.BotToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) token = store.BotToken;
        if (string.IsNullOrWhiteSpace(token)) return Bad("Вставьте токен бота из @BotFather.", "botToken");

        var (bot, error) = await telegram.GetMeAsync(token);
        if (bot is null) return Bad(error ?? "Не удалось проверить токен.", "botToken");
        if (await db.Stores.AnyAsync(s => s.BotUsername == bot.Username && s.Id != store.Id))
            return Bad($"Бот @{bot.Username} уже подключён к другому магазину.", "botToken");

        store.BotToken = token;
        store.BotUsername = bot.Username;
        store.BotName = bot.Name;
        store.BotWarning = await telegram.SetMenuButtonAsync(token, links.MiniAppUrl(store), "Open Shop");
        store.BotLinkedAt = store.BotWarning is null ? DateTime.Now : null;
        await SetUpGreetingAsync(store);

        var settings = await db.Settings.FirstAsync();
        settings.BotUsername = bot.Username;
        await db.SaveChangesAsync();
        return Dto();
    }

    /// <summary>Lets the bot go: its menu button goes back to the default, and we forget the token.</summary>
    [HttpDelete("telegram")]
    public async Task<ActionResult<PlatformsDto>> DeleteTelegram()
    {
        var store = tenant.Store!;
        if (store.BotToken is { Length: > 0 } token)
        {
            await telegram.ResetMenuButtonAsync(token);
            await telegram.StopGreetingAsync(token);
        }
        store.BotToken = null;
        store.BotWebhookSecret = null;
        store.BotUsername = null;
        store.BotName = null;
        store.BotLinkedAt = null;
        store.BotWarning = null;
        var settings = await db.Settings.FirstAsync();
        settings.BotUsername = "";
        await db.SaveChangesAsync();
        return Dto();
    }

    public record MessagesBody(string? About, string? Greeting);

    /// <summary>
    /// The two texts the administrator owns: what an empty chat says before «Начать», and what the bot answers
    /// to /start. Saving pushes the first one to Telegram straight away.
    /// </summary>
    [HttpPut("telegram/messages")]
    public async Task<ActionResult<PlatformsDto>> PutMessages(MessagesBody body)
    {
        var store = tenant.Store!;
        if (store.BotToken is not { Length: > 0 } token) return Bad("Сначала подключите бота.", "botToken");

        var about = Clean(body.About, 500);
        var greeting = Clean(body.Greeting, 1000);
        if (about is null) return Bad("Текст для пустого чата не может быть пустым.", "about");
        if (greeting is null) return Bad("Приветствие не может быть пустым.", "greeting");

        // Remembered as written by the administrator, so the defaults can still be recognised and restored.
        store.BotAbout = about == TelegramGreeter.DefaultAbout(store.Name) ? null : about;
        store.BotGreeting = greeting == TelegramGreeter.DefaultGreeting(store.Name) ? null : greeting;
        await telegram.SetAboutAsync(token, about);
        await db.SaveChangesAsync();
        return Dto();
    }

    /// <summary>Teaches the bot to greet customers: /start, the text of an empty chat, and the webhook.</summary>
    async Task SetUpGreetingAsync(Domain.Store store)
    {
        var webhook = links.WebhookUrl(store);
        store.BotWebhookSecret = webhook is null ? null : Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        // Without a webhook the bot is polled instead, so it still answers — nothing to warn about.
        var about = store.BotAbout ?? TelegramGreeter.DefaultAbout(store.Name);
        await telegram.SetUpGreetingAsync(store.BotToken!, about, webhook, store.BotWebhookSecret);
    }

    PlatformsDto Dto()
    {
        var s = tenant.Store!;
        var subscribers = db.Customers.Count(c => c.TelegramChatId != null);
        var settings = db.Settings.AsNoTracking().First();
        return new PlatformsDto(
            new WebsiteDto(s.Name, s.Slug, links.ShopUrl(s), settings.AboutText, settings.ReturnTerms),
            s.BotUsername is { Length: > 0 } username
                ? new TelegramDto(username, s.BotName ?? username, StoreLinks.BotUrl(s)!, s.BotLinkedAt, s.BotWarning,
                    links.MiniAppUrl(s), links.IsFallback(s), Greets: true,
                    About: s.BotAbout ?? TelegramGreeter.DefaultAbout(s.Name),
                    Greeting: s.BotGreeting ?? TelegramGreeter.DefaultGreeting(s.Name),
                    Subscribers: subscribers)
                : null);
    }

    ActionResult<PlatformsDto> Bad(string error, string field) => BadRequest(new { error, field });
}
