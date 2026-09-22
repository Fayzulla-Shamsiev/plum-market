using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>"Источники": tracked links to see which ads / posts / QR codes bring customers.</summary>
[ApiController]
[Route("api/marketing/sources")]
public class SourcesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var rows = await db.TrafficSources.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
        var origin = $"{Request.Scheme}://{Request.Host}";
        return Ok(rows.Select(x => new
        {
            x.Id, x.Type, x.Name, x.Slug, x.Clicks, x.NewUsers, x.ExistingUsers, x.Orders, x.CreatedAt, x.LastVisitAt,
            // The link to publish: Telegram sources open the bot with a start parameter, website ones carry utm_source.
            Link = x.Type == SourceType.Telegram
                ? $"https://t.me/{s.BotUsername}?start=src_{x.Slug}"
                : $"https://{s.StoreDomain}/?utm_source={x.Slug}",
            // Short link through our redirector — counts clicks even before the visitor identifies.
            TrackedLink = $"{origin}/s/{x.Slug}",
            Conversion = x.Clicks > 0 ? Math.Round(x.Orders * 100.0 / x.Clicks, 1) : 0,
        }));
    }

    public record SourceDto(SourceType Type, string Name);

    [HttpPost]
    public async Task<IActionResult> Create(SourceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Укажите название источника" });
        var slug = await UniqueSlug(dto.Name);
        var src = new TrafficSource { Type = dto.Type, Name = dto.Name.Trim(), Slug = slug, CreatedAt = DateTime.Now };
        db.TrafficSources.Add(src);
        await db.SaveChangesAsync();
        return Ok(new { src.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, SourceDto dto)
    {
        var src = await db.TrafficSources.FindAsync(id);
        if (src is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Укажите название источника" });
        // The slug stays: links already printed or published must keep working.
        src.Name = dto.Name.Trim();
        await db.SaveChangesAsync();
        return Ok(new { src.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var src = await db.TrafficSources.FindAsync(id);
        if (src is null) return NotFound();
        db.TrafficSources.Remove(src);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Telegram start parameters allow [A-Za-z0-9_-] only, so names are transliterated.</summary>
    async Task<string> UniqueSlug(string name)
    {
        var map = "а-a б-b в-v г-g д-d е-e ё-yo ж-zh з-z и-i й-y к-k л-l м-m н-n о-o п-p р-r с-s т-t у-u ф-f х-h ц-ts ч-ch ш-sh щ-sh ъ- ы-y ь- э-e ю-yu я-ya ў-o қ-q ғ-g ҳ-h"
            .Split(' ').Select(p => p.Split('-')).ToDictionary(p => p[0][0], p => p[1]);
        var sb = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (map.TryGetValue(ch, out var lat)) sb.Append(lat);
            else if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var slug = sb.ToString().Trim('_');
        if (slug.Length == 0) slug = "src";
        if (slug.Length > 24) slug = slug[..24].TrimEnd('_');
        var candidate = slug;
        for (var i = 2; await db.TrafficSources.AnyAsync(x => x.Slug == candidate); i++) candidate = $"{slug}_{i}";
        return candidate;
    }
}
