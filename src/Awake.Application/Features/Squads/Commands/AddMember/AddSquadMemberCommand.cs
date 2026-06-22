using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.AddMember;

public record AddSquadMemberCommand(Guid SquadId, Guid UserId) : IRequest<Result<Unit>>;
