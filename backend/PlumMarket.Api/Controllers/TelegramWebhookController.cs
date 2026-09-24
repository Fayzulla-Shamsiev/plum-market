using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// What the merchant's bot says to a customer. Telegram posts every message here, and the bot answers with the
/// shop button — so the bot appears at the top of the customer's chat list and one tap opens the storefront,
/// with no searching and no sign-in of any kind.
/// </summary>
[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController(AppDbContext db, TelegramGreeter greeter) : ControllerBase
{
    public record Update(Message? Message);
    public record Message(Chat Chat, Sender? From, string? Text);
    public record Chat(long Id, string? Type);
    public record Sender([property: JsonPropertyName("first_name")] string? FirstName);

    [HttpPost("{storeId:int}")]
    public async Task<IActionResult> Post(int storeId, [FromBody] Update update)
    {
        // Telegram is the only caller that knows the secret it was given when the webhook was set.
        var secret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        var store = await db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == storeId);
        if (store?.BotToken is not { Length: > 0 } || store.BotWebhookSecret is not { Length: > 0 } expected
            || !CryptoEquals(secret, expected))
            return Unauthorized();

        if (update.Message is { Chat.Id: var chatId } message)
            await greeter.ReplyAsync(store, chatId, message.From?.FirstName, message.Text);
        return Ok();
    }

    static bool CryptoEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}
