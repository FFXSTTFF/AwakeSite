using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.SetLeader;

public record SetSquadLeaderCommand(Guid SquadId, Guid UserId) : IRequest<Result<Unit>>;
