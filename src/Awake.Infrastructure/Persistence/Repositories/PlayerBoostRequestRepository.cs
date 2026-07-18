using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerBoostRequestRepository(AppDbContext context) : IPlayerBoostRequestRepository
{
    public async Task<IReadOnlyList<BoostType>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.BoostType)
            .Select(x => x.BoostType)
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
        Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct = default)
    {
        var existing = await context.PlayerBoostRequests
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        context.PlayerBoostRequests.RemoveRange(existing);
        context.PlayerBoostRequests.AddRange(
            types.Select(t => new PlayerBoostRequest { UserId = userId, BoostType = t }));
        await context.SaveChangesAsync(ct); // одна транзакция — атомарная замена
    }
}
