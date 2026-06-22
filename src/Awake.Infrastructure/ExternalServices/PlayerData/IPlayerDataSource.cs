namespace Awake.Infrastructure.ExternalServices.PlayerData;

public interface IPlayerDataSource
{
    Task<object?> TryGetDataAsync(string nickname, CancellationToken ct = default);
}
