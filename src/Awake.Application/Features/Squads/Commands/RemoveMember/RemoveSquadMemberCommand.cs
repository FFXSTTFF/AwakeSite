using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RemoveMember;

public record RemoveSquadMemberCommand(Guid SquadId, Guid UserId) : IRequest<Result<Unit>>;
