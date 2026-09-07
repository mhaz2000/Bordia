using FluentValidation;

namespace Game.Application.Commands.ProcessGameAction;

/// <summary>
/// Validates the <see cref="ProcessGameActionCommand"/>.
/// </summary>
public class ProcessGameActionCommandValidator : AbstractValidator<ProcessGameActionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessGameActionCommandValidator"/> class.
    /// </summary>
    public ProcessGameActionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.ActionType)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Payload)
            .NotEmpty();
    }
}