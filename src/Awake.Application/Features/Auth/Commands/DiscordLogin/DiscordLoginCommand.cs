using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Models;
using Awake.Application.Features.Auth.Commands.Login;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.DiscordLogin;

public record DiscordLoginCommand(DiscordUserInfo DiscordUser) : IRequest<Result<LoginResponse>>;
