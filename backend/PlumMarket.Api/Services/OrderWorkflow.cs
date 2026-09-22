using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Order lifecycle side effects: overdue detection, bonus accrual on completion and
/// auto-responder notifications to the customer on every status change.
/// </summary>
public class OrderWorkflow(AppDbContext db)
{
    static readonly OrderStatus[] OverdueCandidates = [OrderStatus.New, OrderStatus.InProgress];

    /// <summary>Marks stale New/InProgress orders as overdue. Cheap enough to run before each orders query.</summary>
    public async Task SweepOverdueAsync()
    {
        var settings = await db.Settings.FirstAsync();
        var threshold = DateTime.Now.AddMinutes(-settings.OverdueMinutes);
        var stale = await db.Orders.Include(o => o.Customer).Include(o => o.Branch)
            .Where(o => OverdueCandidates.Contains(o.Status) && o.CreatedAt < threshold)
            .ToListAsync();
        foreach (var o in stale) await ApplyStatusAsync(o, OrderStatus.Overdue, settings);
        if (stale.Count > 0) await db.SaveChangesAsync();
    }

    public async Task ChangeStatusAsync(Order order, OrderStatus status)
    {
        var settings = await db.Settings.FirstAsync();
        await ApplyStatusAsync(order, status, settings);
        await db.SaveChangesAsync();
    }

    async Task ApplyStatusAsync(Order order, OrderStatus status, StoreSettings settings)
    {
        if (order.Status == status) return;
        order.Status = status;
        order.StatusChangedAt = DateTime.Now;

        long bonus = 0;
        if (status == OrderStatus.Completed && settings.BonusEnabled && order.BonusEarned == 0 && settings.SpendPerPoint > 0)
        {
            bonus = order.Total / settings.SpendPerPoint;
            order.Customer.BonusPoints += bonus;
            order.BonusEarned = bonus;
        }
        // Undo the accrual if a completed order gets reopened or cancelled.
        if (status != OrderStatus.Completed && order.BonusEarned > 0)
        {
            order.Customer.BonusPoints = Math.Max(0, order.Customer.BonusPoints - order.BonusEarned);
            order.BonusEarned = 0;
        }

        await QueueNotificationAsync(order, bonus);
    }

    /// <summary>Sends the "New" auto-reply for a freshly placed order.</summary>
    public async Task OnCreatedAsync(Order order)
    {
        await QueueNotificationAsync(order, 0);
        await db.SaveChangesAsync();
    }

    async Task QueueNotificationAsync(Order order, long bonus)
    {
        // The customer turned order updates off in the storefront settings.
        if (!order.Customer.NotifyOrders) return;
        var status = order.Status;
        var lang = order.Customer.Language;
        var template = await db.AutoReplyTemplates.FirstOrDefaultAsync(t => t.Status == status && t.Language == lang)
                       ?? await db.AutoReplyTemplates.FirstOrDefaultAsync(t => t.Status == status && t.Language == "ru");
        if (template is not { Enabled: true } || string.IsNullOrWhiteSpace(template.Text)) return;

        db.Notifications.Add(new NotificationLog
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Channel = order.Platform.ToString(),
            Language = template.Language,
            SentAt = DateTime.Now,
            Text = Render(template.Text, order, bonus),
        });
    }

    public static string Render(string text, Order order, long bonus) => text
        .Replace("{name}", order.Customer.FullName.Split(' ')[0])
        .Replace("{order_id}", order.Id.ToString())
        .Replace("{total}", Money(order.Total))
        .Replace("{branch}", order.Branch?.Name ?? "")
        .Replace("{bonus}", bonus.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' '));

    public static string Money(long v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ') + " сум";
}
