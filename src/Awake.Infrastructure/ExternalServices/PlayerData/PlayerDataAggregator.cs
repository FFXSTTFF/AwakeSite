using System.Collections.Concurrent;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public class PlayerDataAggregator : IPlayerDataAggregator
{
    private readonly IReadOnlyList<IPlayerDataSource> _sources;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, (PlayerProfile Profile, DateTime CachedAt)> _cache = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastForceRefresh = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ForceRefreshCooldown = TimeSpan.FromMinutes(10);

    public PlayerDataAggregator(IEnumerable<IPlayerDataSource> sources, IServiceScopeFactory scopeFactory)
    {
        _sources = sources.ToList();
        _scopeFactory = scopeFactory;
    }

    public async Task<PlayerDataResult> GetPlayerDataAsync(string nickname, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(nickname, out var entry))
        {
            if (DateTime.UtcNow - entry.CachedAt < Ttl)
                return new PlayerDataResult(nickname, entry.Profile);

            _ = Task.Run(() => RefreshAsync(nickname), CancellationToken.None);
            return new PlayerDataResult(nickname, entry.Profile);
        }

        var profile = await FetchAsync(nickname, ct);
        return new PlayerDataResult(nickname, profile);
    }

    public async Task<bool> ForceRefreshAsync(string gameNickname, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var allowed = false;
        // AddOrUpdate атомарен для ключа: решение "пройти/отклонить" и запись
        // таймстампа происходят одной операцией — двойной клик не проскочит
        _lastForceRefresh.AddOrUpdate(gameNickname,
            _ => { allowed = true; return now; },
            (_, last) =>
            {
                allowed = now - last >= ForceRefreshCooldown;
                return allowed ? now : last;
            });
        if (!allowed) return false;

        await FetchAsync(gameNickname, ct);
        return true;
    }

    private async Task RefreshAsync(string nickname)
        => await FetchAsync(nickname, CancellationToken.None);

    private async Task<PlayerProfile?> FetchAsync(string nickname, CancellationToken ct)
    {
        PlayerProfile? profile = null;

        foreach (var source in _sources)
        {
            var result = await source.TryGetDataAsync(nickname, ct);
            if (result is null) continue;

            if (profile is null)
            {
                profile = result;
                if (IsComplete(profile)) break;
                // Otherwise keep going — next source may fill accuracy / playtime / clan history
            }
            else
            {
                profile = Merge(profile, result);
                if (IsComplete(profile)) break;
            }
        }

        if (profile is not null)
        {
            _cache[nickname] = (profile, DateTime.UtcNow);
            await SaveSnapshotAsync(nickname, profile, ct);
        }

        return profile;
    }

    // Write-through: каждый успешный fetch сохраняет снапшот в БД,
    // чтобы профиль открывался мгновенно и данные переживали рестарты.
    private async Task SaveSnapshotAsync(string nickname, PlayerProfile profile, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerStatsSnapshotRepository>();
            await repo.UpsertAsync(nickname, profile, ct);
        }
        catch (Exception ex)
        {
            // Ошибка персистенции не должна ронять успешный fetch
            TrySaveFailedLog(nickname, ex);
        }
    }

    private void TrySaveFailedLog(string nickname, Exception ex)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider
                .GetService<ILogger<PlayerDataAggregator>>();
            logger?.LogWarning(ex, "Failed to persist stats snapshot for {Nickname}", nickname);
        }
        catch
        {
            // логгер недоступен (юнит-тесты) — молча игнорируем
        }
    }

    private static bool IsComplete(PlayerProfile p) =>
        p.ClanHistory.Count > 0 && HasValue(p.Accuracy) && HasValue(p.Playtime);

    // Primary source wins; secondary only fills fields the primary left as placeholder
    private static PlayerProfile Merge(PlayerProfile primary, PlayerProfile secondary) =>
        primary with
        {
            Accuracy    = HasValue(primary.Accuracy) ? primary.Accuracy : secondary.Accuracy,
            Playtime    = HasValue(primary.Playtime) ? primary.Playtime : secondary.Playtime,
            ClanHistory = primary.ClanHistory.Count > 0 ? primary.ClanHistory : secondary.ClanHistory
        };

    private static bool HasValue(string s) =>
        !string.IsNullOrWhiteSpace(s) && s != "—";
}
