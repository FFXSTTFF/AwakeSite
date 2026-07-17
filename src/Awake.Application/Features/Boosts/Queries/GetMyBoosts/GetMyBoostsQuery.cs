using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public record GetMyBoostsQuery(Guid UserId) : IRequest<IReadOnlyList<BoostType>>;
