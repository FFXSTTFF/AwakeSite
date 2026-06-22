using FluentValidation;

namespace Awake.Application.Features.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.");
    }
}
