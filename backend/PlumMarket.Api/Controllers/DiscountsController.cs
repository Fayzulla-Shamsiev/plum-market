using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/discounts")]
public class DiscountsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int? branchId)
    {
        var discounts = await db.Discounts.AsNoTracking().OrderByDescending(d => d.CreatedAt).ToListAsync();
        // Empty BranchIds means "every branch".
        if (branchId is { } b) discounts = discounts.Where(d => d.BranchIds.Count == 0 || d.BranchIds.Contains(b)).ToList();
        var names = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name.Get());
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        var now = DateTime.Now;
        return Ok(discounts.Select(d => new
        {
            d.Id, d.Name, d.Type, d.Value, d.StartsAt, d.EndsAt, d.MinOrderAmount, d.IsActive, d.ProductIds, d.BranchIds,
            Products = d.ProductIds.Where(names.ContainsKey).Select(id => names[id]).ToList(),
            Branches = d.BranchIds.Count == 0 ? ["Все филиалы"] : d.BranchIds.Where(branches.ContainsKey).Select(id => branches[id]).ToList(),
            State = !d.IsActive ? "disabled" : now < d.StartsAt ? "scheduled" : now > d.EndsAt ? "expired" : "active",
        }));
    }

    public record DiscountDto(string Name, DiscountType Type, long Value, List<int> ProductIds, List<int>? BranchIds,
        DateTime? StartsAt, DateTime EndsAt, long? MinOrderAmount, bool IsActive = true);

    [HttpPost]
    public async Task<IActionResult> Create(DiscountDto dto)
    {
        var d = new Discount { CreatedAt = DateTime.Now };
        if (await Apply(d, dto) is { } error) return error;
        db.Discounts.Add(d);
        await db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DiscountDto dto)
    {
        var d = await db.Discounts.FindAsync(id);
        if (d is null) return NotFound();
        if (await Apply(d, dto) is { } error) return error;
        await db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await db.Discounts.FindAsync(id);
        if (d is null) return NotFound();
        db.Discounts.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }

    async Task<IActionResult?> Apply(Discount d, DiscountDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { error = "Укажите название скидки" });
        if (dto.Value <= 0) return BadRequest(new { error = "Размер скидки должен быть больше нуля" });
        if (dto.Type == DiscountType.Percent && dto.Value > 100) return BadRequest(new { error = "Скидка в процентах — не больше 100" });
        if (dto.ProductIds.Count == 0) return BadRequest(new { error = "Выберите хотя бы один товар" });
        var starts = dto.StartsAt ?? d.StartsAt;
        if (starts == default) starts = DateTime.Now;
        if (dto.EndsAt <= starts) return BadRequest(new { error = "Дата окончания должна быть позже начала" });

        if (dto.Type == DiscountType.Fixed)
        {
            var cheapest = await db.Products.Where(p => dto.ProductIds.Contains(p.Id)).MinAsync(p => (long?)p.Price);
            if (cheapest is { } min && dto.Value >= min)
                return BadRequest(new { error = $"Фиксированная скидка больше цены самого дешёвого выбранного товара ({min:N0} сум)" });
        }

        d.Name = dto.Name.Trim();
        d.Type = dto.Type;
        d.Value = dto.Value;
        d.ProductIds = dto.ProductIds.Distinct().ToList();
        d.BranchIds = (dto.BranchIds ?? []).Distinct().ToList();
        d.StartsAt = starts;
        d.EndsAt = dto.EndsAt;
        d.MinOrderAmount = dto.MinOrderAmount is > 0 ? dto.MinOrderAmount : null;
        d.IsActive = dto.IsActive;
        return null;
    }
}
