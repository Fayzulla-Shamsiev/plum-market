using System.Text.Json.Serialization;

namespace PlumMarket.Api.Domain;

/// <summary>Channels the unified inbox receives messages from. Wolt exists only here (no storefront orders).</summary>
public enum ChatChannel { Telegram, Instagram, Website, Wolt }

public enum MessageDirection { In, Out }

public class Conversation : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ChatChannel Channel { get; set; }
    /// <summary>Name shown in the inbox (customer name, Instagram handle, Wolt order…).</summary>
    public string DisplayName { get; set; } = "";
    public string? Handle { get; set; }
    /// <summary>
    /// Storefront visitor who wrote from the website chat before logging in: a random id kept in their browser.
    /// Lets the visitor reopen the same thread; linked to a customer once phone login exists.
    /// </summary>
    public string? VisitorToken { get; set; }
    /// <summary>Set when the thread was opened by a product review ("Обзоры" category).</summary>
    public int? ReviewId { get; set; }
    public int UnreadCount { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string LastMessageText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public MessageDirection Direction { get; set; }
    public string Text { get; set; } = "";
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    /// <summary>image | video | file</summary>
    public string? AttachmentType { get; set; }
    /// <summary>Sent by the auto-responder rather than a person.</summary>
    public bool IsAuto { get; set; }
    public string? SenderName { get; set; }
    public DateTime SentAt { get; set; }
}
