using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Middleware;

/// <summary>
/// Decides which store every API request works with, before any controller runs:
/// <list type="bullet">
/// <item>admin panel (/api/...) — the store of the signed-in administrator (bearer token); no token, no data;</item>
/// <item>storefront (/api/shop/...) — the store whose address the shopper opened: its subdomain
/// (shop.plum.uz, how this works once stores get their own address) or, on a shared host, the slug the browser
/// carries in X-Store / ?store= after opening /shop/{slug};</item>
/// <item>sign-in — no store at all.</item>
/// </list>
/// There is no way to ask for "all stores": an administrator only ever reaches their own, and a shopper only
/// reaches the one whose address they opened.
/// </summary>
public class StoreMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, StoreContext tenant, AdminAuth auth)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            // Telegram calls this one, not a person: it finds its own store by the bot behind the update.
            || path.StartsWith("/api/telegram", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        if (path.StartsWith("/api/shop", StringComparison.OrdinalIgnoreCase))
        {
            var slug = ctx.Request.Headers["X-Store"].ToString();
            if (string.IsNullOrWhiteSpace(slug)) slug = ctx.Request.Query["store"].ToString();
            if (string.IsNullOrWhiteSpace(slug)) slug = Subdomain(ctx.Request.Host.Host);

            var store = string.IsNullOrWhiteSpace(slug) ? null : await db.Stores.FirstOrDefaultAsync(s => s.Slug == slug);
            if (store is null && string.IsNullOrWhiteSpace(slug))
            {
                // Prototype only: every store shares one host here, so an address that names no store falls back
                // to the single store of this installation (or the demo one). With real subdomains this is dead code.
                var all = await db.Stores.OrderBy(s => s.Id).Take(2).ToListAsync();
                store = all.Count == 1 ? all[0] : await db.Stores.FirstOrDefaultAsync(s => s.Slug == DemoData.StoreSlug);
            }
            if (store is null)
            {
                await Fail(ctx, StatusCodes.Status404NotFound, "store_not_found", "Магазин не найден.");
                return;
            }
            tenant.StoreId = store.Id;
            tenant.Store = store;
            await next(ctx);
            return;
        }

        var admin = await auth.CurrentAsync(ctx.Request);
        if (admin is null)
        {
            await Fail(ctx, StatusCodes.Status401Unauthorized, "unauthorized", "Войдите в панель управления.");
            return;
        }
        tenant.Admin = admin;
        tenant.StoreId = admin.StoreId;
        tenant.Store = admin.Store;
        await next(ctx);
    }

    /// <summary>"bakery.plum.uz" → "bakery"; nothing for a bare domain, an IP or localhost.</summary>
    static string? Subdomain(string host)
    {
        if (host.Length == 0 || System.Net.IPAddress.TryParse(host, out _)) return null;
        var labels = host.Split('.');
        // A store's address is <slug>.<domain>.<tld>; "plum.uz" or "localhost" name no store.
        return labels.Length >= 3 && labels[0] is not ("www" or "app") ? labels[0] : null;
    }

    static Task Fail(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = message, code });
    }
}
