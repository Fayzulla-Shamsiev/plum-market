namespace PlumMarket.Api.Services;

using PlumMarket.Api.Domain;

/// <summary>
/// Where a shop can be opened from the outside. The administrator is shown the address of the installation they
/// are working with; Telegram needs an absolute https address, which a local run doesn't have, so the Mini App
/// button falls back to the published prototype.
/// </summary>
public class StoreLinks(IConfiguration configuration, IHttpContextAccessor accessor)
{
    /// <summary>The published prototype, used when this installation isn't reachable from Telegram.</summary>
    public string PublishedUrl =>
        (configuration["Telegram:MiniAppUrl"] ?? "https://plum-market.onrender.com").TrimEnd('/');

    /// <summary>
    /// The address this installation answers on. Behind a proxy the request's own host is the right answer;
    /// set PublicUrl (e.g. https://plum-market.onrender.com) when it isn't.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            if (configuration["PublicUrl"] is { Length: > 0 } configured) return configured.TrimEnd('/');
            var request = accessor.HttpContext?.Request;
            return request is null ? "" : $"{request.Scheme}://{request.Host}";
        }
    }

    /// <summary>The shop's own address: /shop/{slug} here, the store's subdomain once stores get one.</summary>
    public string ShopUrl(Store store) => $"{BaseUrl}/shop/{store.Slug}";

    /// <summary>
    /// What the bot's "Open Shop" button opens: always this shop, never another one. Telegram only opens https,
    /// so a shop running on a local machine points at the same shop on the published address — connect the bot
    /// from the deployed site and the button opens the store the customer is meant to see.
    /// </summary>
    public string MiniAppUrl(Store store)
    {
        var own = ShopUrl(store);
        return own.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? own : $"{PublishedUrl}/shop/{store.Slug}";
    }

    /// <summary>Where Telegram should post this bot's updates, or null while the shop has no public address.</summary>
    public string? WebhookUrl(Store store) =>
        BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? $"{BaseUrl}/api/telegram/{store.Id}" : null;

    /// <summary>True when the button had to fall back to the published prototype rather than this shop.</summary>
    public bool IsFallback(Store store) => MiniAppUrl(store) != ShopUrl(store);

    public static string? BotUrl(Store store) => store.BotUsername is { Length: > 0 } u ? $"https://t.me/{u}" : null;
}
