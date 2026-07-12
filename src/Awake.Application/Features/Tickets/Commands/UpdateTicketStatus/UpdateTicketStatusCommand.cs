using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;

public record UpdateTicketStatusCommand(
    Guid TicketId,
    TicketStatus NewStatus,
    string? ReviewedByDiscordUsername = null
) : IRequest<Result<bool>>;
