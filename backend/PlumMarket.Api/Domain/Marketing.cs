namespace PlumMarket.Api.Domain;

// ---------------------------------------------------------------- Рассылка (Telegram-bot broadcast)

public enum BroadcastStatus { Scheduled, Sent }

public enum DeliveryStatus { Sent, NotSent, Blocked }

public class Broadcast
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string Text { get; set; } = "";
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public BroadcastStatus Status { get; set; }
    /// <summary>When it went out (or is due to, while <see cref="BroadcastStatus.Scheduled"/>).</summary>
    public DateTime SendAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BroadcastRecipient> Recipients { get; set; } = new();
}

public class BroadcastRecipient
{
    public int Id { get; set; }
    public int BroadcastId { get; set; }
    public int CustomerId { get; set; }
    public DeliveryStatus Status { get; set; }
    /// <summary>Per-recipient token in the tracked button link (/r/{token}), so clicks are attributable.</summary>
    public string Token { get; set; } = "";
    public DateTime? ClickedAt { get; set; }
}

// ---------------------------------------------------------------- Промокод

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DiscountType Type { get; set; }
    public long Value { get; set; }
    /// <summary>Cap for percent codes, in сум.</summary>
    public long? MaxDiscount { get; set; }
    /// <summary>Total redemptions allowed; null = unlimited.</summary>
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public long? MinOrderAmount { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool FirstOrderOnly { get; set; }
    /// <summary>Empty = whole catalog.</summary>
    public List<int> CategoryIds { get; set; } = new();
    /// <summary>Empty = every channel.</summary>
    public List<string> Platforms { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

// ---------------------------------------------------------------- Источники (traffic sources)

public enum SourceType { Telegram, Website }

public class TrafficSource
{
    public int Id { get; set; }
    public SourceType Type { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Short code in the tracking link: t.me/bot?start=src_{slug} or /s/{slug}.</summary>
    public string Slug { get; set; } = "";
    public int Clicks { get; set; }
    public int NewUsers { get; set; }
    public int ExistingUsers { get; set; }
    public int Orders { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastVisitAt { get; set; }
}

// ---------------------------------------------------------------- СМС-рассылка

/// <summary>Moderation state shared by SMS templates and SMS campaigns.</summary>
public enum SmsStatus { Moderation, InProgress, Confirmed, Rejected }

public class SmsTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Templates use Moderation → Confirmed | Rejected.</summary>
    public SmsStatus Status { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModeratedAt { get; set; }
}

public class SmsCampaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TemplateId { get; set; }
    public SmsTemplate Template { get; set; } = null!;
    /// <summary>Moderation → InProgress (sending) → Confirmed (delivery confirmed) | Rejected.</summary>
    public SmsStatus Status { get; set; }
    public string? RejectReason { get; set; }
    public int Recipients { get; set; }
    public int Delivered { get; set; }
    public int Segments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime StatusChangedAt { get; set; }
}

// ---------------------------------------------------------------- Пост для канала

public class ChannelPost
{
    public int Id { get; set; }
    public string Channel { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string Text { get; set; } = "";
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
    public DateTime PublishedAt { get; set; }
}

// ---------------------------------------------------------------- Баннер

public enum BannerType { Main, Category }

/// <summary>Where a tap on the banner leads.</summary>
public enum BannerLink { None, Category, Product, Url }

public class Banner
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public BannerType Type { get; set; }
    /// <summary>For <see cref="BannerType.Category"/>: the category page the banner is shown on.</summary>
    public int? CategoryId { get; set; }
    public string? MobileUrl { get; set; }
    public string MobileMediaType { get; set; } = "image";
    public string? DesktopUrl { get; set; }
    public string DesktopMediaType { get; set; } = "image";
    public BannerLink LinkType { get; set; }
    public int? LinkTargetId { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
