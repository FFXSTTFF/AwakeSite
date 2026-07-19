using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerBoostRequestRepository(AppDbContext context) : IPlayerBoostRequestRepository
{
    public async Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.BoostType)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Include(x => x.User)
            .ToListAsync(ct);

    public async Task ReplaceForUserAsync(
        Guid userId, IReadOnlyList<PlayerBoostRequest> requests, CancellationToken ct = default)
    {
        var existing = await context.PlayerBoostRequests
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        context.PlayerBoostRequests.RemoveRange(existing);
        context.PlayerBoostRequests.AddRange(requests);
        await context.SaveChangesAsync(ct); // одна транзакция — атомарная замена
    }
}
