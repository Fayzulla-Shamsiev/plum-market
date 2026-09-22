using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db, OrderWorkflow workflow) : ControllerBase
{
    static readonly OrderStatus[] Active =
        [OrderStatus.New, OrderStatus.InProgress, OrderStatus.Overdue, OrderStatus.Ready, OrderStatus.OnTheWay];

    /// <summary>
    /// Everything the dashboard needs for [from, to] in one call. Revenue figures count completed orders only.
    /// Deltas compare against the preceding period of the same length.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(DateOnly? from, DateOnly? to, int? branchId)
    {
        await workflow.SweepOverdueAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var f = from ?? today.AddDays(-29);
        var t = to ?? today;
        if (t < f) (f, t) = (t, f);
        var start = f.ToDateTime(TimeOnly.MinValue);
        var end = t.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var span = end - start;
        var prevStart = start - span;

        // Volumes are small (a few thousand orders), so aggregate in memory — simpler than
        // fighting SQLite's translation limits, and fast enough for a prototype.
        var q = db.Orders.AsNoTracking().Where(o => o.CreatedAt >= prevStart && o.CreatedAt < end);
        if (branchId is { } b) q = q.Where(o => o.BranchId == b);
        var all = await q.Include(o => o.Items).Include(o => o.Customer).AsSplitQuery().ToListAsync();
        var cur = all.Where(o => o.CreatedAt >= start).ToList();
        var prev = all.Where(o => o.CreatedAt < start).ToList();

        var firstOrderAt = await db.Orders.AsNoTracking().GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, First = g.Min(o => o.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.First);

        var buckets = Bucketing.For(start, end);

        var branches = await db.Branches.AsNoTracking().ToListAsync();
        var visits = await db.SourceVisits.AsNoTracking()
            .Where(v => v.Date >= start && v.Date < end)
            .GroupBy(v => v.Source).Select(g => new { Source = g.Key, Users = g.Sum(v => v.Users) })
            .ToListAsync();

        var completed = cur.Where(o => o.Status == OrderStatus.Completed).ToList();

        return Ok(new
        {
            period = new { from = f, to = t, granularity = buckets.Granularity },
            revenue = new
            {
                current = Revenue(cur),
                previous = Revenue(prev),
            },
            orders = new
            {
                current = OrderCounts(cur),
                previous = OrderCounts(prev),
            },
            customers = new
            {
                current = CustomerStats(cur, start, firstOrderAt),
                previous = CustomerStats(prev, prevStart, firstOrderAt),
                totalAllTime = await db.Customers.CountAsync(),
            },
            revenueChart = buckets.Labels.Select((label, i) =>
            {
                var inBucket = completed.Where(o => buckets.IndexOf(o.CreatedAt) == i).ToList();
                return new { label, revenue = inBucket.Sum(o => o.Total), profit = inBucket.Sum(o => o.Subtotal - o.CostTotal) };
            }),
            ordersDynamics = buckets.Labels.Select((label, i) =>
            {
                var inBucket = cur.Where(o => buckets.IndexOf(o.CreatedAt) == i).ToList();
                return new
                {
                    label,
                    @new = inBucket.Count(o => Active.Contains(o.Status)),
                    completed = inBucket.Count(o => o.Status == OrderStatus.Completed),
                    cancelled = inBucket.Count(o => o.Status == OrderStatus.Cancelled),
                };
            }),
            byPlatform = Enum.GetValues<Platform>().Select(p => new
            {
                platform = p,
                orders = cur.Count(o => o.Platform == p),
                revenue = completed.Where(o => o.Platform == p).Sum(o => o.Total),
            }),
            trafficSources = visits.OrderByDescending(v => v.Users),
            topProducts = completed.SelectMany(o => o.Items)
                .GroupBy(i => i.ProductName)
                .Select(g => new { name = g.Key, quantity = g.Sum(i => i.Quantity), revenue = g.Sum(i => i.Price * i.Quantity) })
                .OrderByDescending(p => p.quantity).Take(10),
            topCustomers = cur.Where(o => o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.CustomerId)
                .Select(g => new { id = g.Key, name = g.First().Customer.FullName, phone = g.First().Customer.Phone, orders = g.Count(), total = g.Sum(o => o.Total) })
                .OrderByDescending(c => c.orders).ThenByDescending(c => c.total).Take(10),
            map = new
            {
                branches = branches.Select(br => new { br.Id, br.Name, br.Address, br.Lat, br.Lng }),
                // Cap the number of pins; the most recent ones are the most useful.
                orders = cur.Where(o => o.Lat != null).OrderByDescending(o => o.CreatedAt).Take(600)
                    .Select(o => new { o.Id, lat = o.Lat, lng = o.Lng, o.Status, o.Total, o.BranchId }),
            },
        });
    }

    static object Revenue(List<Order> orders)
    {
        var done = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var revenue = done.Sum(o => o.Total);
        var cost = done.Sum(o => o.CostTotal);
        var delivery = done.Sum(o => o.DeliveryCost);
        return new { revenue, cost, delivery, profit = revenue - cost - delivery };
    }

    static object OrderCounts(List<Order> orders) => new
    {
        total = orders.Count,
        @new = orders.Count(o => Active.Contains(o.Status)),
        completed = orders.Count(o => o.Status == OrderStatus.Completed),
        cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled),
    };

    static object CustomerStats(List<Order> orders, DateTime periodStart, Dictionary<int, DateTime> firstOrderAt)
    {
        var ids = orders.Select(o => o.CustomerId).Distinct().ToList();
        var newCount = ids.Count(id => firstOrderAt.TryGetValue(id, out var first) && first >= periodStart);
        var paid = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        return new
        {
            total = ids.Count,
            @new = newCount,
            returning = ids.Count - newCount,
            averageOrder = paid.Count == 0 ? 0 : paid.Sum(o => o.Total) / paid.Count,
        };
    }
}

/// <summary>Hourly buckets for a single day, daily up to ~3 months, monthly beyond that.</summary>
public class Bucketing
{
    public string Granularity { get; private init; } = "day";
    public List<string> Labels { get; } = new();
    readonly DateTime _start;

    Bucketing(DateTime start) => _start = start;

    public static Bucketing For(DateTime start, DateTime end)
    {
        var days = (end - start).TotalDays;
        var b = new Bucketing(start) { Granularity = days <= 1 ? "hour" : days <= 92 ? "day" : "month" };
        switch (b.Granularity)
        {
            case "hour":
                for (var h = 0; h < 24; h++) b.Labels.Add($"{h:00}:00");
                break;
            case "day":
                for (var d = start; d < end; d = d.AddDays(1)) b.Labels.Add(d.ToString("dd.MM"));
                break;
            default:
                for (var m = new DateTime(start.Year, start.Month, 1); m < end; m = m.AddMonths(1))
                    b.Labels.Add(m.ToString("MM.yyyy"));
                break;
        }
        return b;
    }

    public int IndexOf(DateTime t) => Granularity switch
    {
        "hour" => t.Hour,
        "day" => (int)(t.Date - _start.Date).TotalDays,
        _ => (t.Year - _start.Year) * 12 + t.Month - _start.Month,
    };
}
