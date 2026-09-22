using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Bump when the model changes. The prototype has no migrations: on start-up a DB with a different
    /// version is dropped and re-seeded (see <see cref="Program"/>).
    /// </summary>
    public const int SchemaVersion = 6;

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SourceVisit> SourceVisits => Set<SourceVisit>();
    public DbSet<AutoReplyTemplate> AutoReplyTemplates => Set<AutoReplyTemplate>();
    public DbSet<NotificationLog> Notifications => Set<NotificationLog>();
    public DbSet<StoreSettings> Settings => Set<StoreSettings>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StockItem> Stock => Set<StockItem>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<BroadcastRecipient> BroadcastRecipients => Set<BroadcastRecipient>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<TrafficSource> TrafficSources => Set<TrafficSource>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsCampaign> SmsCampaigns => Set<SmsCampaign>();
    public DbSet<ChannelPost> ChannelPosts => Set<ChannelPost>();
    public DbSet<Banner> Banners => Set<Banner>();

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().HasIndex(o => o.CreatedAt);
        b.Entity<Order>().HasIndex(o => o.Status);
        b.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        b.Entity<OrderItem>().HasIndex(i => i.ProductId);
        b.Entity<Customer>().HasIndex(c => c.Phone);
        b.Entity<CustomerSession>().HasIndex(s => s.TokenHash).IsUnique();
        b.Entity<CustomerAddress>().HasIndex(a => a.CustomerId);
        b.Entity<SourceVisit>().HasIndex(v => v.Date);
        b.Entity<AutoReplyTemplate>().HasIndex(t => new { t.Status, t.Language }).IsUnique();

        // Enums as strings keep the SQLite file readable.
        b.Entity<Order>().Property(o => o.Status).HasConversion<string>();
        b.Entity<Order>().Property(o => o.Platform).HasConversion<string>();
        b.Entity<Order>().Property(o => o.PaymentMethod).HasConversion<string>();
        b.Entity<Order>().Property(o => o.DeliveryType).HasConversion<string>();
        b.Entity<Customer>().Property(c => c.Platform).HasConversion<string>();
        b.Entity<AutoReplyTemplate>().Property(t => t.Status).HasConversion<string>();

        b.Entity<StoreSettings>().Property(s => s.ChatAutoReplies)
            .HasConversion(JsonConverter<Dictionary<string, string>>(), new DictionaryComparer<Dictionary<string, string>>());

        // --- Catalog ---
        b.Entity<Category>(e =>
        {
            e.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Product>(e =>
        {
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.OwnsMany(p => p.Attributes, a => a.ToJson());
            e.OwnsMany(p => p.Variants, v => v.ToJson());
            e.OwnsMany(p => p.Media, m => m.ToJson());
            e.HasMany(p => p.Stock).WithOne().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<StockItem>(e =>
        {
            e.HasIndex(s => new { s.ProductId, s.BranchId }).IsUnique();
            e.Property(s => s.Status).HasConversion<string>();
        });
        b.Entity<Discount>().Property(d => d.Type).HasConversion<string>();
        b.Entity<Review>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>();
            e.HasIndex(r => r.ProductId);
        });

        // --- Chat ---
        b.Entity<Conversation>(e =>
        {
            e.Property(c => c.Channel).HasConversion<string>();
            e.HasIndex(c => c.LastMessageAt);
            e.HasIndex(c => c.VisitorToken);
            e.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ChatMessage>().Property(m => m.Direction).HasConversion<string>();

        // --- Marketing ---
        b.Entity<Broadcast>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasMany(x => x.Recipients).WithOne().HasForeignKey(r => r.BroadcastId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<BroadcastRecipient>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Token).IsUnique();
        });
        b.Entity<PromoCode>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>();
            e.HasIndex(x => x.Code).IsUnique();
        });
        b.Entity<TrafficSource>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>();
            e.HasIndex(x => x.Slug).IsUnique();
        });
        b.Entity<SmsTemplate>().Property(x => x.Status).HasConversion<string>();
        b.Entity<SmsCampaign>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Banner>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>();
            e.Property(x => x.LinkType).HasConversion<string>();
        });
    }

    static ValueConverter<T, string> JsonConverter<T>() where T : new() => new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null) ?? new T());

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Dictionaries are mutated in place, so change tracking must compare contents, not references.
        c.Properties<Localized>().HaveConversion<LocalizedValueConverter, DictionaryComparer<Localized>>();
    }
}

public class LocalizedValueConverter() : ValueConverter<Localized, string>(
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
    v => JsonSerializer.Deserialize<Localized>(v, (JsonSerializerOptions?)null) ?? new Localized());

public class DictionaryComparer<T>() : ValueComparer<T>(
    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!)
    where T : class;
