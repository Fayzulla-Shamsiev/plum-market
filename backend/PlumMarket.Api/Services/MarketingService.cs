using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>Customer segment for a broadcast / SMS campaign. Explicit <see cref="CustomerIds"/> win over filters.</summary>
public record AudienceFilter(
    List<Platform>? Platforms,
    List<string>? Languages,
    int? MinOrders,
    int? LastVisitDays,
    long? MinBonus,
    List<int>? CustomerIds);

/// <summary>
/// Marketing side effects. External delivery (Telegram Bot API, SMS gateway, moderation by the operator)
/// is simulated: the prototype decides outcomes locally and advances time-based states lazily on read.
/// </summary>
public partial class MarketingService(AppDbContext db)
{
    // Simulated gateway timings.
    public static readonly TimeSpan TemplateReview = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CampaignReview = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan CampaignSending = TimeSpan.FromSeconds(20);

    // ------------------------------------------------------------ audience

    public async Task<List<Customer>> ResolveAudience(AudienceFilter f)
    {
        // Customers who turned off "Акции и новости" in the storefront settings are never included.
        if (f.CustomerIds is { Count: > 0 } ids)
            return await db.Customers.Where(c => ids.Contains(c.Id) && c.NotifyPromos).ToListAsync();

        var q = db.Customers.Where(c => c.NotifyPromos);
        if (f.Platforms is { Count: > 0 } ps) q = q.Where(c => ps.Contains(c.Platform));
        if (f.Languages is { Count: > 0 } ls) q = q.Where(c => ls.Contains(c.Language));
        if (f.LastVisitDays is { } days) q = q.Where(c => c.LastVisitAt >= DateTime.Now.AddDays(-days));
        if (f.MinBonus is { } bonus) q = q.Where(c => c.BonusPoints >= bonus);
        if (f.MinOrders is { } min && min > 0)
            q = q.Where(c => c.Orders.Count(o => o.Status != OrderStatus.Cancelled) >= min);
        return await q.ToListAsync();
    }

    /// <summary>Broadcasts go through the Telegram bot, so only customers who came from the bot can receive them.</summary>
    public static bool ReachableByBot(Customer c) => c.Platform == Platform.Telegram;

    // ------------------------------------------------------------ broadcasts

