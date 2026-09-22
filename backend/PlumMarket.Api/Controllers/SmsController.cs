using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// "СМС-рассылка". Operators in Uzbekistan require every SMS template to pass moderation before it can be
/// sent; campaigns then go through the gateway. Both are simulated (see <see cref="MarketingService"/>).
/// </summary>
[ApiController]
[Route("api/marketing/sms")]
public class SmsController(AppDbContext db, MarketingService marketing) : ControllerBase
{
    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(SmsStatus? status)
    {
        await marketing.TickAsync();
        var q = db.SmsCampaigns.AsNoTracking();
        if (status is { } st) q = q.Where(c => c.Status == st);
        var rows = await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Name, c.Status, c.RejectReason, c.Recipients, c.Delivered, c.Segments, c.CreatedAt, c.StatusChangedAt,
                Template = c.Template.Name, c.Template.Text,
            })
            .ToListAsync();
        var counts = await db.SmsCampaigns.GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);
        return Ok(new { items = rows, counts });
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates()
    {
        await marketing.TickAsync();
        var rows = await db.SmsTemplates.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(rows.Select(t =>
        {
            var seg = MarketingService.Segments(t.Text);
            return new { t.Id, t.Name, t.Text, t.Status, t.RejectReason, t.CreatedAt, t.ModeratedAt, seg.Segments, seg.Unicode };
        }));
    }

    public record TemplateDto(string Name, string Text);

    /// <summary>Submits a template for moderation. Obvious violations are rejected at once, with the reason.</summary>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate(TemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Укажите имя шаблона" });
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest(new { error = "Напишите текст сообщения" });
        var text = dto.Text.Trim();
        var reason = MarketingService.PreModerate(text);
        var t = new SmsTemplate
        {
            Name = dto.Name.Trim(), Text = text, CreatedAt = DateTime.Now,
            Status = reason is null ? SmsStatus.Moderation : SmsStatus.Rejected,
            RejectReason = reason, ModeratedAt = reason is null ? null : DateTime.Now,
        };
        db.SmsTemplates.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { t.Id, t.Status, t.RejectReason });
    }

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var t = await db.SmsTemplates.FindAsync(id);
        if (t is null) return NotFound();
        if (await db.SmsCampaigns.AnyAsync(c => c.TemplateId == id)) return Conflict(new { error = "Шаблон используется в рассылках" });
        db.SmsTemplates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Live counter for the editor: characters, parts, encoding and whether the pre-check would pass.</summary>
    [HttpPost("segments")]
    public IActionResult Segments(TemplateDto dto)
    {
        var s = MarketingService.Segments(dto.Text ?? "");
        return Ok(new { s.Segments, s.Length, s.Unicode, max = MarketingService.MaxSegments, problem = MarketingService.PreModerate(dto.Text ?? "") });
    }

    public record CampaignDto(string Name, int TemplateId, AudienceFilter Audience);

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign(CampaignDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Укажите название рассылки" });
        var t = await db.SmsTemplates.FindAsync(dto.TemplateId);
        if (t is null) return BadRequest(new { error = "Шаблон не найден" });
        if (t.Status != SmsStatus.Confirmed) return BadRequest(new { error = "Шаблон ещё не прошёл модерацию" });
        var audience = await marketing.ResolveAudience(dto.Audience);
        var withPhone = audience.Count(c => !string.IsNullOrWhiteSpace(c.Phone));
        if (withPhone == 0) return BadRequest(new { error = "Нет получателей с номером телефона" });

        var c = new SmsCampaign
        {
            Name = dto.Name.Trim(), Template = t, Status = SmsStatus.Moderation, Recipients = withPhone,
            Segments = MarketingService.Segments(t.Text).Segments, CreatedAt = DateTime.Now, StatusChangedAt = DateTime.Now,
        };
        db.SmsCampaigns.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { c.Id });
    }
}
