using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService
) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null || user.PasswordHash is null)
            return Result<LoginResponse>.Failure("Неверный логин или пароль.");

        var isPasswordValid = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (!isPasswordValid)
            return Result<LoginResponse>.Failure("Неверный логин или пароль.");

        var accessToken = tokenService.GenerateAccessToken(user);

        return Result<LoginResponse>.Success(new LoginResponse(accessToken, user.Username, user.Rank, user.Id.ToString()));
    }
}
