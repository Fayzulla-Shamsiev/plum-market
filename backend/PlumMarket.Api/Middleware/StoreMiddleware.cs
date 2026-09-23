using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Middleware;

/// <summary>
/// Decides which store every API request works with, before any controller runs:
/// <list type="bullet">
/// <item>admin panel (/api/...) — the store of the signed-in administrator (bearer token); no token, no data;</item>
/// <item>storefront (/api/shop/...) — the store the shopper opened, by slug (X-Store header or ?store=);</item>
/// <item>sign-in and the store directory — no store at all.</item>
/// </list>
/// </summary>
public class StoreMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, StoreContext tenant, AdminAuth auth)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/stores", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        if (path.StartsWith("/api/shop", StringComparison.OrdinalIgnoreCase))
        {
            var slug = ctx.Request.Headers["X-Store"].ToString();
            if (string.IsNullOrWhiteSpace(slug)) slug = ctx.Request.Query["store"].ToString();
            Domain.Store? store;
            if (string.IsNullOrWhiteSpace(slug))
            {
                // A deployment with a single store needs no address: it is the store.
                var all = await db.Stores.OrderBy(s => s.Id).Take(2).ToListAsync();
                store = all.Count == 1 ? all[0] : null;
            }
            else store = await db.Stores.FirstOrDefaultAsync(s => s.Slug == slug);
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

    static Task Fail(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = message, code });
    }
}
