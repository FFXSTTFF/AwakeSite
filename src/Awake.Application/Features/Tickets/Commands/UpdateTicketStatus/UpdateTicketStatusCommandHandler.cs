using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;

public class UpdateTicketStatusCommandHandler(
    ITicketRepository ticketRepository,
    ICurrentUserService currentUserService,
    IDiscordNotifier discordNotifier
) : IRequestHandler<UpdateTicketStatusCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<bool>.Failure("Тикет не найден.");

        ticket.Status = request.NewStatus;
        ticket.ReviewedBy = currentUserService.UserId;
        ticket.ReviewedAt = DateTime.UtcNow;

        await ticketRepository.UpdateAsync(ticket, cancellationToken);

        if (request.NewStatus is TicketStatus.Approved or TicketStatus.Rejected)
            await discordNotifier.NotifyTicketDecisionAsync(ticket, cancellationToken);

        return Result<bool>.Success(true);
    }
}
