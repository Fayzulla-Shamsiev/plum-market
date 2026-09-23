using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Data;

/// <summary>
/// A ready-to-show store: an administrator account with ~13 months of trading behind it (catalog, customers,
/// orders in every status, reviews, chat) and a customer account with its own orders, so both sides of the
/// product can be demonstrated without setting anything up.
///
/// It is recreated on start-up only when it is missing, so a deploy always has it and a demo run on a database
/// that survives restarts keeps whatever happened during the presentation.
/// </summary>
public static class DemoData
{
    public const string StoreSlug = "demo";
    public const string StoreName = "Plum Bakery";

    /// <summary>Администратор демо-магазина.</summary>
    public const string AdminName = "Азиз Каримов";
    public const string AdminPhone = "+998901111111";
    public const string AdminPassword = "demo1234";

    /// <summary>Покупатель с историей заказов (витрина: вход по номеру и имени).</summary>
    public const string CustomerName = "Малика Юсупова";
    public const string CustomerPhone = "+998 90 222 22 22";

    public static bool Exists(AppDbContext db) => db.Stores.Any(s => s.Slug == StoreSlug);

    /// <summary>Creates the demo store unless it is already there. Returns true when it seeded.</summary>
    public static bool Ensure(AppDbContext db, StoreContext tenant)
    {
        if (Exists(db)) return false;

        var rnd = new Random(42); // deterministic: the same demo every time
        var now = DateTime.Now;

        var store = new Store { Name = StoreName, Slug = StoreSlug, CreatedAt = now.AddDays(-400) };
        db.Stores.Add(store);
        db.SaveChanges();
        // Everything added from here belongs to the demo store (AppDbContext stamps it).
        tenant.StoreId = store.Id;
        tenant.Store = store;

        db.Admins.Add(new AdminUser
        {
            Name = AdminName, Phone = AdminPhone, PasswordHash = AdminAuth.HashPassword(AdminPassword),
            StoreId = store.Id, CreatedAt = store.CreatedAt, LastLoginAt = now,
        });

        var settings = new StoreSettings
        {
            StoreName = StoreName,
            BotUsername = "plum_bakery_bot",
            BonusEnabled = true,
            SpendPerPoint = 100,
            StoreDomain = $"{StoreSlug}.plum.uz",
            Phone = "+998 78 113 82 12",
            WorkingHours = "Ежедневно 07:30–22:00",
            AboutText = "Plum Bakery — семейная пекарня из Ташкента. С 2019 года каждое утро печём круассаны, хлеб и самсу, " +
                "собираем торты и десерты вручную и варим кофе из свежей обжарки.\n\nУ нас три филиала: в каждом можно позавтракать, " +
                "забрать заказ самовывозом или оформить доставку по городу.",
            DeliveryTerms = "## Самовывоз\nБесплатно из любого филиала. Заказ собираем за 20–40 минут — статус «Готов к выдаче» появится в разделе «Мои заказы». " +
                "Храним готовый заказ до закрытия филиала.\n\n## Доставка по Ташкенту\nКурьер магазина привезёт заказ за 60–90 минут. Стоимость — 15 000 сум, " +
                "от 200 000 сум — бесплатно. Торты на заказ доставляем в согласованное время.\n\n## Получение\nОплата наличными курьеру или на кассе. " +
                "Проверьте заказ при получении; если чего-то не хватает, скажите курьеру или напишите нам в чат.",
            ReturnTerms = "## Отмена заказа\nПока магазин не подтвердил заказ (статус «Новый»), его можно отменить в разделе «Мои заказы». " +
                "После подтверждения заказ уже готовят — для отмены свяжитесь с нами.\n\n## Возврат и обмен\nСвежая выпечка, торты и напитки — " +
                "продукты с ограниченным сроком годности, поэтому вернуть их можно только если товар оказался некачественным или не тем, что вы заказывали. " +
                "Сообщите нам в течение 24 часов после получения и приложите фото.\n\n## Возврат денег\nЕсли заказ оплачен и возврат одобрен, " +
                "вернём деньги тем же способом в течение 3 рабочих дней; при оплате наличными — в филиале или курьером при следующем заказе.",
        };
        db.Settings.Add(settings);
        db.AutoReplyTemplates.AddRange(StoreProvisioner.DefaultAutoReplies());

        var branches = new List<Branch>
        {
            new() { Name = "Чиланзар", Address = "Чиланзар, 9-й квартал, ул. Бунёдкор 12", Phone = "+998 71 200 11 01", WorkingHours = "Ежедневно 07:30–22:00", Lat = 41.2756, Lng = 69.2034 },
            new() { Name = "Юнусабад", Address = "Юнусабад, 4-й квартал, ул. Амира Темура 108", Phone = "+998 71 200 11 02", WorkingHours = "Ежедневно 08:00–22:00", Lat = 41.3647, Lng = 69.2877 },
            new() { Name = "Мирзо-Улугбек", Address = "Мирзо-Улугбек, ул. Буюк Ипак Йули 45", Phone = "+998 71 200 11 03", WorkingHours = "Пн–Сб 08:00–21:00, Вс 09:00–20:00", Lat = 41.3285, Lng = 69.3345 },
        };
        db.Branches.AddRange(branches);

        db.Employees.AddRange(
            new Employee { Name = "Азиз Каримов", Phone = "+998 90 111 22 33", Role = "Менеджер" },
            new Employee { Name = "Нодира Юсупова", Phone = "+998 91 222 33 44", Role = "Оператор" },
            new Employee { Name = "Шерзод Рахимов", Phone = "+998 93 333 44 55", Role = "Оператор" },
            new Employee { Name = "Малика Турсунова", Phone = "+998 94 444 55 66", Role = "Сборщик" },
            new Employee { Name = "Бахтиёр Алиев", Phone = "+998 97 555 66 77", Role = "Сборщик" });

        db.SaveChanges(); // branch ids are needed for per-branch stock
        var products = DemoCatalog.SeedCatalog(db, rnd, branches, now);

        var customers = BuildCustomers(rnd, now);
        db.Customers.AddRange(customers);
        // A small group of regulars places a large share of orders — gives the Top-10 table some shape.
        var loyalty = customers.ToDictionary(c => c, _ => Math.Pow(rnd.NextDouble(), 3) * 10 + 0.3);
        db.SaveChanges(); // assigns product ids used by order lines

        var orders = BuildHistory(rnd, now, customers, loyalty, branches, products, settings);
        db.Orders.AddRange(orders.OrderBy(o => o.CreatedAt)); // ids follow chronology
        db.SaveChanges();

        DemoCatalog.SeedMarketing(db, rnd, products, customers, branches, now);
        DemoMarketing.Seed(db, products, now);
        DemoShopper.Seed(db, rnd, now, branches, products, settings);
        return true;
    }

