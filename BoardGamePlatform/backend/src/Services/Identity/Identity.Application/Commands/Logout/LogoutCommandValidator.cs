using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.Logout;

/// <summary>
/// Validates the <see cref="LogoutCommand"/>.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RefreshTokenRequired);
    }
}
