using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Data;

/// <summary>
/// The demo store's catalog: categories, products with stock per branch, discounts, reviews and the chat inbox.
/// Only <see cref="DemoData"/> uses it — a store created by registration starts empty.
/// </summary>
public static class DemoCatalog
{
    record P(string Ru, string Uz, long Price, string Cat, string Unit = "шт", int? Grams = null, string[]? Tags = null, long? OldPrice = null);

    static readonly (string Key, string Ru, string Uz, string? Parent)[] Categories =
    [
        ("bakery", "Выпечка", "Pishiriqlar", null),
        ("croissant", "Круассаны", "Kruassanlar", "bakery"),
        ("samsa", "Самса и пирожки", "Somsa va pirojkilar", "bakery"),
        ("bread", "Хлеб", "Non", "bakery"),
        ("fastfood", "Пицца и фастфуд", "Pitsa va fastfud", null),
        ("pizza", "Пицца", "Pitsa", "fastfood"),
        ("burgers", "Бургеры и шаурма", "Burgerlar va shaurma", "fastfood"),
        ("drinks", "Напитки", "Ichimliklar", null),
        ("coffee", "Кофе", "Qahva", "drinks"),
        ("tea", "Чай и лимонады", "Choy va limonadlar", "drinks"),
        ("desserts", "Десерты", "Desertlar", null),
        ("cakes", "Торты", "Tortlar", "desserts"),
        ("pastry", "Пирожные", "Pirojniylar", "desserts"),
        ("kitchen", "Салаты и супы", "Salat va shoʻrvalar", null),
    ];

    // Same order as the original seed so order history keeps pointing at the same products.
    static readonly P[] Products =
    [
        new("Круассан классический", "Klassik kruassan", 18000, "croissant", Grams: 70, Tags: ["хит"]),
        new("Круассан шоколадный", "Shokoladli kruassan", 22000, "croissant", Grams: 85),
        new("Круассан с миндалём", "Bodomli kruassan", 26000, "croissant", Grams: 90, Tags: ["новинка"]),
        new("Самса с мясом", "Goʻshtli somsa", 12000, "samsa", Grams: 120, Tags: ["хит"]),
        new("Самса с тыквой", "Qovoqli somsa", 9000, "samsa", Grams: 110, Tags: ["постное"]),
        new("Пицца Маргарита", "Margarita pitsasi", 69000, "pizza", Grams: 550),
        new("Пицца Пепперони", "Pepperoni pitsasi", 79000, "pizza", Grams: 600, Tags: ["острое"]),
        new("Бургер классический", "Klassik burger", 38000, "burgers", Grams: 280),
        new("Чизбургер", "Chizburger", 42000, "burgers", Grams: 300),
        new("Лаваш с курицей", "Tovuqli lavash", 32000, "burgers", Grams: 350),
        new("Шаурма говяжья", "Mol goʻshtli shaurma", 36000, "burgers", Grams: 380),
        new("Хот-дог", "Xot-dog", 18000, "burgers", Grams: 200),
        new("Капучино 300 мл", "Kapuchino 300 ml", 24000, "coffee", "порция"),
        new("Латте 400 мл", "Latte 400 ml", 28000, "coffee", "порция"),
        new("Американо", "Amerikano", 18000, "coffee", "порция"),
        new("Раф ванильный", "Vanilli raf", 32000, "coffee", "порция", Tags: ["новинка"]),
        new("Чай зелёный", "Koʻk choy", 10000, "tea", "порция"),
        new("Лимонад домашний", "Uy limonadi", 22000, "tea", "порция", OldPrice: 26000),
        new("Торт Наполеон (кусок)", "Napoleon torti (boʻlak)", 28000, "cakes", Grams: 150),
        new("Чизкейк Нью-Йорк", "Nyu-York chizkeyki", 34000, "pastry", Grams: 140),
        new("Медовик (кусок)", "Asalli tort (boʻlak)", 26000, "cakes", Grams: 150),
        new("Эклер", "Ekler", 16000, "pastry", Grams: 70),
        new("Тирамису", "Tiramisu", 36000, "pastry", Grams: 160),
        new("Маффин черничный", "Qoraqatli maffin", 15000, "pastry", Grams: 90, OldPrice: 18000),
        new("Хлеб Бородинский", "Borodino noni", 11000, "bread", Grams: 400),
        new("Багет французский", "Fransuz bageti", 9000, "bread", Grams: 250),
        new("Лепёшка тандырная", "Tandir non", 5000, "bread", Grams: 300, Tags: ["хит"]),
        new("Салат Цезарь", "Sezar salati", 42000, "kitchen", "порция", 250),
        new("Суп чечевичный", "Yasmiq shoʻrva", 26000, "kitchen", "порция", 350),
        new("Сэндвич с тунцом", "Tunetsli sendvich", 34000, "burgers", Grams: 220),
        new("Торт Медовик 1,5 кг", "Asalli tort 1,5 kg", 185000, "cakes", Grams: 1500, Tags: ["на заказ"]),
        new("Торт Красный бархат 1,5 кг", "Qizil baxmal torti 1,5 kg", 235000, "cakes", Grams: 1500, Tags: ["на заказ"]),
        new("Набор пирожных (6 шт)", "Pirojniylar toʻplami (6 dona)", 89000, "pastry", "упак", 480, OldPrice: 99000),
        new("Сок апельсиновый фреш", "Apelsin fresh sharbati", 30000, "tea", "порция"),
    ];

