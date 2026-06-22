using FluentValidation;

namespace Awake.Application.Features.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Имя пользователя обязательно.")
            .Length(3, 50).WithMessage("Имя пользователя должно содержать от 3 до 50 символов.")
            .Must(u => !u.Any(char.IsWhiteSpace)).WithMessage("Имя пользователя не должно содержать пробелов.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.")
            .MinimumLength(8).WithMessage("Пароль должен содержать не менее 8 символов.");

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Некорректный формат электронной почты.");
        });
    }
}
