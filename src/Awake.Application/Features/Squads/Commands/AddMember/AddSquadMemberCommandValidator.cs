using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.AddMember;

public class AddSquadMemberCommandValidator : AbstractValidator<AddSquadMemberCommand>
{
    public AddSquadMemberCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
    }
}
