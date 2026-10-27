using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace PlumMarket.Api.Services;

/// <summary>One search result: a thumbnail to choose from and who took it.</summary>
public record StockPhoto(long Id, string Thumb, string Author);

/// <summary>
/// Free stock photos from Pixabay, for an administrator who has no photos of their own yet — the setup assistant
/// shows a few to pick from (see AssistantTools.FindStockPhotos), and the chosen one is copied into /uploads.
///
/// Pixabay's terms shape this class: results are cached for 24 hours, its image URLs are only used to show search
/// results (a chosen photo is downloaded to our server, never hotlinked), and the picker says the photos come from
/// Pixabay. The key comes from configuration only — user secrets `Pixabay:ApiKey` locally, `Pixabay__ApiKey` on
/// the server — and never reaches the browser. Without a key the feature is simply not offered.
/// </summary>
public class StockPhotos(IConfiguration config, IHttpClientFactory factory, IMemoryCache cache, IWebHostEnvironment env,
    ILogger<StockPhotos> log)
{
    const long MaxBytes = 15 * 1024 * 1024;
    /// <summary>
    /// Besides 100 calls a minute, Pixabay refuses calls that come in a quick burst (a 429 without rate-limit headers),
    /// so its API is called at most once per this interval — a search for several items takes a few seconds.
    /// </summary>
    static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(1100);
    readonly SemaphoreSlim gate = new(1, 1);
    DateTime lastCall = DateTime.MinValue;

    string? ApiKey => new[] { config["Pixabay:ApiKey"], config["PIXABAY_API_KEY"] }
        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public bool Enabled => ApiKey is not null;

    /// <summary>Up to <paramref name="count"/> photos for an English query («red roses bouquet»).</summary>
    public async Task<List<StockPhoto>> SearchAsync(string query, int count, CancellationToken ct = default)
    {
        query = query.Trim().ToLowerInvariant();
        if (query.Length > 100) query = query[..100];
        var hits = await HitsAsync($"q={Uri.EscapeDataString(query)}&image_type=photo&safesearch=true&per_page=30", ct);
        // Each hit is also kept as the answer to «id=…», so saving a pick needs no second API call (the busiest part
        // of the limit) — only the download of the picture itself.
        foreach (var h in hits)
            if (!cache.TryGetValue($"pixabay:id={h.GetProperty("id").GetInt64()}", out _))
                cache.Set($"pixabay:id={h.GetProperty("id").GetInt64()}", new List<JsonElement> { h }, TimeSpan.FromHours(24));
        return hits.Take(count).Select(h => new StockPhoto(
            h.GetProperty("id").GetInt64(),
            // webformatURL ends in _640; _340 is the same picture at a size that suits a thumbnail.
            h.GetProperty("webformatURL").GetString()!.Replace("_640.", "_340."),
            h.TryGetProperty("user", out var u) ? u.GetString() ?? "" : "")).ToList();
    }

    /// <summary>Copies the photo into /uploads (as an upload would) and returns its URL there.</summary>
    public async Task<string> SaveAsync(long id, CancellationToken ct = default)
    {
        var hit = (await HitsAsync($"id={id}", ct)).FirstOrDefault();
        if (hit.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("фото не найдено на Pixabay");
        var source = hit.GetProperty("largeImageURL").GetString();
        // Only Pixabay's own servers: the address comes from their answer, but we still never fetch elsewhere.
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !(uri.Host == "pixabay.com" || uri.Host.EndsWith(".pixabay.com", StringComparison.Ordinal)))
            throw new InvalidOperationException("неожиданный адрес фото");

        using var client = factory.CreateClient(nameof(StockPhotos));
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var ext = response.Content.Headers.ContentType?.MediaType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Pixabay прислал не фото"),
        };
        if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidOperationException("фото слишком большое");

        var name = $"{DateTime.Now:yyyyMM}/pixabay-{id}-{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(env.ContentRootPath, "uploads", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var stream = await response.Content.ReadAsStreamAsync(ct))
        await using (var file = File.Create(path))
            await stream.CopyToAsync(file, ct);
        if (new FileInfo(path).Length > MaxBytes)
        {
            File.Delete(path);
            throw new InvalidOperationException("фото слишком большое");
        }
        return "/uploads/" + name;
    }

    async Task<HttpResponseMessage> CallAsync(HttpClient client, string url, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var wait = lastCall + Spacing - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            return await client.GetAsync(url, ct);
        }
        finally
        {
            lastCall = DateTime.UtcNow;
            gate.Release();
        }
    }

    static int? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var v) ? v : null;

    /// <summary>The «hits» of one API call, cached for a day as Pixabay asks.</summary>
    async Task<List<JsonElement>> HitsAsync(string parameters, CancellationToken ct)
    {
        if (ApiKey is not { } key) throw new InvalidOperationException("ключ Pixabay не настроен");
        if (cache.TryGetValue("pixabay:" + parameters, out List<JsonElement>? cached) && cached is not null) return cached;

        using var client = factory.CreateClient(nameof(StockPhotos));
        var url = $"https://pixabay.com/api/?key={Uri.EscapeDataString(key)}&{parameters}";
        var response = await CallAsync(client, url, ct);
        // A burst refusal clears within seconds: wait as long as Pixabay says (or 2 s, then 4 s) and try again.
        for (var attempt = 1; attempt <= 2 && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests; attempt++)
        {
            var reset = Header(response, "X-RateLimit-Reset") ?? 2 * attempt;
            if (reset > 6) break;
            log.LogInformation("Pixabay busy, retrying in {Seconds}s", reset);
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(reset), ct);
            response = await CallAsync(client, url, ct);
        }
        using var _ = response;
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Pixabay answers errors in plain text; it never contains the key.
            log.LogWarning("Pixabay {Status}: {Body} (limit {Limit}, remaining {Remaining}, reset {Reset}s)", (int)response.StatusCode,
                body.Length > 300 ? body[..300] : body, Header(response, "X-RateLimit-Limit"),
                Header(response, "X-RateLimit-Remaining"), Header(response, "X-RateLimit-Reset"));
            throw new InvalidOperationException(response.StatusCode switch
            {
                System.Net.HttpStatusCode.TooManyRequests => "лимит запросов Pixabay исчерпан, попробуйте через минуту",
                System.Net.HttpStatusCode.BadRequest when body.Contains("key", StringComparison.OrdinalIgnoreCase) => "ключ Pixabay отклонён",
                _ => $"Pixabay не ответил ({(int)response.StatusCode})",
            });
        }
        log.LogInformation("Pixabay ok (limit {Limit}, remaining {Remaining})", Header(response, "X-RateLimit-Limit"), Header(response, "X-RateLimit-Remaining"));
        using var doc = JsonDocument.Parse(body);
        var hits = doc.RootElement.GetProperty("hits").EnumerateArray().Select(h => h.Clone()).ToList();
        cache.Set("pixabay:" + parameters, hits, TimeSpan.FromHours(24));
        return hits;
    }
}
