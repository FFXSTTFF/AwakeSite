using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerBoostRequestRepository
{
    Task<IReadOnlyList<BoostType>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    /// <summary>С Include(User) — для сводки, чтобы не ходить за никами вторым запросом.</summary>
    Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Полная замена набора пользователя: remove старых + add новых одним SaveChangesAsync.</summary>
    Task ReplaceForUserAsync(Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct = default);
}
