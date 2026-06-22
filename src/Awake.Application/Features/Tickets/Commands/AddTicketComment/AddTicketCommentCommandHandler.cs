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
    ICurrentUserService currentUserService
) : IRequestHandler<AddTicketCommentCommand, Result<TicketCommentDto>>
{
    public async Task<Result<TicketCommentDto>> Handle(
        AddTicketCommentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<TicketCommentDto>.Failure("Тикет не найден.");

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

        var dto = new TicketCommentDto(
            comment.Id, user.Username, comment.Content, comment.CreatedAt);

        return Result<TicketCommentDto>.Success(dto);
    }
}
