using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateTicket;

public record CreateTicketCommand(string GameNickname, TicketType Type, string Description)
    : IRequest<Result<TicketListItemDto>>;
