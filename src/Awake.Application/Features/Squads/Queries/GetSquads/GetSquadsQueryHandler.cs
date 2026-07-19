using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public class GetSquadsQueryHandler(
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadsQuery, IReadOnlyList<SquadDto>>
{
    public async Task<IReadOnlyList<SquadDto>> Handle(
        GetSquadsQuery request,
        CancellationToken cancellationToken)
    {
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);
        var allUsers = squads.SelectMany(s => s.Members).Select(m => m.User).ToList();
        var enriched = await SquadMemberEnricher.ComputeAsync(
            allUsers, inventoryRepository, proofRepository, boostRepository, itemCache, snapshotRepository, cancellationToken);

        return squads
            .OrderBy(s => s.Number)
            .Select(s => new SquadDto(
                s.Id,
                s.Name,
                s.Number,
                s.Members
                    .OrderByDescending(m => m.IsLeader)
                    .ThenBy(m => m.JoinedAt)
                    .Select(m => new SquadMemberDto(
                        m.UserId,
                        m.User.Username,
                        m.User.GameNickname,
                        m.IsLeader,
                        m.JoinedAt,
                        enriched[m.UserId].Flags,
                        enriched[m.UserId].Kd,
                        enriched[m.UserId].Boosts))
                    .ToList(),
                s.Members.Count))
            .ToList();
    }
}
