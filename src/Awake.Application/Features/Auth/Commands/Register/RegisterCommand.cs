using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Username,
    string Password,
    string? Email
) : IRequest<Result<RegisterResponse>>;
