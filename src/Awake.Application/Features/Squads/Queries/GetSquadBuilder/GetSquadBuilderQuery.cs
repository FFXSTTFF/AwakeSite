using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public record GetSquadBuilderQuery : IRequest<Result<SquadBuilderDto>>;
