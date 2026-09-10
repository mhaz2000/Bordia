using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Game.Application.Commands.ProcessGameAction;

/// <summary>
/// Validates the <see cref="ProcessGameActionCommand"/>.
/// </summary>
public class ProcessGameActionCommandValidator : AbstractValidator<ProcessGameActionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessGameActionCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public ProcessGameActionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.SessionIdRequired);

        RuleFor(x => x.ActionType)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.ActionTypeRequired)
            .MaximumLength(50)
            .WithMessage(ErrorCodes.Validation.ActionTypeTooLong);

        RuleFor(x => x.Payload)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PayloadRequired);
    }
}
