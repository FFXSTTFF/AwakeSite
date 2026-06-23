using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateDiscordTicket;

public record CreateDiscordTicketCommand(
    string DiscordUserId,
    string DiscordUsername,
    string GameNickname,
    TicketType Type,
    string Description
) : IRequest<Result<bool>>;
