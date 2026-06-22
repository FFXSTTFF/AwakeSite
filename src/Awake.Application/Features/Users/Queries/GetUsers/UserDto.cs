using Awake.Domain.Enums;

namespace Awake.Application.Features.Users.Queries.GetUsers;

public record UserDto(
    Guid Id,
    string Username,
    string? Email,
    string? GameNickname,
    UserRank Rank,
    DateTime CreatedAt);
