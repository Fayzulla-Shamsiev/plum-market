using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Public, read-only API of the customer storefront: home page, catalog by category and search.
/// Every endpoint takes <c>lang</c> (ru | uz | oz) and returns names already resolved to that language.
/// </summary>
[ApiController]
[Route("api/shop")]
public class ShopController(AppDbContext db) : ControllerBase
{
    const int SectionSize = 10;

    [HttpGet("home")]
    public async Task<IActionResult> Home(string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var banners = await db.Banners.AsNoTracking().Where(b => b.IsActive && b.Type == BannerType.Main)
            .OrderBy(b => b.SortOrder).ToListAsync();
        var tree = cat.Tree();

        var deals = cat.Sort(cat.Products, "popular").Select(cat.ToCard).Where(c => c.DiscountPercent is not null).Take(SectionSize).ToList();
        var popular = cat.Sort(cat.Products, "popular").Take(SectionSize).Select(cat.ToCard).ToList();
        var sections = tree.Select(node =>
        {
            var ids = cat.Subtree(node.Id);
            var items = cat.Sort(cat.Products.Where(p => p.CategoryId is { } c && ids.Contains(c)), "popular").ToList();
            return new { category = node, total = items.Count, products = items.Take(SectionSize).Select(cat.ToCard) };
        });

        return Ok(new
        {
            store = new { name = settings.StoreName },
            banners = banners.Select(b => new
            {
                b.Id, b.Title, b.DesktopUrl, b.MobileUrl, b.DesktopMediaType, b.MobileMediaType,
                link = b.LinkType switch
                {
                    BannerLink.Category when b.LinkTargetId is { } id => $"/catalog/{id}",
                    BannerLink.Product when b.LinkTargetId is { } id => $"/product/{id}",
                    BannerLink.Url => b.LinkUrl,
                    _ => null,
                },
            }),
            categories = tree,
            deals,
            popular,
            sections,
        });
    }

