using System.Collections.Concurrent;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public class PlayerDataAggregator : IPlayerDataAggregator
{
    private readonly IReadOnlyList<IPlayerDataSource> _sources;
    private readonly ConcurrentDictionary<string, (PlayerProfile Profile, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    public PlayerDataAggregator(IEnumerable<IPlayerDataSource> sources) =>
        _sources = sources.ToList();

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
        if (profile is not null)
            _cache[nickname] = (profile, DateTime.UtcNow);

        return new PlayerDataResult(nickname, profile);
    }

    private async Task RefreshAsync(string nickname)
    {
        var profile = await FetchAsync(nickname, CancellationToken.None);
        if (profile is not null)
            _cache[nickname] = (profile, DateTime.UtcNow);
    }

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

        return profile;
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
