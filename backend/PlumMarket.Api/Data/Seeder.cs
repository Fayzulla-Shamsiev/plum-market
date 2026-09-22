using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Data;

/// <summary>Deterministic demo data: ~13 months of orders for a Tashkent bakery/café chain.</summary>
public static class Seeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Settings.Any()) return;

        var rnd = new Random(42);
        var now = DateTime.Now;

        var settings = new StoreSettings
        {
            StoreName = "Plum Bakery",
            BotUsername = "plum_bakery_bot",
            BonusEnabled = true,
            SpendPerPoint = 100,
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

        var branches = new List<Branch>
        {
            new() { Name = "Чиланзар", Address = "Чиланзар, 9-й квартал, ул. Бунёдкор 12", Phone = "+998 71 200 11 01", WorkingHours = "Ежедневно 07:30–22:00", Lat = 41.2756, Lng = 69.2034 },
            new() { Name = "Юнусабад", Address = "Юнусабад, 4-й квартал, ул. Амира Темура 108", Phone = "+998 71 200 11 02", WorkingHours = "Ежедневно 08:00–22:00", Lat = 41.3647, Lng = 69.2877 },
            new() { Name = "Мирзо-Улугбек", Address = "Мирзо-Улугбек, ул. Буюк Ипак Йули 45", Phone = "+998 71 200 11 03", WorkingHours = "Пн–Сб 08:00–21:00, Вс 09:00–20:00", Lat = 41.3285, Lng = 69.3345 },
        };
        db.Branches.AddRange(branches);

        var employees = new List<Employee>
        {
            new() { Name = "Азиз Каримов", Phone = "+998 90 111 22 33", Role = "Менеджер" },
            new() { Name = "Нодира Юсупова", Phone = "+998 91 222 33 44", Role = "Оператор" },
            new() { Name = "Шерзод Рахимов", Phone = "+998 93 333 44 55", Role = "Оператор" },
            new() { Name = "Малика Турсунова", Phone = "+998 94 444 55 66", Role = "Сборщик" },
            new() { Name = "Бахтиёр Алиев", Phone = "+998 97 555 66 77", Role = "Сборщик" },
        };
        db.Employees.AddRange(employees);

        db.SaveChanges(); // branch ids are needed for per-branch stock
        var products = CatalogSeed.SeedCatalog(db, rnd, branches, now);

        string[] firstNames = ["Азиз", "Дильшод", "Жасур", "Отабек", "Санжар", "Фаррух", "Бекзод", "Улугбек", "Тимур", "Рустам",
            "Шахзод", "Алишер", "Сардор", "Иван", "Дмитрий", "Нигора", "Малика", "Севара", "Дилноза", "Гулноза",
            "Мадина", "Камила", "Зарина", "Шахноза", "Лола", "Анна", "Екатерина", "Мохира", "Феруза", "Наргиза"];
        string[] lastNames = ["Каримов", "Рахимов", "Юсупов", "Алиев", "Турсунов", "Ахмедов", "Назаров", "Исмоилов", "Хасанов",
            "Султанов", "Мирзаев", "Абдуллаев", "Ибрагимов", "Садыков", "Холматов", "Петров", "Ким", "Пак"];
        string[] prefixes = ["90", "91", "93", "94", "95", "97", "98", "99", "33", "88"];

        var start = now.Date.AddDays(-400);
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
            var platformRoll = rnd.NextDouble();
            customers.Add(new Customer
            {
                FullName = $"{first} {last}",
                Username = rnd.NextDouble() < 0.8 ? $"{Translit(first)}_{Translit(last)}{rnd.Next(1, 99)}".ToLowerInvariant() : null,
                Phone = $"+998 {prefixes[rnd.Next(prefixes.Length)]} {rnd.Next(100, 999)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}",
                Language = rnd.NextDouble() switch { < 0.55 => "ru", < 0.9 => "uz", _ => "en" },
                Platform = platformRoll < 0.68 ? Platform.Telegram : platformRoll < 0.9 ? Platform.Website : Platform.Instagram,
                CreatedAt = created,
                LastVisitAt = created,
            });
        }
        customers = customers.OrderBy(c => c.CreatedAt).ToList();
        db.Customers.AddRange(customers);
        // A small group of regulars places a large share of orders — gives the Top-10 table some shape.
        var loyalty = customers.ToDictionary(c => c, _ => Math.Pow(rnd.NextDouble(), 3) * 10 + 0.3);
        db.SaveChanges(); // assigns product ids used by order lines

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

                var order = BuildOrder(rnd, created, customer, branches, employees, products);
                order.Status = PickStatus(rnd, created, now, settings.OverdueMinutes);
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
                orders.Add(order);
            }
        }
        // Guarantee a live queue for the demo regardless of the time of day the DB is seeded.
        (OrderStatus Status, int MinutesAgo)[] live =
        [
            (OrderStatus.New, 2), (OrderStatus.New, 6), (OrderStatus.New, 11), (OrderStatus.New, 19),
            (OrderStatus.InProgress, 14), (OrderStatus.InProgress, 23), (OrderStatus.InProgress, 37),
            (OrderStatus.Ready, 29), (OrderStatus.Ready, 44), (OrderStatus.OnTheWay, 41), (OrderStatus.OnTheWay, 58),
            (OrderStatus.OnTheWay, 66), (OrderStatus.Overdue, 104), (OrderStatus.Overdue, 131), (OrderStatus.Overdue, 162),
        ];
        foreach (var (status, minutesAgo) in live)
        {
            var created = now.AddMinutes(-minutesAgo);
            var customer = WeightedPick(rnd, customers.Where(c => c.CreatedAt <= created).ToList(), c => loyalty[c]);
            var order = BuildOrder(rnd, created, customer, branches, employees, products);
            order.Status = status;
            order.StatusChangedAt = created.AddMinutes(Math.Min(minutesAgo / 2, 30));
            if (status == OrderStatus.New) order.Employee = null;
            if (created > customer.LastVisitAt) customer.LastVisitAt = created;
            orders.Add(order);
        }

        // Customers spend some of their points.
        foreach (var c in customers.Where(c => c.BonusPoints > 0))
            c.BonusPoints = (long)(c.BonusPoints * (0.3 + rnd.NextDouble() * 0.7));

        db.Orders.AddRange(orders.OrderBy(o => o.CreatedAt)); // ids follow chronology

        (string Name, double Weight)[] sources =
        [
            ("Telegram-бот", 1.0), ("Веб-сайт", 0.55), ("Instagram", 0.4),
            ("Telegram-канал", 0.25), ("QR-каталог", 0.15), ("Google", 0.2),
        ];
        for (var day = start; day <= now.Date; day = day.AddDays(1))
        {
            var progress = (day - start).TotalDays / 400.0;
            foreach (var (name, weight) in sources)
                db.SourceVisits.Add(new SourceVisit
                {
                    Source = name,
                    Date = day,
                    Users = (int)Math.Round((20 + progress * 60) * weight * (0.6 + rnd.NextDouble() * 0.8)),
                });
        }

        db.AutoReplyTemplates.AddRange(DefaultTemplates());
        db.SaveChanges();
        CatalogSeed.SeedMarketing(db, rnd, products, customers, branches, now);
        MarketingSeed.Seed(db, rnd, customers, products, now);
    }

    static Order BuildOrder(Random rnd, DateTime created, Customer customer, List<Branch> branches,
        List<Employee> employees, List<Product> products)
    {
        var branch = branches[rnd.Next(branches.Count)];
        var delivery = rnd.NextDouble() < 0.62 ? DeliveryType.Delivery : DeliveryType.Pickup;
        var order = new Order
        {
            Customer = customer,
            Branch = branch,
            Employee = rnd.NextDouble() < 0.9 ? employees[rnd.Next(employees.Count)] : null,
            CreatedAt = created,
            // Most customers order through the channel they signed up on.
            Platform = rnd.NextDouble() < 0.8 ? customer.Platform : (Platform)rnd.Next(3),
            PaymentMethod = rnd.NextDouble() switch
            {
                < 0.35 => PaymentMethod.Cash, < 0.62 => PaymentMethod.Click,
                < 0.9 => PaymentMethod.Payme, _ => PaymentMethod.CardToCard,
            },
            DeliveryType = delivery,
        };
        var lines = rnd.Next(1, 5);
        foreach (var p in products.OrderBy(_ => rnd.Next()).Take(lines))
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

    static OrderStatus PickStatus(Random rnd, DateTime created, DateTime now, int overdueMinutes)
    {
        var age = (now - created).TotalMinutes;
        if (age > 24 * 60 || (age > 180 && rnd.NextDouble() < 0.9))
            return rnd.NextDouble() < 0.1 ? OrderStatus.Cancelled : OrderStatus.Completed;
        if (age > overdueMinutes) return rnd.NextDouble() < 0.5 ? OrderStatus.Overdue : OrderStatus.Completed;
        return rnd.NextDouble() switch
        {
            < 0.35 => OrderStatus.New, < 0.6 => OrderStatus.InProgress,
            < 0.8 => OrderStatus.Ready, _ => OrderStatus.OnTheWay,
        };
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

    public static IEnumerable<AutoReplyTemplate> DefaultTemplates()
    {
        var texts = new Dictionary<OrderStatus, (string Ru, string Uz, string En)>
        {
            [OrderStatus.New] = ("{name}, спасибо! Заказ №{order_id} на сумму {total} принят.",
                "{name}, rahmat! №{order_id} buyurtmangiz ({total}) qabul qilindi.",
                "{name}, thank you! Order #{order_id} for {total} has been received."),
            [OrderStatus.InProgress] = ("Заказ №{order_id} уже готовится.",
                "№{order_id} buyurtmangiz tayyorlanmoqda.",
                "Order #{order_id} is being prepared."),
            [OrderStatus.Overdue] = ("Извините, заказ №{order_id} задерживается. Мы уже работаем над ним.",
                "Uzr, №{order_id} buyurtmangiz kechikmoqda. Ustida ishlayapmiz.",
                "Sorry, order #{order_id} is delayed. We're on it."),
            [OrderStatus.Ready] = ("Заказ №{order_id} готов! Филиал: {branch}.",
                "№{order_id} buyurtmangiz tayyor! Filial: {branch}.",
                "Order #{order_id} is ready! Branch: {branch}."),
            [OrderStatus.OnTheWay] = ("Курьер выехал с заказом №{order_id}. Ожидайте!",
                "Kuryer №{order_id} buyurtma bilan yo'lga chiqdi.",
                "The courier is on the way with order #{order_id}."),
            [OrderStatus.Completed] = ("Заказ №{order_id} выполнен. Начислено бонусов: {bonus}. Приятного аппетита!",
                "№{order_id} buyurtma bajarildi. Bonus: {bonus}. Yoqimli ishtaha!",
                "Order #{order_id} completed. Bonus earned: {bonus}. Enjoy!"),
            [OrderStatus.Cancelled] = ("Заказ №{order_id} отменён. Если это ошибка — напишите нам.",
                "№{order_id} buyurtma bekor qilindi. Xato bo'lsa, bizga yozing.",
                "Order #{order_id} was cancelled. If this is a mistake, message us."),
        };
        foreach (var (status, t) in texts)
        {
            var enabled = status != OrderStatus.Overdue;
            yield return new AutoReplyTemplate { Status = status, Language = "ru", Text = t.Ru, Enabled = enabled };
            yield return new AutoReplyTemplate { Status = status, Language = "uz", Text = t.Uz, Enabled = enabled };
            yield return new AutoReplyTemplate { Status = status, Language = "en", Text = t.En, Enabled = enabled };
        }
    }

    static readonly string[] Streets = ["Навои", "Бабура", "Шота Руставели", "Мукими", "Катартал", "Фархадская",
        "Бунёдкор", "Амира Темура", "Мустакиллик", "Нукусская", "Шахрисабзская", "Ойбека", "Беруни"];

    static readonly string[] Comments = ["Позвоните за 10 минут", "Без лука, пожалуйста", "Домофон не работает",
        "Нужна сдача со 200 000", "Подпишите открытку: С днём рождения!", "Оставить у двери"];

    static string Translit(string s)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ж'] = "j", ['з'] = "z",
            ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p",
            ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "x", ['ц'] = "ts", ['ч'] = "ch",
            ['ш'] = "sh", ['ы'] = "i", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ё'] = "yo",
        };
        return string.Concat(s.ToLowerInvariant().Select(ch => map.TryGetValue(ch, out var r) ? r : ""));
    }
}
