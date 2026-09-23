using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Storefront checkout. Given the cart, how it's received (pickup / delivery) and an optional promo code, it:
/// picks the branch (the one chosen for pickup; for delivery the nearest one that has everything), prices every
/// line for that branch (branch-only and "from N сум" discounts apply here), applies the promo code, adds the delivery
/// fee and, on confirmation, creates the order in the admin's order list and takes the items out of stock.
/// </summary>
public class CheckoutService(AppDbContext db, MarketingService marketing, OrderWorkflow workflow)
{
    public record Line(int ProductId, string? Variant, int Qty);

    public record Request(List<Line> Lines, DeliveryType DeliveryType, int? BranchId, double? Lat, double? Lng, string? PromoCode);

    public record QuoteLine(int ProductId, string? Variant, StorefrontCatalog.Card? Card, int Qty, long Price, long? OldPrice,
        long Sum, long OldSum, bool Available, string? Problem);

    public record BranchOption(int Id, string Name, string Address, double Lat, double Lng, bool CanFulfill, List<string> Missing);

    public record PromoResult(string Code, bool Applied, string? Error, long Discount);

    public record Quote(List<QuoteLine> Lines, List<BranchOption> Branches, int BranchId, DeliveryType DeliveryType,
        int ItemsCount, long Subtotal, long CatalogDiscount, long ItemsTotal, PromoResult? Promo, long DeliveryFee,
        long DeliveryFeeBase, long? FreeDeliveryFrom, long Total, List<string> Problems)
    {
        public bool CanPlace => Problems.Count == 0 && Lines.Count > 0;
    }

    public async Task<Quote> QuoteAsync(StorefrontCatalog cat, Request req, Customer? customer)
    {
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).ToListAsync();
        var byId = cat.Products.ToDictionary(p => p.Id);

        // Merge duplicates (same product + variant) and drop empty lines.
        var lines = req.Lines.Where(l => l.Qty > 0).GroupBy(l => (l.ProductId, l.Variant))
            .Select(g => new Line(g.Key.ProductId, g.Key.Variant, Math.Min(g.Sum(x => x.Qty), 999))).Take(100).ToList();
        var known = lines.Where(l => byId.ContainsKey(l.ProductId)).ToList();

        var options = branches.Select(b =>
        {
            var missing = known.Where(l => !StorefrontCatalog.CanFulfill(byId[l.ProductId], b.Id, l.Qty))
                .Select(l => cat.Name(byId[l.ProductId].Name)).ToList();
            return new BranchOption(b.Id, b.Name, b.Address, b.Lat, b.Lng, missing.Count == 0, missing);
        }).ToList();

        BranchOption? branch;
        if (req.DeliveryType == DeliveryType.Pickup)
            branch = options.FirstOrDefault(o => o.Id == req.BranchId) ?? options.FirstOrDefault(o => o.CanFulfill) ?? options.FirstOrDefault();
        else
        {
            // Delivery: the closest branch that has the whole cart; if none has it, the closest overall.
            var pool = options.Any(o => o.CanFulfill) ? options.Where(o => o.CanFulfill) : options;
            branch = req.Lat is { } lat && req.Lng is { } lng
                ? pool.MinBy(o => Math.Pow(o.Lat - lat, 2) + Math.Pow((o.Lng - lng) * Math.Cos(lat * Math.PI / 180), 2))
                : pool.FirstOrDefault();
        }
        if (branch is null) throw new InvalidOperationException("No branches configured");

        // Two passes: the cart total at branch prices decides which "from N сум" discounts apply.
        long amount = known.Sum(l => cat.PriceOf(byId[l.ProductId], VariantOf(byId[l.ProductId], l.Variant), branch.Id).Price * l.Qty);
        var problems = new List<string>();
        var quoteLines = lines.Select(l =>
        {
            if (!byId.TryGetValue(l.ProductId, out var p))
                return new QuoteLine(l.ProductId, l.Variant, null, l.Qty, 0, null, 0, 0, false, "Товар больше не продаётся");
            var variant = VariantOf(p, l.Variant);
            var (price, old, _) = cat.PriceOf(p, variant, branch.Id, amount);
            var ok = StorefrontCatalog.CanFulfill(p, branch.Id, l.Qty);
            string? problem = null;
            if (!ok)
            {
                var row = p.Stock.FirstOrDefault(s => s.BranchId == branch.Id);
                problem = row is { Status: StockStatus.Limited, Quantity: > 0 } ? $"В филиале осталось {row.Quantity} шт" : "Нет в этом филиале";
            }
            return new QuoteLine(l.ProductId, variant, cat.ToCard(p), l.Qty, price, old, price * l.Qty, (old ?? price) * l.Qty, ok, problem);
        }).ToList();

