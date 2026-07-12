using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public interface IPlayerDataSource
{
    Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default);
}
