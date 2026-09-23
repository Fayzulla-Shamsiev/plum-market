using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(AppDbContext db, OrderWorkflow workflow, CheckoutService checkout) : ControllerBase
{
    const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderFilter filter, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 5, 200);
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var now = DateTime.Now;
        filter.OverdueBefore = now.AddMinutes(-settings.OverdueMinutes);

        var baseQuery = await filter.ApplyAsync(db.Orders.AsNoTracking(), db);
        var byStatus = await baseQuery.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var counts = OrderFilter.Tabs.ToDictionary(t => t.Key,
            t => byStatus.Where(s => t.Value.Contains(s.Key)).Sum(s => s.Count));
        counts["overdue"] = await filter.Overdue(baseQuery).CountAsync();

        var q = filter.ApplyTab(baseQuery);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.CreatedAt, o.StatusChangedAt, o.Status, o.DeliveryType, o.Total, o.Address, o.RecipientName, o.RecipientPhone,
                Customer = new { o.Customer.Id, o.Customer.FullName, o.Customer.Phone },
                Branch = o.Branch.Name,
                ItemsCount = o.Items.Sum(i => i.Quantity),
            })
            .ToListAsync();
        var items = rows.Select(o =>
        {
            var probe = new Order { Status = o.Status, DeliveryType = o.DeliveryType, CreatedAt = o.CreatedAt };
            return new
            {
                o.Id, o.CreatedAt, o.StatusChangedAt, o.Status, o.DeliveryType, o.Total, o.Address, o.Customer, o.Branch, o.ItemsCount,
                recipient = o.RecipientName is null ? null : new { name = o.RecipientName, phone = o.RecipientPhone },
                next = OrderFlow.Next(probe),
                canCancel = OrderFlow.StoreCanCancel(o.Status),
                overdue = OrderFlow.IsOverdue(probe, settings.OverdueMinutes, now),
            };
        });

        return Ok(new { items, total, page, pageSize, counts });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var o = await db.Orders.AsNoTracking()
            .Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Items).Include(o => o.History)
            .AsSplitQuery().FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return NotFound();
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var notifications = await db.Notifications.AsNoTracking().Where(n => n.OrderId == id)
            .OrderByDescending(n => n.SentAt).ToListAsync();
        return Ok(new
        {
            o.Id, o.CreatedAt, o.StatusChangedAt, o.Status, o.DeliveryType,
            o.Subtotal, o.DeliveryCost, o.Total, o.CostTotal, o.BonusEarned, o.Address, o.Lat, o.Lng, o.Comment,
            o.RecipientName, o.RecipientPhone, o.PromoCode, o.PromoDiscount, o.CancelReason,
            Customer = new { o.Customer.Id, o.Customer.FullName, o.Customer.Phone, o.Customer.Email, o.Customer.Language, o.Customer.BonusPoints },
            Branch = new { o.Branch.Id, o.Branch.Name, o.Branch.Address },
            Items = o.Items.Select(i => new { i.ProductName, i.Quantity, i.Price, Sum = i.Price * i.Quantity }),
            // Delivery control: every step with its time and who made it; the remaining steps come from the flow.
            History = o.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new { h.Status, h.At, h.By }),
            Steps = OrderFlow.Steps(o.DeliveryType),
            Next = OrderFlow.Next(o),
            CanCancel = OrderFlow.StoreCanCancel(o.Status),
            Overdue = OrderFlow.IsOverdue(o, settings.OverdueMinutes, DateTime.Now),
            Notifications = notifications,
        });
    }

    public record StatusChange(OrderStatus Status, string? Reason);

    /// <summary>Next step or cancel. Anything else is refused with the reason (the flow is strictly sequential).</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, StatusChange body)
    {
        var order = await db.Orders.Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (body.Status == OrderStatus.Cancelled)
        {
            // Store cancellations return limited stock and the promo use, same as a customer cancelling.
            if (await checkout.CancelAsync(order, body.Reason, "Магазин") is { } err) return Conflict(new { error = err });
            return await Get(id);
        }
        if (await workflow.ChangeStatusAsync(order, body.Status) is { } error) return Conflict(new { error });
        return await Get(id);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] OrderFilter filter)
    {
        var orders = await LoadFull(filter);
        var bytes = OrderDocuments.ExportExcel(orders);
        return File(bytes, XlsxMime, $"orders_{Stamp(filter)}.xlsx");
    }

    [HttpGet("assembly-sheet")]
    public async Task<IActionResult> AssemblySheet([FromQuery] OrderFilter filter, string mode = "orders", string format = "pdf")
    {
        var orders = await LoadFull(filter);
        mode = mode == "products" ? "products" : "orders";
        if (format == "xlsx")
            return File(OrderDocuments.AssemblyExcel(orders, mode), XlsxMime, $"assembly_{mode}_{Stamp(filter)}.xlsx");

        var settings = await db.Settings.FirstAsync();
        var period = filter.From is null && filter.To is null ? "за всё время" : $"{filter.From:dd.MM.yyyy} — {filter.To:dd.MM.yyyy}";
        return File(OrderDocuments.AssemblyPdf(orders, mode, settings.StoreName, period), "application/pdf", $"assembly_{mode}_{Stamp(filter)}.pdf");
    }

    async Task<List<Order>> LoadFull(OrderFilter filter)
    {
        var q = await filter.ApplyAsync(db.Orders.AsNoTracking(), db);
        return await q.Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Items)
            .OrderBy(o => o.CreatedAt).AsSplitQuery().ToListAsync();
    }

    static string Stamp(OrderFilter f) => f.From is null && f.To is null
        ? "all"
        : $"{f.From:yyyyMMdd}-{f.To:yyyyMMdd}";
}
