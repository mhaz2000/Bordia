using FluentValidation;

namespace Identity.Application.Commands.Logout;

/// <summary>
/// Validates the <see cref="LogoutCommand"/>.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommandValidator"/> class.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}