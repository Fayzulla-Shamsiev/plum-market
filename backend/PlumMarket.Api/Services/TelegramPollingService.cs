using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;

namespace PlumMarket.Api.Services;

/// <summary>
/// Keeps the bots that have no webhook talking. Telegram can only call a webhook on a public https address, so
/// a shop being tried out on a local machine would have a silent bot; this asks Telegram for its messages
/// instead. Bots with a webhook are left alone — Telegram refuses both at once.
/// </summary>
public class TelegramPollingService(IServiceScopeFactory scopes, TelegramBotApi telegram, ILogger<TelegramPollingService> log)
    : BackgroundService
{
    readonly Dictionary<int, long> _offsets = new();

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            var polled = 0;
            try
            {
                polled = await PollAsync(stopping);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "Telegram polling failed");
            }
            // Nothing to poll: look again in a while, in case a bot is connected in the panel.
            try { await Task.Delay(TimeSpan.FromSeconds(polled > 0 ? 1 : 15), stopping); }
            catch (OperationCanceledException) { return; }
        }
    }

    async Task<int> PollAsync(CancellationToken stopping)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var greeter = scope.ServiceProvider.GetRequiredService<TelegramGreeter>();

        var stores = await db.Stores.IgnoreQueryFilters()
            .Where(s => s.BotToken != null && s.BotWebhookSecret == null)
            .ToListAsync(stopping);

        foreach (var store in stores)
        {
            var updates = await telegram.GetUpdatesAsync(store.BotToken!, _offsets.GetValueOrDefault(store.Id), stopping);
            foreach (var update in updates)
            {
                _offsets[store.Id] = update.UpdateId + 1;
                if (update.Message is { } message) await greeter.HandleAsync(store, message, stopping);
            }
        }
        return stores.Count;
    }
}
