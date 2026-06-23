using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateDiscordTicket;

public class CreateDiscordTicketCommandHandler(
    ITicketRepository ticketRepository,
    INotificationService notificationService,
    IDiscordNotifier discordNotifier
) : IRequestHandler<CreateDiscordTicketCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDiscordTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = new Ticket
        {
            AuthorId = null,
            DiscordUserId = request.DiscordUserId,
            DiscordUsername = request.DiscordUsername,
            GameNickname = request.GameNickname,
            Type = request.Type,
            Description = request.Description,
            Status = TicketStatus.Pending,
            DiscordChannelId = request.DiscordChannelId,
        };

        await ticketRepository.AddAsync(ticket, cancellationToken);

        await notificationService.CreateForRankAsync(
            UserRank.Officer,
            "Новая заявка с Discord",
            $"{request.DiscordUsername} — {request.GameNickname}",
            cancellationToken);

        await discordNotifier.NotifyNewTicketAsync(ticket, cancellationToken);

        return Result<Guid>.Success(ticket.Id);
    }
}
