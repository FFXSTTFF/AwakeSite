using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public record GetSquadsQuery : IRequest<IReadOnlyList<SquadDto>>;
