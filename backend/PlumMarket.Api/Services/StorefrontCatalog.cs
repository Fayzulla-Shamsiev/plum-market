using System.Text;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Read model for the customer storefront. It loads the active catalog once per request and derives what the
/// shopper sees: the price after the discount that is running now, rating, popularity and availability. Anything
/// the merchant changes in the admin panel (price, discount, category, active flag) shows up on the next request.
/// </summary>
public class StorefrontCatalog
{
    /// <param name="MaxQty">How many can be bought right now; null = no limit.</param>
    /// <param name="Variants">Variant names in the merchant's order; the first one is the default. Empty = nothing to choose.</param>
    public record Card(int Id, int? CategoryId, string Name, string? Image, long Price, long? OldPrice, int? DiscountPercent,
        double Rating, int ReviewsCount, string Unit, int? WeightGrams, List<string> Tags, bool InStock, int? MaxQty,
        List<string> Variants);

    public record Availability(bool Unlimited, int Quantity, List<(int BranchId, StockStatus Status, int Quantity)> Branches);

    public record CategoryNode(int Id, int? ParentId, string Name, string? Image, int ProductsCount, List<CategoryNode> Children);

    public List<Category> Categories { get; }
    public List<Product> Products { get; }
    public string Lang { get; }

    readonly Dictionary<int, (double Avg, int Count)> ratings;
    readonly Dictionary<int, int> sold;
    readonly List<Discount> discounts;

    StorefrontCatalog(List<Category> categories, List<Product> products, Dictionary<int, (double, int)> ratings,
        Dictionary<int, int> sold, List<Discount> discounts, string lang)
    {
        Categories = categories;
        Products = products;
        this.ratings = ratings;
        this.sold = sold;
        this.discounts = discounts;
        Lang = Localized.Languages.Contains(lang) ? lang : "ru";
    }

