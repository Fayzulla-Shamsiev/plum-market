using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>Query-string filters shared by the orders list, export and assembly sheet.</summary>
public class OrderFilter
{
    public string? Tab { get; set; }
    public string? Search { get; set; }
    public int? BranchId { get; set; }
    public DeliveryType? Delivery { get; set; }
    /// <summary>Comma-separated list of statuses.</summary>
    public string? Statuses { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Order list tabs. "overdue" isn't a status set: it's New/Assembling orders past the store's time limit.</summary>
    public static readonly Dictionary<string, OrderStatus[]> Tabs = new()
    {
        ["all"] = Enum.GetValues<OrderStatus>(),
        ["new"] = [OrderStatus.New],
        ["assembling"] = [OrderStatus.Assembling],
        ["ready"] = [OrderStatus.Ready],
        ["delivery"] = [OrderStatus.HandedToCourier, OrderStatus.OnTheWay],
        ["delivered"] = [OrderStatus.Delivered],
        ["history"] = [OrderStatus.Completed, OrderStatus.Cancelled],
    };

    /// <summary>Set by the controller from store settings, used by the "overdue" tab.</summary>
    public DateTime? OverdueBefore { get; set; }

    public List<OrderStatus> ParsedStatuses() => (Statuses ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => Enum.TryParse<OrderStatus>(s, true, out var v) ? v : (OrderStatus?)null)
        .OfType<OrderStatus>().ToList();

    /// <summary>Applies everything except the tab, so tab counters can be computed from the same base.</summary>
    public async Task<IQueryable<Order>> ApplyAsync(IQueryable<Order> q, AppDbContext db)
    {
        if (BranchId is { } b) q = q.Where(o => o.BranchId == b);
        if (Delivery is { } d) q = q.Where(o => o.DeliveryType == d);
        if (From is { } from) q = q.Where(o => o.CreatedAt >= from.ToDateTime(TimeOnly.MinValue));
        if (To is { } to) q = q.Where(o => o.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var statuses = ParsedStatuses();
        if (statuses.Count > 0) q = q.Where(o => statuses.Contains(o.Status));

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().TrimStart('#');
            // SQLite's LIKE/lower() are ASCII-only, so match names case-insensitively in memory.
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var customers = await db.Customers.AsNoTracking()
                .Select(c => new { c.Id, c.FullName, c.Phone, c.Username }).ToListAsync();
            var ids = customers.Where(c =>
                    c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Username ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (digits.Length >= 3 && c.Phone.Replace(" ", "").Contains(digits)))
                .Select(c => c.Id).ToList();
            var orderId = int.TryParse(term, out var n) ? n : -1;
            q = q.Where(o => o.Id == orderId || ids.Contains(o.CustomerId));
        }
        return q;
    }

    public IQueryable<Order> ApplyTab(IQueryable<Order> q)
    {
        if (Tab == "overdue") return Overdue(q);
        if (Tab is null || Tab == "all" || !Tabs.TryGetValue(Tab, out var statuses)) return q;
        return q.Where(o => statuses.Contains(o.Status));
    }

    public IQueryable<Order> Overdue(IQueryable<Order> q)
    {
        var before = OverdueBefore ?? DateTime.Now.AddMinutes(-90);
        return q.Where(o => (o.Status == OrderStatus.New || o.Status == OrderStatus.Assembling) && o.CreatedAt < before);
    }
}
