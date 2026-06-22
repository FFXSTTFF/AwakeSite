using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.RemoveMember;

public class RemoveSquadMemberCommandValidator : AbstractValidator<RemoveSquadMemberCommand>
{
    public RemoveSquadMemberCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
    }
}
