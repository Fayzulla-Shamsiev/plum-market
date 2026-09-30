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

/// <summary>A shop created at registration: one administrator, one storefront.</summary>
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Storefront address of this shop — /shop/{slug} in the prototype, a subdomain in production.</summary>
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    // --- Telegram Mini App: the merchant's own bot, connected in Платформы → Telegram-бот ---
    // The same shop is always open on the web; a bot is an extra way in, not a different store.
    /// <summary>The bot token. It controls the whole bot, so it never leaves the server.</summary>
    public string? BotToken { get; set; }
    /// <summary>Read from Telegram when the token is checked, not typed by the administrator.</summary>
    public string? BotUsername { get; set; }
    public string? BotName { get; set; }
    /// <summary>When the shop was last attached to the bot's menu button ("Open Shop").</summary>
    public DateTime? BotLinkedAt { get; set; }

    /// <summary>
    /// What an empty chat with the bot says, before the customer has pressed «Начать» (Telegram calls it the
    /// bot description). Null means the wording the store gets by default.
    /// </summary>
    public string? BotAbout { get; set; }
    /// <summary>The bot's answer to /start. Null means the default greeting. Placeholders: {name}, {store}.</summary>
    public string? BotGreeting { get; set; }
    /// <summary>Why the bot isn't fully set up, when it isn't (Telegram refused something).</summary>
    public string? BotWarning { get; set; }
    /// <summary>
    /// Shared secret Telegram sends back with every update, so a webhook call can be trusted. Also tells us
    /// whether the bot is answering /start at all: no secret, no webhook (a local run has no public address).
    /// </summary>
    public string? BotWebhookSecret { get; set; }

    // --- First run: the AI assistant that sets the shop up together with the administrator ---
    /// <summary>
    /// When the administrator finished (or skipped) the setup conversation. Until then signing in opens the
    /// assistant rather than the panel.
    /// </summary>
    public DateTime? OnboardedAt { get; set; }
    /// <summary>
    /// Setup steps the administrator explicitly accepted as they are (e.g. the default delivery price), comma
    /// separated. Steps that can be read off the data (a catalog, a branch address) are never stored here.
    /// </summary>
    public string SetupAccepted { get; set; } = "";
}

/// <summary>
/// One message of the setup assistant's conversation with the administrator. Kept in OpenAI's shape (role,
/// tool calls, tool results) so the conversation can be continued after a reload; only user and assistant
/// messages with text are shown in the panel.
/// </summary>
public class AssistantMessage : IStoreOwned
{
    public int StoreId { get; set; }
    public int Id { get; set; }
    /// <summary>user | assistant | tool</summary>
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    /// <summary>Quick replies the assistant offers under its message (JSON array of strings).</summary>
    public string? Suggestions { get; set; }
    /// <summary>The assistant's tool calls, exactly as OpenAI returned them (JSON array).</summary>
    public string? ToolCalls { get; set; }
    /// <summary>For a tool result: which call it answers.</summary>
    public string? ToolCallId { get; set; }
    /// <summary>What a tool did, for the card the panel shows under the answer (JSON object).</summary>
    public string? Card { get; set; }
    /// <summary>Uploaded images the administrator attached (JSON array of /uploads URLs).</summary>
    public string? Attachments { get; set; }
    /// <summary>
    /// For a long text the administrator pasted: the product cards distilled from it (JSON). The assistant works from
    /// this instead of the raw text — shorter, and structured the same way every time.
    /// </summary>
    public string? Digest { get; set; }
    public DateTime CreatedAt { get; set; }
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
    /// <summary>
    /// Panel pages whose short guided tour this administrator has already seen (comma-separated keys such as
    /// "dashboard,orders"; "*" = all). A page's tour runs by itself only on the first visit; «?» replays it.
    /// </summary>
    public string ToursSeen { get; set; } = "";
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
