using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerStatsSnapshotRepository
{
    Task<PlayerStatsSnapshot?> GetByNicknameAsync(string gameNickname, CancellationToken ct = default);
    Task UpsertAsync(string gameNickname, PlayerProfile profile, CancellationToken ct = default);
}
