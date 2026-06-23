using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Queries.GetTicketById;

public class GetTicketByIdQueryHandler(
    ITicketRepository ticketRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IPlayerDataAggregator playerDataAggregator
) : IRequestHandler<GetTicketByIdQuery, Result<TicketDetailDto>>
{
    public async Task<Result<TicketDetailDto>> Handle(
        GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdWithDetailsAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<TicketDetailDto>.Failure("Тикет не найден.");

        var isOfficerPlus = currentUserService.Rank >= UserRank.Officer;
        var isAuthor = ticket.AuthorId == currentUserService.UserId;

        if (!isOfficerPlus && !isAuthor)
            return Result<TicketDetailDto>.Failure("Нет доступа к этому тикету.");

        object? playerData = null;
        if (isOfficerPlus)
        {
            var pd = await playerDataAggregator.GetPlayerDataAsync(ticket.GameNickname, cancellationToken);
            playerData = pd;
        }

        string? reviewedByUsername = null;
        if (ticket.ReviewedBy.HasValue)
        {
            var reviewer = await userRepository.GetByIdAsync(ticket.ReviewedBy.Value, cancellationToken);
            reviewedByUsername = reviewer?.Username;
        }

        var comments = ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TicketCommentDto(c.Id, c.Author.Username, c.Content, c.CreatedAt))
            .ToList();

        var dto = new TicketDetailDto(
            ticket.Id, ticket.Type, ticket.Status, ticket.GameNickname,
            ticket.Author?.Username ?? ticket.DiscordUsername ?? "Discord",
            ticket.Description, ticket.CreatedAt,
            ticket.ReviewedAt, reviewedByUsername,
            comments, playerData);

        return Result<TicketDetailDto>.Success(dto);
    }
}
