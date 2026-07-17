using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public class GetBoostsSummaryQueryHandler(
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<GetBoostsSummaryQuery, IReadOnlyList<BoostSummaryEntryDto>>
{
    public async Task<IReadOnlyList<BoostSummaryEntryDto>> Handle(
        GetBoostsSummaryQuery request, CancellationToken cancellationToken)
    {
        var all = await boostRepository.GetAllAsync(cancellationToken);
        return all
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                return new BoostSummaryEntryDto(
                    g.Key,
                    user.Username,
                    user.GameNickname,
                    g.Select(r => r.BoostType).OrderBy(t => t).ToList());
            })
            .OrderByDescending(e => e.BoostTypes.Count)
            .ThenBy(e => e.GameNickname ?? e.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
