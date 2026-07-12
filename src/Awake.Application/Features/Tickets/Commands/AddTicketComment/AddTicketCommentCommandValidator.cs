using FluentValidation;

namespace Awake.Application.Features.Tickets.Commands.AddTicketComment;

public class AddTicketCommentCommandValidator : AbstractValidator<AddTicketCommentCommand>
{
    public AddTicketCommentCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty().WithMessage("ID тикета обязателен.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000)
            .WithMessage("Текст комментария обязателен и не должен превышать 1000 символов.");
    }
}
