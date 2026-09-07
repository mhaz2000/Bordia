using FluentValidation;

namespace Game.Application.Commands.ResumeGame;

/// <summary>
/// Validates the <see cref="ResumeGameCommand"/>.
/// </summary>
public class ResumeGameCommandValidator : AbstractValidator<ResumeGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeGameCommandValidator"/> class.
    /// </summary>
    public ResumeGameCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();
    }
}