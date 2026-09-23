using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Data;

/// <summary>
/// The demo customer: the account you sign into on the storefront (номер телефона + имя) to see a profile that
/// is already lived-in — an order on its way, an order waiting for the store, finished orders, bonuses, reviews
/// to leave, a saved address and a chat with the store.
/// </summary>
public static class DemoShopper
{
    public static void Seed(AppDbContext db, Random rnd, DateTime now, List<Branch> branches, List<Product> products, StoreSettings settings)
    {
        var customer = new Customer
        {
            FullName = DemoData.CustomerName,
            Phone = DemoData.CustomerPhone,
            Email = "malika.demo@plum.uz",
            Country = "Узбекистан",
            BirthDate = new DateOnly(1996, 4, 18),
            Gender = "female",
            Language = "ru",
            Platform = Platform.Website,
            CreatedAt = now.AddDays(-210),
            LastVisitAt = now.AddMinutes(-12),
        };
        db.Customers.Add(customer);
        db.SaveChanges();

        db.CustomerAddresses.Add(new CustomerAddress
        {
            CustomerId = customer.Id,
            Address = "г. Ташкент, ул. Амира Темура, д. 42",
            Details = "кв. 17, 4 этаж, домофон 17К",
            Lat = 41.3187,
            Lng = 69.2797,
            LastUsedAt = now.AddDays(-4),
        });

        // Finished orders first — they are what fills «Мои отзывы» and the bonus balance.
        var orders = new List<Order>();
        foreach (var daysAgo in new[] { 96, 61, 34, 17, 4 })
        {
            var order = Make(rnd, now.AddDays(-daysAgo).AddHours(rnd.Next(9, 20)), customer, branches, products,
                daysAgo % 2 == 0 ? DeliveryType.Delivery : DeliveryType.Pickup);
            order.Status = OrderStatus.Completed;
            order.StatusChangedAt = order.CreatedAt.AddMinutes(rnd.Next(45, 130));
            order.BonusEarned = order.Total / settings.SpendPerPoint;
            customer.BonusPoints += order.BonusEarned;
            orders.Add(order);
        }

        var cancelled = Make(rnd, now.AddDays(-23).AddHours(12), customer, branches, products, DeliveryType.Delivery);
        cancelled.Status = OrderStatus.Cancelled;
        cancelled.StatusChangedAt = cancelled.CreatedAt.AddMinutes(9);
        cancelled.CancelReason = "Передумала, закажу позже";
        orders.Add(cancelled);

        // …and two live ones: one the store is already delivering, one it has not confirmed yet (the customer
        // can still cancel that one themselves).
        var onTheWay = Make(rnd, now.AddMinutes(-52), customer, branches, products, DeliveryType.Delivery);
        onTheWay.Status = OrderStatus.OnTheWay;
        onTheWay.StatusChangedAt = now.AddMinutes(-9);
        onTheWay.Comment = "Позвоните, пожалуйста, за 10 минут";
        orders.Add(onTheWay);

        var fresh = Make(rnd, now.AddMinutes(-7), customer, branches, products, DeliveryType.Pickup);
        fresh.Status = OrderStatus.New;
        fresh.StatusChangedAt = fresh.CreatedAt;
        orders.Add(fresh);

        foreach (var o in orders) DemoData.AddHistory(rnd, o);
        db.Orders.AddRange(orders.OrderBy(o => o.CreatedAt));
        customer.BonusPoints -= customer.BonusPoints / 4; // some points already spent
        db.SaveChanges();

        // Two products already rated (one with the store's answer); everything else bought stays in «Оценить».
        var reviewed = orders.Where(o => o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt).Take(2)
            .Select(o => o.Items[0].ProductId).Distinct().ToList();
        var replies = new[]
        {
            ("Всё как всегда свежее, спасибо! Забирала самовывозом, собрали за 15 минут.", 5, "Спасибо, Малика! Всегда рады вам 🙂"),
            ("Вкусно, но в этот раз десерт приехал немного помятым.", 4, (string?)null),
        };
        for (var i = 0; i < reviewed.Count && i < replies.Length; i++)
        {
            var (comment, rating, reply) = replies[i];
            var created = now.AddDays(-12 + i * 5);
            db.Reviews.Add(new Review
            {
                ProductId = reviewed[i],
                CustomerId = customer.Id,
                Rating = rating,
                Comment = comment,
                Status = reply is null ? ReviewStatus.New : ReviewStatus.Answered,
                Reply = reply,
                CreatedAt = created,
                RepliedAt = reply is null ? null : created.AddHours(2),
            });
        }

        // A chat the store has already answered, so «Связаться с нами» opens onto a conversation.
        var conversation = new Conversation
        {
            CustomerId = customer.Id,
            Channel = ChatChannel.Website,
            DisplayName = customer.FullName,
            CreatedAt = now.AddDays(-4).AddHours(-1),
        };
        conversation.Messages.Add(new ChatMessage
        {
            Direction = MessageDirection.In, Text = "Здравствуйте! Можно заказать торт Медовик на субботу к 15:00?",
            SentAt = now.AddDays(-4).AddHours(-1),
        });
        conversation.Messages.Add(new ChatMessage
        {
            Direction = MessageDirection.Out, Text = "Здравствуйте, Малика! Да, конечно — оформите заказ за сутки, и мы соберём его к 15:00.",
            SentAt = now.AddDays(-4).AddMinutes(-35), SenderName = "Нодира Юсупова",
        });
        conversation.LastMessageAt = conversation.Messages.Max(m => m.SentAt);
        conversation.LastMessageText = conversation.Messages.MaxBy(m => m.SentAt)!.Text;
        db.Conversations.Add(conversation);
        db.SaveChanges();
    }

    static Order Make(Random rnd, DateTime created, Customer customer, List<Branch> branches,
        List<Product> products, DeliveryType type)
    {
        var order = DemoData.BuildOrder(rnd, created, customer, branches, products, type);
        if (type == DeliveryType.Delivery)
        {
            order.Address = "г. Ташкент, ул. Амира Темура, д. 42, кв. 17";
            order.Lat = 41.3187;
            order.Lng = 69.2797;
            order.Total = order.Subtotal + order.DeliveryCost;
        }
        return order;
    }
}
