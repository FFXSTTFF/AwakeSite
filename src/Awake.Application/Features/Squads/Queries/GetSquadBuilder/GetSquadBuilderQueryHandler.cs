using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public class GetSquadBuilderQueryHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IPlayerBoostRequestRepository boostRepository,
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

        var enriched = await SquadMemberEnricher.ComputeAsync(
            allUsers, inventoryRepository, proofRepository, boostRepository, itemCache, snapshotRepository, cancellationToken);

        BuilderFighterDto ToFighter(User u) =>
            new(u.Id, u.Username, u.GameNickname, u.DiscordAvatarUrl, enriched[u.Id].Flags, enriched[u.Id].Kd);

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
