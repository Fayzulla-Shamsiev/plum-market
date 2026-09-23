using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Which store the current request belongs to. Filled once per request by <see cref="StoreMiddleware"/> —
/// from the admin's session token for the admin panel, from the storefront address for shop requests — and read
/// back by <see cref="Data.AppDbContext"/> to filter every query.
/// </summary>
public class StoreContext
{
    public int? StoreId { get; set; }
    public Store? Store { get; set; }
    /// <summary>The signed-in administrator, on admin-panel requests.</summary>
    public AdminUser? Admin { get; set; }
}
