using FluentValidation;

namespace Game.Application.Commands.ReconnectPlayer;

/// <summary>
/// Validates the <see cref="ReconnectPlayerCommand"/>.
/// </summary>
public class ReconnectPlayerCommandValidator : AbstractValidator<ReconnectPlayerCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnectPlayerCommandValidator"/> class.
    /// </summary>
    public ReconnectPlayerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.ConnectionId)
            .NotEmpty();
    }
}