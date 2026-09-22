using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// Storefront profile extras: "Мои отзывы" (products waiting for a rating and the customer's own reviews) and the
/// public info pages (О нас, Условия доставки, Условия возврата и обмена, Связаться с нами).
/// New reviews land in the admin's Маркетинг → Обзоры and Чат → Обзоры, exactly like reviews from other channels.
/// </summary>
[ApiController]
[Route("api/shop")]
public class ShopProfileController(AppDbContext db, ShopAuth auth) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> Info()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Name, b.Address, b.Phone, b.WorkingHours, b.Lat, b.Lng }).ToListAsync();
        return Ok(new
        {
            store = new { name = s.StoreName, s.Phone, s.WorkingHours, about = s.AboutText, bot = s.BotUsername },
            branches,
            delivery = new { fee = s.DeliveryFee, freeFrom = s.FreeDeliveryFrom, terms = s.DeliveryTerms },
            returns = new { terms = s.ReturnTerms },
        });
    }

    /// <summary>
    /// Pending = products from the customer's completed orders they haven't reviewed yet (newest order first).
    /// Rated = their reviews with the store's reply.
    /// </summary>
    [HttpGet("account/reviews")]
    public async Task<IActionResult> MyReviews(string lang = "ru")
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized(new { error = "Войдите по номеру телефона" });
        var reviews = await db.Reviews.AsNoTracking().Where(r => r.CustomerId == c.Id).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var reviewed = reviews.Select(r => r.ProductId).ToHashSet();

        var bought = await db.OrderItems.AsNoTracking()
            .Join(db.Orders.Where(o => o.CustomerId == c.Id && o.Status == OrderStatus.Completed), i => i.OrderId, o => o.Id,
                (i, o) => new { i.ProductId, o.Id, o.StatusChangedAt })
            .OrderByDescending(x => x.StatusChangedAt)
            .ToListAsync();
        var pendingIds = bought.Where(b => !reviewed.Contains(b.ProductId)).DistinctBy(b => b.ProductId).ToList();

        var ids = pendingIds.Select(p => p.ProductId).Concat(reviews.Select(r => r.ProductId)).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        object Product(int id) => products.TryGetValue(id, out var p)
            ? new { id, name = p.Name.Get(lang), image = p.Media.FirstOrDefault(m => m.Type == "image")?.Url, available = p.IsActive }
            : new { id, name = $"#{id}", image = (string?)null, available = false };

        return Ok(new
        {
            pending = pendingIds.Where(p => products.ContainsKey(p.ProductId))
                .Select(p => new { product = Product(p.ProductId), orderId = p.Id, receivedAt = p.StatusChangedAt }),
            rated = reviews.Select(r => new { r.Id, product = Product(r.ProductId), r.Rating, r.Comment, r.CreatedAt, r.Reply, r.RepliedAt }),
        });
    }

    public record ReviewBody(int ProductId, int Rating, string? Comment);

    [HttpPost("account/reviews")]
    public async Task<IActionResult> Create(ReviewBody body)
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized(new { error = "Войдите по номеру телефона" });
        if (Validate(body) is { } error) return BadRequest(new { error });
        // Only products the customer actually received can be rated, once.
        var received = await db.Orders.AnyAsync(o => o.CustomerId == c.Id && o.Status == OrderStatus.Completed && o.Items.Any(i => i.ProductId == body.ProductId));
        if (!received) return BadRequest(new { error = "Оценить можно только полученный товар" });
        if (await db.Reviews.AnyAsync(r => r.CustomerId == c.Id && r.ProductId == body.ProductId))
            return Conflict(new { error = "Вы уже оценили этот товар" });
        var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == body.ProductId);

        var review = new Review
        {
            ProductId = body.ProductId, CustomerId = c.Id, Rating = body.Rating, Comment = (body.Comment ?? "").Trim(),
            Status = ReviewStatus.New, CreatedAt = DateTime.Now,
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        // Same shape as other review threads, so the merchant can answer from Чат → Обзоры.
        var text = ReviewText(review, product);
        db.Conversations.Add(new Conversation
        {
            CustomerId = c.Id, Channel = ChatChannel.Website, DisplayName = c.FullName, ReviewId = review.Id, UnreadCount = 1,
            CreatedAt = review.CreatedAt, LastMessageAt = review.CreatedAt, LastMessageText = text,
            Messages = [new ChatMessage { Direction = MessageDirection.In, Text = text, SentAt = review.CreatedAt }],
        });
        await db.SaveChangesAsync();
        return Ok(new { review.Id });
    }

    /// <summary>Edit own review. The store's reply stays; the review goes back to "Новый" so the merchant sees the change.</summary>
    [HttpPut("account/reviews/{id:int}")]
    public async Task<IActionResult> Update(int id, ReviewBody body)
    {
        if (await auth.CurrentAsync(Request) is not { } c) return Unauthorized(new { error = "Войдите по номеру телефона" });
        if (Validate(body) is { } error) return BadRequest(new { error });
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == c.Id);
        if (review is null) return NotFound(new { error = "Отзыв не найден" });
        review.Rating = body.Rating;
        review.Comment = (body.Comment ?? "").Trim();
        review.Status = ReviewStatus.New;

        var product = await db.Products.AsNoTracking().FirstAsync(p => p.Id == review.ProductId);
        if (await db.Conversations.FirstOrDefaultAsync(x => x.ReviewId == id) is { } conv)
        {
            var text = "✏️ Отзыв изменён\n" + ReviewText(review, product);
            db.ChatMessages.Add(new ChatMessage { ConversationId = conv.Id, Direction = MessageDirection.In, Text = text, SentAt = DateTime.Now });
            conv.UnreadCount++;
            conv.LastMessageAt = DateTime.Now;
            conv.LastMessageText = text;
        }
        await db.SaveChangesAsync();
        return Ok(new { review.Id });
    }

    static string? Validate(ReviewBody b) =>
        b.Rating is < 1 or > 5 ? "Поставьте оценку от 1 до 5"
        : (b.Comment?.Length ?? 0) > 1000 ? "Отзыв слишком длинный (до 1000 символов)"
        : null;

    static string ReviewText(Review r, Product p) =>
        $"{new string('★', r.Rating)}{new string('☆', 5 - r.Rating)} «{p.Name.Get()}»\n{r.Comment}".TrimEnd();
}
