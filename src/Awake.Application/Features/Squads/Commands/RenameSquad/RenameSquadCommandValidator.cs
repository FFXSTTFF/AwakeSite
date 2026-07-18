using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public class RenameSquadCommandValidator : AbstractValidator<RenameSquadCommand>
{
    public RenameSquadCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Название отряда обязательно.")
            .MaximumLength(100).WithMessage("Название отряда не длиннее 100 символов.");
    }
}
