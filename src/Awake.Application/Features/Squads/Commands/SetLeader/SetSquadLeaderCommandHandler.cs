using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.SetLeader;

public class SetSquadLeaderCommandHandler(ISquadRepository squadRepository)
    : IRequestHandler<SetSquadLeaderCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        SetSquadLeaderCommand request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdWithMembersAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<Unit>.Failure("Отряд не найден.");

        var newLeader = squad.Members.FirstOrDefault(m => m.UserId == request.UserId);
        if (newLeader is null)
            return Result<Unit>.Failure("Пользователь не состоит в этом отряде.");

        foreach (var member in squad.Members.Where(m => m.IsLeader && m.UserId != request.UserId))
        {
            member.IsLeader = false;
            await squadRepository.UpdateMemberAsync(member, cancellationToken);
        }

        newLeader.IsLeader = true;
        await squadRepository.UpdateMemberAsync(newLeader, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
