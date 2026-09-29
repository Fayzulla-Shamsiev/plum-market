using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// The AI assistant a new administrator lands in after registration: a conversation that sets the shop up, next to
/// a list of what is still left to do. Like every admin endpoint it works only on the signed-in administrator's store.
/// </summary>
[ApiController]
[Route("api/assistant")]
public class AssistantController(SetupAssistant assistant, AppDbContext db, StoreContext tenant, StoreLinks links) : ControllerBase
{
    public record State(bool Enabled, bool Onboarded, string StoreName, string ShopUrl,
        List<AssistantMessageDto> Messages, List<SetupStep> Progress);
    public record Turn(List<AssistantMessageDto> Messages, List<SetupStep> Progress, bool Onboarded, string StoreName);
    public record MessageBody(string? Text, List<string>? Attachments);

    [HttpGet]
    public async Task<State> Get() => new(assistant.Enabled, tenant.Store!.OnboardedAt is not null, tenant.Store.Name,
        links.ShopUrl(tenant.Store), await assistant.HistoryAsync(), await assistant.ProgressAsync());

    [HttpPost("messages")]
    public async Task<ActionResult<Turn>> Send(MessageBody body, CancellationToken ct)
    {
        var text = body.Text?.Trim() ?? "";
        var attachments = body.Attachments ?? [];
        if (text.Length == 0 && attachments.Count == 0) return BadRequest(new { error = "Напишите сообщение." });
        var messages = await assistant.SendAsync(text, attachments, ct);
        return new Turn(messages, await assistant.ProgressAsync(), tenant.Store!.OnboardedAt is not null, tenant.Store.Name);
    }

    /// <summary>
    /// «Перейти в админ-панель»: the first-run setup is over (finished or skipped), so the next sign-in opens the
    /// panel. The assistant stays reachable from the sidebar.
    /// </summary>
    [HttpPost("finish")]
    public async Task<IActionResult> Finish()
    {
        tenant.Store!.OnboardedAt ??= DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(new { onboarded = true });
    }

    public record BranchRef(int Id, string Name);
    public record StockCell(int BranchId, StockStatus Status, int Quantity);
    public record ProductAvailability(int Id, string Name, long Price, string Unit, string? ImageUrl, int Photos, List<StockCell> Stock);
    public record Availability(List<BranchRef> Branches, List<ProductAvailability> Products);

    /// <summary>
    /// The table under «Добавлено товаров»: where each of these products is sold and how many are left there. Read live,
    /// so it always shows what Склад shows, whoever changed it last.
    /// </summary>
    [HttpGet("availability")]
    public async Task<Availability> GetAvailability([FromQuery] string ids)
    {
        var wanted = (ids ?? "").Split(',').Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Take(100).ToList();
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => new BranchRef(b.Id, b.Name)).ToListAsync();
        var products = await db.Products.AsNoTracking().Include(p => p.Stock).Where(p => wanted.Contains(p.Id)).ToListAsync();
        return new Availability(branches, products.OrderBy(p => wanted.IndexOf(p.Id))
            .Select(p => new ProductAvailability(p.Id, p.Name.Get(), p.Price, p.Unit,
                p.Media.FirstOrDefault(m => m.Type == "image")?.Url, p.Media.Count(m => m.Type == "image"),
                p.Stock.Select(s => new StockCell(s.BranchId, s.Status, s.Quantity)).ToList())).ToList());
    }

    /// <param name="Quantity">Pieces in that branch; null = unlimited.</param>
    public record BranchStock(int BranchId, int? Quantity);
    public record ProductBranches(int ProductId, List<BranchStock> Branches);

    /// <summary>Saves the table: a product is sold in the ticked branches only, with the typed quantity or unlimited.</summary>
    [HttpPut("availability")]
    public async Task<IActionResult> PutAvailability(List<ProductBranches> body)
    {
        var storeBranches = await db.Branches.Select(b => b.Id).ToListAsync();
        var ids = body.Select(x => x.ProductId).ToList();
        var products = await db.Products.Include(p => p.Stock).Where(p => ids.Contains(p.Id)).ToListAsync();
        foreach (var item in body)
        {
            if (products.FirstOrDefault(p => p.Id == item.ProductId) is not { } product) continue;
            var cells = item.Branches.Where(b => storeBranches.Contains(b.BranchId)).ToList();
            if (cells.Any(c => c.Quantity is < 0)) return BadRequest(new { error = "Остаток не может быть отрицательным." });
            // Each branch may have its own quantity, so rows are set one by one.
            AssistantTools.SetStock(product, cells.Select(c => c.BranchId).ToList(), null, keepQuantity: true);
            foreach (var c in cells)
            {
                var row = product.Stock.First(s => s.BranchId == c.BranchId);
                row.Status = c.Quantity is null ? StockStatus.Unlimited : c.Quantity > 0 ? StockStatus.Limited : StockStatus.OutOfStock;
                row.Quantity = c.Quantity ?? 0;
                row.UpdatedAt = DateTime.Now;
            }
            product.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return Ok(await GetAvailability(string.Join(',', ids)));
    }

    public record CategoryPhoto(int Id, string Name, string? ImageUrl, int Products);

    /// <summary>The card under «Создано категорий»: each new category with its photo, read live.</summary>
    [HttpGet("categories")]
    public async Task<List<CategoryPhoto>> Categories([FromQuery] string ids)
    {
        var wanted = (ids ?? "").Split(',').Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Take(100).ToList();
        var rows = await db.Categories.AsNoTracking().Where(c => wanted.Contains(c.Id)).ToListAsync();
        var counts = await db.Products.Where(p => p.CategoryId != null && wanted.Contains(p.CategoryId.Value))
            .GroupBy(p => p.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return rows.OrderBy(c => wanted.IndexOf(c.Id))
            .Select(c => new CategoryPhoto(c.Id, c.Name.Get(), c.ImageUrl, counts.GetValueOrDefault(c.Id))).ToList();
    }

    public record PhotoBody(string Kind, int Id, string? Url);

    /// <summary>
    /// The photo button in those cards: a product's main photo (first in its gallery) or a category's image. Only files
    /// uploaded to this app are accepted; an empty url removes the photo.
    /// </summary>
    [HttpPut("photo")]
    public async Task<IActionResult> Photo(PhotoBody body)
    {
        var url = body.Url?.Trim();
        if (url is { Length: > 0 } && (!url.StartsWith("/uploads/", StringComparison.Ordinal) || url.Contains("..")))
            return BadRequest(new { error = "Сначала загрузите фото." });
        if (body.Kind == "category")
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == body.Id);
            if (c is null) return NotFound(new { error = "Категория не найдена." });
            c.ImageUrl = string.IsNullOrEmpty(url) ? null : url;
        }
        else
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == body.Id);
            if (p is null) return NotFound(new { error = "Товар не найден." });
            var main = p.Media.FirstOrDefault(m => m.Type == "image");
            if (main is not null) p.Media.Remove(main);
            if (!string.IsNullOrEmpty(url)) p.Media.Insert(0, new ProductMedia { Url = url, Type = "image" });
            p.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return Ok(new { url });
    }

    /// <summary>«Начать заново»: forgets the conversation; nothing in the shop is undone.</summary>
    [HttpDelete]
    public async Task<State> Reset()
    {
        await assistant.ResetAsync();
        return await Get();
    }
}
