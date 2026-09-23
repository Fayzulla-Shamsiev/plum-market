using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Data;

/// <summary>
/// Marketing content of the demo store — the two sections the MVP admin still has: промокоды и баннеры.
/// (Broadcasts, SMS and traffic sources were dropped from the MVP, so nothing is seeded for them.)
/// </summary>
public static class DemoMarketing
{
    public static void Seed(AppDbContext db, List<Product> products, DateTime now)
    {
        db.PromoCodes.AddRange(
            new PromoCode
            {
                Code = "WELCOME15", Type = DiscountType.Percent, Value = 15, MaxDiscount = 40000, FirstOrderOnly = true,
                StartsAt = now.AddDays(-120), EndsAt = now.AddDays(240), UsedCount = 86, CreatedAt = now.AddDays(-120),
            },
            new PromoCode
            {
                Code = "COFFEE10", Type = DiscountType.Fixed, Value = 10000, MinOrderAmount = 50000, UsageLimit = 500,
                StartsAt = now.AddDays(-20), EndsAt = now.AddDays(10), UsedCount = 212, CreatedAt = now.AddDays(-20),
            },
            new PromoCode
            {
                Code = "TORT2025", Type = DiscountType.Percent, Value = 10, MinOrderAmount = 150000,
                StartsAt = now.AddDays(-300), EndsAt = now.AddDays(-250), UsedCount = 41, CreatedAt = now.AddDays(-300),
            },
            new PromoCode
            {
                Code = "WEEKEND", Type = DiscountType.Percent, Value = 5, StartsAt = now.AddDays(3), EndsAt = now.AddDays(5),
                CreatedAt = now.AddDays(-1),
            });

        var cakes = products.Where(p => p.Name.Get().StartsWith("Торт")).ToList();
        db.Banners.AddRange(
            new Banner
            {
                Title = "Кофе −20% по утрам", Type = BannerType.Main, SortOrder = 0, CreatedAt = now.AddDays(-12),
                MobileUrl = "/demo/banner-coffee-mobile.svg", DesktopUrl = "/demo/banner-coffee-desktop.svg",
                LinkType = BannerLink.Url, LinkUrl = "/catalog",
            },
            new Banner
            {
                Title = "Праздничные торты на заказ", Type = BannerType.Main, SortOrder = 1, CreatedAt = now.AddDays(-30),
                MobileUrl = "/demo/banner-cakes-mobile.svg", DesktopUrl = "/demo/banner-cakes-desktop.svg",
                LinkType = BannerLink.Product, LinkTargetId = cakes.LastOrDefault()?.Id,
            },
            new Banner
            {
                Title = "Новый круассан с миндалём", Type = BannerType.Main, SortOrder = 2, CreatedAt = now.AddDays(-60),
                MobileUrl = "/demo/banner-croissant-mobile.svg", DesktopUrl = "/demo/banner-croissant-desktop.svg",
                LinkType = BannerLink.None, IsActive = false,
            });
        db.SaveChanges();
    }
}
