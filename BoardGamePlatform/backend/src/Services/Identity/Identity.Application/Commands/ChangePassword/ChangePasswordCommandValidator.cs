using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.ChangePassword;

/// <summary>
/// Validates the <see cref="ChangePasswordCommand"/>.
/// </summary>
public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.CurrentPasswordRequired);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PasswordRequired)
            .MinimumLength(8)
            .WithMessage(ErrorCodes.Validation.PasswordTooShort)
            .MaximumLength(128)
            .WithMessage(ErrorCodes.Validation.PasswordTooLong)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage(ErrorCodes.Validation.NewPasswordDiffers);
    }
}