    static List<Customer> BuildCustomers(Random rnd, DateTime now)
    {
        string[] firstNames = ["Азиз", "Дильшод", "Жасур", "Отабек", "Санжар", "Фаррух", "Бекзод", "Улугбек", "Тимур", "Рустам",
            "Шахзод", "Алишер", "Сардор", "Иван", "Дмитрий", "Нигора", "Малика", "Севара", "Дилноза", "Гулноза",
            "Мадина", "Камила", "Зарина", "Шахноза", "Лола", "Анна", "Екатерина", "Мохира", "Феруза", "Наргиза"];
        string[] lastNames = ["Каримов", "Рахимов", "Юсупов", "Алиев", "Турсунов", "Ахмедов", "Назаров", "Исмоилов", "Хасанов",
            "Султанов", "Мирзаев", "Абдуллаев", "Ибрагимов", "Садыков", "Холматов", "Петров", "Ким", "Пак"];
        string[] prefixes = ["90", "91", "93", "94", "95", "97", "98", "99", "33", "88"];

        var customers = new List<Customer>();
        for (var i = 0; i < 380; i++)
        {
            var first = firstNames[rnd.Next(firstNames.Length)];
            var female = "НигораМаликаСевараДилнозаГулнозаМадинаКамилаЗаринаШахнозаЛолаАннаЕкатеринаМохираФерузаНаргиза".Contains(first);
            var last = lastNames[rnd.Next(lastNames.Length)];
            if (female && !last.StartsWith("Ким") && !last.StartsWith("Пак")) last += "а";
            // Skew sign-ups towards recent months — the store is growing.
            var daysAgo = (int)(400 * Math.Pow(rnd.NextDouble(), 1.4));
            var created = now.Date.AddDays(-daysAgo).AddMinutes(rnd.Next(8 * 60, 23 * 60));
            if (created > now) created = now.AddHours(-2);
            customers.Add(new Customer
            {
                FullName = $"{first} {last}",
                Phone = $"+998 {prefixes[rnd.Next(prefixes.Length)]} {rnd.Next(100, 999)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}",
                Language = rnd.NextDouble() < 0.6 ? "ru" : "uz",
                // The MVP has one sales channel: the storefront.
                Platform = Platform.Website,
                CreatedAt = created,
                LastVisitAt = created,
            });
        }
        return customers.OrderBy(c => c.CreatedAt).ToList();
    }

