using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public class GetMyBoostsQueryHandler(
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<GetMyBoostsQuery, IReadOnlyList<BoostType>>
{
    public Task<IReadOnlyList<BoostType>> Handle(
        GetMyBoostsQuery request, CancellationToken cancellationToken) =>
        boostRepository.GetByUserIdAsync(request.UserId, cancellationToken);
}
