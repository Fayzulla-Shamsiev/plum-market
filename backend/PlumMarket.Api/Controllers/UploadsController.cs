using Microsoft.AspNetCore.Mvc;

namespace PlumMarket.Api.Controllers;

/// <summary>Stores merchant files (product media, category images/banners, chat attachments) under /uploads.</summary>
[ApiController]
[Route("api/uploads")]
public class UploadsController(IWebHostEnvironment env) : ControllerBase
{
    const long MaxBytes = 50 * 1024 * 1024;

    // No SVG: it can carry script and would be served from the app's own origin.
    static readonly HashSet<string> Images = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    static readonly HashSet<string> Videos = [".mp4", ".webm", ".mov"];
    static readonly HashSet<string> Documents = [".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".zip"];

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { error = "Пустой файл" });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var type = Images.Contains(ext) ? "image" : Videos.Contains(ext) ? "video" : Documents.Contains(ext) ? "file" : null;
        if (type is null) return BadRequest(new { error = $"Тип файла {ext} не поддерживается" });

        // Random name: never trust the client's file name as a path.
        var name = $"{DateTime.Now:yyyyMM}/{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(env.ContentRootPath, "uploads", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var fs = System.IO.File.Create(path))
            await file.CopyToAsync(fs);

        return Ok(new { url = "/uploads/" + name, name = Path.GetFileName(file.FileName), type, size = file.Length });
    }
}
