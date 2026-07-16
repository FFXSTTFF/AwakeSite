using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Entities;
using Awake.Domain.Exceptions;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadById;

public class GetSquadByIdQueryHandler(
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadByIdQuery, SquadDto>
{
    public async Task<SquadDto> Handle(
        GetSquadByIdQuery request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdWithMembersAsync(request.SquadId, cancellationToken)
            ?? throw new NotFoundException(nameof(Squad), request.SquadId);

        var users = squad.Members.Select(m => m.User).ToList();
        var enriched = await SquadMemberEnricher.ComputeAsync(
            users, inventoryRepository, proofRepository, itemCache, snapshotRepository, cancellationToken);

        return new SquadDto(
            squad.Id,
            squad.Name,
            squad.Number,
            squad.Members
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.JoinedAt)
                .Select(m => new SquadMemberDto(
                    m.UserId,
                    m.User.Username,
                    m.User.GameNickname,
                    m.IsLeader,
                    m.JoinedAt,
                    enriched[m.UserId].Flags,
                    enriched[m.UserId].Kd))
                .ToList(),
            squad.Members.Count);
    }
}
