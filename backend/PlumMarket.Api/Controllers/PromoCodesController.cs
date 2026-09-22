using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/marketing/promocodes")]
public partial class PromoCodesController(AppDbContext db, MarketingService marketing) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var now = DateTime.Now;
        var rows = await db.PromoCodes.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(rows.Select(p => new
        {
            p.Id, p.Code, p.Type, p.Value, p.MaxDiscount, p.UsageLimit, p.UsedCount, p.MinOrderAmount,
            p.StartsAt, p.EndsAt, p.FirstOrderOnly, p.CategoryIds, p.Platforms, p.IsActive, p.CreatedAt,
            State = !p.IsActive ? "disabled"
                : now < p.StartsAt ? "scheduled"
                : now > p.EndsAt ? "expired"
                : p.UsageLimit is { } l && p.UsedCount >= l ? "exhausted"
                : "active",
        }));
    }

    public record PromoDto(string Code, DiscountType Type, long Value, long? MaxDiscount, int? UsageLimit, long? MinOrderAmount,
        DateTime? StartsAt, DateTime EndsAt, bool FirstOrderOnly, List<int>? CategoryIds, List<string>? Platforms, bool IsActive = true);

    [HttpPost]
    public async Task<IActionResult> Create(PromoDto dto)
    {
        var p = new PromoCode { CreatedAt = DateTime.Now };
        if (await Apply(p, dto) is { } error) return error;
        db.PromoCodes.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PromoDto dto)
    {
        var p = await db.PromoCodes.FindAsync(id);
        if (p is null) return NotFound();
        if (await Apply(p, dto) is { } error) return error;
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.PromoCodes.FindAsync(id);
        if (p is null) return NotFound();
        db.PromoCodes.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Random readable code (no 0/O, 1/I look-alikes).</summary>
    [HttpGet("generate")]
    public async Task<IActionResult> Generate()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do code = "PLUM" + string.Concat(Enumerable.Range(0, 5).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]));
        while (await db.PromoCodes.AnyAsync(p => p.Code == code));
        return Ok(new { code });
    }

    /// <summary>Checks a code the way checkout will (the admin's "Проверить" box).</summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check(string code, long amount, int? customerId, Platform? platform) =>
        Ok(await marketing.CheckPromo(code, amount, customerId, platform));

    async Task<IActionResult?> Apply(PromoCode p, PromoDto dto)
    {
        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        if (!CodeFormat().IsMatch(code)) return BadRequest(new { error = "Код: 3–20 латинских букв, цифр, «-» или «_»" });
        if (await db.PromoCodes.AnyAsync(x => x.Code == code && x.Id != p.Id)) return Conflict(new { error = $"Промокод {code} уже существует" });
        if (dto.Value <= 0) return BadRequest(new { error = "Размер скидки должен быть больше нуля" });
        if (dto.Type == DiscountType.Percent && dto.Value > 100) return BadRequest(new { error = "Скидка в процентах — не больше 100" });
        if (dto.UsageLimit is < 1) return BadRequest(new { error = "Лимит использований должен быть не меньше 1" });
        var starts = dto.StartsAt ?? (p.StartsAt == default ? DateTime.Now : p.StartsAt);
        if (dto.EndsAt <= starts) return BadRequest(new { error = "Дата окончания должна быть позже начала" });
        if (dto.Type == DiscountType.Fixed && dto.MinOrderAmount is { } min && dto.Value >= min)
            return BadRequest(new { error = "Фиксированная скидка должна быть меньше минимальной суммы заказа" });

        p.Code = code;
        p.Type = dto.Type;
        p.Value = dto.Value;
        p.MaxDiscount = dto.Type == DiscountType.Percent && dto.MaxDiscount is > 0 ? dto.MaxDiscount : null;
        p.UsageLimit = dto.UsageLimit;
        p.MinOrderAmount = dto.MinOrderAmount is > 0 ? dto.MinOrderAmount : null;
        p.StartsAt = starts;
        p.EndsAt = dto.EndsAt;
        p.FirstOrderOnly = dto.FirstOrderOnly;
        p.CategoryIds = dto.CategoryIds ?? [];
        p.Platforms = (dto.Platforms ?? []).Where(x => Enum.TryParse<Platform>(x, out _)).Distinct().ToList();
        p.IsActive = dto.IsActive;
        return null;
    }

    [GeneratedRegex("^[A-Z0-9_-]{3,20}$")]
    private static partial Regex CodeFormat();
}
