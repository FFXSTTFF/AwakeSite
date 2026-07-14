using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public class GetSquadBuilderQueryHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadBuilderQuery, Result<SquadBuilderDto>>
{
    public async Task<Result<SquadBuilderDto>> Handle(
        GetSquadBuilderQuery request, CancellationToken cancellationToken)
    {
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);
        var eligible = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);

        // Все бойцы, которые появятся на экране: участники отрядов + пул
        var squadUserIds = squads
            .SelectMany(s => s.Members)
            .Select(m => m.UserId)
            .ToHashSet();
        var allUsers = eligible
            .Concat(squads.SelectMany(s => s.Members).Select(m => m.User))
            .Where(u => u is not null)
            .DistinctBy(u => u.Id)
            .ToList();
        var allIds = allUsers.Select(u => u.Id).ToList();

        var inventories = await inventoryRepository.GetByUserIdsAsync(allIds, cancellationToken);
        var proofs = await proofRepository.GetByUserIdsAsync(allIds, cancellationToken);

        var nicknames = allUsers
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .ToList();
        var snapshots = (await snapshotRepository.GetByNicknamesAsync(nicknames, cancellationToken))
            .ToDictionary(s => s.GameNickname, StringComparer.OrdinalIgnoreCase);

        var itemsByUser = inventories.ToLookup(i => i.UserId);
        var proofsByUser = proofs.ToLookup(p => p.UserId);

        BuilderFighterDto ToFighter(User u)
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

            return new BuilderFighterDto(u.Id, u.Username, u.GameNickname, u.DiscordAvatarUrl, flags, kd);
        }

        var fightersById = allUsers.ToDictionary(u => u.Id, ToFighter);

        var squadDtos = squads
            .OrderBy(s => s.Number)
            .Select(s => new BuilderSquadDto(
                s.Id, s.Name, s.Number,
                s.Members
                    .Where(m => fightersById.ContainsKey(m.UserId))
                    .Select(m => fightersById[m.UserId])
                    .ToList()))
            .ToList();

        var pool = allUsers
            .Where(u => !squadUserIds.Contains(u.Id))
            .Where(u => u.Rank >= UserRank.Member)
            .OrderByDescending(u => fightersById[u.Id].Kd ?? -1)
            .Select(u => fightersById[u.Id])
            .ToList();

        return Result<SquadBuilderDto>.Success(new SquadBuilderDto(squadDtos, pool));
    }
}
