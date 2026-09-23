namespace PlumMarket.Api.Domain;

/// <summary>
/// Everything a merchant owns belongs to exactly one <see cref="Store"/>. The data separation required by the
/// spec ("каждый администратор видит и управляет только своим магазином") is enforced in one place: a global
/// query filter on every entity that implements this interface (see AppDbContext).
/// </summary>
public interface IStoreOwned
{
    int StoreId { get; set; }
}

/// <summary>A shop created at registration: one administrator, one storefront.</summary>
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Storefront address of this shop — /shop/{slug} in the prototype, a subdomain in production.</summary>
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>The entrepreneur who registered the store and signs in to its admin panel.</summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Normalised to digits with a leading "+" so "+998 90 123 45 67" and "998901234567" are one account.</summary>
    public string Phone { get; set; } = "";
    /// <summary>PBKDF2 hash, salt and iteration count in one string (see AdminAuth).</summary>
    public string PasswordHash { get; set; } = "";
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}

/// <summary>Admin-panel session. Only a hash of the bearer token is stored.</summary>
public class AdminSession
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = "";
    public int AdminUserId { get; set; }
    public AdminUser Admin { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
