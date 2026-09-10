using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.RefreshToken;

/// <summary>
/// Validates the <see cref="RefreshTokenCommand"/>.
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RefreshTokenRequired);
    }
}
