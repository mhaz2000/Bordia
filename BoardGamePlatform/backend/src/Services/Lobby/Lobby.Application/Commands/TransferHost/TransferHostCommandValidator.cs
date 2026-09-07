using FluentValidation;

namespace Lobby.Application.Commands.TransferHost;

/// <summary>
/// Validates the <see cref="TransferHostCommand"/>.
/// </summary>
public class TransferHostCommandValidator : AbstractValidator<TransferHostCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransferHostCommandValidator"/> class.
    /// </summary>
    public TransferHostCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();

        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}