    // Second paragraph of the demo descriptions, so the storefront has a short and a full description to show.
    static readonly Dictionary<string, (string Ru, string Uz)> Details = new()
    {
        ["bakery"] = ("Тесто замешиваем ночью и выпекаем с 6 утра, поэтому к открытию всё ещё тёплое. Используем сливочное масло 82,5%, муку высшего сорта и никаких улучшителей. Хранить при комнатной температуре до 24 часов; разогреть 3–4 минуты в духовке при 180 °C.",
            "Xamirni tunda qoramiz va ertalab soat 6 dan pishiramiz, shuning uchun ochilishda hammasi hali iliq. 82,5% sariyogʻ va oliy navli un ishlatamiz. Xona haroratida 24 soatgacha saqlang."),
        ["fastfood"] = ("Готовим сразу после заказа: соусы делаем сами, овощи режем каждое утро. Упаковываем в термопакет, чтобы блюдо доехало горячим. Лучше съесть в течение 30 минут после получения.",
            "Buyurtmadan soʻng darhol tayyorlaymiz: souslarni oʻzimiz qilamiz, sabzavotlarni har tongda toʻgʻraymiz. Issiq yetib borishi uchun termo paketga joylaymiz."),
        ["drinks"] = ("Кофе — свежая обжарка арабики из Эфиопии и Бразилии, молоко 3,2% или растительное на выбор. Лимонады и соки готовим без сиропов-концентратов, только из фруктов. Подаём в стакане с крышкой, объём указан в названии.",
            "Qahva — Efiopiya va Braziliya arabikasi, yangi qovurilgan. Limonad va sharbatlar faqat mevalardan, konsentratsiz tayyorlanadi."),
        ["desserts"] = ("Десерты собираем вручную в нашем цехе, крем — на натуральных сливках 33%. Хранить в холодильнике при +2…+6 °C не более 48 часов. Торты на заказ принимаем за сутки; надпись на торте — бесплатно.",
            "Desertlarni sexda qoʻlda yigʻamiz, krem — 33% tabiiy qaymoqda. Muzlatgichda +2…+6 °C da 48 soatgacha saqlang. Buyurtma tortlar bir kun oldin qabul qilinadi."),
        ["kitchen"] = ("Готовим порционно в течение дня, заправку кладём отдельно, чтобы салат не потерял хруст. Супы варим на домашнем бульоне без бульонных кубиков. Хранить в холодильнике до 12 часов.",
            "Kun davomida porsiyalab tayyorlaymiz, salat qarsildoq qolishi uchun zapravka alohida qoʻyiladi. Shoʻrvalar uy bulonida, kubiklarsiz."),
    };

    static string DetailKey(string cat) => cat switch
    {
        "croissant" or "samsa" or "bread" => "bakery",
        "pizza" or "burgers" => "fastfood",
        "coffee" or "tea" => "drinks",
        "cakes" or "pastry" => "desserts",
        _ => "kitchen",
    };

