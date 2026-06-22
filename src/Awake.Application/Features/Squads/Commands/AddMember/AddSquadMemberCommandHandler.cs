using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.AddMember;

public class AddSquadMemberCommandHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository
) : IRequestHandler<AddSquadMemberCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        AddSquadMemberCommand request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<Unit>.Failure("Отряд не найден.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<Unit>.Failure("Пользователь не найден.");

        var alreadyInSquad = await squadRepository.IsUserInAnySquadAsync(request.UserId, cancellationToken);
        if (alreadyInSquad)
            return Result<Unit>.Failure("Пользователь уже состоит в отряде.");

        var count = await squadRepository.GetMemberCountAsync(request.SquadId, cancellationToken);
        if (count >= 5)
            return Result<Unit>.Failure("Отряд уже заполнен (максимум 5 участников).");

        await squadRepository.AddMemberAsync(new SquadMember
        {
            SquadId = request.SquadId,
            UserId = request.UserId,
            IsLeader = false,
        }, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