        if (quoteLines.Count == 0) problems.Add("Корзина пуста");
        if (quoteLines.Any(l => !l.Available))
            problems.Add(req.DeliveryType == DeliveryType.Pickup
                ? $"В филиале «{branch.Name}» есть не всё — выберите другой филиал или уберите товары"
                : "Часть товаров сейчас недоступна — уберите их из заказа");

        var live = quoteLines.Where(l => l.Available).ToList();
        var itemsTotal = live.Sum(l => l.Sum);
        var subtotal = live.Sum(l => l.OldSum);

        PromoResult? promo = null;
        if (!string.IsNullOrWhiteSpace(req.PromoCode))
        {
            promo = await ApplyPromoAsync(cat, req.PromoCode, live, byId, itemsTotal, customer);
            if (!promo.Applied) problems.Add(promo.Error ?? "Промокод не применён");
        }
        var afterPromo = itemsTotal - (promo?.Discount ?? 0);

        long fee = 0;
        if (req.DeliveryType == DeliveryType.Delivery)
            fee = settings.FreeDeliveryFrom is { } free && afterPromo >= free ? 0 : settings.DeliveryFee;

        return new Quote(quoteLines, options, branch.Id, req.DeliveryType, live.Sum(l => l.Qty), subtotal, subtotal - itemsTotal,
            itemsTotal, promo, fee, settings.DeliveryFee, settings.FreeDeliveryFrom, afterPromo + fee, problems);
    }

    static string? VariantOf(Product p, string? requested) =>
        p.Variants.Any(v => v.Name == requested) ? requested : p.Variants.FirstOrDefault()?.Name;

    async Task<PromoResult> ApplyPromoAsync(StorefrontCatalog cat, string code, List<QuoteLine> live, Dictionary<int, Product> byId,
        long itemsTotal, Customer? customer)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var check = await marketing.CheckPromo(normalized, itemsTotal, customer?.Id, Platform.Website);
        if (!check.Valid) return new PromoResult(normalized, false, check.Error, 0);

        // A code limited to categories only discounts the matching items.
        var promo = await db.PromoCodes.AsNoTracking().FirstAsync(p => p.Code == normalized);
        if (promo.CategoryIds.Count == 0) return new PromoResult(normalized, true, null, check.Discount);
        var allowed = promo.CategoryIds.SelectMany(cat.Subtree).ToHashSet();
        var eligible = live.Where(l => byId[l.ProductId].CategoryId is { } c && allowed.Contains(c)).Sum(l => l.Sum);
        if (eligible == 0) return new PromoResult(normalized, false, "Промокод не действует на товары в корзине", 0);
        var discount = promo.Type == DiscountType.Percent ? eligible * promo.Value / 100 : promo.Value;
        if (promo.MaxDiscount is { } cap) discount = Math.Min(discount, cap);
        return new PromoResult(normalized, true, null, Math.Min(discount, eligible));
    }

    public record PlaceRequest(Request Order, string RecipientName, string RecipientPhone, string? Address, string? AddressDetails,
        string? Comment, PaymentMethod PaymentMethod, string Lang);

    /// <summary>Re-quotes, then creates the order. Returns the order, or the reason it can't be placed.</summary>
    public async Task<(Order? Order, Quote Quote, string? Error)> PlaceAsync(StorefrontCatalog cat, PlaceRequest req, Customer customer)
    {
        var quote = await QuoteAsync(cat, req.Order, customer);
        if (!quote.CanPlace) return (null, quote, quote.Problems.FirstOrDefault() ?? "Корзина пуста");
        if (req.PaymentMethod != PaymentMethod.Cash) return (null, quote, "Этот способ оплаты недоступен");
        if (string.IsNullOrWhiteSpace(req.RecipientName)) return (null, quote, "Укажите имя получателя");
        if (Phone.Normalize(req.RecipientPhone) is not { } recipientPhone) return (null, quote, "Проверьте номер телефона получателя");
        var isDelivery = req.Order.DeliveryType == DeliveryType.Delivery;
        if (isDelivery && string.IsNullOrWhiteSpace(req.Address)) return (null, quote, "Укажите адрес доставки");

        await using var tx = await db.Database.BeginTransactionAsync();
        var products = await db.Products.Include(p => p.Stock).Where(p => quote.Lines.Select(l => l.ProductId).Contains(p.Id)).ToListAsync();
        var now = DateTime.Now;
        var address = isDelivery
            ? string.Join(", ", new[] { req.Address!.Trim(), req.AddressDetails?.Trim() }.Where(x => !string.IsNullOrEmpty(x)))
            : null;
        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = quote.BranchId,
            CreatedAt = now,
            StatusChangedAt = now,
            Status = OrderStatus.New,
            Platform = Platform.Website,
            PaymentMethod = req.PaymentMethod,
            DeliveryType = req.Order.DeliveryType,
            Subtotal = quote.ItemsTotal,
            PromoCode = quote.Promo?.Applied == true ? quote.Promo.Code : null,
            PromoDiscount = quote.Promo?.Discount ?? 0,
            DeliveryCost = quote.DeliveryFee,
            Total = quote.Total,
            Address = address,
            Lat = isDelivery ? req.Order.Lat : null,
            Lng = isDelivery ? req.Order.Lng : null,
            Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim()[..Math.Min(req.Comment.Trim().Length, 500)],
            RecipientName = req.RecipientName.Trim()[..Math.Min(req.RecipientName.Trim().Length, 80)],
            RecipientPhone = recipientPhone,
        };
        foreach (var l in quote.Lines)
        {
            var p = products.First(x => x.Id == l.ProductId);
            order.Items.Add(new OrderItem
            {
                ProductId = p.Id,
                ProductName = l.Variant is null ? p.Name.Get() : $"{p.Name.Get()} ({l.Variant})",
                Variant = l.Variant,
                Quantity = l.Qty,
                Price = l.Price,
                CostPrice = p.CostPrice,
            });
            // Take limited stock out of the fulfilling branch.
            if (p.Stock.FirstOrDefault(s => s.BranchId == quote.BranchId) is { Status: StockStatus.Limited } row)
            {
                row.Quantity -= l.Qty;
                row.UpdatedAt = now;
            }
        }
        order.CostTotal = order.Items.Sum(i => i.CostPrice * i.Quantity);
        db.Orders.Add(order);

        if (order.PromoCode is { } code && await db.PromoCodes.FirstOrDefaultAsync(p => p.Code == code) is { } promo)
            promo.UsedCount++;

        var tracked = await db.Customers.FirstAsync(c => c.Id == customer.Id);
        tracked.LastVisitAt = now;
        tracked.Language = req.Lang == "ru" ? "ru" : "uz";
        if (isDelivery) await RememberAddressAsync(customer.Id, req.Address!.Trim(), req.AddressDetails?.Trim(), req.Order.Lat, req.Order.Lng);

        await db.SaveChangesAsync();
        order.Customer = tracked;
        order.Branch = await db.Branches.FirstAsync(b => b.Id == order.BranchId);
        await workflow.OnCreatedAsync(order);
        await tx.CommitAsync();
        return (order, quote, null);
    }

    async Task RememberAddressAsync(int customerId, string address, string? details, double? lat, double? lng)
    {
        var existing = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Address == address);
        if (existing is null)
            db.CustomerAddresses.Add(new CustomerAddress { CustomerId = customerId, Address = address, Details = details, Lat = lat, Lng = lng, LastUsedAt = DateTime.Now });
        else
        {
            existing.Details = details;
            existing.Lat = lat ?? existing.Lat;
            existing.Lng = lng ?? existing.Lng;
            existing.LastUsedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Cancels an order for the customer (only while it's still Новый) or the store (until it's delivered), see
    /// <see cref="OrderFlow"/>. Limited stock goes back to the branch and the promo code use is returned.
    /// </summary>
    public async Task<string?> CancelAsync(Order order, string? reason, string by = "Покупатель")
    {
        var allowed = by == "Покупатель" ? OrderFlow.CustomerCanCancel(order.Status) : OrderFlow.StoreCanCancel(order.Status);
        if (!allowed)
            return by == "Покупатель" ? "Магазин уже принял заказ — для отмены свяжитесь с нами"
                : $"Заказ в статусе «{OrderFlow.Label(order.Status, order.DeliveryType)}» уже нельзя отменить";
        var fallback = by == "Покупатель" ? "Отменён покупателем" : "Отменён магазином";
        order.CancelReason = string.IsNullOrWhiteSpace(reason) ? fallback : $"{fallback}: {reason.Trim()[..Math.Min(reason.Trim().Length, 300)]}";
        var ids = order.Items.Select(i => i.ProductId).ToList();
        var stock = await db.Stock.Where(s => s.BranchId == order.BranchId && ids.Contains(s.ProductId) && s.Status == StockStatus.Limited).ToListAsync();
        foreach (var i in order.Items)
            if (stock.FirstOrDefault(s => s.ProductId == i.ProductId) is { } row)
            {
                row.Quantity += i.Quantity;
                row.UpdatedAt = DateTime.Now;
            }
        if (order.PromoCode is { } code && await db.PromoCodes.FirstOrDefaultAsync(p => p.Code == code) is { UsedCount: > 0 } promo)
            promo.UsedCount--;
        return await workflow.ChangeStatusAsync(order, OrderStatus.Cancelled, by);
    }
}

/// <summary>Uzbek phone numbers in the one format the database uses: "+998 90 123 45 67".</summary>
public static class Phone
{
    public static string? Normalize(string? input)
    {
        var digits = new string((input ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("998")) digits = digits[3..];
        if (digits.Length != 9) return null;
        return $"+998 {digits[..2]} {digits[2..5]} {digits[5..7]} {digits[7..]}";
    }
}
