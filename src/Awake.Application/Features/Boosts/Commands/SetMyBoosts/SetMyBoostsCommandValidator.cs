using FluentValidation;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandValidator : AbstractValidator<SetMyBoostsCommand>
{
    public SetMyBoostsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
        RuleFor(x => x.Selections).NotNull().WithMessage("Список бустов обязателен.");
        RuleFor(x => x.Selections)
            .Must(s => s == null || s.Select(x => x.BoostType).Distinct().Count() == s.Count)
            .WithMessage("Не больше одного предмета на тип буста.");
        RuleForEach(x => x.Selections).ChildRules(sel =>
        {
            sel.RuleFor(s => s.BoostType).IsInEnum().WithMessage("Недопустимый тип буста.");
            sel.RuleFor(s => s.ItemId).NotEmpty().WithMessage("ID предмета обязателен.");
        });
    }
}
