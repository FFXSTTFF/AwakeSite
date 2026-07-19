using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public class GetBoostsSummaryQueryHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
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
                    g.OrderBy(r => r.BoostType)
                     .Select(r => BoostItemMapper.ToDto(r, itemCache))
                     .ToList());
            })
            .OrderByDescending(e => e.Boosts.Count)
            .ThenBy(e => e.GameNickname ?? e.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
