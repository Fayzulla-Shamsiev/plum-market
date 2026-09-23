using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Order lifecycle: enforces the step-by-step flow (<see cref="OrderFlow"/>), keeps the status history, handles bonus
/// points on completion and sends the auto-reply for each status into the customer's chat with the store.
/// </summary>
public class OrderWorkflow(AppDbContext db)
{
    /// <summary>
    /// Moves an order to <paramref name="status"/> if the flow allows it: the next step, or a cancellation while that's
    /// still possible for whoever asks. Records the change, handles bonus points and tells the customer.
    /// Returns an error message instead of changing anything when the move isn't allowed.
    /// </summary>
    public async Task<string?> ChangeStatusAsync(Order order, OrderStatus status, string by = "Магазин", bool force = false)
    {
        if (order.Status == status) return null;
        if (!force)
        {
            if (status == OrderStatus.Cancelled)
            {
                var allowed = by == "Покупатель" ? OrderFlow.CustomerCanCancel(order.Status) : OrderFlow.StoreCanCancel(order.Status);
                if (!allowed) return by == "Покупатель"
                    ? "Магазин уже принял заказ — для отмены свяжитесь с нами"
                    : $"Заказ в статусе «{OrderFlow.Label(order.Status, order.DeliveryType)}» уже нельзя отменить";
            }
            else if (OrderFlow.Next(order) != status)
                return $"Следующий шаг для этого заказа — «{(OrderFlow.Next(order) is { } n ? OrderFlow.Label(n, order.DeliveryType) : "нет")}»";
        }

        var settings = await db.Settings.FirstAsync();
        order.Status = status;
        order.StatusChangedAt = DateTime.Now;
        db.Add(new OrderStatusChange { OrderId = order.Id, Status = status, At = order.StatusChangedAt, By = by });

        long bonus = 0;
        if (status == OrderStatus.Completed && settings.BonusEnabled && order.BonusEarned == 0 && settings.SpendPerPoint > 0)
        {
            bonus = order.Total / settings.SpendPerPoint;
            order.Customer.BonusPoints += bonus;
            order.BonusEarned = bonus;
        }
        // Undo the accrual if a completed order gets cancelled.
        if (status != OrderStatus.Completed && order.BonusEarned > 0)
        {
            order.Customer.BonusPoints = Math.Max(0, order.Customer.BonusPoints - order.BonusEarned);
            order.BonusEarned = 0;
        }

        await QueueNotificationAsync(order, bonus);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>A freshly placed order: first history entry and the "Новый" auto-reply.</summary>
    public async Task OnCreatedAsync(Order order)
    {
        db.Add(new OrderStatusChange { OrderId = order.Id, Status = order.Status, At = order.CreatedAt, By = "Покупатель" });
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

        var text = Render(template.Text, order, bonus);
        db.Notifications.Add(new NotificationLog
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Channel = "Чат магазина",
            Language = template.Language,
            SentAt = DateTime.Now,
            Text = text,
        });

        // The message arrives in the customer's chat with the store (storefront → Профиль → Связаться с нами → Чат),
        // with a link to the order, so the admin also sees in the inbox what the customer was told.
        var conv = await db.Conversations.Where(c => c.CustomerId == order.CustomerId && c.ReviewId == null && c.Channel == ChatChannel.Website)
            .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync();
        if (conv is null)
        {
            conv = new Conversation
            {
                CustomerId = order.CustomerId, Channel = ChatChannel.Website, DisplayName = order.Customer.FullName, CreatedAt = DateTime.Now,
            };
            db.Conversations.Add(conv);
        }
        var body = $"📦 Заказ №{order.Id} (/profile/orders/{order.Id})\n{text}";
        conv.Messages.Add(new ChatMessage { Direction = MessageDirection.Out, Text = body, IsAuto = true, SenderName = "Статус заказа", SentAt = DateTime.Now });
        conv.LastMessageAt = DateTime.Now;
        conv.LastMessageText = text;
    }

    public static string Render(string text, Order order, long bonus) => text
        .Replace("{name}", order.Customer.FullName.Split(' ')[0])
        .Replace("{order_id}", order.Id.ToString())
        .Replace("{total}", Money(order.Total))
        .Replace("{branch}", order.Branch?.Name ?? "")
        .Replace("{bonus}", bonus.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' '));

    public static string Money(long v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ') + " сум";
}
