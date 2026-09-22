using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// "Написать продавцу" from the storefront. A visitor who isn't logged in is identified by a random token kept in
/// their browser. Their thread lands in the admin inbox (Чат → channel "Сайт") like any other conversation, and the
/// merchant's replies come back here. A signed-in customer always gets their own thread (on any device), and the
/// support chat can point at one of their orders.
/// </summary>
[ApiController]
[Route("api/shop/chat")]
public partial class ShopChatController(AppDbContext db, ChatService chat, ShopAuth auth) : ControllerBase
{
    [GeneratedRegex("^[A-Za-z0-9-]{16,64}$")]
    private static partial Regex TokenFormat();

    public record OpenBody(string Token, string? Name);

    /// <summary>Returns the visitor's thread (creating it on first use) with its messages.</summary>
    [HttpPost("open")]
    public async Task<IActionResult> Open(OpenBody body)
    {
        if (!TokenFormat().IsMatch(body.Token)) return BadRequest(new { error = "Неверный идентификатор чата" });
        var customer = await auth.CurrentAsync(Request);
        var conv = await Find(body.Token);
        if (conv is null)
        {
            var name = customer?.FullName ?? (string.IsNullOrWhiteSpace(body.Name) ? "Гость сайта" : body.Name.Trim());
            conv = new Conversation
            {
                Channel = ChatChannel.Website, VisitorToken = customer is null ? body.Token : null, CustomerId = customer?.Id,
                DisplayName = name[..Math.Min(name.Length, 60)],
                CreatedAt = DateTime.Now, LastMessageAt = DateTime.Now, LastMessageText = "Новый диалог",
            };
            db.Conversations.Add(conv);
            await db.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(body.Name) && conv.DisplayName == "Гость сайта")
        {
            conv.DisplayName = body.Name.Trim()[..Math.Min(body.Name.Trim().Length, 60)];
            await db.SaveChangesAsync();
        }
        return Ok(await View(conv));
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token) =>
        await Find(token) is { } conv ? Ok(await View(conv)) : NotFound();

    public record SendBody(string Text, int? ProductId, int? OrderId);

    [HttpPost("{token}/messages")]
    public async Task<IActionResult> Send(string token, SendBody body)
    {
        var text = body.Text?.Trim() ?? "";
        if (text.Length == 0) return BadRequest(new { error = "Пустое сообщение" });
        if (text.Length > 2000) return BadRequest(new { error = "Сообщение слишком длинное" });
        var conv = await Find(token);
        if (conv is null) return NotFound();

        // A question asked from a product page carries the product, so the merchant knows what it's about.
        if (body.ProductId is { } pid && await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid) is { } product)
            text = $"🛍 {product.Name.Get()} (/product/{pid})\n{text}";
        // A question about one of the customer's own orders ("Чат с поддержкой").
        else if (body.OrderId is { } oid && conv.CustomerId is { } cid && await db.Orders.AnyAsync(o => o.Id == oid && o.CustomerId == cid))
            text = $"📦 Заказ №{oid} (/profile/orders/{oid})\n{text}";
        await chat.ReceiveAsync(conv, text);
        return Ok(await View(conv));
    }

    /// <summary>Signed in: the customer's website thread (any device). Guest: the thread of this browser's token.</summary>
    async Task<Conversation?> Find(string token)
    {
        if (await auth.CurrentAsync(Request) is { } customer)
            return await db.Conversations.Include(c => c.Customer)
                .Where(c => c.CustomerId == customer.Id && c.ReviewId == null && c.Channel == ChatChannel.Website)
                .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync();
        return TokenFormat().IsMatch(token)
            ? await db.Conversations.Include(c => c.Customer).FirstOrDefaultAsync(c => c.VisitorToken == token && c.ReviewId == null)
            : null;
    }

    async Task<object> View(Conversation conv)
    {
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var messages = await db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conv.Id)
            .OrderBy(m => m.SentAt).ThenBy(m => m.Id)
            .Select(m => new { m.Id, mine = m.Direction == MessageDirection.In, m.Text, m.AttachmentUrl, m.AttachmentType, m.IsAuto, m.SentAt })
            .ToListAsync();
        return new { conv.Id, name = conv.DisplayName, store = settings.StoreName, messages };
    }
}
