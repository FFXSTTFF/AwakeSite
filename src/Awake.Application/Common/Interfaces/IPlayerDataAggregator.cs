using Awake.Application.Common.Models;

namespace Awake.Application.Common.Interfaces;

public interface IPlayerDataAggregator
{
    Task<PlayerDataResult> GetPlayerDataAsync(string gameNickname, CancellationToken ct = default);
}
