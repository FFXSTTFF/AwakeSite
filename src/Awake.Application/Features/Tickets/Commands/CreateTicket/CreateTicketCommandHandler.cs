using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketCommandHandler(
    ITicketRepository ticketRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IDiscordNotifier discordNotifier,
    INotificationService notificationService,
    IPlayerDataAggregator playerDataAggregator
) : IRequestHandler<CreateTicketCommand, Result<TicketListItemDto>>
{
    public async Task<Result<TicketListItemDto>> Handle(
        CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(currentUserService.UserId, cancellationToken);
        if (user is null)
            return Result<TicketListItemDto>.Failure("Пользователь не найден.");

        var ticket = new Ticket
        {
            AuthorId = user.Id,
            GameNickname = request.GameNickname,
            Type = request.Type,
            Description = request.Description,
            Status = TicketStatus.Pending,
            Loadout = request.Loadout,
        };

        await ticketRepository.AddAsync(ticket, cancellationToken);

        await notificationService.CreateForRankAsync(
            UserRank.Officer,
            "Новая заявка",
            $"{user.Username} — {request.GameNickname}",
            cancellationToken);

        await discordNotifier.NotifyNewTicketAsync(ticket, cancellationToken);

        // Pre-warm player data cache so officers see stats immediately (fire-and-forget)
        _ = Task.Run(() => playerDataAggregator.GetPlayerDataAsync(request.GameNickname));

        var dto = new TicketListItemDto(
            ticket.Id, ticket.Type, ticket.Status, ticket.GameNickname,
            user.Username, ticket.CreatedAt);

        return Result<TicketListItemDto>.Success(dto);
    }
}
