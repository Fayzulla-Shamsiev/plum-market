using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(AppDbContext db, ChatService chat) : ControllerBase
{
    /// <summary>
    /// Inbox list. <paramref name="filter"/>: all | unread | instagram | reviews (the spec's categories);
    /// <paramref name="channel"/> narrows by platform (the dropdown next to "Все чаты").
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> List(string filter = "all", ChatChannel? channel = null, string? search = null)
    {
        var q = db.Conversations.AsNoTracking();
        q = filter switch
        {
            "unread" => q.Where(c => c.UnreadCount > 0),
            "instagram" => q.Where(c => c.Channel == ChatChannel.Instagram),
            "reviews" => q.Where(c => c.ReviewId != null),
            _ => q.Where(c => c.ReviewId == null),
        };
        if (channel is { } ch) q = q.Where(c => c.Channel == ch);

        var rows = await q.OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationRow(c.Id, c.DisplayName, c.Handle, c.Channel, c.CustomerId,
                c.Customer != null ? c.Customer.Phone : null, c.ReviewId, c.UnreadCount, c.LastMessageAt, c.LastMessageText))
            .ToListAsync();

        // Names are Cyrillic; SQLite's case-insensitive matching is ASCII-only, so search in memory.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(r => r.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                                   || (r.Handle ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                                   || (r.Phone ?? "").Replace(" ", "").Contains(term.Replace(" ", ""))
                                   || r.LastMessageText.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var unread = await db.Conversations.Where(c => c.UnreadCount > 0)
            .GroupBy(c => c.ReviewId != null).Select(g => new { Reviews = g.Key, Count = g.Count() }).ToListAsync();
        return Ok(new
        {
            items = rows,
            counts = new
            {
                unread = unread.Where(u => !u.Reviews).Sum(u => u.Count),
                reviews = unread.Where(u => u.Reviews).Sum(u => u.Count),
            },
        });
    }

    public record ConversationRow(int Id, string DisplayName, string? Handle, ChatChannel Channel, int? CustomerId, string? Phone,
        int? ReviewId, int UnreadCount, DateTime LastMessageAt, string LastMessageText);

    /// <summary>Opens a thread: returns its messages and marks it read.</summary>
    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var conv = await db.Conversations.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == id);
        if (conv is null) return NotFound();
        if (conv.UnreadCount > 0)
        {
            conv.UnreadCount = 0;
            await db.SaveChangesAsync();
        }
        return Ok(await Detail(conv));
    }

    async Task<object> Detail(Conversation conv)
    {
        var messages = await db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conv.Id)
            .OrderBy(m => m.SentAt).ThenBy(m => m.Id).ToListAsync();
        object? customer = conv.Customer is { } c
            ? new
            {
                c.Id, c.FullName, c.Phone, c.Username, c.Platform, c.Language, c.BonusPoints,
                Orders = await db.Orders.CountAsync(o => o.CustomerId == c.Id && o.Status != OrderStatus.Cancelled),
            }
            : null;
        object? review = null;
        if (conv.ReviewId is { } rid)
            review = await db.Reviews.AsNoTracking().Where(r => r.Id == rid)
                .Join(db.Products, r => r.ProductId, p => p.Id, (r, p) => new { r.Id, r.Rating, r.Status, r.ProductId, ProductName = p.Name })
                .Select(r => new { r.Id, r.Rating, r.Status, r.ProductId, ProductName = r.ProductName })
                .FirstOrDefaultAsync();
        return new
        {
            conv.Id, conv.DisplayName, conv.Handle, conv.Channel, conv.ReviewId, customer, review, messages,
        };
    }

    public record SendBody(string? Text, string? AttachmentUrl, string? AttachmentName, string? AttachmentType, string? SenderName);

    [HttpPost("conversations/{id:int}/messages")]
    public async Task<IActionResult> Send(int id, SendBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Text) && string.IsNullOrWhiteSpace(body.AttachmentUrl))
            return BadRequest(new { error = "Пустое сообщение" });
        var conv = await db.Conversations.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == id);
        if (conv is null) return NotFound();
        await chat.SendAsync(conv, body.Text ?? "", body.SenderName ?? "Менеджер", body.AttachmentUrl, body.AttachmentName, body.AttachmentType);
        return Ok(await Detail(conv));
    }

    /// <summary>Clients page "Чат" button: finds the customer's thread or starts one on their platform.</summary>
    [HttpPost("for-customer/{customerId:int}")]
    public async Task<IActionResult> ForCustomer(int customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();
        var conv = await db.Conversations.Where(c => c.CustomerId == customerId && c.ReviewId == null)
            .OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync();
        if (conv is null)
        {
            conv = new Conversation
            {
                CustomerId = customerId, Channel = ChatService.ChannelFor(customer.Platform), DisplayName = customer.FullName,
                Handle = customer.Username is null ? null : "@" + customer.Username,
                CreatedAt = DateTime.Now, LastMessageAt = DateTime.Now, LastMessageText = "Новый диалог",
            };
            db.Conversations.Add(conv);
            await db.SaveChangesAsync();
        }
        return Ok(new { conv.Id });
    }

    [HttpGet("unread")]
    public async Task<IActionResult> Unread() => Ok(new { count = await db.Conversations.CountAsync(c => c.UnreadCount > 0) });

    public record ChatSettingsDto(bool ChatInGroup, bool ChatWithBot, bool AutoReplyEnabled, Dictionary<string, string> AutoReplies);

    [HttpGet("settings")]
    public async Task<ChatSettingsDto> GetSettings()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var replies = Enum.GetNames<ChatChannel>().ToDictionary(n => n, n => s.ChatAutoReplies.GetValueOrDefault(n, ""));
        return new ChatSettingsDto(s.ChatInGroup, s.ChatWithBot, s.ChatAutoReplyEnabled, replies);
    }

    [HttpPut("settings")]
    public async Task<ChatSettingsDto> PutSettings(ChatSettingsDto body)
    {
        var s = await db.Settings.FirstAsync();
        s.ChatInGroup = body.ChatInGroup;
        s.ChatWithBot = body.ChatWithBot;
        s.ChatAutoReplyEnabled = body.AutoReplyEnabled;
        s.ChatAutoReplies = Enum.GetNames<ChatChannel>()
            .ToDictionary(n => n, n => (body.AutoReplies.GetValueOrDefault(n) ?? "").Trim());
        await db.SaveChangesAsync();
        return await GetSettings();
    }
}
