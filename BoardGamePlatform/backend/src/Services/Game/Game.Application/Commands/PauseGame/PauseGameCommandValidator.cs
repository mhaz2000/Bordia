using FluentValidation;

namespace Game.Application.Commands.PauseGame;

/// <summary>
/// Validates the <see cref="PauseGameCommand"/>.
/// </summary>
public class PauseGameCommandValidator : AbstractValidator<PauseGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PauseGameCommandValidator"/> class.
    /// </summary>
    public PauseGameCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();
    }
}