    public static async Task<StorefrontCatalog> LoadAsync(AppDbContext db, string lang)
    {
        var now = DateTime.Now;
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        // A product is only visible when its whole category chain is active.
        var visibleCats = VisibleCategoryIds(categories);
        var products = (await db.Products.AsNoTracking().Include(p => p.Stock).Where(p => p.IsActive).ToListAsync())
            .Where(p => p.CategoryId is null || visibleCats.Contains(p.CategoryId.Value))
            .ToList();

        var ratings = await db.Reviews.AsNoTracking().GroupBy(r => r.ProductId)
            .Select(g => new { g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => (x.Avg, x.Count));

        // "Popular" = units sold in completed orders over the last 90 days.
        var since = now.AddDays(-90);
        var sold = await db.OrderItems.AsNoTracking()
            .Join(db.Orders.Where(o => o.CreatedAt >= since && o.Status == OrderStatus.Completed), i => i.OrderId, o => o.Id, (i, _) => i)
            .GroupBy(i => i.ProductId).Select(g => new { g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty);

        // Discounts running now. Catalog pages apply only the store-wide ones; see PriceOf.
        var discounts = await db.Discounts.AsNoTracking().Where(d => d.IsActive && d.StartsAt <= now && d.EndsAt >= now).ToListAsync();

        return new StorefrontCatalog(categories, products, ratings, sold, discounts, lang);
    }

    static HashSet<int> VisibleCategoryIds(List<Category> active)
    {
        var byId = active.ToDictionary(c => c.Id);
        var visible = new HashSet<int>();
        foreach (var c in active)
        {
            var cur = c;
            while (cur.ParentId is { } pid && byId.TryGetValue(pid, out var parent)) cur = parent;
            if (cur.ParentId is null) visible.Add(c.Id); // reached a root without hitting an inactive parent
        }
        return visible;
    }

    public string Name(Localized l) => l.Get(Lang);

    public Card ToCard(Product p)
    {
        var (price, oldPrice, percent) = PriceOf(p);
        var (avg, count) = ratings.GetValueOrDefault(p.Id);
        var stock = AvailabilityOf(p);
        return new Card(p.Id, p.CategoryId, Name(p.Name), p.Media.FirstOrDefault(m => m.Type == "image")?.Url,
            price, oldPrice, percent, Math.Round(avg, 1), count, p.Unit, p.WeightGrams, p.Tags, InStock(p),
            stock.Unlimited ? null : stock.Quantity, p.Variants.Select(v => v.Name).ToList());
    }

    /// <summary>
    /// What a shopper can buy before choosing a branch: unlimited if any branch sells it without a limit,
    /// otherwise the sum of the limited quantities. No stock rows at all = the merchant doesn't track stock.
    /// </summary>
    public static Availability AvailabilityOf(Product p)
    {
        var branches = p.Stock.Select(s => (s.BranchId, s.Status, s.Status == StockStatus.Limited ? s.Quantity : 0)).ToList();
        var unlimited = p.Stock.Count == 0 || p.Stock.Any(s => s.Status == StockStatus.Unlimited);
        return new Availability(unlimited, branches.Sum(b => b.Item3), branches);
    }

    /// <summary>
    /// Price of a product (or one of its variants) after the best discount that applies. Without a branch or order
    /// amount only store-wide discounts count, which is what catalog pages show. At checkout the branch and the cart
    /// total are known, so branch-only discounts and "from N сум" discounts can apply as well.
    /// The struck-out price is the merchant's old price, or the price before the discount.
    /// </summary>
    public (long Price, long? OldPrice, int? Percent) PriceOf(Product p, string? variant = null, int? branchId = null, long? orderAmount = null)
    {
        var v = variant is null ? null : p.Variants.FirstOrDefault(x => x.Name == variant);
        var basePrice = v?.Price ?? p.Price;
        var price = basePrice;
        foreach (var d in discounts.Where(d => d.ProductIds.Contains(p.Id)
                     && (d.BranchIds.Count == 0 || branchId is { } b && d.BranchIds.Contains(b))
                     && (d.MinOrderAmount is null || orderAmount is { } a && a >= d.MinOrderAmount)))
        {
            var discounted = d.Type == DiscountType.Percent ? basePrice - basePrice * d.Value / 100 : basePrice - d.Value;
            if (discounted > 0 && discounted < price) price = discounted / 100 * 100;
        }
        long? merchantOld = basePrice == p.Price ? p.OldPrice : null;
        long? old = merchantOld is { } o && o > price ? o : price < basePrice ? basePrice : null;
        int? percent = old is { } x ? (int)Math.Round(100.0 * (x - price) / x) : null;
        return (price, old, percent is > 0 ? percent : null);
    }

    /// <summary>Whether a branch can hand over <paramref name="qty"/> of the product right now.</summary>
    public static bool CanFulfill(Product p, int branchId, int qty)
    {
        if (p.Stock.Count == 0) return true; // stock not tracked: sold everywhere
        var row = p.Stock.FirstOrDefault(s => s.BranchId == branchId);
        return row is not null && (row.Status == StockStatus.Unlimited || row.Status == StockStatus.Limited && row.Quantity >= qty);
    }

    public static bool InStock(Product p) => AvailabilityOf(p) is var a && (a.Unlimited || a.Quantity > 0);

    public (double Avg, int Count) RatingOf(int productId) => ratings.GetValueOrDefault(productId);

    public int SoldOf(int productId) => sold.GetValueOrDefault(productId);

    /// <summary>Category and all its descendants.</summary>
    public HashSet<int> Subtree(int id)
    {
        var set = new HashSet<int> { id };
        var frontier = new Queue<int>([id]);
        while (frontier.TryDequeue(out var cur))
            foreach (var child in Categories.Where(c => c.ParentId == cur))
                if (set.Add(child.Id)) frontier.Enqueue(child.Id);
        return set;
    }

    public List<CategoryNode> Tree()
    {
        var counts = Products.Where(p => p.CategoryId != null).GroupBy(p => p.CategoryId!.Value).ToDictionary(g => g.Key, g => g.Count());
        CategoryNode Build(Category c)
        {
            var children = Categories.Where(x => x.ParentId == c.Id).Select(Build).Where(n => n.ProductsCount > 0).ToList();
            return new CategoryNode(c.Id, c.ParentId, Name(c.Name), c.ImageUrl,
                counts.GetValueOrDefault(c.Id) + children.Sum(x => x.ProductsCount), children);
        }
        // Empty categories are hidden from shoppers.
        return Categories.Where(c => c.ParentId is null).Select(Build).Where(n => n.ProductsCount > 0).ToList();
    }

    /// <summary>Stock-outs go last, then the requested order.</summary>
    public IEnumerable<Product> Sort(IEnumerable<Product> items, string? sort) =>
        (sort switch
        {
            "price_asc" => items.OrderBy(p => !InStock(p)).ThenBy(p => PriceOf(p).Price),
            "price_desc" => items.OrderBy(p => !InStock(p)).ThenByDescending(p => PriceOf(p).Price),
            "new" => items.OrderBy(p => !InStock(p)).ThenByDescending(p => p.CreatedAt),
            "rating" => items.OrderBy(p => !InStock(p)).ThenByDescending(p => RatingOf(p.Id).Avg).ThenByDescending(p => RatingOf(p.Id).Count),
            "popular" => items.OrderBy(p => !InStock(p)).ThenByDescending(p => SoldOf(p.Id)),
            _ => items.OrderBy(p => !InStock(p)).ThenBy(p => p.SortOrder).ThenBy(p => p.Id),
        });

    // ---------------- Search ----------------

    /// <summary>
    /// Products and categories matching the query in any catalog language. Uzbek Latin and Cyrillic are compared
    /// in both scripts, so "somsa" finds "Сомса" and the other way round. Ranked: name starts with the query,
    /// then a word in the name starts with it, then the name contains it, then the category, tags or description match.
    /// </summary>
    public (List<Category> Categories, List<Product> Products) Search(string query)
    {
        var terms = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return ([], []);
        // Every term must match somewhere (AND), e.g. "торт мед" → "Торт Медовик".
        var cats = Categories
            .Select(c => (c, Score: Score(terms, Texts(c.Name))))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).Select(x => x.c).ToList();
        var catById = Categories.ToDictionary(c => c.Id);

        var products = Products.Select(p =>
            {
                var name = Score(terms, Texts(p.Name));
                var category = p.CategoryId is { } cid && catById.TryGetValue(cid, out var c) ? Score(terms, Texts(c.Name)) : 0;
                var extra = Score(terms, [.. Texts(p.Description), .. p.Tags.Select(Normalize)]);
                // Mixed queries ("пицца пепперони") can split across name and category, so fall back to all fields together.
                var combined = name == 0 && category == 0 && extra == 0 ? 0
                    : Score(terms, [.. Texts(p.Name), .. (p.CategoryId is { } id && catById.TryGetValue(id, out var cc) ? Texts(cc.Name) : []), .. p.Tags.Select(Normalize)]);
                var score = name > 0 ? 100 + name : category > 0 ? 50 + category : combined > 0 ? 40 + combined : extra > 0 ? 10 + extra : 0;
                return (p, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderBy(x => !InStock(x.p)).ThenByDescending(x => x.Score).ThenByDescending(x => SoldOf(x.p.Id))
            .Select(x => x.p).ToList();
        return (cats, products);
    }

    static IEnumerable<string> Texts(Localized l)
    {
        foreach (var v in l.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var n = Normalize(v);
            yield return n;
            // Cover the other Uzbek script for every value (Russian is left as-is by the transliterator's rules).
            yield return Normalize(UzTransliterator.ToCyrillic(v));
            yield return Normalize(UzTransliterator.ToLatin(v));
        }
    }

    /// <summary>0 = at least one term matched nothing; otherwise higher is better.</summary>
    static int Score(string[] terms, IEnumerable<string> texts)
    {
        var list = texts.Distinct().ToList();
        var total = 0;
        foreach (var t in terms)
        {
            var best = 0;
            foreach (var s in list)
            {
                if (s.StartsWith(t)) best = Math.Max(best, 3);
                else if (s.Contains(' ' + t)) best = Math.Max(best, 2);
                else if (s.Contains(t)) best = Math.Max(best, 1);
            }
            if (best == 0) return 0;
            total += best;
        }
        return total;
    }

    /// <summary>Lower-case, unify ё/е and the several apostrophes used in Uzbek Latin (oʻ, o', o`), collapse spaces.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(ch switch
            {
                'ё' => 'е',
                'ʻ' or 'ʼ' or '`' or '´' or '‘' or '’' => '\'',
                _ when char.IsLetterOrDigit(ch) || ch == '\'' => ch,
                _ => ' ',
            });
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
