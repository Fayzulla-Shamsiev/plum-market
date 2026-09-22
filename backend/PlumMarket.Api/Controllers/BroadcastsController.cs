using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>"Рассылка": mass messages to customers through the store's Telegram bot.</summary>
[ApiController]
[Route("api/marketing/broadcasts")]
public class BroadcastsController(AppDbContext db, MarketingService marketing) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await marketing.TickAsync();
        var rows = await db.Broadcasts.AsNoTracking().OrderByDescending(b => b.SendAt)
            .Select(b => new
            {
                b.Id, b.Name, b.Status, b.SendAt, b.ImageUrl,
                Total = b.Recipients.Count,
                Sent = b.Recipients.Count(r => r.Status == DeliveryStatus.Sent),
                NotSent = b.Recipients.Count(r => r.Status == DeliveryStatus.NotSent),
                Blocked = b.Recipients.Count(r => r.Status == DeliveryStatus.Blocked),
                Clicks = b.Recipients.Count(r => r.ClickedAt != null),
            })
            .ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        await marketing.TickAsync();
        var b = await db.Broadcasts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        var recipients = await db.BroadcastRecipients.AsNoTracking().Where(r => r.BroadcastId == id)
            .Join(db.Customers, r => r.CustomerId, c => c.Id, (r, c) => new
            {
                r.Status, r.ClickedAt, r.Token, CustomerId = c.Id, c.FullName, c.Phone, c.Platform,
            })
            .ToListAsync();
        // Most useful first: who clicked, then delivered, then the ones the bot couldn't reach.
        recipients = recipients.OrderByDescending(r => r.ClickedAt != null)
            .ThenBy(r => r.Status switch { DeliveryStatus.Sent => 0, DeliveryStatus.NotSent => 1, _ => 2 })
            .ThenBy(r => r.FullName).ToList();
        return Ok(new { b.Id, b.Name, b.Text, b.ImageUrl, b.ButtonText, b.ButtonUrl, b.Status, b.SendAt, b.CreatedAt, recipients });
    }

    /// <summary>Step 1 of creating a broadcast: how many customers match, and how many the bot can reach.</summary>
    [HttpPost("audience")]
    public async Task<IActionResult> Audience(AudienceFilter filter)
    {
        var customers = await marketing.ResolveAudience(filter);
        return Ok(new
        {
            total = customers.Count,
            reachable = customers.Count(MarketingService.ReachableByBot),
            sample = customers.OrderByDescending(c => c.LastVisitAt).Take(8).Select(c => new { c.Id, c.FullName, c.Platform }),
        });
    }

    public record CreateBody(string Name, string Text, string? ImageUrl, string? ButtonText, string? ButtonUrl,
        DateTime? SendAt, AudienceFilter Audience);

    [HttpPost]
    public async Task<IActionResult> Create(CreateBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest(new { error = "Укажите название рассылки" });
        if (string.IsNullOrWhiteSpace(body.Text)) return BadRequest(new { error = "Напишите текст сообщения" });
        if (body.Text.Length > (body.ImageUrl is null ? 4096 : 1024))
            return BadRequest(new { error = body.ImageUrl is null ? "Текст длиннее 4096 символов (лимит Telegram)" : "Подпись к картинке длиннее 1024 символов (лимит Telegram)" });
        var hasButton = !string.IsNullOrWhiteSpace(body.ButtonText) || !string.IsNullOrWhiteSpace(body.ButtonUrl);
        if (hasButton && (string.IsNullOrWhiteSpace(body.ButtonText) || !Uri.TryCreate(body.ButtonUrl, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")))
            return BadRequest(new { error = "Для кнопки нужны текст и полная ссылка (https://…)" });
        if (body.SendAt is { } at && at < DateTime.Now.AddMinutes(-1)) return BadRequest(new { error = "Время отправки уже прошло" });

        var audience = await marketing.ResolveAudience(body.Audience);
        if (audience.Count == 0) return BadRequest(new { error = "Не выбрано ни одного получателя" });

        var scheduled = body.SendAt is { } t && t > DateTime.Now.AddMinutes(1);
        var b = new Broadcast
        {
            Name = body.Name.Trim(), Text = body.Text.Trim(), ImageUrl = body.ImageUrl,
            ButtonText = hasButton ? body.ButtonText!.Trim() : null, ButtonUrl = hasButton ? body.ButtonUrl!.Trim() : null,
            CreatedAt = DateTime.Now, SendAt = scheduled ? body.SendAt!.Value : DateTime.Now,
            Status = scheduled ? BroadcastStatus.Scheduled : BroadcastStatus.Sent,
        };
        if (scheduled)
            foreach (var c in audience)
                b.Recipients.Add(new BroadcastRecipient { CustomerId = c.Id, Token = MarketingService.NewToken(), Status = DeliveryStatus.NotSent });
        else
            marketing.Deliver(b, audience);

        db.Broadcasts.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { b.Id });
    }

    /// <summary>Only a scheduled broadcast can be cancelled; sent messages can't be recalled.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await db.Broadcasts.FindAsync(id);
        if (b is null) return NotFound();
        if (b.Status == BroadcastStatus.Sent) return Conflict(new { error = "Отправленную рассылку нельзя удалить" });
        db.Broadcasts.Remove(b);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
