using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? search, Platform? platform, string sort = "lastVisit", int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 5, 100);
        var rows = await db.Customers.AsNoTracking()
            .Select(c => new CustomerRow(
                c.Id, c.FullName, c.Username, c.Phone, c.Platform, c.BonusPoints, c.CreatedAt, c.LastVisitAt, c.Language,
                c.Orders.Count(o => o.Status != OrderStatus.Cancelled),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total)))
            .ToListAsync();

        // Filtered in memory: SQLite's case-insensitive matching only covers ASCII, and names are Cyrillic.
        IEnumerable<CustomerRow> list = rows;
        if (platform is { } p) list = list.Where(r => r.Platform == p);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().TrimStart('@');
            var digits = new string(term.Where(char.IsDigit).ToArray());
            list = list.Where(r =>
                r.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Username ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (digits.Length >= 3 && r.Phone.Replace(" ", "").Contains(digits)));
        }
        list = sort switch
        {
            "orders" => list.OrderByDescending(r => r.Orders),
            "bonus" => list.OrderByDescending(r => r.BonusPoints),
            "created" => list.OrderByDescending(r => r.CreatedAt),
            "name" => list.OrderBy(r => r.FullName),
            _ => list.OrderByDescending(r => r.LastVisitAt),
        };
        var materialized = list.ToList();
        return Ok(new
        {
            items = materialized.Skip((page - 1) * pageSize).Take(pageSize),
            total = materialized.Count,
            page, pageSize,
        });
    }

    public record CustomerRow(int Id, string FullName, string? Username, string Phone, Platform Platform, long BonusPoints,
        DateTime CreatedAt, DateTime LastVisitAt, string Language, int Orders, long Spent);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (c is null) return NotFound();
        var orders = await db.Orders.AsNoTracking().Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, o.Platform, o.BonusEarned })
            .ToListAsync();
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        return Ok(new
        {
            c.Id, c.FullName, c.Username, c.Phone, c.Platform, c.Language, c.BonusPoints, c.CreatedAt, c.LastVisitAt,
            stats = new
            {
                orders = orders.Count(o => o.Status != OrderStatus.Cancelled),
                completed = completed.Count,
                spent = completed.Sum(o => o.Total),
                averageOrder = completed.Count == 0 ? 0 : completed.Sum(o => o.Total) / completed.Count,
                bonusEarned = completed.Sum(o => o.BonusEarned),
            },
            orders = orders.Take(20),
        });
    }
}
