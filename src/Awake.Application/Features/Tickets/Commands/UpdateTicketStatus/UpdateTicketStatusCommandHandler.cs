using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;

public class UpdateTicketStatusCommandHandler(
    ITicketRepository ticketRepository,
    ICurrentUserService currentUserService,
    IDiscordNotifier discordNotifier,
    IDiscordBotService discordBotService,
    INotificationService notificationService
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
        {
            var statusText = request.NewStatus == TicketStatus.Approved ? "принята ✅" : "отклонена ❌";
            var message = $"Ваша заявка ({ticket.GameNickname}) была {statusText}.";

            // In-app notification for website users
            if (ticket.AuthorId.HasValue)
            {
                await notificationService.CreateAsync(
                    ticket.AuthorId.Value,
                    "Решение по заявке",
                    message,
                    cancellationToken);
            }

            // Discord DM for Discord-submitted tickets
            if (!string.IsNullOrEmpty(ticket.DiscordUserId))
            {
                await discordBotService.SendDmAsync(ticket.DiscordUserId, message, cancellationToken);
            }

            await discordNotifier.NotifyTicketDecisionAsync(ticket, cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
