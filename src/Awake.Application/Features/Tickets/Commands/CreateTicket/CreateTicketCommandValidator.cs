using FluentValidation;

namespace Awake.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.GameNickname).NotEmpty().MaximumLength(100)
            .WithMessage("Игровой никнейм обязателен и не должен превышать 100 символов.");
        RuleFor(x => x.Type).IsInEnum()
            .WithMessage("Недопустимый тип тикета.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000)
            .WithMessage("Описание обязательно и не должно превышать 2000 символов.");
    }
}
