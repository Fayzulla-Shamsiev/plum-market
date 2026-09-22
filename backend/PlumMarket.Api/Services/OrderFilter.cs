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
    public int? EmployeeId { get; set; }
    public PaymentMethod? Payment { get; set; }
    public DeliveryType? Delivery { get; set; }
    public Platform? Platform { get; set; }
    /// <summary>Comma-separated list of statuses.</summary>
    public string? Statuses { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public static readonly Dictionary<string, OrderStatus[]> Tabs = new()
    {
        ["all"] = Enum.GetValues<OrderStatus>(),
        ["new"] = [OrderStatus.New],
        ["inProgress"] = [OrderStatus.InProgress],
        ["overdue"] = [OrderStatus.Overdue],
        ["ready"] = [OrderStatus.Ready],
        ["onTheWay"] = [OrderStatus.OnTheWay],
        ["history"] = [OrderStatus.Completed, OrderStatus.Cancelled],
    };

    public List<OrderStatus> ParsedStatuses() => (Statuses ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => Enum.TryParse<OrderStatus>(s, true, out var v) ? v : (OrderStatus?)null)
        .OfType<OrderStatus>().ToList();

    /// <summary>Applies everything except the tab, so tab counters can be computed from the same base.</summary>
    public async Task<IQueryable<Order>> ApplyAsync(IQueryable<Order> q, AppDbContext db)
    {
        if (BranchId is { } b) q = q.Where(o => o.BranchId == b);
        if (EmployeeId is { } e) q = q.Where(o => o.EmployeeId == e);
        if (Payment is { } p) q = q.Where(o => o.PaymentMethod == p);
        if (Delivery is { } d) q = q.Where(o => o.DeliveryType == d);
        if (Platform is { } pl) q = q.Where(o => o.Platform == pl);
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
        if (Tab is null || Tab == "all" || !Tabs.TryGetValue(Tab, out var statuses)) return q;
        return q.Where(o => statuses.Contains(o.Status));
    }
}
