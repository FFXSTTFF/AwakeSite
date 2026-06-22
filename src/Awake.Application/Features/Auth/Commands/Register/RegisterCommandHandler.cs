using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Register;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher
) : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await userRepository.ExistsByUsernameAsync(request.Username, cancellationToken);
        if (exists)
            return Result<RegisterResponse>.Failure("Имя пользователя уже занято.");

        var passwordHash = passwordHasher.Hash(request.Password);

        var user = new User
        {
            Username = request.Username,
            PasswordHash = passwordHash,
            Email = request.Email,
            Rank = UserRank.Guest
        };

        await userRepository.AddAsync(user, cancellationToken);

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Username));
    }
}
