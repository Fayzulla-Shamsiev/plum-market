using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Вход и регистрация администратора:
/// новый — имя → номер телефона → пароль → выбор платформы → аккаунт и магазин → админ-панель;
/// существующий — номер телефона → пароль → админ-панель своего магазина.
/// Выбор платформы: веб-сайт (магазин открывается по ссылке) или Telegram Mini App (магазин привязывается к
/// боту администратора и открывается кнопкой «Open Shop» внутри Telegram).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, AdminAuth auth, TelegramBotApi telegram, StoreLinks links) : ControllerBase
{
    public record RegisterRequest(string Name, string Phone, string Password, string? StoreName,
        StorePlatform Platform = StorePlatform.Website, string? BotToken = null);
    public record LoginRequest(string Phone, string Password);
    public record Session(string Token, AdminDto Admin);
    public record AdminDto(int Id, string Name, string Phone, StoreDto Store);
    public record StoreDto(int Id, string Name, string Slug, StorePlatform Platform, string Url, BotDto? Bot);
    public record BotDto(string Username, string Name, string Url, DateTime? LinkedAt, string? Warning, string ButtonUrl);

    [HttpPost("register")]
    public async Task<ActionResult<Session>> Register(RegisterRequest req)
    {
        var name = (req.Name ?? "").Trim();
        var phone = AdminAuth.NormalizePhone(req.Phone ?? "");
        var password = req.Password ?? "";
        var storeName = string.IsNullOrWhiteSpace(req.StoreName) ? $"Магазин {name}".Trim() : req.StoreName.Trim();

        if (name.Length < 2) return Error("Введите имя.", "name");
        if (!AdminAuth.IsValidPhone(phone)) return Error("Введите номер телефона.", "phone");
        if (password.Length < 6) return Error("Пароль должен быть не короче 6 символов.", "password");
        if (await db.Admins.AnyAsync(a => a.Phone == phone))
            return Error("Этот номер уже зарегистрирован — войдите в свой магазин.", "phone");

        // A Telegram shop is only worth creating if the bot really exists: check the token before anything is
        // written, and take the bot's name and username from Telegram rather than asking for them.
        TelegramBotApi.BotInfo? bot = null;
        if (req.Platform == StorePlatform.Telegram)
        {
            if (string.IsNullOrWhiteSpace(req.BotToken))
                return Error("Вставьте токен бота из @BotFather.", "botToken");
            var (found, error) = await telegram.GetMeAsync(req.BotToken);
            if (found is null) return Error(error ?? "Не удалось проверить токен.", "botToken");
            if (await db.Stores.AnyAsync(s => s.BotUsername == found.Username))
                return Error($"Бот @{found.Username} уже подключён к другому магазину.", "botToken");
            bot = found;
        }

        var (admin, token) = await auth.RegisterAsync(name, phone, password, storeName);
        if (bot is not null) await LinkBotAsync(admin.Store, req.BotToken!, bot);
        return new Session(token, Dto(admin));
    }

    [HttpPost("login")]
    public async Task<ActionResult<Session>> Login(LoginRequest req)
    {
        var phone = AdminAuth.NormalizePhone(req.Phone ?? "");
        var admin = await db.Admins.Include(a => a.Store).FirstOrDefaultAsync(a => a.Phone == phone);
        // The same message for an unknown number and a wrong password: it says nothing about who is registered.
        if (admin is null || !AdminAuth.VerifyPassword(req.Password ?? "", admin.PasswordHash))
            return Error("Неверный номер телефона или пароль.", "password");

        return new Session(await auth.OpenSessionAsync(admin), Dto(admin));
    }

    /// <summary>Restores the panel after a page reload: the browser only keeps the token.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<AdminDto>> Me()
    {
        var admin = await auth.CurrentAsync(Request);
        return admin is null ? Unauthorized(new { error = "Войдите в панель управления.", code = "unauthorized" }) : Dto(admin);
    }

    public record DemoAccounts(string StoreSlug, string StoreName, DemoAdmin Admin, DemoCustomer Customer);
    public record DemoAdmin(string Phone, string Password);
    /// <summary>Favourites and "recently viewed" live in the browser, so the demo account brings its own.</summary>
    public record DemoCustomer(string Phone, string Name, List<int> Favorites, List<int> Viewed);

    /// <summary>
    /// The ready-made demo accounts, so the sign-in pages can offer them instead of asking people to remember a
    /// phone number during a presentation. Returns 204 when this installation has no demo store.
    /// </summary>
    [HttpGet("demo")]
    public async Task<ActionResult<DemoAccounts>> Demo()
    {
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == DemoData.StoreSlug);
        if (store is null) return NoContent();
        // Reads another store's catalog than the request's (there is none), so the per-store filter is off.
        var picks = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.StoreId == store.Id && p.IsActive)
            .OrderBy(p => p.SortOrder).Select(p => p.Id).Take(9).ToListAsync();
        return new DemoAccounts(store.Slug, store.Name,
            new DemoAdmin(DemoData.AdminPhone, DemoData.AdminPassword),
            new DemoCustomer(DemoData.CustomerPhone, DemoData.CustomerName,
                picks.Take(4).ToList(), picks.Skip(2).Take(6).Reverse().ToList()));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await auth.SignOutAsync(Request);
        return NoContent();
    }

    /// <summary>Attaches the shop to the bot's menu button ("Open Shop") and remembers the bot on the store.</summary>
    async Task LinkBotAsync(Store store, string token, TelegramBotApi.BotInfo bot)
    {
        store.Platform = StorePlatform.Telegram;
        store.BotToken = token.Trim();
        store.BotUsername = bot.Username;
        store.BotName = bot.Name;
        store.BotWarning = await telegram.SetMenuButtonAsync(store.BotToken, links.MiniAppUrl(store), "Open Shop");
        store.BotLinkedAt = store.BotWarning is null ? DateTime.Now : null;

        // The storefront and the admin panel already show a bot username; keep them in step.
        var settings = await db.Settings.FirstOrDefaultAsync(s => s.StoreId == store.Id);
        if (settings is not null) settings.BotUsername = bot.Username;
        await db.SaveChangesAsync();
    }

    AdminDto Dto(AdminUser a) => new(a.Id, a.Name, a.Phone, StoreOf(a.Store));

    StoreDto StoreOf(Store s) => new(s.Id, s.Name, s.Slug, s.Platform, links.ShopUrl(s),
        s.BotUsername is { Length: > 0 } username
            ? new BotDto(username, s.BotName ?? username, StoreLinks.BotUrl(s)!, s.BotLinkedAt, s.BotWarning, links.MiniAppUrl(s))
            : null);

    ActionResult<Session> Error(string message, string field) => BadRequest(new { error = message, field });
}
