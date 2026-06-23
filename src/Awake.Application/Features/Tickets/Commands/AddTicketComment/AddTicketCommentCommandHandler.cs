using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.AddTicketComment;

public class AddTicketCommentCommandHandler(
    ITicketRepository ticketRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    IDiscordBotService discordBotService
) : IRequestHandler<AddTicketCommentCommand, Result<TicketCommentDto>>
{
    public async Task<Result<TicketCommentDto>> Handle(
        AddTicketCommentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<TicketCommentDto>.Failure("Тикет не найден.");

        if (ticket.Status == Domain.Enums.TicketStatus.Closed)
            return Result<TicketCommentDto>.Failure("Заявка закрыта. Комментарии недоступны.");

        var user = await userRepository.GetByIdAsync(currentUserService.UserId, cancellationToken);
        if (user is null)
            return Result<TicketCommentDto>.Failure("Пользователь не найден.");

        var comment = new TicketComment
        {
            TicketId = request.TicketId,
            AuthorId = user.Id,
            Content = request.Content,
        };

        await ticketRepository.AddCommentAsync(comment, cancellationToken);

        // Notify ticket author if they are a website user and not the commenter
        if (ticket.AuthorId.HasValue && ticket.AuthorId.Value != currentUserService.UserId)
        {
            await notificationService.CreateAsync(
                ticket.AuthorId.Value,
                "Новый комментарий к заявке",
                $"{user.Username}: {request.Content[..Math.Min(80, request.Content.Length)]}…",
                cancellationToken);
        }

        // Notify the reviewer if they exist and are not the commenter
        if (ticket.ReviewedBy.HasValue && ticket.ReviewedBy.Value != currentUserService.UserId)
        {
            await notificationService.CreateAsync(
                ticket.ReviewedBy.Value,
                "Новый комментарий в рассматриваемой заявке",
                $"{user.Username}: {request.Content[..Math.Min(80, request.Content.Length)]}…",
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(ticket.DiscordChannelId))
            await discordBotService.PostCommentAsync(
                ticket.DiscordChannelId, user.Username, comment.Content, cancellationToken);

        var dto = new TicketCommentDto(
            comment.Id, user.Username, comment.Content, comment.CreatedAt);

        return Result<TicketCommentDto>.Success(dto);
    }
}
