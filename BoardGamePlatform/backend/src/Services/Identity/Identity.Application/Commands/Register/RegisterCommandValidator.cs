using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.Register;

/// <summary>
/// Validates the <see cref="RegisterCommand"/>.
/// </summary>
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.EmailRequired)
            .EmailAddress()
            .WithMessage(ErrorCodes.Validation.EmailInvalid)
            .MaximumLength(256)
            .WithMessage(ErrorCodes.Validation.EmailTooLong);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PasswordRequired)
            .MinimumLength(8)
            .WithMessage(ErrorCodes.Validation.PasswordTooShort)
            .MaximumLength(128)
            .WithMessage(ErrorCodes.Validation.PasswordTooLong);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.DisplayNameRequired)
            .MaximumLength(50)
            .WithMessage(ErrorCodes.Validation.DisplayNameTooLong);
    }
}
