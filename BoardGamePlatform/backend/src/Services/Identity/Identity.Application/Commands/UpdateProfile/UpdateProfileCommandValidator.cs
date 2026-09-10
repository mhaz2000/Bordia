using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Validates the <see cref="UpdateProfileCommand"/>.
/// </summary>
public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProfileCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.DisplayNameRequired)
            .MaximumLength(50)
            .WithMessage(ErrorCodes.Validation.DisplayNameTooLong);
    }
}
