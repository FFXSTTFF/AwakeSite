using Awake.Application.Common.Models;

namespace Awake.Application.Common.Interfaces;

public interface IPlayerDataAggregator
{
    Task<PlayerDataResult> GetPlayerDataAsync(string gameNickname, CancellationToken ct = default);

    /// Принудительное обновление статистики (кнопка на профиле).
    /// false — отклонено rate-limit'ом (чаще 1 раза в 10 минут на ник).
    Task<bool> ForceRefreshAsync(string gameNickname, CancellationToken ct = default);
}