    /// <summary>~13 months of orders, plus a live queue covering every step of the flow.</summary>
    static List<Order> BuildHistory(Random rnd, DateTime now, List<Customer> customers, Dictionary<Customer, double> loyalty,
        List<Branch> branches, List<Product> products, StoreSettings settings)
    {
        var start = now.Date.AddDays(-400);
        var orders = new List<Order>();
        for (var day = start; day <= now.Date; day = day.AddDays(1))
        {
            var progress = (day - start).TotalDays / 400.0;
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.35 : 1.0;
            var count = (int)Math.Round((3 + progress * 9) * weekend * (0.7 + rnd.NextDouble() * 0.6));
            for (var n = 0; n < count; n++)
            {
                var created = day.AddMinutes(rnd.Next(8 * 60, 23 * 60 + 30));
                if (created > now) continue;

                var eligible = customers.Where(c => c.CreatedAt <= created).ToList();
                if (eligible.Count == 0) continue;
                var customer = WeightedPick(rnd, eligible, c => loyalty[c]);

                var order = BuildOrder(rnd, created, customer, branches, products);
                order.Status = PickStatus(rnd, created, now, order.DeliveryType);
                order.StatusChangedAt = order.Status is OrderStatus.Completed or OrderStatus.Cancelled
                    ? created.AddMinutes(rnd.Next(25, 140))
                    : created.AddMinutes(Math.Min(rnd.Next(1, 20), Math.Max(1, (now - created).TotalMinutes)));
                if (order.StatusChangedAt > now) order.StatusChangedAt = now;

                if (order.Status == OrderStatus.Completed && settings.BonusEnabled)
                {
                    order.BonusEarned = order.Total / settings.SpendPerPoint;
                    customer.BonusPoints += order.BonusEarned;
                }
                if (created > customer.LastVisitAt) customer.LastVisitAt = created.AddMinutes(-rnd.Next(1, 15));
                AddHistory(rnd, order);
                orders.Add(order);
            }
        }

        // Guarantee a live queue across every step of the flow, whatever time of day the demo is created.
        // The first two "New" ones are old enough to show as overdue.
        (OrderStatus Status, int MinutesAgo, DeliveryType Type)[] live =
        [
            (OrderStatus.New, 2, DeliveryType.Delivery), (OrderStatus.New, 6, DeliveryType.Pickup), (OrderStatus.New, 11, DeliveryType.Delivery),
            (OrderStatus.New, 104, DeliveryType.Delivery), (OrderStatus.Assembling, 131, DeliveryType.Pickup),
            (OrderStatus.Assembling, 14, DeliveryType.Delivery), (OrderStatus.Assembling, 23, DeliveryType.Pickup),
            (OrderStatus.Ready, 29, DeliveryType.Delivery), (OrderStatus.Ready, 44, DeliveryType.Pickup),
            (OrderStatus.HandedToCourier, 38, DeliveryType.Delivery), (OrderStatus.HandedToCourier, 47, DeliveryType.Delivery),
            (OrderStatus.OnTheWay, 52, DeliveryType.Delivery), (OrderStatus.OnTheWay, 66, DeliveryType.Delivery),
            (OrderStatus.Delivered, 74, DeliveryType.Delivery), (OrderStatus.Delivered, 88, DeliveryType.Delivery),
        ];
        foreach (var (status, minutesAgo, type) in live)
        {
            var created = now.AddMinutes(-minutesAgo);
            var customer = WeightedPick(rnd, customers.Where(c => c.CreatedAt <= created).ToList(), c => loyalty[c]);
            var order = BuildOrder(rnd, created, customer, branches, products, type);
            order.Status = status;
            order.StatusChangedAt = status == OrderStatus.New ? created : created.AddMinutes(Math.Min(minutesAgo * 0.8, minutesAgo - 1));
            if (created > customer.LastVisitAt) customer.LastVisitAt = created;
            AddHistory(rnd, order);
            orders.Add(order);
        }

        // Customers spend some of their points.
        foreach (var c in customers.Where(c => c.BonusPoints > 0))
            c.BonusPoints = (long)(c.BonusPoints * (0.3 + rnd.NextDouble() * 0.7));
        return orders;
    }

