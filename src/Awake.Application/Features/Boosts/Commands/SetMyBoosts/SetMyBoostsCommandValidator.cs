using FluentValidation;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandValidator : AbstractValidator<SetMyBoostsCommand>
{
    public SetMyBoostsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
        RuleFor(x => x.BoostTypes).NotNull().WithMessage("Список бустов обязателен.");
        RuleForEach(x => x.BoostTypes).IsInEnum().WithMessage("Недопустимый тип буста.");
    }
}
