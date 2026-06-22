using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Username,
    string Password
) : IRequest<Result<LoginResponse>>;
