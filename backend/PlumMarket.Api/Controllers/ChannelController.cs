using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Controllers;

/// <summary>
/// "Пост для канала": connect the store's bot to a Telegram channel as an admin and publish posts through it.
/// The prototype can't call the Bot API, so the connection check validates the username format only and
/// publishing stores the post.
/// </summary>
[ApiController]
[Route("api/marketing/channel")]
public partial class ChannelController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var posts = await db.ChannelPosts.AsNoTracking().OrderByDescending(p => p.PublishedAt).ToListAsync();
        return Ok(new { s.BotUsername, channel = s.ChannelUsername, connectedAt = s.ChannelConnectedAt, s.StoreDomain, posts });
    }

    public record ConnectBody(string Channel);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect(ConnectBody body)
    {
        var raw = (body.Channel ?? "").Trim();
        // Accept "@name", "name" or "https://t.me/name".
        var m = ChannelName().Match(raw);
        if (!m.Success) return BadRequest(new { error = "Укажите публичный канал: @name или https://t.me/name (5–32 символа)" });
        var s = await db.Settings.FirstAsync();
        s.ChannelUsername = "@" + m.Groups[1].Value;
        s.ChannelConnectedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(new { channel = s.ChannelUsername, connectedAt = s.ChannelConnectedAt });
    }

    [HttpDelete("connect")]
    public async Task<IActionResult> Disconnect()
    {
        var s = await db.Settings.FirstAsync();
        s.ChannelUsername = null;
        s.ChannelConnectedAt = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record PostBody(string Text, string? ImageUrl, string? ButtonText, string? ButtonUrl);

    [HttpPost("posts")]
    public async Task<IActionResult> Publish(PostBody body)
    {
        var s = await db.Settings.FirstAsync();
        if (s.ChannelUsername is null) return BadRequest(new { error = "Сначала подключите канал" });
        if (string.IsNullOrWhiteSpace(body.Text)) return BadRequest(new { error = "Напишите текст поста" });
        if (body.Text.Length > (body.ImageUrl is null ? 4096 : 1024))
            return BadRequest(new { error = body.ImageUrl is null ? "Пост длиннее 4096 символов" : "Подпись к фото длиннее 1024 символов" });
        var hasButton = !string.IsNullOrWhiteSpace(body.ButtonText) || !string.IsNullOrWhiteSpace(body.ButtonUrl);
        if (hasButton && (string.IsNullOrWhiteSpace(body.ButtonText) || !Uri.TryCreate(body.ButtonUrl, UriKind.Absolute, out _)))
            return BadRequest(new { error = "Для кнопки нужны текст и полная ссылка" });

        var post = new ChannelPost
        {
            Channel = s.ChannelUsername, Text = body.Text.Trim(), ImageUrl = body.ImageUrl,
            ButtonText = hasButton ? body.ButtonText!.Trim() : null, ButtonUrl = hasButton ? body.ButtonUrl!.Trim() : null,
            PublishedAt = DateTime.Now,
        };
        db.ChannelPosts.Add(post);
        await db.SaveChangesAsync();
        return Ok(post);
    }

    [GeneratedRegex(@"^(?:https?://)?(?:t(?:elegram)?\.me/|@)?([A-Za-z][A-Za-z0-9_]{4,31})/?$")]
    private static partial Regex ChannelName();
}
