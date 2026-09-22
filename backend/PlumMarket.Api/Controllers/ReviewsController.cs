using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>"Обзоры": customer reviews of products. Answers here and in the chat's "Обзоры" thread stay in sync.</summary>
[ApiController]
[Route("api/marketing/reviews")]
public class ReviewsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(ReviewStatus? status, int? rating, string? search, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 5, 100);
        var q = db.Reviews.AsNoTracking();
        if (status is { } st) q = q.Where(r => r.Status == st);
        if (rating is { } rt) q = q.Where(r => r.Rating == rt);
        var rows = await q.OrderByDescending(r => r.CreatedAt)
            .Join(db.Products, r => r.ProductId, p => p.Id, (r, p) => new
            {
                r.Id, r.Rating, r.Status, r.Comment, r.Reply, r.CreatedAt, r.RepliedAt, r.ProductId,
                ProductName = p.Name, Customer = r.Customer.FullName, CustomerId = r.Customer.Id,
            })
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim();
            rows = rows.Where(r => r.Comment.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || r.Customer.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || r.ProductName.Values.Any(v => v.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        var counts = await db.Reviews.GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);
        var conversations = await db.Conversations.Where(c => c.ReviewId != null)
            .ToDictionaryAsync(c => c.ReviewId!.Value, c => c.Id);
        return Ok(new
        {
            items = rows.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new { r, ConversationId = conversations.TryGetValue(r.Id, out var cid) ? cid : (int?)null })
                .Select(x => new
                {
                    x.r.Id, x.r.Rating, x.r.Status, x.r.Comment, x.r.Reply, x.r.CreatedAt, x.r.RepliedAt, x.r.ProductId,
                    x.r.ProductName, x.r.Customer, x.r.CustomerId, x.ConversationId,
                }),
            total = rows.Count, page, pageSize,
            counts,
            average = rows.Count > 0 ? Math.Round(rows.Average(r => r.Rating), 2) : 0,
        });
    }

    public record ReplyBody(string Text);

    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReplyBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Text)) return BadRequest(new { error = "Напишите ответ" });
        var r = await db.Reviews.FindAsync(id);
        if (r is null) return NotFound();
        r.Reply = body.Text.Trim();
        r.Status = ReviewStatus.Answered;
        r.RepliedAt = DateTime.Now;

        // Mirror the answer into the review's chat thread so both screens show the same history.
        if (await db.Conversations.FirstOrDefaultAsync(c => c.ReviewId == id) is { } conv)
        {
            db.ChatMessages.Add(new ChatMessage
            {
                ConversationId = conv.Id, Direction = MessageDirection.Out, Text = r.Reply, SenderName = "Менеджер", SentAt = r.RepliedAt.Value,
            });
            conv.UnreadCount = 0;
            conv.LastMessageAt = r.RepliedAt.Value;
            conv.LastMessageText = r.Reply;
        }
        await db.SaveChangesAsync();
        return Ok(new { r.Id, r.Status, r.Reply, r.RepliedAt });
    }
}
