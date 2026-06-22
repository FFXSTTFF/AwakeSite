using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RemoveMember;

public class RemoveSquadMemberCommandHandler(ISquadRepository squadRepository)
    : IRequestHandler<RemoveSquadMemberCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        RemoveSquadMemberCommand request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdWithMembersAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<Unit>.Failure("Отряд не найден.");

        var member = squad.Members.FirstOrDefault(m => m.UserId == request.UserId);
        if (member is null)
            return Result<Unit>.Failure("Пользователь не состоит в этом отряде.");

        await squadRepository.RemoveMemberAsync(request.SquadId, request.UserId, cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
