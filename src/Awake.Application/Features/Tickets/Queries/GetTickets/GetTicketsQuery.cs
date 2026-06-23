using Awake.Application.Features.Tickets.Dtos;
using MediatR;

namespace Awake.Application.Features.Tickets.Queries.GetTickets;

public record GetTicketsQuery : IRequest<IReadOnlyList<TicketListItemDto>>;
