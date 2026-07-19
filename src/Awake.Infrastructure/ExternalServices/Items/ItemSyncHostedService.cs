using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Items;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Items;

public class ItemSyncHostedService(
    IItemCacheService cache,
    IHttpClientFactory httpClientFactory,
    ILogger<ItemSyncHostedService> logger
) : IHostedService, IDisposable
{
    private const string ListingUrl =
        "https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru/listing.json";

    private const string IconBase =
        "https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru";

    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SyncAsync();
        _timer = new Timer(_ => Task.Run(SyncAsync), null, TimeUntilNextWednesday(), TimeSpan.FromDays(7));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private async Task SyncAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("stalzone");
            var json = await client.GetStringAsync(ListingUrl);
            var entries = JsonSerializer.Deserialize<List<ListingEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (entries is null) return;

            var relevant = entries
                .Where(e => !string.IsNullOrEmpty(e.Data) &&
                            (e.Data.StartsWith("/items/weapon/") ||
                             e.Data.StartsWith("/items/armor/") ||
                             e.Data.StartsWith("/items/supply/")))
                .ToList();

            // Для supply-предметов вычитываем effect_type из JSON самого предмета.
            // Ошибка одного предмета не валит синк — предмет остаётся без типа буста.
            var boostTypes = new Dictionary<string, BoostType?>();
            var semaphore = new SemaphoreSlim(8);
            await Task.WhenAll(relevant
                .Where(e => e.Data.StartsWith("/items/supply/"))
                .Select(async e =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var itemJson = await client.GetStringAsync(IconBase + e.Data);
                        using var doc = JsonDocument.Parse(itemJson);
                        var effect = BoostEffectParser.ExtractEffectType(doc.RootElement);
                        lock (boostTypes) boostTypes[e.Data] = BoostEffectParser.MapToBoostType(effect);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to fetch effect_type for {Item}", e.Data);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));

            var items = relevant
                .Select(e =>
                {
                    // e.Data = "/items/weapon/sniper_rifle/1r79g.json"
                    // parts  = ["", "items", "weapon", "sniper_rifle", "1r79g.json"]
                    var parts = e.Data.Split('/');
                    var id = parts[^1].Replace(".json", "");
                    var category = string.Join("/", parts[2..^1]);
                    var nameRu = e.Name?.Lines?.GetValueOrDefault("ru") ?? id;
                    var icon = IconBase + e.Icon;
                    var boost = boostTypes.GetValueOrDefault(e.Data);
                    return new ItemDto(id, category, nameRu, icon, e.Color ?? "", boost);
                })
                .Where(x => !string.IsNullOrEmpty(x.NameRu))
                .ToList();

            cache.Load(items);
            logger.LogInformation("Item cache refreshed: {Count} items loaded", cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync item cache from stalzone-database");
        }
    }

    private static TimeSpan TimeUntilNextWednesday()
    {
        var now = DateTime.UtcNow;
        var daysUntil = ((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        var next = now.Date.AddDays(daysUntil);
        return next - now;
    }
}

file class ListingEntry
{
    public string Data { get; set; } = "";
    public string Icon { get; set; } = "";
    public ListingName? Name { get; set; }
    public string? Color { get; set; }
}

file class ListingName
{
    public Dictionary<string, string>? Lines { get; set; }
}
