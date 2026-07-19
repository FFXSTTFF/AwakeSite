using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public class GetMyBoostsQueryHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
) : IRequestHandler<GetMyBoostsQuery, IReadOnlyList<BoostItemDto>>
{
    public async Task<IReadOnlyList<BoostItemDto>> Handle(
        GetMyBoostsQuery request, CancellationToken cancellationToken) =>
        (await boostRepository.GetByUserIdAsync(request.UserId, cancellationToken))
            .Select(r => BoostItemMapper.ToDto(r, itemCache))
            .ToList();
}
