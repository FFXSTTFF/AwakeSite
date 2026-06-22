using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<Result<RefreshResponse>>;
