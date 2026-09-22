using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>Storefront account: sign in by phone + name, profile basics, saved delivery addresses.</summary>
[ApiController]
[Route("api/shop/account")]
public class ShopAccountController(AppDbContext db, ShopAuth auth) : ControllerBase
{
    public record LoginBody(string Phone, string Name, string? ChatToken, string? Lang);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginBody body)
    {
        if (Phone.Normalize(body.Phone) is not { } phone) return BadRequest(new { error = "Введите номер в формате +998 XX XXX XX XX" });
        var name = (body.Name ?? "").Trim();
        if (name.Length < 2) return BadRequest(new { error = "Введите имя" });
        if (name.Length > 60) name = name[..60];

        var (customer, token) = await auth.SignInAsync(phone, name, body.Lang ?? "ru");
        // A chat started as a guest joins the account, so the merchant sees who wrote.
        if (!string.IsNullOrWhiteSpace(body.ChatToken))
        {
            var guest = await db.Conversations.Where(c => c.VisitorToken == body.ChatToken && c.CustomerId == null).ToListAsync();
            foreach (var c in guest)
            {
                c.CustomerId = customer.Id;
                c.DisplayName = customer.FullName;
            }
            await db.SaveChangesAsync();
        }
        return Ok(new { token, me = await Me(customer) });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await auth.SignOutAsync(Request);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe() =>
        await auth.CurrentAsync(Request) is { } c ? Ok(await Me(c)) : Unauthorized(new { error = "Войдите по номеру телефона" });

    public record ProfileBody(string FirstName, string? LastName, string Phone, string? Email, string? Country, DateOnly? BirthDate, string? Gender);

    /// <summary>"Редактировать профиль": name, surname, phone, e-mail, country, birth date, gender.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> PutMe(ProfileBody body)
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized(new { error = "Войдите по номеру телефона" });
        if (string.IsNullOrWhiteSpace(body.FirstName)) return BadRequest(new { error = "Введите имя" });
        if (Phone.Normalize(body.Phone) is not { } phone) return BadRequest(new { error = "Введите номер в формате +998 XX XXX XX XX" });
        var email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email.Trim();
        if (email is not null && (email.Length > 120 || !System.Net.Mail.MailAddress.TryCreate(email, out _) || !email.Contains('.')))
            return BadRequest(new { error = "Проверьте адрес электронной почты" });
        if (body.BirthDate is { } bd && (bd > DateOnly.FromDateTime(DateTime.Today) || bd.Year < 1900))
            return BadRequest(new { error = "Проверьте дату рождения" });
        if (body.Gender is not (null or "male" or "female")) return BadRequest(new { error = "Неизвестное значение пола" });
        if (phone != c.Phone && await db.Customers.AnyAsync(x => x.Phone == phone && x.Id != c.Id))
            return Conflict(new { error = "Этот номер уже привязан к другому аккаунту" });

        var customer = await db.Customers.FirstAsync(x => x.Id == c.Id);
        var fullName = $"{body.FirstName.Trim()} {body.LastName?.Trim()}".Trim();
        customer.FullName = fullName[..Math.Min(fullName.Length, 80)];
        customer.Phone = phone;
        customer.Email = email;
        customer.Country = string.IsNullOrWhiteSpace(body.Country) ? null : body.Country.Trim()[..Math.Min(body.Country.Trim().Length, 60)];
        customer.BirthDate = body.BirthDate;
        customer.Gender = body.Gender;
        await db.SaveChangesAsync();
        return Ok(await Me(customer));
    }

    public record SettingsBody(string? Language, bool NotifyOrders, bool NotifyPromos);

    /// <summary>"Настройки": interface language (also used for auto-replies) and which messages the customer receives.</summary>
    [HttpPut("settings")]
    public async Task<IActionResult> PutSettings(SettingsBody body)
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized(new { error = "Войдите по номеру телефона" });
        var customer = await db.Customers.FirstAsync(x => x.Id == c.Id);
        if (body.Language is { } lang) customer.Language = lang == "ru" ? "ru" : "uz";
        customer.NotifyOrders = body.NotifyOrders;
        customer.NotifyPromos = body.NotifyPromos;
        await db.SaveChangesAsync();
        return Ok(await Me(customer));
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses()
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized();
        return Ok(await db.CustomerAddresses.AsNoTracking().Where(a => a.CustomerId == c.Id)
            .OrderByDescending(a => a.LastUsedAt).Take(10).ToListAsync());
    }

    [HttpDelete("addresses/{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized();
        await db.CustomerAddresses.Where(a => a.Id == id && a.CustomerId == c.Id).ExecuteDeleteAsync();
        return NoContent();
    }

    async Task<object> Me(Customer c)
    {
        var parts = c.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var active = new[] { OrderStatus.Completed, OrderStatus.Cancelled };
        return new
        {
            c.Id,
            name = c.FullName,
            firstName = parts.ElementAtOrDefault(0) ?? "",
            lastName = parts.ElementAtOrDefault(1) ?? "",
            c.Phone,
            c.Email, c.Country, c.BirthDate, c.Gender, c.Language, c.NotifyOrders, c.NotifyPromos,
            c.BonusPoints,
            ordersCount = await db.Orders.CountAsync(o => o.CustomerId == c.Id),
            activeOrders = await db.Orders.CountAsync(o => o.CustomerId == c.Id && !active.Contains(o.Status)),
        };
    }
}
