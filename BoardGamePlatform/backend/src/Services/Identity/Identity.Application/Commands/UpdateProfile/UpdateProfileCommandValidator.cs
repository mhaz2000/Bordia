using FluentValidation;

namespace Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Validates the <see cref="UpdateProfileCommand"/>.
/// </summary>
public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProfileCommandValidator"/> class.
    /// </summary>
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(50);
    }
}