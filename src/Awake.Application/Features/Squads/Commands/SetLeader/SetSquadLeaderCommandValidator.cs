using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.SetLeader;

public class SetSquadLeaderCommandValidator : AbstractValidator<SetSquadLeaderCommand>
{
    public SetSquadLeaderCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
    }
}