    public static List<Product> SeedCatalog(AppDbContext db, Random rnd, List<Branch> branches, DateTime now)
    {
        var cats = new Dictionary<string, Category>();
        var order = 0;
        foreach (var (key, ru, uz, parent) in Categories)
        {
            var c = new Category
            {
                Name = Localized.Of(ru, uz, UzTransliterator.ToCyrillic(uz)),
                Description = Localized.Of($"{ru} — свежее каждый день.", $"{uz} — har kuni yangi.", UzTransliterator.ToCyrillic($"{uz} — har kuni yangi.")),
                CreatedAt = now.AddDays(-400 + order * 3),
                SortOrder = order++,
                Layout = parent is null ? "grid2" : "grid3",
            };
            cats[key] = c;
            db.Categories.Add(c);
        }
        db.SaveChanges();
        foreach (var (key, _, _, parent) in Categories)
            if (parent is not null) cats[key].ParentId = cats[parent].Id;

        var products = new List<Product>();
        for (var i = 0; i < Products.Length; i++)
        {
            var p = Products[i];
            var cat = cats[p.Cat];
            var cost = (long)(p.Price * (0.38 + rnd.NextDouble() * 0.2)) / 100 * 100;
            var created = now.AddDays(-400 + i * 4).AddHours(rnd.Next(9, 18));
            var product = new Product
            {
                Category = cat,
                Name = Localized.Of(p.Ru, p.Uz, UzTransliterator.ToCyrillic(p.Uz)),
                // Leave a few descriptions empty so "Сгенерировать" has something to do in the demo.
                Description = i % 5 == 4
                    ? new Localized()
                    : Localized.Of($"{p.Ru} — готовим из свежих продуктов каждый день.\n\n{Details[DetailKey(p.Cat)].Ru}",
                        $"{p.Uz} — har kuni yangi mahsulotlardan tayyorlaymiz.\n\n{Details[DetailKey(p.Cat)].Uz}",
                        UzTransliterator.ToCyrillic($"{p.Uz} — har kuni yangi mahsulotlardan tayyorlaymiz.\n\n{Details[DetailKey(p.Cat)].Uz}")),
                Price = p.Price,
                OldPrice = p.OldPrice,
                CostPrice = cost,
                Unit = p.Unit,
                WeightGrams = p.Grams,
                Tags = p.Tags?.ToList() ?? new(),
                CreatedAt = created,
                UpdatedAt = created.AddDays(rnd.Next(0, 30)),
                SortOrder = i,
                IsActive = i != 11, // one inactive product to show the toggle
            };
            if (p.Cat == "pizza")
            {
                product.Variants = [new() { Name = "25 см", Price = p.Price }, new() { Name = "30 см", Price = p.Price + 20000 }, new() { Name = "35 см", Price = p.Price + 35000 }];
                product.Attributes = [new() { Name = "Тесто", Value = "тонкое" }];
            }
            if (p.Cat == "coffee")
                product.Variants = [new() { Name = "Обычное молоко" }, new() { Name = "Овсяное молоко", Price = p.Price + 6000 }];
            if (p.Cat is "cakes" && p.Grams >= 1500)
                product.Attributes = [new() { Name = "Срок изготовления", Value = "24 часа" }, new() { Name = "Порций", Value = "10–12" }];

            // Big cakes are made only in the main branch; everything else is sold everywhere.
            foreach (var b in p.Cat == "cakes" && p.Grams >= 1500 ? branches.Take(1) : branches)
            {
                var roll = rnd.NextDouble();
                var limited = p.Cat is "cakes" or "pastry" or "bread" || roll < 0.2;
                product.Stock.Add(new StockItem
                {
                    BranchId = b.Id,
                    Status = limited ? (roll < 0.08 ? StockStatus.OutOfStock : StockStatus.Limited) : StockStatus.Unlimited,
                    Quantity = limited ? (roll < 0.08 ? 0 : rnd.Next(3, 60)) : 0,
                    UpdatedAt = now.AddHours(-rnd.Next(1, 200)),
                });
            }
            products.Add(product);
        }
        db.Products.AddRange(products);
        db.SaveChanges();
        return products;
    }

