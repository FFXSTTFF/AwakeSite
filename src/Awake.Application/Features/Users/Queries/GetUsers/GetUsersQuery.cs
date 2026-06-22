using MediatR;

namespace Awake.Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;
