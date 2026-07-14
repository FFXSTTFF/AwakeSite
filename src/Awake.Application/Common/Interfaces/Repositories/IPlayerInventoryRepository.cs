using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerInventoryRepository
{
    Task<IReadOnlyList<PlayerInventoryItem>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<PlayerInventoryItem?> GetAsync(Guid userId, string itemId, CancellationToken ct = default);
    Task AddAsync(PlayerInventoryItem item, CancellationToken ct = default);
    Task RemoveAsync(PlayerInventoryItem item, CancellationToken ct = default);
}
