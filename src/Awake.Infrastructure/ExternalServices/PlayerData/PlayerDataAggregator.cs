using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Models;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public class PlayerDataAggregator(IEnumerable<IPlayerDataSource> sources) : IPlayerDataAggregator
{
    public async Task<PlayerDataResult> GetPlayerDataAsync(string gameNickname, CancellationToken ct = default)
    {
        var tasks = sources.Select(s => s.TryGetDataAsync(gameNickname, ct));
        var results = await Task.WhenAll(tasks);
        return new PlayerDataResult(gameNickname, results.Where(r => r is not null).ToList());
    }
}
