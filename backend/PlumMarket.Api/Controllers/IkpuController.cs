using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/ikpu")]
public class IkpuController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? search, bool missingOnly = false, int? branchId = null)
    {
        var q = db.Products.AsNoTracking();
        if (branchId is { } b) q = q.Where(p => p.Stock.Any(s => s.BranchId == b));
        if (missingOnly) q = q.Where(p => p.Ikpu == null || p.Ikpu == "");
        var rows = await q.OrderBy(p => p.SortOrder)
            .Select(p => new { p.Id, p.Name, p.Ikpu, p.PackageCode, p.UnitCode, p.Unit, Category = p.Category != null ? p.Category.Name : null })
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(r => r.Name.Values.Any(v => v.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                                   || (r.Ikpu ?? "").Contains(search.Trim())).ToList();
        return Ok(new
        {
            items = rows.Select(r => new
            {
                r.Id, r.Name, r.Ikpu, r.PackageCode, r.UnitCode, r.Unit, r.Category,
                IkpuName = IkpuCatalog.Find(r.Ikpu)?.Name,
            }),
            missing = await db.Products.CountAsync(p => p.Ikpu == null || p.Ikpu == ""),
            reference = IkpuCatalog.Entries.Select(e => new { e.Code, e.Name, e.PackageCode, e.PackageName, e.UnitCode, e.UnitName }),
        });
    }

    public record IkpuDto(string? Ikpu, string? PackageCode, string? UnitCode);

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int productId, IkpuDto dto)
    {
        var ikpu = dto.Ikpu?.Trim();
        if (!string.IsNullOrEmpty(ikpu) && (ikpu.Length != 17 || !ikpu.All(char.IsDigit)))
            return BadRequest(new { error = "ИКПУ — 17 цифр" });
        var p = await db.Products.FindAsync(productId);
        if (p is null) return NotFound();
        p.Ikpu = string.IsNullOrEmpty(ikpu) ? null : ikpu;
        p.PackageCode = string.IsNullOrWhiteSpace(dto.PackageCode) ? null : dto.PackageCode.Trim();
        p.UnitCode = string.IsNullOrWhiteSpace(dto.UnitCode) ? null : dto.UnitCode.Trim();
        p.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.Ikpu, p.PackageCode, p.UnitCode, IkpuName = IkpuCatalog.Find(p.Ikpu)?.Name });
    }

    /// <summary>"Сгенерировать ИКПУ": suggests a code for one product (not saved until the merchant confirms).</summary>
    [HttpPost("{productId:int}/suggest")]
    public async Task<IActionResult> Suggest(int productId)
    {
        var p = await db.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == productId);
        if (p is null) return NotFound();
        var e = IkpuCatalog.Suggest(p.Name.Get(), p.Category?.Name.Get());
        return e is null
            ? NotFound(new { error = "Не удалось подобрать ИКПУ — укажите вручную" })
            : Ok(new { ikpu = e.Code, packageCode = e.PackageCode, unitCode = e.UnitCode, ikpuName = e.Name });
    }

    /// <summary>Fills in every product that has no ИКПУ yet; returns how many were matched.</summary>
    [HttpPost("generate-missing")]
    public async Task<IActionResult> GenerateMissing()
    {
        var products = await db.Products.Include(p => p.Category).Where(p => p.Ikpu == null || p.Ikpu == "").ToListAsync();
        var filled = 0;
        foreach (var p in products)
        {
            if (IkpuCatalog.Suggest(p.Name.Get(), p.Category?.Name.Get()) is not { } e) continue;
            p.Ikpu = e.Code;
            p.PackageCode ??= e.PackageCode;
            p.UnitCode ??= e.UnitCode;
            p.UpdatedAt = DateTime.Now;
            filled++;
        }
        await db.SaveChangesAsync();
        return Ok(new { filled, remaining = products.Count - filled });
    }
}
