using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public class GetSquadReserveQueryHandler(
    IUserRepository userRepository,
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadReserveQuery, IReadOnlyList<ReserveMemberDto>>
{
    public async Task<IReadOnlyList<ReserveMemberDto>> Handle(
        GetSquadReserveQuery request, CancellationToken cancellationToken)
    {
        var eligible = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);

        var squadUserIds = squads
            .SelectMany(s => s.Members)
            .Select(m => m.UserId)
            .ToHashSet();

        var reserve = eligible.Where(u => !squadUserIds.Contains(u.Id)).ToList();

        var enriched = await Squads.SquadMemberEnricher.ComputeAsync(
            reserve, inventoryRepository, proofRepository, boostRepository, itemCache, snapshotRepository, cancellationToken);

        return reserve
            .Select(u => new ReserveMemberDto(
                u.Id, u.Username, u.GameNickname,
                enriched[u.Id].Flags, enriched[u.Id].Kd, enriched[u.Id].Boosts))
            .OrderByDescending(r => r.Kd ?? -1)
            .ToList();
    }
}
