using Microsoft.AspNetCore.Mvc;
using PlumMarket.Api.Domain;
using PlumMarket.Api.Services;

namespace PlumMarket.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController(AiContentService ai, StoreContext tenant) : ControllerBase
{
    public record BotTextBody(string Kind, string? Existing);

    /// <summary>Writes the bot's own wording — the empty-chat text or the greeting (Платформы → Telegram-бот).</summary>
    [HttpPost("bot-text")]
    public async Task<IActionResult> BotText(BotTextBody body, CancellationToken ct)
    {
        var kind = body.Kind == "greeting" ? "greeting" : "about";
        var (text, note) = await ai.BotTextAsync(kind, tenant.Store?.Name ?? "", body.Existing, ct);
        return text is null ? BadRequest(new { error = note }) : Ok(new { text });
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(new { provider = ai.Provider });

    public record TranslateBody(string Source, Dictionary<string, string> Fields);

    /// <summary>"Перевести": returns the given fields in every other catalog language.</summary>
    [HttpPost("translate")]
    public async Task<IActionResult> Translate(TranslateBody body, CancellationToken ct)
    {
        if (!Localized.Languages.Contains(body.Source)) return BadRequest(new { error = "Неизвестный язык" });
        var r = await ai.TranslateAsync(body.Source, body.Fields, ct);
        return Ok(new { translations = r.Values, provider = r.Provider, note = r.Note });
    }

    /// <summary>"Сгенерировать и заполнить автоматически": description in every catalog language.</summary>
    [HttpPost("describe")]
    public async Task<IActionResult> Describe(DescribeRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest(new { error = "Сначала укажите название" });
        var r = await ai.DescribeAsync(body, ct);
        return Ok(new { translations = r.Values, provider = r.Provider, note = r.Note });
    }
}