    /// <summary>What the storefront shell needs on every page: store name, branches (footer) and the category tree (menu).</summary>
    [HttpGet("meta")]
    public async Task<IActionResult> Meta(string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => new { b.Id, b.Name, b.Address }).ToListAsync();
        return Ok(new { store = new { name = settings.StoreName }, branches, categories = cat.Tree() });
    }

    /// <summary>Products of a category (including its sub-categories). Without a category: the whole catalog.</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(int? categoryId, string? sort, bool inStockOnly = false, int page = 1, int pageSize = 24, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        pageSize = Math.Clamp(pageSize, 1, 96);
        IEnumerable<Product> items = cat.Products;
        object? category = null;

        if (categoryId is { } id)
        {
            var c = cat.Categories.FirstOrDefault(x => x.Id == id);
            if (c is null) return NotFound(new { error = "Категория не найдена" });
            var ids = cat.Subtree(id);
            items = items.Where(p => p.CategoryId is { } pc && ids.Contains(pc));

            var trail = new List<object>();
            for (var cur = c; cur is not null; cur = cat.Categories.FirstOrDefault(x => x.Id == cur.ParentId))
                trail.Insert(0, new { cur.Id, name = cat.Name(cur.Name) });
            var node = FindNode(cat.Tree(), id);
            // The merchant's default product order for this category applies until the shopper picks another.
            sort ??= c.ProductSort == "manual" ? null : c.ProductSort;
            category = new
            {
                c.Id, c.ParentId, name = cat.Name(c.Name), description = cat.Name(c.Description), c.ImageUrl, c.BannerUrl, c.Layout,
                defaultSort = c.ProductSort,
                trail,
                // Siblings let the shopper hop between sub-categories without going back up.
                children = node?.Children ?? [],
                siblings = c.ParentId is { } pid ? FindNode(cat.Tree(), pid)?.Children ?? [] : [],
            };
        }

        if (inStockOnly) items = items.Where(StorefrontCatalog.InStock);
        var sorted = cat.Sort(items, sort).ToList();
        return Ok(new
        {
            category,
            sort = sort ?? "manual",
            total = sorted.Count,
            page,
            pageSize,
            items = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(cat.ToCard),
        });
    }

    /// <summary>Full search results page.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, int? categoryId, string? sort, int page = 1, int pageSize = 24, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var (cats, products) = cat.Search(q ?? "");
        // Facet: how many hits fall under each top-level category, so the shopper can narrow the results.
        var roots = cat.Tree();
        var facets = roots.Select(r => new { r.Id, r.Name, count = products.Count(p => p.CategoryId is { } c && cat.Subtree(r.Id).Contains(c)) })
            .Where(f => f.count > 0).ToList();
        IEnumerable<Product> filtered = products;
        if (categoryId is { } id) filtered = filtered.Where(p => p.CategoryId is { } c && cat.Subtree(id).Contains(c));
        // "relevance" keeps the search ranking; any other sort re-orders the hits.
        var list = (sort is null or "relevance" ? filtered : cat.Sort(filtered, sort)).ToList();
        pageSize = Math.Clamp(pageSize, 1, 96);
        return Ok(new
        {
            query = q ?? "",
            categories = cats.Take(8).Select(c => new { c.Id, name = cat.Name(c.Name), c.ImageUrl, path = Path(cat, c) }),
            facets,
            total = list.Count,
            page,
            pageSize,
            items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(cat.ToCard),
        });
    }

    /// <summary>Type-ahead for the header search box.</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest(string? q, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var (cats, products) = cat.Search(q ?? "");
        return Ok(new
        {
            categories = cats.Take(4).Select(c => new { c.Id, name = cat.Name(c.Name), path = Path(cat, c) }),
            products = products.Take(6).Select(cat.ToCard),
            total = products.Count,
        });
    }

    /// <summary>Product page: everything except the review list, which is paged separately.</summary>
    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var p = cat.Products.FirstOrDefault(x => x.Id == id);
        if (p is null) return NotFound(new { error = "Товар не найден" });
        var c = p.CategoryId is { } cid ? cat.Categories.FirstOrDefault(x => x.Id == cid) : null;
        var trail = new List<object>();
        for (var cur = c; cur is not null; cur = cat.Categories.FirstOrDefault(x => x.Id == cur.ParentId))
            trail.Insert(0, new { cur.Id, name = cat.Name(cur.Name) });

        var stock = StorefrontCatalog.AvailabilityOf(p);
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        var description = cat.Name(p.Description);
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var allRatings = await db.Reviews.AsNoTracking().Select(r => r.Rating).ToListAsync();
        var since = await db.Orders.AsNoTracking().MinAsync(o => (DateTime?)o.CreatedAt);

        return Ok(new
        {
            card = cat.ToCard(p),
            trail,
            media = p.Media,
            description,
            shortDescription = ShortText(description),
            variants = p.Variants.Select(v =>
            {
                var (price, old, percent) = cat.PriceOf(p, v.Name);
                return new { v.Name, price, oldPrice = old, discountPercent = percent };
            }),
            attributes = p.Attributes,
            p.Unit, p.WeightGrams, p.LengthCm, p.WidthCm, p.HeightCm, p.Tags,
            stock = new
            {
                stock.Unlimited,
                stock.Quantity,
                // Per-branch availability, so a shopper planning a pickup knows where to go.
                branches = branches.Select(b =>
                {
                    var row = stock.Branches.FirstOrDefault(x => x.BranchId == b.Id);
                    var status = p.Stock.Count == 0 ? "Unlimited" : row == default ? "None" : row.Status.ToString();
                    return new { b.Id, b.Name, b.Address, status, quantity = row.Quantity };
                }),
            },
            seller = new
            {
                name = settings.StoreName,
                rating = allRatings.Count == 0 ? 0 : Math.Round(allRatings.Average(), 1),
                reviewsCount = allRatings.Count,
                productsCount = cat.Products.Count,
                branchesCount = branches.Count,
                since,
            },
        });
    }

    /// <summary>Reviews of a product with the rating breakdown. sort: new | high | low; rating filters to one star value.</summary>
    [HttpGet("products/{id:int}/reviews")]
    public async Task<IActionResult> Reviews(int id, string? sort, int? rating, int page = 1, int pageSize = 5)
    {
        var all = await db.Reviews.AsNoTracking().Include(r => r.Customer).Where(r => r.ProductId == id).ToListAsync();
        var breakdown = Enumerable.Range(1, 5).Reverse().Select(star => new { star, count = all.Count(r => r.Rating == star) });
        IEnumerable<Review> list = rating is { } star ? all.Where(r => r.Rating == star) : all;
        list = sort switch
        {
            "high" => list.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "low" => list.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => list.OrderByDescending(r => r.CreatedAt),
        };
        var filtered = list.ToList();
        pageSize = Math.Clamp(pageSize, 1, 50);
        return Ok(new
        {
            average = all.Count == 0 ? 0 : Math.Round(all.Average(r => r.Rating), 1),
            count = all.Count,
            breakdown,
            total = filtered.Count,
            page,
            items = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(r => new
            {
                r.Id, r.Rating, r.Comment, r.CreatedAt, r.Reply, r.RepliedAt,
                author = PublicName(r.Customer.FullName),
            }),
        });
    }

    /// <summary>
    /// "Рекомендуем": products bought together with this one first, then the same sub-category, then the same
    /// top-level category, then store bestsellers. Out-of-stock items are skipped.
    /// </summary>
    [HttpGet("products/{id:int}/recommended")]
    public async Task<IActionResult> Recommended(int id, int take = 12, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var p = cat.Products.FirstOrDefault(x => x.Id == id);
        if (p is null) return NotFound();

        var orderIds = db.OrderItems.Where(i => i.ProductId == id).Select(i => i.OrderId);
        var together = await db.OrderItems.AsNoTracking().Where(i => orderIds.Contains(i.OrderId) && i.ProductId != id)
            .GroupBy(i => i.ProductId).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(20).Select(x => x.Key).ToListAsync();

        var root = p.CategoryId is { } cid ? cat.Tree().FirstOrDefault(r => cat.Subtree(r.Id).Contains(cid))?.Id : null;
        var sameRoot = root is { } r ? cat.Subtree(r) : [];
        var byId = cat.Products.ToDictionary(x => x.Id);
        var picked = together.Take(4).Where(byId.ContainsKey).Select(x => byId[x])
            .Concat(cat.Sort(cat.Products.Where(x => x.CategoryId == p.CategoryId), "popular"))
            .Concat(cat.Sort(cat.Products.Where(x => x.CategoryId is { } c && sameRoot.Contains(c)), "popular"))
            .Concat(together.Skip(4).Where(byId.ContainsKey).Select(x => byId[x]))
            .Concat(cat.Sort(cat.Products, "popular"))
            .Where(x => x.Id != id && StorefrontCatalog.InStock(x))
            .DistinctBy(x => x.Id).Take(Math.Clamp(take, 1, 24));
        return Ok(picked.Select(cat.ToCard));
    }

    /// <summary>Cards for a list of ids in the given order (recently viewed, favourites). Unknown/hidden ids are skipped.</summary>
    [HttpGet("products")]
    public async Task<IActionResult> Products(string? ids, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var byId = cat.Products.ToDictionary(x => x.Id);
        var list = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var v) ? v : 0).Where(byId.ContainsKey).Distinct().Take(60);
        return Ok(list.Select(x => cat.ToCard(byId[x])));
    }

    public record CartLine(int ProductId, string? Variant, int Qty);
    public record CartBody(List<CartLine> Lines);

    /// <summary>
    /// Prices a cart kept in the browser with today's prices, discounts and stock, so what the shopper sees
    /// always matches the admin. Quantities above what's available are capped; hidden products come back as unavailable.
    /// </summary>
    [HttpPost("cart/quote")]
    public async Task<IActionResult> Quote(CartBody body, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var byId = cat.Products.ToDictionary(x => x.Id);
        var lines = body.Lines.Take(100).Select(l =>
        {
            if (!byId.TryGetValue(l.ProductId, out var p))
                return new { l.ProductId, variant = l.Variant, card = (StorefrontCatalog.Card?)null, available = false, qty = 0, maxQty = (int?)0, price = 0L, oldPrice = (long?)null, sum = 0L, oldSum = 0L };
            var variant = p.Variants.Any(v => v.Name == l.Variant) ? l.Variant : p.Variants.FirstOrDefault()?.Name;
            var (price, old, _) = cat.PriceOf(p, variant);
            var stock = StorefrontCatalog.AvailabilityOf(p);
            int? max = stock.Unlimited ? null : stock.Quantity;
            var qty = Math.Max(0, max is { } m ? Math.Min(l.Qty, m) : Math.Min(l.Qty, 999));
            return new { l.ProductId, variant, card = (StorefrontCatalog.Card?)cat.ToCard(p), available = qty > 0, qty, maxQty = max, price, oldPrice = old, sum = price * qty, oldSum = (old ?? price) * qty };
        }).ToList();
        var live = lines.Where(l => l.available).ToList();
        return Ok(new
        {
            lines,
            count = live.Sum(l => l.qty),
            subtotal = live.Sum(l => l.oldSum),
            discount = live.Sum(l => l.oldSum - l.sum),
            total = live.Sum(l => l.sum),
        });
    }

    /// <summary>First sentence (or ~160 characters) of the description for the top of the product page.</summary>
    static string ShortText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cut = text.IndexOfAny(['.', '!', '?', '\n']);
        var first = cut > 0 ? text[..(cut + 1)] : text;
        return first.Length <= 180 ? first.Trim() : text[..160].TrimEnd() + "…";
    }

    /// <summary>"Азиз Каримов" → "Азиз К." — reviewers' surnames aren't published.</summary>
    static string PublicName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch { 0 => "Покупатель", 1 => parts[0], _ => $"{parts[0]} {parts[1][0]}." };
    }

    static StorefrontCatalog.CategoryNode? FindNode(IEnumerable<StorefrontCatalog.CategoryNode> nodes, int id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            if (FindNode(n.Children, id) is { } found) return found;
        }
        return null;
    }

    /// <summary>"Выпечка › Круассаны" — shown under category hits so same-named sub-categories are distinguishable.</summary>
    static string Path(StorefrontCatalog cat, Category c)
    {
        var parts = new List<string>();
        for (var cur = c; cur is not null; cur = cat.Categories.FirstOrDefault(x => x.Id == cur.ParentId))
            parts.Insert(0, cat.Name(cur.Name));
        return string.Join(" › ", parts);
    }
}
