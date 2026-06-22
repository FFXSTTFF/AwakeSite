using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Users.Commands.UpdateRank;

public class UpdateUserRankCommandHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUser
) : IRequestHandler<UpdateUserRankCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        UpdateUserRankCommand request,
        CancellationToken cancellationToken)
    {
        if (request.NewRank == UserRank.Leader && currentUser.Rank != UserRank.Leader)
            return Result<Unit>.Failure("Только лидер может назначать других лидеров.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<Unit>.Failure("Пользователь не найден.");

        user.Rank = request.NewRank;
        await userRepository.UpdateAsync(user, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
