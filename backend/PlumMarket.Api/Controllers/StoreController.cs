using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Admin "Магазин": what the storefront shows about the store (О нас, contacts, delivery price and terms, return
/// terms), the order time limit, and branches (pickup points, used for stock and delivery).
/// </summary>
[ApiController]
[Route("api/store")]
public class StoreController(AppDbContext db) : ControllerBase
{
    public record StoreSettingsDto(string StoreName, string? Phone, string? WorkingHours, string? AboutText, long DeliveryFee,
        long? FreeDeliveryFrom, string? DeliveryTerms, string? ReturnTerms, int OverdueMinutes);

    [HttpGet("settings")]
    public async Task<StoreSettingsDto> GetSettings()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        return new(s.StoreName, s.Phone, s.WorkingHours, s.AboutText, s.DeliveryFee, s.FreeDeliveryFrom, s.DeliveryTerms, s.ReturnTerms, s.OverdueMinutes);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> PutSettings(StoreSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StoreName)) return BadRequest(new { error = "Укажите название магазина" });
        if (dto.DeliveryFee < 0 || dto.FreeDeliveryFrom < 0) return BadRequest(new { error = "Суммы не могут быть отрицательными" });
        if (dto.OverdueMinutes is < 5 or > 24 * 60) return BadRequest(new { error = "Лимит времени — от 5 минут до 24 часов" });
        var s = await db.Settings.FirstAsync();
        s.StoreName = dto.StoreName.Trim();
        s.Phone = Clean(dto.Phone, 40);
        s.WorkingHours = Clean(dto.WorkingHours, 120);
        s.AboutText = Clean(dto.AboutText, 4000);
        s.DeliveryFee = dto.DeliveryFee;
        s.FreeDeliveryFrom = dto.FreeDeliveryFrom is > 0 ? dto.FreeDeliveryFrom : null;
        s.DeliveryTerms = Clean(dto.DeliveryTerms, 8000);
        s.ReturnTerms = Clean(dto.ReturnTerms, 8000);
        s.OverdueMinutes = dto.OverdueMinutes;
        await db.SaveChangesAsync();
        return Ok(await GetSettings());
    }

    [HttpGet("branches")]
    public async Task<IActionResult> Branches()
    {
        var orders = await db.Orders.AsNoTracking().GroupBy(o => o.BranchId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        return Ok(branches.Select(b => new { b.Id, b.Name, b.Address, b.Phone, b.WorkingHours, b.Lat, b.Lng, orders = orders.GetValueOrDefault(b.Id) }));
    }

    public record BranchDto(string Name, string Address, string? Phone, string? WorkingHours, double Lat, double Lng, int? CopyStockFrom);

    /// <summary>New branch. Its assortment can be copied from an existing branch; otherwise it sells nothing until stock is set in Склад.</summary>
    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch(BranchDto dto)
    {
        if (Validate(dto) is { } error) return BadRequest(new { error });
        var b = new Branch();
        Apply(b, dto);
        db.Branches.Add(b);
        await db.SaveChangesAsync();
        if (dto.CopyStockFrom is { } src)
        {
            var rows = await db.Stock.AsNoTracking().Where(s => s.BranchId == src).ToListAsync();
            db.Stock.AddRange(rows.Select(r => new StockItem { ProductId = r.ProductId, BranchId = b.Id, Status = r.Status, Quantity = r.Quantity, UpdatedAt = DateTime.Now }));
            await db.SaveChangesAsync();
        }
        return Ok(b);
    }

    [HttpPut("branches/{id:int}")]
    public async Task<IActionResult> UpdateBranch(int id, BranchDto dto)
    {
        if (Validate(dto) is { } error) return BadRequest(new { error });
        var b = await db.Branches.FindAsync(id);
        if (b is null) return NotFound();
        Apply(b, dto);
        await db.SaveChangesAsync();
        return Ok(b);
    }

    [HttpDelete("branches/{id:int}")]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        if (await db.Orders.AnyAsync(o => o.BranchId == id))
            return Conflict(new { error = "У филиала есть заказы — его нельзя удалить, иначе пропадёт история заказов" });
        if (await db.Branches.CountAsync() <= 1) return Conflict(new { error = "Нужен хотя бы один филиал" });
        await db.Stock.Where(s => s.BranchId == id).ExecuteDeleteAsync();
        await db.Branches.Where(b => b.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    static string? Validate(BranchDto d) =>
        string.IsNullOrWhiteSpace(d.Name) ? "Укажите название филиала"
        : string.IsNullOrWhiteSpace(d.Address) ? "Укажите адрес"
        : d.Lat is < -90 or > 90 || d.Lng is < -180 or > 180 || (d.Lat == 0 && d.Lng == 0) ? "Отметьте филиал на карте"
        : null;

    static void Apply(Branch b, BranchDto d)
    {
        b.Name = d.Name.Trim();
        b.Address = d.Address.Trim();
        b.Phone = Clean(d.Phone, 40);
        b.WorkingHours = Clean(d.WorkingHours, 120);
        b.Lat = d.Lat;
        b.Lng = d.Lng;
    }

    static string? Clean(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim()[..Math.Min(v.Trim().Length, max)];
}