    public static void SeedMarketing(AppDbContext db, Random rnd, List<Product> products, List<Customer> customers,
        List<Branch> branches, DateTime now)
    {
        db.Discounts.AddRange(
            new Discount
            {
                Name = "Утренний кофе −20%", Type = DiscountType.Percent, Value = 20,
                ProductIds = products.Where(p => p.Category!.Name.Get() == "Кофе").Select(p => p.Id).ToList(),
                BranchIds = [], StartsAt = now.AddDays(-10), EndsAt = now.AddDays(20), CreatedAt = now.AddDays(-10),
            },
            new Discount
            {
                Name = "Торт на праздник — минус 30 000", Type = DiscountType.Fixed, Value = 30000,
                ProductIds = products.Where(p => p.Unit == "шт" && p.WeightGrams >= 1500).Select(p => p.Id).ToList(),
                BranchIds = [branches[0].Id], StartsAt = now.AddDays(-3), EndsAt = now.AddDays(11), MinOrderAmount = 200000,
                CreatedAt = now.AddDays(-3),
            },
            new Discount
            {
                Name = "Самса-день", Type = DiscountType.Percent, Value = 15,
                ProductIds = products.Where(p => p.Category!.Name.Get() == "Самса и пирожки").Select(p => p.Id).ToList(),
                BranchIds = [branches[1].Id, branches[2].Id], StartsAt = now.AddDays(-40), EndsAt = now.AddDays(-33),
                CreatedAt = now.AddDays(-41),
            });

        string[] good = ["Очень вкусно, всегда свежее!", "Доставили быстро, всё горячее", "Лучшее в городе 👍", "Беру каждое утро",
            "Juda mazali, rahmat!", "Порция большая, цена нормальная"];
        string[] meh = ["Вкусно, но в этот раз было суховато", "Долго ждал доставку", "Мало начинки", "Hammasi yaxshi, lekin biroz sovuq keldi"];
        var reviews = new List<Review>();
        foreach (var p in products)
        {
            var count = rnd.Next(0, 9);
            for (var i = 0; i < count; i++)
            {
                var rating = rnd.NextDouble() < 0.78 ? rnd.Next(4, 6) : rnd.Next(2, 4);
                var created = now.AddDays(-rnd.Next(0, 200)).AddMinutes(-rnd.Next(0, 1440));
                var answered = created < now.AddDays(-3) && rnd.NextDouble() < 0.7;
                reviews.Add(new Review
                {
                    ProductId = p.Id,
                    Customer = customers[rnd.Next(customers.Count)],
                    Rating = rating,
                    Comment = rating >= 4 ? good[rnd.Next(good.Length)] : meh[rnd.Next(meh.Length)],
                    Status = answered ? ReviewStatus.Answered : ReviewStatus.New,
                    Reply = answered ? "Спасибо за отзыв! Будем рады видеть вас снова." : null,
                    CreatedAt = created,
                    RepliedAt = answered ? created.AddHours(3) : null,
                });
            }
        }
        db.Reviews.AddRange(reviews);
        db.SaveChanges();

        SeedChats(db, rnd, products, customers, reviews, now);
    }