    internal static Order BuildOrder(Random rnd, DateTime created, Customer customer, List<Branch> branches,
        List<Product> products, DeliveryType? type = null)
    {
        var branch = branches[rnd.Next(branches.Count)];
        var delivery = type ?? (rnd.NextDouble() < 0.62 ? DeliveryType.Delivery : DeliveryType.Pickup);
        var order = new Order
        {
            Customer = customer,
            Branch = branch,
            CreatedAt = created,
            // MVP: storefront orders paid in cash on receipt.
            Platform = Platform.Website,
            PaymentMethod = PaymentMethod.Cash,
            DeliveryType = delivery,
            RecipientName = customer.FullName,
            RecipientPhone = customer.Phone,
        };
        var lines = rnd.Next(1, 5);
        foreach (var p in products.Where(p => p.IsActive).OrderBy(_ => rnd.Next()).Take(lines))
            order.Items.Add(new OrderItem
            {
                ProductId = p.Id, ProductName = p.Name.Get(), Price = p.Price, CostPrice = p.CostPrice,
                Quantity = rnd.NextDouble() < 0.7 ? 1 : rnd.Next(2, 5),
            });
        order.Subtotal = order.Items.Sum(i => i.Price * i.Quantity);
        order.CostTotal = order.Items.Sum(i => i.CostPrice * i.Quantity);
        if (delivery == DeliveryType.Delivery)
        {
            order.DeliveryCost = order.Subtotal >= 200_000 ? 0 : 15_000;
            order.Lat = branch.Lat + (rnd.NextDouble() - 0.5) * 0.09;
            order.Lng = branch.Lng + (rnd.NextDouble() - 0.5) * 0.11;
            order.Address = $"г. Ташкент, ул. {Streets[rnd.Next(Streets.Length)]}, д. {rnd.Next(1, 120)}, кв. {rnd.Next(1, 80)}";
        }
        order.Total = order.Subtotal + order.DeliveryCost;
        if (rnd.NextDouble() < 0.15) order.Comment = Comments[rnd.Next(Comments.Length)];
        return order;
    }

    /// <summary>Old orders are finished; today's are spread over the steps of their own flow.</summary>
    static OrderStatus PickStatus(Random rnd, DateTime created, DateTime now, DeliveryType type)
    {
        var age = (now - created).TotalMinutes;
        if (age > 24 * 60 || (age > 180 && rnd.NextDouble() < 0.9))
            return rnd.NextDouble() < 0.08 ? OrderStatus.Cancelled : OrderStatus.Completed;
        var steps = OrderFlow.Steps(type);
        return steps[rnd.Next(steps.Count - 1)]; // any step except Completed
    }

    /// <summary>Status history consistent with the flow: every step up to the current one, spread over the order's life.</summary>
    internal static void AddHistory(Random rnd, Order order)
    {
        var start = order.CreatedAt;
        var end = order.StatusChangedAt < start ? start : order.StatusChangedAt;
        var path = order.Status == OrderStatus.Cancelled
            ? (rnd.NextDouble() < 0.6 ? [OrderStatus.New, OrderStatus.Cancelled] : new[] { OrderStatus.New, OrderStatus.Assembling, OrderStatus.Cancelled })
            : OrderFlow.Steps(order.DeliveryType).TakeWhile(s => s != order.Status).Append(order.Status).ToArray();
        for (var i = 0; i < path.Length; i++)
        {
            var at = path.Length == 1 ? start : start + (end - start) * i / (path.Length - 1);
            order.History.Add(new OrderStatusChange { Status = path[i], At = at, By = i == 0 || (path[i] == OrderStatus.Cancelled && rnd.NextDouble() < 0.5) ? "Покупатель" : "Магазин" });
        }
    }

    static T WeightedPick<T>(Random rnd, List<T> items, Func<T, double> weight)
    {
        var total = items.Sum(weight);
        var roll = rnd.NextDouble() * total;
        foreach (var item in items)
        {
            roll -= weight(item);
            if (roll <= 0) return item;
        }
        return items[^1];
    }

    internal static readonly string[] Streets = ["Навои", "Бабура", "Шота Руставели", "Мукими", "Катартал", "Фархадская",
        "Бунёдкор", "Амира Темура", "Мустакиллик", "Нукусская", "Шахрисабзская", "Ойбека", "Беруни"];

    static readonly string[] Comments = ["Позвоните за 10 минут", "Без лука, пожалуйста", "Домофон не работает",
        "Нужна сдача со 200 000", "Подпишите открытку: С днём рождения!", "Оставить у двери"];
}
