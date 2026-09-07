using FluentValidation;

namespace Identity.Application.Commands.ChangePassword;

/// <summary>
/// Validates the <see cref="ChangePasswordCommand"/>.
/// </summary>
public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordCommandValidator"/> class.
    /// </summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must differ from the current password.");
    }
}