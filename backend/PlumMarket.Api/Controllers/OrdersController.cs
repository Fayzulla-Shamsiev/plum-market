using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(AppDbContext db, OrderWorkflow workflow) : ControllerBase
{
    const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderFilter filter, int page = 1, int pageSize = 20)
    {
        await workflow.SweepOverdueAsync();
        pageSize = Math.Clamp(pageSize, 5, 100);

        var baseQuery = await filter.ApplyAsync(db.Orders.AsNoTracking(), db);
        var byStatus = await baseQuery.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var counts = OrderFilter.Tabs.ToDictionary(t => t.Key,
            t => byStatus.Where(s => t.Value.Contains(s.Key)).Sum(s => s.Count));

        var q = filter.ApplyTab(baseQuery);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.CreatedAt, o.Status, o.Platform, o.PaymentMethod, o.DeliveryType, o.Total,
                Customer = new { o.Customer.Id, o.Customer.FullName, o.Customer.Phone },
                Branch = o.Branch.Name,
                Employee = o.Employee != null ? o.Employee.Name : null,
                ItemsCount = o.Items.Sum(i => i.Quantity),
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize, counts });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var o = await db.Orders.AsNoTracking()
            .Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Employee).Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return NotFound();
        var notifications = await db.Notifications.AsNoTracking().Where(n => n.OrderId == id)
            .OrderByDescending(n => n.SentAt).ToListAsync();
        return Ok(new
        {
            o.Id, o.CreatedAt, o.StatusChangedAt, o.Status, o.Platform, o.PaymentMethod, o.DeliveryType,
            o.Subtotal, o.DeliveryCost, o.Total, o.CostTotal, o.BonusEarned, o.Address, o.Lat, o.Lng, o.Comment,
            o.RecipientName, o.RecipientPhone, o.PromoCode, o.PromoDiscount, o.CancelReason,
            Customer = new { o.Customer.Id, o.Customer.FullName, o.Customer.Phone, o.Customer.Username, o.Customer.Language, o.Customer.BonusPoints },
            Branch = new { o.Branch.Id, o.Branch.Name, o.Branch.Address },
            Employee = o.Employee is null ? null : new { o.Employee.Id, o.Employee.Name },
            Items = o.Items.Select(i => new { i.ProductName, i.Quantity, i.Price, Sum = i.Price * i.Quantity }),
            Notifications = notifications,
        });
    }

    public record StatusChange(OrderStatus Status);

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, StatusChange body)
    {
        var order = await db.Orders.Include(o => o.Customer).Include(o => o.Branch).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        await workflow.ChangeStatusAsync(order, body.Status);
        return await Get(id);
    }

    public record EmployeeChange(int? EmployeeId);

    [HttpPatch("{id:int}/employee")]
    public async Task<IActionResult> AssignEmployee(int id, EmployeeChange body)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        if (body.EmployeeId is { } e && !await db.Employees.AnyAsync(x => x.Id == e)) return BadRequest("Unknown employee");
        order.EmployeeId = body.EmployeeId;
        await db.SaveChangesAsync();
        return await Get(id);
    }

    /// <summary>Demo helper: creates a random incoming order as if it came from the storefront / bot.</summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate()
    {
        var rnd = Random.Shared;
        var customers = await db.Customers.ToListAsync();
        var customer = customers[rnd.Next(customers.Count)];
        var branches = await db.Branches.ToListAsync();
        var branch = branches[rnd.Next(branches.Count)];
        var products = await db.Products.ToListAsync();
        var delivery = rnd.NextDouble() < 0.6 ? DeliveryType.Delivery : DeliveryType.Pickup;

        var order = new Order
        {
            Customer = customer, Branch = branch, CreatedAt = DateTime.Now, StatusChangedAt = DateTime.Now,
            Status = OrderStatus.New, Platform = customer.Platform, DeliveryType = delivery,
            PaymentMethod = (PaymentMethod)rnd.Next(4),
        };
        foreach (var p in products.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
            order.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name.Get(), Price = p.Price, CostPrice = p.CostPrice, Quantity = rnd.Next(1, 3) });
        order.Subtotal = order.Items.Sum(i => i.Price * i.Quantity);
        order.CostTotal = order.Items.Sum(i => i.CostPrice * i.Quantity);
        if (delivery == DeliveryType.Delivery)
        {
            order.DeliveryCost = order.Subtotal >= 200_000 ? 0 : 15_000;
            order.Lat = branch.Lat + (rnd.NextDouble() - 0.5) * 0.08;
            order.Lng = branch.Lng + (rnd.NextDouble() - 0.5) * 0.1;
            order.Address = "г. Ташкент, ул. Навои, д. " + rnd.Next(1, 90);
        }
        order.Total = order.Subtotal + order.DeliveryCost;
        customer.LastVisitAt = DateTime.Now;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await workflow.OnCreatedAsync(order);
        return await Get(order.Id);
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
        return await q.Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Employee).Include(o => o.Items)
            .OrderBy(o => o.CreatedAt).AsSplitQuery().ToListAsync();
    }

    static string Stamp(OrderFilter f) => f.From is null && f.To is null
        ? "all"
        : $"{f.From:yyyyMMdd}-{f.To:yyyyMMdd}";
}
