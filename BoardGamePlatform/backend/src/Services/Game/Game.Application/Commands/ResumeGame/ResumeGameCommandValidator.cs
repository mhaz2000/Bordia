using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Game.Application.Commands.ResumeGame;

/// <summary>
/// Validates the <see cref="ResumeGameCommand"/>.
/// </summary>
public class ResumeGameCommandValidator : AbstractValidator<ResumeGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeGameCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public ResumeGameCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.SessionIdRequired);
    }
}
