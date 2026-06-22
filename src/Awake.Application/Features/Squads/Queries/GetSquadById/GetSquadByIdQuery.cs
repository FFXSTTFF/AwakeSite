using Awake.Application.Features.Squads.Queries.GetSquads;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadById;

public record GetSquadByIdQuery(Guid SquadId) : IRequest<SquadDto>;
