using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateDiscordTicket;

public record CreateDiscordTicketCommand(
    string DiscordUserId,
    string DiscordUsername,
    string GameNickname,
    TicketType Type,
    string Description,
    string? DiscordChannelId = null,
    Loadout? Loadout = null
) : IRequest<Result<Guid>>;
