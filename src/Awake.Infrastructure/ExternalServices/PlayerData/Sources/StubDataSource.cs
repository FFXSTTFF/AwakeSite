using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public class StubDataSource : IPlayerDataSource
{
    public Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
        => Task.FromResult<PlayerProfile?>(null);
}
