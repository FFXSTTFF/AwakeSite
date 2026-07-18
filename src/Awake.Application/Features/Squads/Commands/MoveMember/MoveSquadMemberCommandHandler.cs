using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.MoveMember;

public class MoveSquadMemberCommandHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository
) : IRequestHandler<MoveSquadMemberCommand, Result<bool>>
{
    public const int MaxSquadSize = 5;

    public async Task<Result<bool>> Handle(
        MoveSquadMemberCommand request, CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<bool>.Failure("Отряд не найден.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<bool>.Failure("Пользователь не найден.");

        var current = await squadRepository.GetMembershipByUserIdAsync(request.UserId, cancellationToken);
        if (current is not null && current.SquadId == request.SquadId)
            return Result<bool>.Success(false); // уже там — no-op

        var count = await squadRepository.GetMemberCountAsync(request.SquadId, cancellationToken);
        if (count >= MaxSquadSize)
            return Result<bool>.Failure("Отряд укомплектован (5/5).");

        await squadRepository.MoveMemberAsync(current?.SquadId, new SquadMember
        {
            SquadId = request.SquadId,
            UserId = request.UserId,
            IsLeader = false,
        }, cancellationToken);

        return Result<bool>.Success(true);
    }
}
