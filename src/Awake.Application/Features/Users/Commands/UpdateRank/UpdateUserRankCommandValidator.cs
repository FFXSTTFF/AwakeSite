using FluentValidation;

namespace Awake.Application.Features.Users.Commands.UpdateRank;

public class UpdateUserRankCommandValidator : AbstractValidator<UpdateUserRankCommand>
{
    public UpdateUserRankCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
        RuleFor(x => x.NewRank).IsInEnum().WithMessage("Недопустимый ранг.");
    }
}
