using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Public short links that count clicks and redirect:
/// /r/{token} — a broadcast button (per recipient), /s/{slug} — a traffic source.
/// </summary>
[ApiController]
public class TrackingController(AppDbContext db) : ControllerBase
{
    [HttpGet("/r/{token}")]
    public async Task<IActionResult> BroadcastClick(string token)
    {
        var r = await db.BroadcastRecipients.FirstOrDefaultAsync(x => x.Token == token);
        if (r is null) return NotFound();
        var b = await db.Broadcasts.FindAsync(r.BroadcastId);
        r.ClickedAt ??= DateTime.Now; // unique clicks: the first one counts
        await db.SaveChangesAsync();
        return b?.ButtonUrl is { } url ? Redirect(url) : NotFound();
    }

    [HttpGet("/s/{slug}")]
    public async Task<IActionResult> SourceClick(string slug)
    {
        var s = await db.TrafficSources.FirstOrDefaultAsync(x => x.Slug == slug);
        if (s is null) return NotFound();
        s.Clicks++;
        s.LastVisitAt = DateTime.Now;
        await db.SaveChangesAsync();
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        // New/existing users and orders are attributed by the storefront once the visitor identifies.
        return Redirect(s.Type == SourceType.Telegram
            ? $"https://t.me/{settings.BotUsername}?start=src_{s.Slug}"
            : $"https://{settings.StoreDomain}/?utm_source={s.Slug}");
    }
}
