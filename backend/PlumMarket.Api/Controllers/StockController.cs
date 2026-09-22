using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>"Склад": per-branch stock plus product pricing, and a sales history per product.</summary>
[ApiController]
[Route("api/stock")]
public class StockController(AppDbContext db) : ControllerBase
{
    const int VelocityDays = 30;

    [HttpGet]
    public async Task<IActionResult> List(int branchId, string? search, StockStatus? status)
    {
        var rows = await db.Stock.AsNoTracking().Where(s => s.BranchId == branchId)
            .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new { s, p })
            .OrderBy(x => x.p.SortOrder)
            .Select(x => new
            {
                x.p.Id, x.p.Name, x.p.Media, x.p.CostPrice, x.p.Price, x.p.WeightGrams, x.p.Unit, x.p.IsActive,
                x.s.Status, x.s.Quantity, x.s.UpdatedAt,
            })
            .ToListAsync();
        if (status is { } st) rows = rows.Where(r => r.Status == st).ToList();
        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(r => r.Name.Values.Any(v => v.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

        var sold = await SoldSince(branchId, DateTime.Now.AddDays(-VelocityDays));
        return Ok(new
        {
            velocityDays = VelocityDays,
            items = rows.Select(r => new
            {
                r.Id, r.Name, r.CostPrice, r.Price, r.WeightGrams, r.Unit, r.IsActive, r.Status, r.Quantity, r.UpdatedAt,
                ImageUrl = r.Media.FirstOrDefault(m => m.Type == "image")?.Url,
                MarginPercent = r.Price > 0 ? Math.Round((r.Price - r.CostPrice) * 100.0 / r.Price, 1) : 0,
                // Units sold per day over the last 30 days in this branch.
                Velocity = Math.Round(sold.GetValueOrDefault(r.Id) / (double)VelocityDays, 1),
            }),
        });
    }

    public record StockPatch(long? CostPrice, long? Price, int? WeightGrams, StockStatus? Status, int? Quantity);

    /// <summary>Inline edits from the warehouse table. Prices/weight are product-wide; status/quantity are per branch.</summary>
    [HttpPatch("{productId:int}")]
    public async Task<IActionResult> Patch(int productId, int branchId, StockPatch body)
    {
        var p = await db.Products.FindAsync(productId);
        var s = await db.Stock.FirstOrDefaultAsync(x => x.ProductId == productId && x.BranchId == branchId);
        if (p is null || s is null) return NotFound();

        if (body.Price is { } price)
        {
            if (price <= 0) return BadRequest(new { error = "Цена должна быть больше нуля" });
            p.Price = price;
            if (p.OldPrice <= price) p.OldPrice = null;
        }
        if (body.CostPrice is { } cost)
        {
            if (cost < 0) return BadRequest(new { error = "Входная цена не может быть отрицательной" });
            p.CostPrice = cost;
        }
        if (body.WeightGrams is { } w) p.WeightGrams = w > 0 ? w : null;
        if (body.Status is { } status)
        {
            s.Status = status;
            if (status == StockStatus.OutOfStock) s.Quantity = 0;
        }
        if (body.Quantity is { } qty)
        {
            if (qty < 0) return BadRequest(new { error = "Остаток не может быть отрицательным" });
            s.Quantity = qty;
            // Setting a count implies tracked stock; zero means sold out.
            if (s.Status == StockStatus.Unlimited || (s.Status == StockStatus.OutOfStock && qty > 0)) s.Status = StockStatus.Limited;
            if (qty == 0 && s.Status == StockStatus.Limited) s.Status = StockStatus.OutOfStock;
        }
        p.UpdatedAt = s.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(new
        {
            p.Id, p.CostPrice, p.Price, p.WeightGrams, s.Status, s.Quantity, s.UpdatedAt,
            MarginPercent = p.Price > 0 ? Math.Round((p.Price - p.CostPrice) * 100.0 / p.Price, 1) : 0,
        });
    }

    /// <summary>"История продаж": totals and a 12-month chart for one product (completed orders only).</summary>
    [HttpGet("{productId:int}/history")]
    public async Task<IActionResult> History(int productId, int? branchId)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId);
        if (p is null) return NotFound();
        var since = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-11);
        var q = db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Completed);
        if (branchId is { } b) q = q.Where(o => o.BranchId == b);
        var lines = await db.OrderItems.AsNoTracking().Where(i => i.ProductId == productId)
            .Join(q, i => i.OrderId, o => o.Id, (i, o) => new { o.CreatedAt, i.Quantity, Sum = i.Price * i.Quantity })
            .Where(l => l.CreatedAt >= since)
            .ToListAsync();

        var months = Enumerable.Range(0, 12).Select(i => since.AddMonths(i)).Select(m => new
        {
            Month = m.ToString("MM.yyyy"),
            Quantity = lines.Where(l => l.CreatedAt.Year == m.Year && l.CreatedAt.Month == m.Month).Sum(l => l.Quantity),
            Revenue = lines.Where(l => l.CreatedAt.Year == m.Year && l.CreatedAt.Month == m.Month).Sum(l => l.Sum),
        });
        return Ok(new
        {
            p.Id, p.Name,
            TotalQuantity = lines.Sum(l => l.Quantity),
            TotalRevenue = lines.Sum(l => l.Sum),
            FirstSaleAt = lines.Count > 0 ? lines.Min(l => l.CreatedAt) : (DateTime?)null,
            Months = months,
        });
    }

    async Task<Dictionary<int, int>> SoldSince(int branchId, DateTime since) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Completed && o.CreatedAt >= since)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new { g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty);
}
