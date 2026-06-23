using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;

public class UpdateTicketStatusCommandHandler(
    ITicketRepository ticketRepository,
    IUserRepository userRepository,
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
        ticket.ReviewedBy = currentUserService.IsAuthenticated ? currentUserService.UserId : null;
        ticket.ReviewedAt = DateTime.UtcNow;

        await ticketRepository.UpdateAsync(ticket, cancellationToken);

        if (request.NewStatus is TicketStatus.Approved or TicketStatus.Rejected)
        {
            var statusText = request.NewStatus == TicketStatus.Approved
                ? "принята ✅"
                : "отклонена ❌";
            var dmMessage = $"Ваша заявка ({ticket.GameNickname}) была {statusText}.";

            // In-app notification for website users
            if (ticket.AuthorId.HasValue)
            {
                await notificationService.CreateAsync(
                    ticket.AuthorId.Value,
                    "Решение по заявке",
                    dmMessage,
                    cancellationToken);
            }

            // Post update in private ticket channel (if it exists)
            if (!string.IsNullOrEmpty(ticket.DiscordChannelId))
            {
                string reviewerName;
                if (!string.IsNullOrEmpty(request.ReviewedByDiscordUsername))
                    reviewerName = request.ReviewedByDiscordUsername;
                else if (currentUserService.IsAuthenticated)
                {
                    var reviewer = await userRepository.GetByIdAsync(currentUserService.UserId, cancellationToken);
                    reviewerName = reviewer?.Username ?? "Офицер";
                }
                else
                    reviewerName = "Офицер";

                var statusEmbed = request.NewStatus == TicketStatus.Approved
                    ? "✅ **Заявка принята**"
                    : "❌ **Заявка отклонена**";
                var message = $"{statusEmbed}\nРешение принял: **{reviewerName}**";

                await discordBotService.PostStatusUpdateAsync(
                    ticket.DiscordChannelId, message, cancellationToken);
            }

            // Discord DM for Discord-submitted tickets without a channel
            if (!string.IsNullOrEmpty(ticket.DiscordUserId) && string.IsNullOrEmpty(ticket.DiscordChannelId))
                await discordBotService.SendDmAsync(ticket.DiscordUserId, dmMessage, cancellationToken);

            await discordNotifier.NotifyTicketDecisionAsync(ticket, cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
