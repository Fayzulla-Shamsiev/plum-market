using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Storefront sign-in by phone number and name, as the MVP spec describes (no SMS code yet). The browser keeps a
/// random bearer token; the database stores only its SHA-256 hash.
/// ⚠️ Without SMS confirmation anyone who knows a phone number can sign in as that customer. The login/registration
/// spec will add an OTP step before this can go live.
/// </summary>
public class ShopAuth(AppDbContext db)
{
    static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>The signed-in customer for this request, or null.</summary>
    public async Task<Customer?> CurrentAsync(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var hash = Hash(header["Bearer ".Length..].Trim());
        var session = await db.CustomerSessions.Include(s => s.Customer).FirstOrDefaultAsync(s => s.TokenHash == hash);
        if (session is null || session.LastSeenAt < DateTime.Now - Lifetime) return null;
        if (DateTime.Now - session.LastSeenAt > TimeSpan.FromHours(1))
        {
            session.LastSeenAt = DateTime.Now;
            session.Customer.LastVisitAt = DateTime.Now;
            await db.SaveChangesAsync();
        }
        return session.Customer;
    }

    /// <summary>
    /// Finds the customer by phone or registers a new one, and opens a session. An existing customer's name is
    /// updated when the entered one is different (it's what they want to be called and what orders will show).
    /// </summary>
    public async Task<(Customer Customer, string Token)> SignInAsync(string phone, string name, string lang)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        var now = DateTime.Now;
        if (customer is null)
        {
            customer = new Customer
            {
                FullName = name, Phone = phone, Platform = Platform.Website, Language = lang == "ru" ? "ru" : "uz",
                CreatedAt = now, LastVisitAt = now,
            };
            db.Customers.Add(customer);
        }
        else
        {
            if (!customer.FullName.StartsWith(name, StringComparison.CurrentCultureIgnoreCase)) customer.FullName = name;
            customer.LastVisitAt = now;
        }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        db.CustomerSessions.Add(new CustomerSession { Customer = customer, TokenHash = Hash(token), CreatedAt = now, LastSeenAt = now });
        await db.SaveChangesAsync();
        return (customer, token);
    }

    public async Task SignOutAsync(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return;
        var hash = Hash(header["Bearer ".Length..].Trim());
        await db.CustomerSessions.Where(s => s.TokenHash == hash).ExecuteDeleteAsync();
    }
}
