using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.MoveMember;

public class MoveSquadMemberCommandValidator : AbstractValidator<MoveSquadMemberCommand>
{
    public MoveSquadMemberCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
    }
}
