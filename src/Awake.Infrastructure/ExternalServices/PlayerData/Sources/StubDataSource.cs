namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public class StubDataSource : IPlayerDataSource
{
    public Task<object?> TryGetDataAsync(string nickname, CancellationToken ct = default)
        => Task.FromResult<object?>(null);
}
