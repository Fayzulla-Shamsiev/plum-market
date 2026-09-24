namespace PlumMarket.Api.Domain;

/// <summary>
/// Everything a merchant owns belongs to exactly one <see cref="Store"/>. The data separation required by the
/// spec ("каждый администратор видит и управляет только своим магазином") is enforced in one place: a global
/// query filter on every entity that implements this interface (see AppDbContext).
/// </summary>
public interface IStoreOwned
{
    int StoreId { get; set; }
}

/// <summary>Where the shop's customers open it (chosen at registration, spec «Выбор платформы»).</summary>
public enum StorePlatform
{
    /// <summary>A normal storefront opened by its address.</summary>
    Website,
    /// <summary>The same storefront, opened inside the merchant's own Telegram bot as a Mini App.</summary>
    Telegram,
}

/// <summary>A shop created at registration: one administrator, one storefront.</summary>
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Storefront address of this shop — /shop/{slug} in the prototype, a subdomain in production.</summary>
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public StorePlatform Platform { get; set; }

    // --- Telegram Mini App: the merchant's own bot, created by them in @BotFather ---
    /// <summary>The bot token. It controls the whole bot, so it never leaves the server.</summary>
    public string? BotToken { get; set; }
    /// <summary>Read from Telegram when the token is checked, not typed by the administrator.</summary>
    public string? BotUsername { get; set; }
    public string? BotName { get; set; }
    /// <summary>When the shop was last attached to the bot's menu button ("Open Shop").</summary>
    public DateTime? BotLinkedAt { get; set; }
    /// <summary>Why the menu button isn't attached, when it isn't — e.g. the shop has no https address yet.</summary>
    public string? BotWarning { get; set; }
}

/// <summary>The entrepreneur who registered the store and signs in to its admin panel.</summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Normalised to digits with a leading "+" so "+998 90 123 45 67" and "998901234567" are one account.</summary>
    public string Phone { get; set; } = "";
    /// <summary>PBKDF2 hash, salt and iteration count in one string (see AdminAuth).</summary>
    public string PasswordHash { get; set; } = "";
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}

/// <summary>Admin-panel session. Only a hash of the bearer token is stored.</summary>
public class AdminSession
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = "";
    public int AdminUserId { get; set; }
    public AdminUser Admin { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
