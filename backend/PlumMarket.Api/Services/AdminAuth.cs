using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Admin sign-in by phone number and password (spec "Вход и регистрация администратора"). Registration creates
/// the administrator together with a store; the browser keeps a random bearer token whose hash is stored here.
/// </summary>
public class AdminAuth(AppDbContext db, StoreContext tenant)
{
    static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    const int Iterations = 120_000;

    /// <summary>"+998901234567" — one account per phone, however it was typed.</summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits;           // 90 123 45 67
        if (digits.StartsWith('8') && digits.Length == 10) digits = "998" + digits[1..];
        return "+" + digits;
    }

    public static bool IsValidPhone(string normalized) => normalized.Length is >= 11 and <= 16;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2.{Iterations}.{Convert.ToHexString(salt)}.{Convert.ToHexString(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromHexString(parts[2]);
        var expected = Convert.FromHexString(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>The administrator behind this request's bearer token, or null.</summary>
    public async Task<AdminUser?> CurrentAsync(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var hash = TokenHash(header["Bearer ".Length..].Trim());
        var session = await db.AdminSessions.Include(s => s.Admin).ThenInclude(a => a.Store)
            .FirstOrDefaultAsync(s => s.TokenHash == hash);
        if (session is null || session.LastSeenAt < DateTime.Now - Lifetime) return null;
        if (DateTime.Now - session.LastSeenAt > TimeSpan.FromMinutes(30))
        {
            session.LastSeenAt = DateTime.Now;
            await db.SaveChangesAsync();
        }
        return session.Admin;
    }

    /// <summary>Имя → номер телефона → пароль → аккаунт и магазин.</summary>
    public async Task<(AdminUser Admin, string Token)> RegisterAsync(string name, string phone, string password, string storeName)
    {
        var now = DateTime.Now;
        var store = new Store { Name = storeName, Slug = await UniqueSlugAsync(storeName), CreatedAt = now };
        db.Stores.Add(store);
        await db.SaveChangesAsync();
        // From here on this request belongs to the new store, so everything it writes lands in it.
        tenant.StoreId = store.Id;
        tenant.Store = store;

        var admin = new AdminUser
        {
            Name = name, Phone = phone, PasswordHash = HashPassword(password),
            Store = store, CreatedAt = now, LastLoginAt = now,
        };
        db.Admins.Add(admin);
        await db.SaveChangesAsync();

        StoreProvisioner.Provision(db, store, admin);
        await db.SaveChangesAsync();
        return (admin, await OpenSessionAsync(admin));
    }

    public async Task<string> OpenSessionAsync(AdminUser admin)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var now = DateTime.Now;
        db.AdminSessions.Add(new AdminSession { AdminUserId = admin.Id, TokenHash = TokenHash(token), CreatedAt = now, LastSeenAt = now });
        admin.LastLoginAt = now;
        await db.SaveChangesAsync();
        return token;
    }

    public async Task SignOutAsync(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return;
        var hash = TokenHash(header["Bearer ".Length..].Trim());
        await db.AdminSessions.Where(s => s.TokenHash == hash).ExecuteDeleteAsync();
    }

    /// <summary>"Plum Bakery" → "plum-bakery", with a number appended if that address is taken.</summary>
    async Task<string> UniqueSlugAsync(string name)
    {
        var latin = UzTransliterator.ToLatin(name.ToLowerInvariant());
        var slug = new string(latin.Select(ch => char.IsLetterOrDigit(ch) && ch < 128 ? ch : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length is 0) slug = "shop";
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        var taken = await db.Stores.Where(s => s.Slug == slug || s.Slug.StartsWith(slug + "-")).Select(s => s.Slug).ToListAsync();
        if (!taken.Contains(slug)) return slug;
        for (var n = 2; ; n++)
            if (!taken.Contains($"{slug}-{n}")) return $"{slug}-{n}";
    }
}
