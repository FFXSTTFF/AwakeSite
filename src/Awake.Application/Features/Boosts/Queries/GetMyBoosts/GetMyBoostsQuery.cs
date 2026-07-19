using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public record GetMyBoostsQuery(Guid UserId) : IRequest<IReadOnlyList<BoostItemDto>>;
