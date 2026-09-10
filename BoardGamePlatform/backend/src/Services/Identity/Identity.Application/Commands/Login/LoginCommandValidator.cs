using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.Login;

/// <summary>
/// Validates the <see cref="LoginCommand"/>.
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.IdentifierRequired)
            .MaximumLength(256)
            .WithMessage(ErrorCodes.Validation.IdentifierTooLong);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PasswordRequired);
    }
}
