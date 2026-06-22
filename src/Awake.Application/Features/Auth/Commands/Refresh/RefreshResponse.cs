using Awake.Domain.Enums;

namespace Awake.Application.Features.Auth.Commands.Refresh;

public record RefreshResponse(string AccessToken, string Username, UserRank Rank, string UserId, string NewRefreshToken);
