using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerInventoryRepository(AppDbContext context) : IPlayerInventoryRepository
{
    public async Task<IReadOnlyList<PlayerInventoryItem>> GetByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerInventoryItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerInventoryItem>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerInventoryItems
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(ct);

    public Task<PlayerInventoryItem?> GetAsync(Guid userId, string itemId, CancellationToken ct = default) =>
        context.PlayerInventoryItems
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ItemId == itemId, ct);

    public async Task AddAsync(PlayerInventoryItem item, CancellationToken ct = default)
    {
        context.PlayerInventoryItems.Add(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(PlayerInventoryItem item, CancellationToken ct = default)
    {
        context.PlayerInventoryItems.Remove(item);
        await context.SaveChangesAsync(ct);
    }
}
