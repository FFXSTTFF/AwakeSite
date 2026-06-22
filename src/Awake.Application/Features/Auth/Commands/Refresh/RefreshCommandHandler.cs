using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Refresh;

public class RefreshCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    ITokenService tokenService
) : IRequestHandler<RefreshCommand, Result<RefreshResponse>>
{
    public async Task<Result<RefreshResponse>> Handle(
        RefreshCommand request,
        CancellationToken cancellationToken)
    {
        var stored = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            return Result<RefreshResponse>.Failure("Refresh token недействителен или истёк.");

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
            return Result<RefreshResponse>.Failure("Пользователь не найден.");

        await refreshTokenRepository.RevokeAsync(request.RefreshToken, cancellationToken);

        var newAccessToken = tokenService.GenerateAccessToken(user);
        return Result<RefreshResponse>.Success(
            new RefreshResponse(newAccessToken, user.Username, user.Rank, user.Id.ToString()));
    }
}