    static void SeedChats(AppDbContext db, Random rnd, List<Product> products, List<Customer> customers, List<Review> reviews, DateTime now)
    {
        (string In, string Out)[] dialogs =
        [
            ("Здравствуйте! Вы сегодня работаете до скольки?", "Здравствуйте! До 23:00, ждём вас 🙂"),
            ("Можно заказать торт на субботу?", "Конечно! Какой торт и на сколько персон?"),
            ("Salom, yetkazib berish qancha turadi?", "Salom! 200 000 soʻmdan yuqori buyurtmalarga bepul, qolganlariga 15 000 soʻm."),
            ("Заказ собрали быстрее, чем обещали — спасибо!", "Рады стараться! Ждём вас снова 🙂"),
            ("Курьер опаздывает, заказ #3301", "Извините за задержку, курьер будет через 10 минут."),
            ("Есть ли у вас безглютеновая выпечка?", "Пока нет, но скоро добавим в меню."),
            ("Можно оплатить картой при получении?", "Да, у курьера есть терминал."),
            ("Hi! Do you deliver to Yunusabad?", "Hi! Yes, we deliver across Tashkent."),
            ("Спасибо, всё было очень вкусно!", "Спасибо вам! Будем рады видеть снова ❤️"),
            ("Хочу изменить адрес доставки", "Напишите новый адрес, пожалуйста — передадим курьеру."),
            ("Сколько стоит набор пирожных?", "89 000 сум, сейчас действует скидка."),
        ];
        string[] followUps = ["Хорошо, спасибо!", "Понял, жду", "А можно ещё добавить круассан?", "Ok, rahmat", "Отлично 👍"];
        string[] freshQuestions = ["Добрый день, а можно заказ к 9 утра?", "Где ваш ближайший филиал к Чиланзару?",
            "Salom, bugun tort olsa boʻladimi?", "Заказ до сих пор не пришёл 😕", "Здравствуйте, есть вакансии?"];

        var conversations = new List<Conversation>();
        var chatCustomers = customers.OrderBy(_ => rnd.Next()).Take(22).ToList();
        for (var i = 0; i < chatCustomers.Count; i++)
        {
            var c = chatCustomers[i];
            var conv = new Conversation { Customer = c, Channel = ChatChannel.Website, DisplayName = c.FullName };
            var start = now.AddHours(-rnd.Next(1, 24 * 20)).AddMinutes(-rnd.Next(0, 60));
            var (qIn, qOut) = dialogs[i % dialogs.Length];
            var t = start;
            conv.Messages.Add(new ChatMessage { Direction = MessageDirection.In, Text = qIn, SentAt = t });
            t = t.AddMinutes(rnd.Next(1, 12));
            conv.Messages.Add(new ChatMessage { Direction = MessageDirection.Out, Text = qOut, SentAt = t, SenderName = "Нодира Юсупова" });
            if (rnd.NextDouble() < 0.6)
            {
                t = t.AddMinutes(rnd.Next(1, 30));
                conv.Messages.Add(new ChatMessage { Direction = MessageDirection.In, Text = followUps[rnd.Next(followUps.Length)], SentAt = t });
            }
            // The most recent handful are waiting for an answer.
            if (i < 6)
            {
                t = now.AddMinutes(-rnd.Next(2, 180));
                conv.Messages.Add(new ChatMessage { Direction = MessageDirection.In, Text = freshQuestions[i % freshQuestions.Length], SentAt = t });
                conv.UnreadCount = 1;
            }
            conversations.Add(conv);
        }

        // "Обзоры": each recent review opens a thread the merchant can answer from the inbox.
        foreach (var r in reviews.OrderByDescending(r => r.CreatedAt).Take(8))
        {
            var product = products.First(p => p.Id == r.ProductId);
            var conv = new Conversation
            {
                Customer = r.Customer, Channel = ChatChannel.Website, DisplayName = r.Customer.FullName, ReviewId = r.Id,
                UnreadCount = r.Status == ReviewStatus.New ? 1 : 0,
            };
            conv.Messages.Add(new ChatMessage
            {
                Direction = MessageDirection.In, SentAt = r.CreatedAt,
                Text = $"{new string('★', r.Rating)}{new string('☆', 5 - r.Rating)} «{product.Name.Get()}»\n{r.Comment}",
            });
            if (r.Reply is not null)
                conv.Messages.Add(new ChatMessage { Direction = MessageDirection.Out, Text = r.Reply, SentAt = r.RepliedAt!.Value, SenderName = "Азиз Каримов" });
            conversations.Add(conv);
        }

        foreach (var conv in conversations)
        {
            var last = conv.Messages.MaxBy(m => m.SentAt)!;
            conv.LastMessageAt = last.SentAt;
            conv.LastMessageText = last.Text;
            conv.CreatedAt = conv.Messages.Min(m => m.SentAt);
        }
        db.Conversations.AddRange(conversations);

        var settings = db.Settings.Local.First();
        settings.ChatAutoReplyEnabled = true;
        settings.ChatAutoReplies = new()
        {
            [nameof(ChatChannel.Website)] = "Здравствуйте! Мы получили ваше сообщение и ответим в течение 10 минут.",
        };
        db.SaveChanges();
    }
}
