using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>Storefront checkout and "Мои заказы". Orders placed here show up in the admin's Заказы as platform «Сайт».</summary>
[ApiController]
[Route("api/shop")]
public class ShopOrdersController(AppDbContext db, ShopAuth auth, CheckoutService checkout) : ControllerBase
{
    static readonly OrderStatus[] Finished = [OrderStatus.Completed, OrderStatus.Cancelled];

    /// <summary>Live checkout totals. Works signed out too (promo "first order" checks then assume a new customer).</summary>
    [HttpPost("checkout/quote")]
    public async Task<IActionResult> Quote(CheckoutService.Request body, string lang = "ru")
    {
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        return Ok(await checkout.QuoteAsync(cat, body, await auth.CurrentAsync(Request)));
    }

    public record PlaceBody(CheckoutService.Request Order, string RecipientName, string RecipientPhone, string? Address,
        string? AddressDetails, string? Comment, PaymentMethod PaymentMethod = PaymentMethod.Cash);

    [HttpPost("orders")]
    public async Task<IActionResult> Place(PlaceBody body, string lang = "ru")
    {
        if (await auth.CurrentAsync(Request) is not { } customer) return Unauthorized(new { error = "Войдите по номеру телефона" });
        var cat = await StorefrontCatalog.LoadAsync(db, lang);
        var (order, quote, error) = await checkout.PlaceAsync(cat,
            new CheckoutService.PlaceRequest(body.Order, body.RecipientName, body.RecipientPhone, body.Address, body.AddressDetails,
                body.Comment, body.PaymentMethod, lang), customer);
        if (order is null) return UnprocessableEntity(new { error, quote });
        return Ok(await View(order.Id, customer));
    }

    /// <summary>scope: active (default) | all.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> List(string scope = "active", int page = 1, int pageSize = 10)
    {
        if (await auth.CurrentAsync(Request) is not { } customer) return Unauthorized(new { error = "Войдите по номеру телефона" });
        var q = db.Orders.AsNoTracking().Where(o => o.CustomerId == customer.Id);
        var activeCount = await q.CountAsync(o => !Finished.Contains(o.Status));
        var allCount = await q.CountAsync();
        if (scope != "all") q = q.Where(o => !Finished.Contains(o.Status));
        var total = await q.CountAsync();
        pageSize = Math.Clamp(pageSize, 1, 50);
        var ids = await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(o => o.Id).ToListAsync();
        var items = new List<object>();
        foreach (var id in ids) items.Add(await View(id, customer));
        return Ok(new { activeCount, allCount, total, page, items });
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (await auth.CurrentAsync(Request) is not { } customer) return Unauthorized(new { error = "Войдите по номеру телефона" });
        return await db.Orders.AnyAsync(o => o.Id == id && o.CustomerId == customer.Id) ? Ok(await View(id, customer)) : NotFound(new { error = "Заказ не найден" });
    }

    public record CancelBody(string? Reason);

    [HttpPost("orders/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelBody body)
    {
        if (await auth.CurrentAsync(Request) is not { } customer) return Unauthorized(new { error = "Войдите по номеру телефона" });
        var order = await db.Orders.Include(o => o.Customer).Include(o => o.Branch).Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customer.Id);
        if (order is null) return NotFound(new { error = "Заказ не найден" });
        if (await checkout.CancelAsync(order, body.Reason) is { } error) return Conflict(new { error });
        return Ok(await View(id, customer));
    }

    async Task<object> View(int id, Customer customer)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Branch).Include(x => x.Items).FirstAsync(x => x.Id == id);
        var productIds = o.Items.Select(i => i.ProductId).ToList();
        var images = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Media.FirstOrDefault(m => m.Type == "image")?.Url);
        return new
        {
            o.Id, o.Status, o.CreatedAt, o.StatusChangedAt, o.DeliveryType, o.PaymentMethod,
            branch = new { o.Branch.Id, o.Branch.Name, o.Branch.Address },
            o.Address, o.Comment,
            recipientName = o.RecipientName ?? customer.FullName,
            recipientPhone = o.RecipientPhone ?? customer.Phone,
            itemsCount = o.Items.Sum(i => i.Quantity),
            o.Subtotal, o.PromoCode, o.PromoDiscount, o.DeliveryCost, o.Total,
            canCancel = CheckoutService.Cancellable.Contains(o.Status),
            o.CancelReason,
            items = o.Items.Select(i => new
            {
                i.ProductId,
                // Stored with the variant appended for the admin; the storefront shows it separately.
                name = i.Variant is { } v && i.ProductName.EndsWith($" ({v})") ? i.ProductName[..^(v.Length + 3)] : i.ProductName,
                i.Variant, qty = i.Quantity, i.Price, sum = i.Price * i.Quantity, image = images.GetValueOrDefault(i.ProductId),
            }),
        };
    }
}
