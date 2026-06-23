using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Queries.GetTickets;

public class GetTicketsQueryHandler(
    ITicketRepository ticketRepository,
    ICurrentUserService currentUserService
) : IRequestHandler<GetTicketsQuery, IReadOnlyList<TicketListItemDto>>
{
    public async Task<IReadOnlyList<TicketListItemDto>> Handle(
        GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var isOfficerPlus = currentUserService.Rank >= UserRank.Officer;

        var tickets = isOfficerPlus
            ? await ticketRepository.GetAllAsync(cancellationToken)
            : await ticketRepository.GetByAuthorAsync(currentUserService.UserId, cancellationToken);

        return tickets.Select(t => new TicketListItemDto(
            t.Id, t.Type, t.Status, t.GameNickname,
            t.Author?.Username ?? t.DiscordUsername ?? "Discord",
            t.CreatedAt)).ToList();
    }
}
