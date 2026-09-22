using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Data;

/// <summary>Demo data for the Marketing section: past broadcasts, promo codes, sources, SMS, banners.</summary>
public static class MarketingSeed
{
    public static void Seed(AppDbContext db, Random rnd, List<Customer> customers, List<Product> products, DateTime now)
    {
        var bot = customers.Where(MarketingService.ReachableByBot).ToList();

        (string Name, string Text, string? Button, int DaysAgo, double ClickRate)[] broadcasts =
        [
            ("Скидка 20% на кофе по утрам", "☕ Каждое утро до 11:00 — минус 20% на весь кофе. Ждём вас!", "Заказать кофе", 12, 0.18),
            ("Новинка: круассан с миндалём", "🥐 Попробуйте наш новый круассан с миндальным кремом — уже в меню.", "Посмотреть", 26, 0.22),
            ("Торты к Новому году", "🎂 Принимаем заказы на праздничные торты. Закажите заранее — за 24 часа.", "Выбрать торт", 270, 0.12),
            ("Бесплатная доставка на выходных", "🚚 В эту субботу и воскресенье доставка бесплатно при заказе от 100 000 сум.", null, 55, 0),
        ];
        foreach (var (name, text, button, daysAgo, clickRate) in broadcasts)
        {
            var at = now.Date.AddDays(-daysAgo).AddHours(10);
            var b = new Broadcast
            {
                Name = name, Text = text, ButtonText = button,
                ButtonUrl = button is null ? null : "https://plum-bakery.plum.uz/menu",
                Status = BroadcastStatus.Sent, SendAt = at, CreatedAt = at.AddMinutes(-30),
            };
            // Mostly bot users (the reachable ones) plus a few web customers who can't get it.
            var audience = bot.Where(c => c.CreatedAt < at).Concat(customers.Where(c => !MarketingService.ReachableByBot(c) && c.CreatedAt < at).Take(15));
            foreach (var c in audience)
            {
                var status = !MarketingService.ReachableByBot(c) ? DeliveryStatus.NotSent
                    : rnd.NextDouble() < 0.04 ? DeliveryStatus.Blocked : DeliveryStatus.Sent;
                b.Recipients.Add(new BroadcastRecipient
                {
                    CustomerId = c.Id, Status = status, Token = MarketingService.NewToken(),
                    ClickedAt = status == DeliveryStatus.Sent && rnd.NextDouble() < clickRate ? at.AddMinutes(rnd.Next(1, 600)) : null,
                });
            }
            db.Broadcasts.Add(b);
        }
        // One scheduled for tomorrow to show the "Запланирована" state.
        var tomorrow = now.Date.AddDays(1).AddHours(9);
        var scheduled = new Broadcast
        {
            Name = "Утренний анонс выпечки", Text = "Доброе утро! Свежие круассаны уже в печи 🥐", Status = BroadcastStatus.Scheduled,
            SendAt = tomorrow, CreatedAt = now.AddHours(-1),
        };
        foreach (var c in bot.Take(60))
            scheduled.Recipients.Add(new BroadcastRecipient { CustomerId = c.Id, Status = DeliveryStatus.NotSent, Token = MarketingService.NewToken() });
        db.Broadcasts.Add(scheduled);

        db.PromoCodes.AddRange(
            new PromoCode { Code = "WELCOME15", Type = DiscountType.Percent, Value = 15, MaxDiscount = 40000, FirstOrderOnly = true,
                StartsAt = now.AddDays(-120), EndsAt = now.AddDays(240), UsedCount = 86, CreatedAt = now.AddDays(-120) },
            new PromoCode { Code = "COFFEE10", Type = DiscountType.Fixed, Value = 10000, MinOrderAmount = 50000, UsageLimit = 500,
                StartsAt = now.AddDays(-20), EndsAt = now.AddDays(10), UsedCount = 212, Platforms = ["Telegram"], CreatedAt = now.AddDays(-20) },
            new PromoCode { Code = "TORT2025", Type = DiscountType.Percent, Value = 10, MinOrderAmount = 150000,
                StartsAt = now.AddDays(-300), EndsAt = now.AddDays(-250), UsedCount = 41, CreatedAt = now.AddDays(-300) },
            new PromoCode { Code = "INSTA50", Type = DiscountType.Fixed, Value = 50000, MinOrderAmount = 250000, UsageLimit = 30,
                StartsAt = now.AddDays(-15), EndsAt = now.AddDays(15), UsedCount = 30, Platforms = ["Instagram"], CreatedAt = now.AddDays(-15) },
            new PromoCode { Code = "WEEKEND", Type = DiscountType.Percent, Value = 5, StartsAt = now.AddDays(3), EndsAt = now.AddDays(5),
                CreatedAt = now.AddDays(-1) });

        (SourceType Type, string Name, string Slug, int Clicks, int DaysAgo)[] sources =
        [
            (SourceType.Telegram, "Реклама в канале «Ташкент Еда»", "tashkent_eda", 1840, 60),
            (SourceType.Telegram, "QR-код на кассе", "qr_kassa", 960, 200),
            (SourceType.Website, "Instagram — ссылка в профиле", "insta_bio", 1320, 150),
            (SourceType.Website, "Google Ads — торты", "google_tort", 710, 40),
            (SourceType.Telegram, "Листовки у метро Чиланзар", "flyer_chilonzor", 230, 25),
            (SourceType.Website, "Блогер @toshkent_food", "blog_tfood", 540, 12),
        ];
        foreach (var (type, name, slug, clicks, daysAgo) in sources)
        {
            var newUsers = (int)(clicks * (0.18 + rnd.NextDouble() * 0.12));
            db.TrafficSources.Add(new TrafficSource
            {
                Type = type, Name = name, Slug = slug, Clicks = clicks, NewUsers = newUsers,
                ExistingUsers = (int)(clicks * (0.08 + rnd.NextDouble() * 0.1)),
                Orders = (int)(newUsers * (0.35 + rnd.NextDouble() * 0.3)),
                CreatedAt = now.AddDays(-daysAgo), LastVisitAt = now.AddHours(-rnd.Next(1, 72)),
            });
        }

        var tplPromo = new SmsTemplate
        {
            Name = "Промо выходных", Text = "Plum Bakery: в субботу и воскресенье -15% на торты по промокоду WEEKEND. plum-bakery.plum.uz",
            Status = SmsStatus.Confirmed, CreatedAt = now.AddDays(-30), ModeratedAt = now.AddDays(-30).AddHours(3),
        };
        var tplNew = new SmsTemplate
        {
            Name = "Новинки месяца", Text = "Plum Bakery: попробуйте новый круассан с миндалем! Заказ: plum-bakery.plum.uz",
            Status = SmsStatus.Confirmed, CreatedAt = now.AddDays(-8), ModeratedAt = now.AddDays(-8).AddHours(1),
        };
        db.SmsTemplates.AddRange(tplPromo, tplNew,
            new SmsTemplate
            {
                Name = "Розыгрыш", Text = "ВЫ ВЫИГРАЛИ ТОРТ!!! ЗАБЕРИТЕ ПРИЗ ПО ССЫЛКЕ bit.ly/plumtort",
                Status = SmsStatus.Rejected, RejectReason = "Сокращённые ссылки запрещены операторами — укажите полный адрес сайта",
                CreatedAt = now.AddDays(-5), ModeratedAt = now.AddDays(-5),
            });
        db.SmsCampaigns.AddRange(
            new SmsCampaign { Name = "Выходные −15%, все клиенты", Template = tplPromo, Status = SmsStatus.Confirmed, Recipients = customers.Count,
                Delivered = (int)(customers.Count * 0.97), Segments = MarketingService.Segments(tplPromo.Text).Segments,
                CreatedAt = now.AddDays(-28), StatusChangedAt = now.AddDays(-28).AddHours(2) },
            new SmsCampaign { Name = "Новинки — постоянные клиенты", Template = tplNew, Status = SmsStatus.Confirmed, Recipients = 142,
                Delivered = 138, Segments = MarketingService.Segments(tplNew.Text).Segments,
                CreatedAt = now.AddDays(-7), StatusChangedAt = now.AddDays(-7).AddHours(1) },
            new SmsCampaign { Name = "Новинки — Юнусабад", Template = tplNew, Status = SmsStatus.Rejected, Recipients = 96,
                RejectReason = "Недостаточно средств на SMS-балансе — пополните баланс и создайте рассылку заново",
                Segments = MarketingService.Segments(tplNew.Text).Segments, CreatedAt = now.AddDays(-2), StatusChangedAt = now.AddDays(-2).AddMinutes(15) });

        var cakes = products.Where(p => p.Name.Get().StartsWith("Торт")).ToList();
        db.Banners.AddRange(
            new Banner { Title = "Кофе −20% по утрам", Type = BannerType.Main, MobileUrl = "/demo/banner-coffee-mobile.svg", DesktopUrl = "/demo/banner-coffee-desktop.svg", LinkType = BannerLink.Url, LinkUrl = "https://plum-bakery.plum.uz/menu",
                SortOrder = 0, CreatedAt = now.AddDays(-12) },
            new Banner { Title = "Праздничные торты на заказ", Type = BannerType.Main, MobileUrl = "/demo/banner-cakes-mobile.svg", DesktopUrl = "/demo/banner-cakes-desktop.svg", LinkType = BannerLink.Product,
                LinkTargetId = cakes.LastOrDefault()?.Id, SortOrder = 1, CreatedAt = now.AddDays(-30) },
            new Banner { Title = "Новый круассан с миндалём", Type = BannerType.Main, MobileUrl = "/demo/banner-croissant-mobile.svg", DesktopUrl = "/demo/banner-croissant-desktop.svg", LinkType = BannerLink.None, IsActive = false,
                SortOrder = 2, CreatedAt = now.AddDays(-60) });
        db.SaveChanges();
    }
}
