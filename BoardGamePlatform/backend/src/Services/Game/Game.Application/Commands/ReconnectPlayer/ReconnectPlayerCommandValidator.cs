using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Game.Application.Commands.ReconnectPlayer;

/// <summary>
/// Validates the <see cref="ReconnectPlayerCommand"/>.
/// </summary>
public class ReconnectPlayerCommandValidator : AbstractValidator<ReconnectPlayerCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnectPlayerCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public ReconnectPlayerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.SessionIdRequired);

        RuleFor(x => x.ConnectionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.ConnectionIdRequired);
    }
}
