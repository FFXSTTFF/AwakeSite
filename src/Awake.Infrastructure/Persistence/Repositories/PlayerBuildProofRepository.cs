using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerBuildProofRepository(AppDbContext context) : IPlayerBuildProofRepository
{
    public async Task<IReadOnlyList<PlayerBuildProof>> GetByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerBuildProofs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new PlayerBuildProof
            {
                Id = x.Id,
                UserId = x.UserId,
                BuildType = x.BuildType,
                ContentType = x.ContentType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                // Image намеренно не выбирается
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerBuildProof>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerBuildProofs
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new PlayerBuildProof
            {
                Id = x.Id,
                UserId = x.UserId,
                BuildType = x.BuildType,
                ContentType = x.ContentType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
            })
            .ToListAsync(ct);

    public Task<PlayerBuildProof?> GetAsync(Guid userId, BuildType type, CancellationToken ct = default) =>
        context.PlayerBuildProofs
            .FirstOrDefaultAsync(x => x.UserId == userId && x.BuildType == type, ct);

    public async Task AddAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Add(proof);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Update(proof);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Remove(proof);
        await context.SaveChangesAsync(ct);
    }
}
