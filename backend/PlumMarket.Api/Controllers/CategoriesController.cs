using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    /// <summary>Flat list (the UI builds the tree from <c>parentId</c>) with sub-category and product counts.</summary>
    [HttpGet]
    public async Task<IActionResult> List(int? branchId)
    {
        var cats = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        var products = db.Products.AsNoTracking();
        if (branchId is { } b) products = products.Where(p => p.Stock.Any(s => s.BranchId == b));
        var productCounts = await products.Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // A parent's product count includes everything in its sub-categories.
        int Total(int id) => productCounts.GetValueOrDefault(id) + cats.Where(x => x.ParentId == id).Sum(x => Total(x.Id));
        return Ok(cats.Select(c => new
        {
            c.Id, c.ParentId, c.Name, c.ImageUrl, c.IsActive, c.CreatedAt, c.SortOrder, c.Layout,
            SubcategoriesCount = cats.Count(x => x.ParentId == c.Id),
            ProductsCount = Total(c.Id),
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } c ? Ok(c) : NotFound();

    public record CategoryDto(int? ParentId, Localized Name, Localized? Description, string? ImageUrl, string? BannerUrl,
        string? Layout, string? ProductSort, bool IsActive = true, int? SortOrder = null);

    [HttpPost]
    public async Task<IActionResult> Create(CategoryDto dto)
    {
        var c = new Category { CreatedAt = DateTime.Now, SortOrder = await db.Categories.CountAsync() };
        if (await Apply(c, dto) is { } error) return error;
        db.Categories.Add(c);
        await db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryDto dto)
    {
        var c = await db.Categories.FindAsync(id);
        if (c is null) return NotFound();
        if (await Apply(c, dto) is { } error) return error;
        await db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c is null) return NotFound();
        if (await db.Categories.AnyAsync(x => x.ParentId == id))
            return Conflict(new { error = "Сначала удалите или перенесите подкатегории" });
        var products = await db.Products.CountAsync(p => p.CategoryId == id);
        if (products > 0)
            return Conflict(new { error = $"В категории {products} товаров — перенесите их в другую категорию" });
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<IActionResult?> Apply(Category c, CategoryDto dto)
    {
        var name = Clean(dto.Name);
        if (string.IsNullOrWhiteSpace(name.GetValueOrDefault("ru")))
            return BadRequest(new { error = "Укажите название на русском языке" });
        if (dto.ParentId is { } parentId)
        {
            if (parentId == c.Id || await IsDescendant(parentId, c.Id))
                return BadRequest(new { error = "Категория не может быть вложена сама в себя" });
            if (!await db.Categories.AnyAsync(x => x.Id == parentId))
                return BadRequest(new { error = "Родительская категория не найдена" });
        }
        c.ParentId = dto.ParentId;
        c.Name = name;
        c.Description = Clean(dto.Description ?? new Localized());
        c.ImageUrl = dto.ImageUrl;
        c.BannerUrl = dto.BannerUrl;
        c.Layout = dto.Layout is "grid2" or "grid3" or "list" ? dto.Layout : "grid2";
        c.ProductSort = dto.ProductSort is "manual" or "popular" or "new" or "price_asc" or "price_desc" ? dto.ProductSort : "manual";
        c.IsActive = dto.IsActive;
        if (dto.SortOrder is { } so) c.SortOrder = so;
        return null;
    }

    /// <summary>True when <paramref name="candidate"/> sits somewhere below <paramref name="ancestor"/>.</summary>
    async Task<bool> IsDescendant(int candidate, int ancestor)
    {
        if (ancestor == 0) return false;
        var parents = await db.Categories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId);
        for (int? cur = candidate; cur is { } id; cur = parents.GetValueOrDefault(id))
            if (id == ancestor) return true;
        return false;
    }

    internal static Localized Clean(Localized l) =>
        new(l.Where(kv => Localized.Languages.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim()));
}
