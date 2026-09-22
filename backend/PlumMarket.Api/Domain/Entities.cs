namespace PlumMarket.Api.Domain;

// Money is stored as whole UZS (сум) in long — UZS has no practical sub-units,
// and SQLite cannot aggregate decimals server-side.

public enum Platform { Telegram, Website, Instagram }

public enum OrderStatus { New, InProgress, Overdue, Ready, OnTheWay, Completed, Cancelled }

public enum PaymentMethod { Cash, CardToCard, Click, Payme }

public enum DeliveryType { Pickup, Delivery }

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Phone { get; set; }
    /// <summary>Free text shown to shoppers, e.g. "Ежедневно 08:00–22:00".</summary>
    public string? WorkingHours { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = "";
}

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Username { get; set; }
    public string Phone { get; set; } = "";
    public string Language { get; set; } = "ru"; // ru | uz | en
    public Platform Platform { get; set; }
    public long BonusPoints { get; set; }
    // Profile fields the storefront customer can edit ("Редактировать профиль").
    public string? Email { get; set; }
    public string? Country { get; set; }
    public DateOnly? BirthDate { get; set; }
    /// <summary>male | female | null (not specified).</summary>
    public string? Gender { get; set; }
    // "Настройки": what the customer agreed to receive.
    public bool NotifyOrders { get; set; } = true;
    public bool NotifyPromos { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime LastVisitAt { get; set; }
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime StatusChangedAt { get; set; }
    public OrderStatus Status { get; set; }
    public Platform Platform { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DeliveryType DeliveryType { get; set; }

    public long Subtotal { get; set; }
    public long DeliveryCost { get; set; }
    public long CostTotal { get; set; }
    public long Total { get; set; }
    /// <summary>Points credited to the customer when this order was completed (0 if none).</summary>
    public long BonusEarned { get; set; }

    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Comment { get; set; }

    /// <summary>Who receives the order; may differ from the account holder. Null on older/seeded orders = the customer.</summary>
    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }
    /// <summary>Promo code applied at checkout and the amount it took off the items.</summary>
    public string? PromoCode { get; set; }
    public long PromoDiscount { get; set; }
    /// <summary>Set when the customer cancelled the order from the storefront.</summary>
    public string? CancelReason { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    /// <summary>Chosen variant (e.g. "30 см"); also appended to <see cref="ProductName"/> so the admin sees it.</summary>
    public string? Variant { get; set; }
    public int Quantity { get; set; }
    public long Price { get; set; }
    public long CostPrice { get; set; }
}

/// <summary>Daily unique users per traffic source (fed by the storefront in the real system).</summary>
public class SourceVisit
{
    public int Id { get; set; }
    public string Source { get; set; } = "";
    public DateTime Date { get; set; }
    public int Users { get; set; }
}

/// <summary>Auto-responder text sent to a customer when an order moves into <see cref="Status"/>.</summary>
public class AutoReplyTemplate
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string Language { get; set; } = "ru";
    public bool Enabled { get; set; } = true;
    public string Text { get; set; } = "";
}

public class NotificationLog
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public string Channel { get; set; } = "";
    public string Language { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; }
}

/// <summary>Single-row store settings for this prototype (one merchant).</summary>
public class StoreSettings
{
    public int Id { get; set; }
    public string StoreName { get; set; } = "";
    public string BotUsername { get; set; } = "";
    public string Languages { get; set; } = "ru,uz,en";
    public bool BonusEnabled { get; set; }
    /// <summary>How many сум the customer must spend to earn 1 bonus point (1 point = 1 сум).</summary>
    public long SpendPerPoint { get; set; } = 100;
    /// <summary>New / in-progress orders older than this are marked overdue.</summary>
    public int OverdueMinutes { get; set; } = 90;

    // --- Chat settings ("Настройки чата") ---
    /// <summary>Mirror conversations into the merchant's Telegram group.</summary>
    public bool ChatInGroup { get; set; }
    /// <summary>Accept messages written to the Telegram bot into the inbox.</summary>
    public bool ChatWithBot { get; set; } = true;
    public bool ChatAutoReplyEnabled { get; set; }
    /// <summary>Auto-reply text per channel, keyed by <see cref="ChatChannel"/> name.</summary>
    public Dictionary<string, string> ChatAutoReplies { get; set; } = new();

    // --- Telegram channel the bot posts to ("Пост для канала") ---
    public string? ChannelUsername { get; set; }
    public DateTime? ChannelConnectedAt { get; set; }
    // --- Storefront delivery (the admin "Доставка" section will edit these) ---
    public long DeliveryFee { get; set; } = 15000;
    /// <summary>Orders from this amount (after discounts) are delivered free; null = never free.</summary>
    public long? FreeDeliveryFrom { get; set; } = 200000;

    // --- Storefront info pages ("О нас", "Условия доставки", "Условия возврата и обмена") ---
    public string? Phone { get; set; }
    public string? WorkingHours { get; set; }
    public string? AboutText { get; set; }
    /// <summary>Paragraphs separated by blank lines; a line starting with "## " is a heading.</summary>
    public string? DeliveryTerms { get; set; }
    public string? ReturnTerms { get; set; }

    /// <summary>Storefront address used in tracking links and banner/broadcast buttons.</summary>
    public string StoreDomain { get; set; } = "plum-bakery.plum.uz";

    // --- Product import from an external source (Продукты → Импорт → параметры) ---
    public string? ImportSource { get; set; }
    public string? ImportUrl { get; set; }
    public string? ImportApiKey { get; set; }
    public bool ImportAutoSync { get; set; }
}

/// <summary>Storefront login session. Only a hash of the token is stored.</summary>
public class CustomerSession
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

/// <summary>Delivery address saved by a customer at checkout, offered again next time.</summary>
public class CustomerAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Address { get; set; } = "";
    /// <summary>Apartment, entrance, floor, intercom — free text.</summary>
    public string? Details { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public DateTime LastUsedAt { get; set; }
}
