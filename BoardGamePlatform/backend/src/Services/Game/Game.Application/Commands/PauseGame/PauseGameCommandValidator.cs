using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Game.Application.Commands.PauseGame;

/// <summary>
/// Validates the <see cref="PauseGameCommand"/>.
/// </summary>
public class PauseGameCommandValidator : AbstractValidator<PauseGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PauseGameCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public PauseGameCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.SessionIdRequired);
    }
}
