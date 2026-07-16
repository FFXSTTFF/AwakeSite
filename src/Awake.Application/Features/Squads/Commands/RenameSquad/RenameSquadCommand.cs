using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public record RenameSquadCommand(Guid SquadId, string Name) : IRequest<Result<Unit>>;
