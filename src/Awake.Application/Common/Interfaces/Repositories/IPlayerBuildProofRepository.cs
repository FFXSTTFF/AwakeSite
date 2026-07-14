using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerBuildProofRepository
{
    /// <summary>Без поля Image (byte[]) — только метаданные, чтобы не тащить картинки списком.</summary>
    Task<IReadOnlyList<PlayerBuildProof>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Без поля Image — только метаданные (как GetByUserAsync).</summary>
    Task<IReadOnlyList<PlayerBuildProof>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<PlayerBuildProof?> GetAsync(Guid userId, BuildType type, CancellationToken ct = default);
    Task AddAsync(PlayerBuildProof proof, CancellationToken ct = default);
    Task UpdateAsync(PlayerBuildProof proof, CancellationToken ct = default);
    Task RemoveAsync(PlayerBuildProof proof, CancellationToken ct = default);
}
