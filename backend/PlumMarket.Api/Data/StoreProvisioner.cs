using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Data;

/// <summary>
/// The "готовый шаблон интернет-магазина" a new administrator gets right after registration: store settings,
/// one branch to fill in, and the order auto-replies. The catalog starts empty — categories, products, prices
/// and photos are what the administrator adds next, and the storefront shows them immediately.
/// </summary>
public static class StoreProvisioner
{
    public static void Provision(AppDbContext db, Store store, AdminUser admin)
    {
        db.Settings.Add(new StoreSettings
        {
            StoreId = store.Id,
            StoreName = store.Name,
            Phone = admin.Phone,
            WorkingHours = "Ежедневно 09:00\u201321:00",
            StoreDomain = $"{store.Slug}.plum.uz",
            Languages = "ru,uz",
            BonusEnabled = false,
            SpendPerPoint = 100,
            OverdueMinutes = 90,
            DeliveryFee = 15_000,
            FreeDeliveryFrom = 200_000,
            AboutText = $"{store.Name} \u2014 интернет-магазин. Расскажите здесь о себе: чем вы занимаетесь, " +
                "что продаёте и почему у вас стоит покупать. Текст меняется в разделе «Платформы → Веб-сайт».",
            DeliveryTerms = "## Самовывоз\nБесплатно из филиала магазина. Когда заказ будет собран, в разделе «Мои заказы» " +
                "появится статус «Готов к выдаче».\n\n## Доставка\nКурьер привезёт заказ в течение дня. Стоимость доставки " +
                "и сумма бесплатной доставки настраиваются в админ-панели.\n\n## Оплата\nНаличными при получении.",
            ReturnTerms = "## Отмена заказа\nПока магазин не подтвердил заказ (статус «Новый»), покупатель может отменить его сам " +
                "в разделе «Мои заказы». После подтверждения отмена возможна только через магазин.\n\n## Возврат и обмен\n" +
                "Опишите здесь свои условия возврата: в какой срок принимаете товар обратно и что для этого нужно.",
        });

        // A store needs at least one branch: stock, pickup and delivery are counted per branch.
        db.Branches.Add(new Branch
        {
            StoreId = store.Id,
            Name = "Основной филиал",
            Address = "",
            Phone = admin.Phone,
            WorkingHours = "Ежедневно 09:00\u201321:00",
            // Centre of Tashkent until the administrator moves the pin on the map.
            Lat = 41.3111,
            Lng = 69.2797,
        });

        foreach (var template in DefaultAutoReplies())
        {
            template.StoreId = store.Id;
            db.AutoReplyTemplates.Add(template);
        }
    }

    /// <summary>Order notifications every store starts with (the demo store reuses them).</summary>
    internal static IEnumerable<AutoReplyTemplate> DefaultAutoReplies()
    {
        var texts = new Dictionary<OrderStatus, (string Ru, string Uz, string En)>
        {
            [OrderStatus.New] = ("{name}, спасибо! Заказ №{order_id} на сумму {total} принят.",
                "{name}, rahmat! №{order_id} buyurtmangiz ({total}) qabul qilindi.",
                "{name}, thank you! Order #{order_id} for {total} has been received."),
            [OrderStatus.Assembling] = ("Заказ №{order_id} подтверждён и уже собирается.",
                "№{order_id} buyurtmangiz tasdiqlandi va yigʻilmoqda.",
                "Order #{order_id} is confirmed and being prepared."),
            [OrderStatus.Ready] = ("Заказ №{order_id} готов! Филиал: {branch}.",
                "№{order_id} buyurtmangiz tayyor! Filial: {branch}.",
                "Order #{order_id} is ready! Branch: {branch}."),
            [OrderStatus.HandedToCourier] = ("Заказ №{order_id} передан курьеру.",
                "№{order_id} buyurtma kuryerga topshirildi.",
                "Order #{order_id} has been handed to the courier."),
            [OrderStatus.OnTheWay] = ("Курьер выехал с заказом №{order_id}. Ожидайте!",
                "Kuryer №{order_id} buyurtma bilan yoʻlga chiqdi.",
                "The courier is on the way with order #{order_id}."),
            [OrderStatus.Delivered] = ("Заказ №{order_id} доставлен. Приятного аппетита!",
                "№{order_id} buyurtma yetkazildi. Yoqimli ishtaha!",
                "Order #{order_id} has been delivered. Enjoy!"),
            [OrderStatus.Completed] = ("Заказ №{order_id} завершён. Начислено бонусов: {bonus}. Спасибо, что выбрали нас!",
                "№{order_id} buyurtma yakunlandi. Bonus: {bonus}. Rahmat!",
                "Order #{order_id} completed. Bonus earned: {bonus}. Thank you!"),
            [OrderStatus.Cancelled] = ("Заказ №{order_id} отменён. Если это ошибка — напишите нам.",
                "№{order_id} buyurtma bekor qilindi. Xato boʻlsa, bizga yozing.",
                "Order #{order_id} was cancelled. If this is a mistake, message us."),
        };
        foreach (var (status, t) in texts)
        {
            const bool enabled = true;
            yield return new AutoReplyTemplate { Status = status, Language = "ru", Text = t.Ru, Enabled = enabled };
            yield return new AutoReplyTemplate { Status = status, Language = "uz", Text = t.Uz, Enabled = enabled };
            yield return new AutoReplyTemplate { Status = status, Language = "en", Text = t.En, Enabled = enabled };
        }
    }
}
