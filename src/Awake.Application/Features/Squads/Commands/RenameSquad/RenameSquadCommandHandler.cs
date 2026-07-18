using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public class RenameSquadCommandHandler(ISquadRepository squadRepository)
    : IRequestHandler<RenameSquadCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(RenameSquadCommand request, CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<Unit>.Failure("Отряд не найден.");

        squad.Name = request.Name.Trim();
        await squadRepository.UpdateAsync(squad, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
