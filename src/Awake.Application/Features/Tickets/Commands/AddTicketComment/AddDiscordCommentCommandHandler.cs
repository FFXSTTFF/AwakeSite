using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.AddTicketComment;

public class AddDiscordCommentCommandHandler(
    ITicketRepository ticketRepository,
    INotificationService notificationService
) : IRequestHandler<AddDiscordCommentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        AddDiscordCommentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<bool>.Failure("Тикет не найден.");

        var comment = new TicketComment
        {
            TicketId = request.TicketId,
            AuthorId = null,
            DiscordAuthorName = request.DiscordUsername,
            Content = request.Content,
        };

        await ticketRepository.AddCommentAsync(comment, cancellationToken);

        // Notify website users following this ticket
        if (ticket.AuthorId.HasValue)
            await notificationService.CreateAsync(
                ticket.AuthorId.Value,
                "Новый комментарий к заявке",
                $"{request.DiscordUsername} (Discord): {request.Content[..Math.Min(80, request.Content.Length)]}…",
                cancellationToken);

        // Do NOT mirror back to Discord — the message is already there (user typed it)
        return Result<bool>.Success(true);
    }
}
