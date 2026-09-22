using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// Inbox write-side: incoming messages (from channel webhooks — simulated in the prototype), merchant
/// replies, the per-channel auto-responder, and syncing replies back to product reviews.
/// Delivery to Telegram/Instagram/Wolt is out of scope; messages are stored and shown in the inbox.
/// </summary>
public class ChatService(AppDbContext db)
{
    /// <summary>Don't auto-reply to the same thread more often than this.</summary>
    static readonly TimeSpan AutoReplyCooldown = TimeSpan.FromHours(6);

    public async Task<ChatMessage> ReceiveAsync(Conversation conv, string text, string? attachmentUrl = null,
        string? attachmentName = null, string? attachmentType = null)
    {
        var msg = new ChatMessage
        {
            ConversationId = conv.Id, Direction = MessageDirection.In, Text = text,
            AttachmentUrl = attachmentUrl, AttachmentName = attachmentName, AttachmentType = attachmentType,
            SentAt = DateTime.Now,
        };
        db.ChatMessages.Add(msg);
        conv.UnreadCount++;
        Touch(conv, msg);
        if (conv.Customer is { } c) c.LastVisitAt = msg.SentAt;
        await db.SaveChangesAsync();
        await MaybeAutoReplyAsync(conv);
        return msg;
    }

    public async Task<ChatMessage> SendAsync(Conversation conv, string text, string? senderName,
        string? attachmentUrl, string? attachmentName, string? attachmentType)
    {
        var msg = new ChatMessage
        {
            ConversationId = conv.Id, Direction = MessageDirection.Out, Text = text.Trim(), SenderName = senderName,
            AttachmentUrl = attachmentUrl, AttachmentName = attachmentName, AttachmentType = attachmentType,
            SentAt = DateTime.Now,
        };
        db.ChatMessages.Add(msg);
        conv.UnreadCount = 0;
        Touch(conv, msg);

        // Answering in a review thread publishes the answer on the review ("Отвечено").
        if (conv.ReviewId is { } reviewId && !string.IsNullOrWhiteSpace(msg.Text)
            && await db.Reviews.FindAsync(reviewId) is { } review)
        {
            review.Reply = msg.Text;
            review.Status = ReviewStatus.Answered;
            review.RepliedAt = msg.SentAt;
        }
        await db.SaveChangesAsync();
        return msg;
    }

    async Task MaybeAutoReplyAsync(Conversation conv)
    {
        if (conv.ReviewId is not null) return;
        var s = await db.Settings.FirstAsync();
        if (!s.ChatAutoReplyEnabled) return;
        if (!s.ChatAutoReplies.TryGetValue(conv.Channel.ToString(), out var text) || string.IsNullOrWhiteSpace(text)) return;

        var lastAuto = await db.ChatMessages.Where(m => m.ConversationId == conv.Id && m.IsAuto)
            .OrderByDescending(m => m.SentAt).Select(m => (DateTime?)m.SentAt).FirstOrDefaultAsync();
        if (lastAuto is { } t && DateTime.Now - t < AutoReplyCooldown) return;

        var msg = new ChatMessage
        {
            ConversationId = conv.Id, Direction = MessageDirection.Out, Text = text, IsAuto = true,
            SenderName = "Автоответ", SentAt = DateTime.Now.AddSeconds(1),
        };
        db.ChatMessages.Add(msg);
        Touch(conv, msg);
        await db.SaveChangesAsync();
    }

    static void Touch(Conversation conv, ChatMessage msg)
    {
        conv.LastMessageAt = msg.SentAt;
        conv.LastMessageText = string.IsNullOrWhiteSpace(msg.Text) ? $"📎 {msg.AttachmentName}" : msg.Text;
    }

    public static ChatChannel ChannelFor(Platform p) => p switch
    {
        Platform.Instagram => ChatChannel.Instagram,
        Platform.Website => ChatChannel.Website,
        _ => ChatChannel.Telegram,
    };
}