    /// <summary>Simulated Bot API delivery: ~4% of bot users have blocked the bot; others receive it.</summary>
    public void Deliver(Broadcast b, IEnumerable<Customer> audience)
    {
        var rnd = new Random(b.Name.GetHashCode() ^ audience.Count());
        foreach (var c in audience)
            b.Recipients.Add(new BroadcastRecipient
            {
                CustomerId = c.Id,
                Token = NewToken(),
                Status = !ReachableByBot(c) ? DeliveryStatus.NotSent
                    : rnd.NextDouble() < 0.04 ? DeliveryStatus.Blocked
                    : DeliveryStatus.Sent,
            });
    }

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(9)).Replace('+', '-').Replace('/', '_');

    /// <summary>Sends scheduled broadcasts that are due and moves SMS items through moderation/sending.</summary>
    public async Task TickAsync()
    {
        var now = DateTime.Now;
        var due = await db.Broadcasts.Include(b => b.Recipients)
            .Where(b => b.Status == BroadcastStatus.Scheduled && b.SendAt <= now).ToListAsync();
        foreach (var b in due)
        {
            // Recipients were resolved at scheduling time; delivery happens now.
            var customers = await db.Customers.Where(c => b.Recipients.Select(r => r.CustomerId).Contains(c.Id)).ToListAsync();
            b.Recipients.Clear();
            Deliver(b, customers);
            b.Status = BroadcastStatus.Sent;
        }

        foreach (var t in await db.SmsTemplates.Where(t => t.Status == SmsStatus.Moderation).ToListAsync())
            if (now - t.CreatedAt >= TemplateReview)
            {
                t.Status = SmsStatus.Confirmed;
                t.ModeratedAt = t.CreatedAt + TemplateReview;
            }

        foreach (var c in await db.SmsCampaigns.Where(c => c.Status == SmsStatus.Moderation || c.Status == SmsStatus.InProgress).ToListAsync())
        {
            if (c.Status == SmsStatus.Moderation && now - c.StatusChangedAt >= CampaignReview)
            {
                c.Status = SmsStatus.InProgress;
                c.StatusChangedAt = c.StatusChangedAt + CampaignReview;
            }
            if (c.Status == SmsStatus.InProgress && now - c.StatusChangedAt >= CampaignSending)
            {
                c.Status = SmsStatus.Confirmed;
                c.StatusChangedAt = c.StatusChangedAt + CampaignSending;
                // Operators report ~97% delivery; the rest are switched-off / invalid numbers.
                c.Delivered = (int)Math.Round(c.Recipients * 0.97);
            }
        }
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------ SMS

    const string Gsm7 = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    /// <summary>SMS parts: 160/153 chars in GSM-7, 70/67 when the text needs Unicode (Cyrillic, ʻ, emoji).</summary>
    public static (int Segments, int Length, bool Unicode) Segments(string text)
    {
        var unicode = text.Any(ch => !Gsm7.Contains(ch));
        var single = unicode ? 70 : 160;
        var multi = unicode ? 67 : 153;
        var len = text.Length;
        var parts = len == 0 ? 0 : len <= single ? 1 : (int)Math.Ceiling(len / (double)multi);
        return (parts, len, unicode);
    }

    public const int MaxSegments = 6;

    /// <summary>
    /// The operator's automatic pre-check. Anything that passes still waits <see cref="TemplateReview"/>
    /// for a (simulated) human moderator.
    /// </summary>
    public static string? PreModerate(string text)
    {
        if (ShortLink().IsMatch(text)) return "Сокращённые ссылки запрещены операторами — укажите полный адрес сайта";
        if (Segments(text).Segments > MaxSegments) return $"Слишком длинный текст: больше {MaxSegments} SMS";
        var letters = text.Where(char.IsLetter).ToList();
        if (letters.Count >= 20 && letters.Count(char.IsUpper) > letters.Count * 0.6) return "Текст написан заглавными буквами";
        return null;
    }

    [GeneratedRegex(@"\b(bit\.ly|tinyurl\.com|t\.co|goo\.gl|cutt\.ly|clck\.ru|is\.gd)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ShortLink();

    // ------------------------------------------------------------ promo codes

    public record PromoCheck(bool Valid, string? Error, long Discount);

    /// <summary>Rules a storefront checkout applies to a promo code (exposed for testing from the admin).</summary>
    public async Task<PromoCheck> CheckPromo(string code, long orderAmount, int? customerId, Platform? platform)
    {
        var p = await db.PromoCodes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpper());
        var now = DateTime.Now;
        if (p is null) return new(false, "Промокод не найден", 0);
        if (!p.IsActive) return new(false, "Промокод отключён", 0);
        if (now < p.StartsAt) return new(false, $"Промокод начнёт действовать {p.StartsAt:dd.MM.yyyy HH:mm}", 0);
        if (now > p.EndsAt) return new(false, "Срок действия промокода истёк", 0);
        if (p.UsageLimit is { } limit && p.UsedCount >= limit) return new(false, "Лимит использований исчерпан", 0);
        if (p.MinOrderAmount is { } min && orderAmount < min) return new(false, $"Минимальная сумма заказа — {min:N0} сум", 0);
        if (platform is { } pl && p.Platforms.Count > 0 && !p.Platforms.Contains(pl.ToString()))
            return new(false, "Промокод не действует на этой платформе", 0);
        if (customerId is { } cid && p.FirstOrderOnly && await db.Orders.AnyAsync(o => o.CustomerId == cid && o.Status != OrderStatus.Cancelled))
            return new(false, "Промокод только для первого заказа", 0);

        var discount = p.Type == DiscountType.Percent ? orderAmount * p.Value / 100 : p.Value;
        if (p.MaxDiscount is { } cap) discount = Math.Min(discount, cap);
        return new(true, null, Math.Min(discount, orderAmount));
    }
}
