using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Features.Squads;

public static class SquadMemberEnricher
{
    public static async Task<IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd)>> ComputeAsync(
        IReadOnlyList<User> users,
        IPlayerInventoryRepository inventoryRepository,
        IPlayerBuildProofRepository proofRepository,
        IItemCacheService itemCache,
        IPlayerStatsSnapshotRepository snapshotRepository,
        CancellationToken ct = default)
    {
        var ids = users.Select(u => u.Id).ToList();
        var inventories = await inventoryRepository.GetByUserIdsAsync(ids, ct);
        var proofs = await proofRepository.GetByUserIdsAsync(ids, ct);

        var nicknames = users
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .ToList();
        var snapshots = (await snapshotRepository.GetByNicknamesAsync(nicknames, ct))
            .ToDictionary(s => s.GameNickname, StringComparer.OrdinalIgnoreCase);

        var itemsByUser = inventories.ToLookup(i => i.UserId);
        var proofsByUser = proofs.ToLookup(p => p.UserId);

        return users.ToDictionary(u => u.Id, u =>
        {
            var known = itemsByUser[u.Id]
                .Select(entry => itemCache.GetById(entry.ItemId))
                .Where(i => i is not null)
                .Cast<ItemDto>();
            var userProofs = proofsByUser[u.Id].ToList();
            var flags = PlayerFlagsCalculator.Calculate(
                known,
                hasSpeedProof: userProofs.Any(p => p.BuildType == BuildType.Speed),
                hasVitalityProof: userProofs.Any(p => p.BuildType == BuildType.Vitality));

            double? kd = u.GameNickname is not null
                && snapshots.TryGetValue(u.GameNickname, out var snap)
                    ? snap.KdRatio
                    : null;

            return (flags, kd);
        });
    }
}
