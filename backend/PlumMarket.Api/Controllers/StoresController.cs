using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// The storefronts running on this installation. In production every store has its own address, so a shopper
/// never sees this list; in the prototype it is how you pick which shop to open (/shop/{slug}).
/// </summary>
[ApiController]
[Route("api/stores")]
public class StoresController(AppDbContext db) : ControllerBase
{
    public record StoreCard(string Slug, string Name, int Products, string? About);

    [HttpGet]
    public async Task<List<StoreCard>> List()
    {
        var stores = await db.Stores.OrderBy(s => s.Id).ToListAsync();
        var ids = stores.Select(s => s.Id).ToList();
        // These two read across stores, so the per-store filter has to be off.
        var products = await db.Products.IgnoreQueryFilters()
            .Where(p => ids.Contains(p.StoreId) && p.IsActive)
            .GroupBy(p => p.StoreId).Select(g => new { StoreId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.StoreId, x => x.Count);
        var about = await db.Settings.IgnoreQueryFilters()
            .Where(s => ids.Contains(s.StoreId)).ToDictionaryAsync(s => s.StoreId, s => s.AboutText);

        return stores.Select(s => new StoreCard(s.Slug, s.Name, products.GetValueOrDefault(s.Id),
            about.GetValueOrDefault(s.Id) is { Length: > 0 } text ? Shorten(text) : null)).ToList();
    }

    static string Shorten(string text)
    {
        var line = text.Split('\n')[0].Trim();
        return line.Length > 140 ? line[..140].TrimEnd() + "…" : line;
    }
}
