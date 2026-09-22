using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    /// <summary>Reference data for filters and headers.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        return Ok(new
        {
            store = new { s.StoreName, s.BotUsername, languages = s.Languages.Split(','), s.OverdueMinutes },
            branches = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.Name }).ToListAsync(),
            employees = await db.Employees.AsNoTracking().Select(e => new { e.Id, e.Name, e.Role }).ToListAsync(),
        });
    }

    public record BonusSettings(bool Enabled, long SpendPerPoint);

    [HttpGet("settings/bonus")]
    public async Task<BonusSettings> GetBonus()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        return new BonusSettings(s.BonusEnabled, s.SpendPerPoint);
    }

    [HttpPut("settings/bonus")]
    public async Task<IActionResult> PutBonus(BonusSettings body)
    {
        if (body.SpendPerPoint < 1) return BadRequest(new { error = "Сумма за 1 балл должна быть больше нуля" });
        var s = await db.Settings.FirstAsync();
        s.BonusEnabled = body.Enabled;
        s.SpendPerPoint = body.SpendPerPoint;
        await db.SaveChangesAsync();
        return Ok(new BonusSettings(s.BonusEnabled, s.SpendPerPoint));
    }

    [HttpGet("autoreplies")]
    public async Task<IActionResult> GetAutoReplies() => Ok(await db.AutoReplyTemplates.AsNoTracking()
        .OrderBy(t => t.Id).Select(t => new { t.Status, t.Language, t.Enabled, t.Text }).ToListAsync());

    public record AutoReplyDto(OrderStatus Status, string Language, bool Enabled, string Text);

    [HttpPut("autoreplies")]
    public async Task<IActionResult> PutAutoReplies(List<AutoReplyDto> body)
    {
        var existing = await db.AutoReplyTemplates.ToListAsync();
        foreach (var dto in body)
        {
            var t = existing.FirstOrDefault(x => x.Status == dto.Status && x.Language == dto.Language);
            if (t is null)
            {
                t = new AutoReplyTemplate { Status = dto.Status, Language = dto.Language };
                db.AutoReplyTemplates.Add(t);
            }
            t.Enabled = dto.Enabled;
            t.Text = dto.Text.Trim();
        }
        await db.SaveChangesAsync();
        return await GetAutoReplies();
    }
}
