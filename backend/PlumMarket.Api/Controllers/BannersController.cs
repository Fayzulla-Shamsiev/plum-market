using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/marketing/banners")]
public class BannersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var banners = await db.Banners.AsNoTracking().OrderBy(b => b.Type).ThenBy(b => b.SortOrder).ToListAsync();
        var cats = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name.Get());
        var prods = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name.Get());
        return Ok(banners.Select(b => new
        {
            b.Id, b.Title, b.Type, b.CategoryId, b.MobileUrl, b.MobileMediaType, b.DesktopUrl, b.DesktopMediaType,
            b.LinkType, b.LinkTargetId, b.LinkUrl, b.IsActive, b.SortOrder, b.CreatedAt,
            Category = b.CategoryId is { } c ? cats.GetValueOrDefault(c) : null,
            LinkLabel = b.LinkType switch
            {
                BannerLink.Category => b.LinkTargetId is { } id ? "Категория: " + cats.GetValueOrDefault(id) : null,
                BannerLink.Product => b.LinkTargetId is { } id ? "Товар: " + prods.GetValueOrDefault(id) : null,
                BannerLink.Url => b.LinkUrl,
                _ => null,
            },
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        await db.Banners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id) is { } b ? Ok(b) : NotFound();

    public record BannerDto(string Title, BannerType Type, int? CategoryId, string? MobileUrl, string? MobileMediaType,
        string? DesktopUrl, string? DesktopMediaType, BannerLink LinkType, int? LinkTargetId, string? LinkUrl, bool IsActive = true);

    [HttpPost]
    public async Task<IActionResult> Create(BannerDto dto)
    {
        var b = new Banner { CreatedAt = DateTime.Now, SortOrder = await db.Banners.CountAsync(x => x.Type == dto.Type) };
        if (await Apply(b, dto) is { } error) return error;
        db.Banners.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { b.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BannerDto dto)
    {
        var b = await db.Banners.FindAsync(id);
        if (b is null) return NotFound();
        if (await Apply(b, dto) is { } error) return error;
        await db.SaveChangesAsync();
        return Ok(new { b.Id });
    }

    public record ActiveBody(bool IsActive);

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ActiveBody body)
    {
        var b = await db.Banners.FindAsync(id);
        if (b is null) return NotFound();
        b.IsActive = body.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { b.Id, b.IsActive });
    }

    public record MoveBody(int Direction);

    /// <summary>Swaps the banner with its neighbour of the same type (slider order).</summary>
    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> Move(int id, MoveBody body)
    {
        var b = await db.Banners.FindAsync(id);
        if (b is null) return NotFound();
        var siblings = await db.Banners.Where(x => x.Type == b.Type).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;
        var idx = siblings.IndexOf(b);
        var target = idx + Math.Sign(body.Direction);
        if (target >= 0 && target < siblings.Count)
            (siblings[idx].SortOrder, siblings[target].SortOrder) = (siblings[target].SortOrder, siblings[idx].SortOrder);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await db.Banners.FindAsync(id);
        if (b is null) return NotFound();
        db.Banners.Remove(b);
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<IActionResult?> Apply(Banner b, BannerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { error = "Укажите название баннера" });
        if (string.IsNullOrWhiteSpace(dto.MobileUrl) && string.IsNullOrWhiteSpace(dto.DesktopUrl))
            return BadRequest(new { error = "Загрузите изображение или видео хотя бы для одной версии" });
        if (dto.Type == BannerType.Category && (dto.CategoryId is null || !await db.Categories.AnyAsync(c => c.Id == dto.CategoryId)))
            return BadRequest(new { error = "Выберите категорию, в которой показывать баннер" });
        switch (dto.LinkType)
        {
            case BannerLink.Category when dto.LinkTargetId is null || !await db.Categories.AnyAsync(c => c.Id == dto.LinkTargetId):
                return BadRequest(new { error = "Выберите категорию для перехода" });
            case BannerLink.Product when dto.LinkTargetId is null || !await db.Products.AnyAsync(p => p.Id == dto.LinkTargetId):
                return BadRequest(new { error = "Выберите товар для перехода" });
            case BannerLink.Url when !Uri.TryCreate(dto.LinkUrl, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https"):
                return BadRequest(new { error = "Укажите полную ссылку (https://…)" });
        }

        b.Title = dto.Title.Trim();
        b.Type = dto.Type;
        b.CategoryId = dto.Type == BannerType.Category ? dto.CategoryId : null;
        b.MobileUrl = dto.MobileUrl;
        b.MobileMediaType = dto.MobileMediaType == "video" ? "video" : "image";
        b.DesktopUrl = dto.DesktopUrl;
        b.DesktopMediaType = dto.DesktopMediaType == "video" ? "video" : "image";
        b.LinkType = dto.LinkType;
        b.LinkTargetId = dto.LinkType is BannerLink.Category or BannerLink.Product ? dto.LinkTargetId : null;
        b.LinkUrl = dto.LinkType == BannerLink.Url ? dto.LinkUrl!.Trim() : null;
        b.IsActive = dto.IsActive;
        return null;
    }
}
