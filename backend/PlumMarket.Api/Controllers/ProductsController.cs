using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> List(string? search, int? categoryId, int? branchId, bool? active, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 5, 100);
        var q = db.Products.AsNoTracking();
        if (categoryId is { } cid)
        {
            // A parent category shows its sub-categories' products too.
            var ids = await CategoryWithChildren(cid);
            q = q.Where(p => p.CategoryId != null && ids.Contains(p.CategoryId.Value));
        }
        if (branchId is { } b) q = q.Where(p => p.Stock.Any(s => s.BranchId == b));
        if (active is { } a) q = q.Where(p => p.IsActive == a);

        var rows = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id, p.Name, p.Price, p.OldPrice, p.IsActive, p.CreatedAt, p.Unit, p.CategoryId, p.Media,
                Category = p.Category != null ? p.Category.Name : null,
                Branches = p.Stock.Count,
                Stock = branchId == null ? null : p.Stock.Where(s => s.BranchId == branchId).Select(s => new { s.Status, s.Quantity }).FirstOrDefault(),
            })
            .ToListAsync();

        // Cyrillic-aware search across all languages happens in memory (SQLite LIKE is ASCII-only).
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(r => r.Name.Values.Any(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))
                                   || r.Id.ToString() == term).ToList();
        }

        var pageRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ids2 = pageRows.Select(r => r.Id).ToList();
        var ratings = await db.Reviews.Where(r => ids2.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(g => new { g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key);

        return Ok(new
        {
            items = pageRows.Select(r => new
            {
                r.Id, r.Name, r.Price, r.OldPrice, r.IsActive, r.CreatedAt, r.Unit, r.CategoryId, r.Category, r.Branches, r.Stock,
                ImageUrl = r.Media.FirstOrDefault(m => m.Type == "image")?.Url,
                Rating = ratings.TryGetValue(r.Id, out var x) ? Math.Round(x.Avg, 1) : (double?)null,
                ReviewsCount = ratings.TryGetValue(r.Id, out var y) ? y.Count : 0,
            }),
            total = rows.Count, page, pageSize,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await db.Products.AsNoTracking().Include(p => p.Stock).FirstOrDefaultAsync(p => p.Id == id);
        if (p is null) return NotFound();
        return Ok(ToDetail(p));
    }

    static object ToDetail(Product p) => new
    {
        p.Id, p.CategoryId, p.Name, p.Description, p.Price, p.OldPrice, p.CostPrice, p.Unit,
        p.WeightGrams, p.LengthCm, p.WidthCm, p.HeightCm, p.Tags, p.Attributes, p.Variants, p.Media,
        p.Ikpu, p.PackageCode, p.UnitCode, p.IsActive, p.CreatedAt, p.UpdatedAt,
        BranchIds = p.Stock.Select(s => s.BranchId).OrderBy(x => x).ToList(),
    };

    public record ProductDto(
        int? CategoryId, Localized Name, Localized? Description, long Price, long? OldPrice, long CostPrice, string? Unit,
        int? WeightGrams, int? LengthCm, int? WidthCm, int? HeightCm, List<string>? Tags,
        List<ProductAttribute>? Attributes, List<ProductVariant>? Variants, List<ProductMedia>? Media,
        bool IsActive, List<int>? BranchIds);

    [HttpPost]
    public async Task<IActionResult> Create(ProductDto dto)
    {
        var p = new Product { CreatedAt = DateTime.Now, SortOrder = await db.Products.CountAsync() };
        if (await Apply(p, dto) is { } error) return error;
        db.Products.Add(p);
        await db.SaveChangesAsync();
        return Ok(ToDetail(p));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductDto dto)
    {
        var p = await db.Products.Include(p => p.Stock).FirstOrDefaultAsync(p => p.Id == id);
        if (p is null) return NotFound();
        if (await Apply(p, dto) is { } error) return error;
        await db.SaveChangesAsync();
        return Ok(ToDetail(p));
    }

    public record ActiveBody(bool IsActive);

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ActiveBody body)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        p.IsActive = body.IsActive;
        p.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        // Order lines keep their own name/price snapshot, so history survives the deletion.
        db.Products.Remove(p);
        foreach (var d in await db.Discounts.ToListAsync()) d.ProductIds.Remove(id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<IActionResult?> Apply(Product p, ProductDto dto)
    {
        var name = CategoriesController.Clean(dto.Name);
        if (string.IsNullOrWhiteSpace(name.GetValueOrDefault("ru"))) return BadRequest(new { error = "Укажите название на русском языке" });
        if (dto.Price <= 0) return BadRequest(new { error = "Цена должна быть больше нуля" });
        if (dto.OldPrice is { } op && op <= dto.Price) return BadRequest(new { error = "Старая цена должна быть выше текущей" });
        if (dto.CostPrice < 0) return BadRequest(new { error = "Себестоимость не может быть отрицательной" });
        if (dto.CategoryId is { } cid && !await db.Categories.AnyAsync(c => c.Id == cid)) return BadRequest(new { error = "Категория не найдена" });

        p.CategoryId = dto.CategoryId;
        p.Name = name;
        p.Description = CategoriesController.Clean(dto.Description ?? new Localized());
        p.Price = dto.Price;
        p.OldPrice = dto.OldPrice;
        p.CostPrice = dto.CostPrice;
        p.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "шт" : dto.Unit.Trim();
        p.WeightGrams = dto.WeightGrams;
        p.LengthCm = dto.LengthCm;
        p.WidthCm = dto.WidthCm;
        p.HeightCm = dto.HeightCm;
        p.Tags = (dto.Tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        p.Attributes = (dto.Attributes ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        p.Variants = (dto.Variants ?? []).Where(v => !string.IsNullOrWhiteSpace(v.Name)).ToList();
        p.Media = (dto.Media ?? []).Where(m => !string.IsNullOrWhiteSpace(m.Url)).ToList();
        p.IsActive = dto.IsActive;
        p.UpdatedAt = DateTime.Now;

        // Branch availability = one stock row per branch. New branches start as "unlimited".
        var branchIds = dto.BranchIds ?? await db.Branches.Select(b => b.Id).ToListAsync();
        p.Stock.RemoveAll(s => !branchIds.Contains(s.BranchId));
        foreach (var b in branchIds.Where(b => p.Stock.All(s => s.BranchId != b)))
            p.Stock.Add(new StockItem { BranchId = b, Status = StockStatus.Unlimited, UpdatedAt = DateTime.Now });
        return null;
    }

    async Task<List<int>> CategoryWithChildren(int id)
    {
        var all = await db.Categories.AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync();
        var result = new List<int> { id };
        for (var i = 0; i < result.Count; i++)
            result.AddRange(all.Where(c => c.ParentId == result[i]).Select(c => c.Id));
        return result;
    }

    // ---------------------------------------------------------------- import

    static readonly string[] ImportColumns =
        ["Название (RU)*", "Название (UZ, латиница)", "Название (UZ, кириллица)", "Описание (RU)", "Описание (UZ)",
         "Категория", "Цена*", "Старая цена", "Себестоимость", "Ед. изм.", "Вес, г", "Теги (через запятую)", "ИКПУ"];

    [HttpGet("import/template")]
    public IActionResult Template()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Товары");
        for (var i = 0; i < ImportColumns.Length; i++) ws.Cell(1, i + 1).Value = ImportColumns[i];
        object[][] samples =
        [
            ["Круассан с ветчиной", "Vetchinali kruassan", "", "Слоёный круассан с ветчиной и сыром", "", "Круассаны", 28000, "", 12000, "шт", 110, "новинка", ""],
            ["Капучино 400 мл", "Kapuchino 400 ml", "", "", "", "Кофе", 28000, 32000, 9000, "порция", "", "", ""],
        ];
        for (var r = 0; r < samples.Length; r++)
            for (var c = 0; c < samples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(samples[r][c]);
        var header = ws.Range(1, 1, 1, ImportColumns.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE7F6");
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(8, 40);

        var help = wb.AddWorksheet("Инструкция");
        string[] lines =
        [
            "Одна строка — один товар. Обязательны: «Название (RU)» и «Цена».",
            "Если товар с таким названием (RU) уже есть — он будет обновлён, иначе создан.",
            "Категория ищется по названию на русском; если её нет — будет создана.",
            "Если заполнено название на латинице, кириллица заполнится автоматически.",
            "Новые товары становятся доступны во всех филиалах.",
            "Цены — целые числа в сумах, без пробелов.",
        ];
        for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
        help.Column(1).Width = 90;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), XlsxMime, "plum_products_template.xlsx");
    }

    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Загрузите файл .xlsx (используйте шаблон)" });

        XLWorkbook wb;
        try
        {
            await using var s = file.OpenReadStream();
            var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            wb = new XLWorkbook(ms);
        }
        catch
        {
            return BadRequest(new { error = "Не удалось прочитать файл Excel" });
        }

        using (wb)
        {
            var ws = wb.Worksheets.First();
            var products = await db.Products.Include(p => p.Stock).ToListAsync();
            var categories = await db.Categories.ToListAsync();
            var branchIds = await db.Branches.Select(b => b.Id).ToListAsync();
            int created = 0, updated = 0;
            var errors = new List<string>();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                string Cell(int col) => row.Cell(col).GetFormattedString().Trim();
                long? Money(int col) => long.TryParse(Cell(col).Replace(" ", "").Replace(",", ""), out var v) ? v : null;

                var n = row.RowNumber();
                var ru = Cell(1);
                if (ru.Length == 0) continue;
                if (Money(7) is not { } price || price <= 0) { errors.Add($"Строка {n}: не указана цена для «{ru}»"); continue; }

                var p = products.FirstOrDefault(x => string.Equals(x.Name.Get(), ru, StringComparison.OrdinalIgnoreCase));
                if (p is null)
                {
                    p = new Product { CreatedAt = DateTime.Now, SortOrder = products.Count };
                    foreach (var b in branchIds) p.Stock.Add(new StockItem { BranchId = b, UpdatedAt = DateTime.Now });
                    products.Add(p);
                    db.Products.Add(p);
                    created++;
                }
                else updated++;

                var uz = Cell(2);
                var oz = Cell(3);
                if (oz.Length == 0 && uz.Length > 0) oz = UzTransliterator.ToCyrillic(uz);
                p.Name = CategoriesController.Clean(Localized.Of(ru, uz, oz));
                var descUz = Cell(5);
                p.Description = CategoriesController.Clean(Localized.Of(Cell(4), descUz, descUz.Length > 0 ? UzTransliterator.ToCyrillic(descUz) : ""));
                p.Price = price;
                p.OldPrice = Money(8) is { } op && op > price ? op : null;
                p.CostPrice = Money(9) ?? p.CostPrice;
                p.Unit = Cell(10) is { Length: > 0 } unit ? unit : "шт";
                p.WeightGrams = int.TryParse(Cell(11), out var g) ? g : p.WeightGrams;
                if (Cell(12) is { Length: > 0 } tags) p.Tags = tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
                if (Cell(13) is { Length: > 0 } ikpu) p.Ikpu = ikpu;
                p.UpdatedAt = DateTime.Now;

                if (Cell(6) is { Length: > 0 } catName)
                {
                    var cat = categories.FirstOrDefault(c => string.Equals(c.Name.Get(), catName, StringComparison.OrdinalIgnoreCase));
                    if (cat is null)
                    {
                        cat = new Category { Name = Localized.Of(catName), CreatedAt = DateTime.Now, SortOrder = categories.Count };
                        categories.Add(cat);
                        db.Categories.Add(cat);
                    }
                    p.Category = cat;
                }
            }
            await db.SaveChangesAsync();
            return Ok(new { created, updated, errors });
        }
    }

    public record ImportSettingsDto(string? Source, string? Url, string? ApiKey, bool AutoSync);

    /// <summary>Parameters for syncing the catalog from an external system (Billz, 1C, МойСклад…). Stored only.</summary>
    [HttpGet("import/settings")]
    public async Task<ImportSettingsDto> GetImportSettings()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        // Never send the stored key back to the browser — only whether one is set.
        return new ImportSettingsDto(s.ImportSource, s.ImportUrl, string.IsNullOrEmpty(s.ImportApiKey) ? null : "••••••••", s.ImportAutoSync);
    }

    [HttpPut("import/settings")]
    public async Task<ImportSettingsDto> PutImportSettings(ImportSettingsDto dto)
    {
        var s = await db.Settings.FirstAsync();
        s.ImportSource = dto.Source;
        s.ImportUrl = dto.Url?.Trim();
        if (dto.ApiKey is { } key && key != "••••••••") s.ImportApiKey = key.Trim();
        s.ImportAutoSync = dto.AutoSync;
        await db.SaveChangesAsync();
        return await GetImportSettings();
    }
}
