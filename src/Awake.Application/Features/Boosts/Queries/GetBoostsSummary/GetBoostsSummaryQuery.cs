using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record GetBoostsSummaryQuery() : IRequest<IReadOnlyList<BoostSummaryEntryDto>>;
