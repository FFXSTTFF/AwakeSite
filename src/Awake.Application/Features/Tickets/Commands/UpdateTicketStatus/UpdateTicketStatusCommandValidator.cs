using Awake.Domain.Enums;
using FluentValidation;

namespace Awake.Application.Features.Tickets.Commands.UpdateTicketStatus;

public class UpdateTicketStatusCommandValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty().WithMessage("ID тикета обязателен.");
        RuleFor(x => x.NewStatus)
            .Must(s => s is TicketStatus.InReview or TicketStatus.Approved or TicketStatus.Rejected)
            .WithMessage("Недопустимый статус. Допустимые значения: InReview, Approved, Rejected.");
    }
}
