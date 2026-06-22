using Awake.Domain.Enums;

namespace Awake.Application.Features.Auth.Commands.Login;

public record LoginResponse(string AccessToken, string Username, UserRank Rank);
