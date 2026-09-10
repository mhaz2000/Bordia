using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.TransferHost;

/// <summary>
/// Validates the <see cref="TransferHostCommand"/>.
/// </summary>
public class TransferHostCommandValidator : AbstractValidator<TransferHostCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransferHostCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public TransferHostCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PlayerIdRequired);
    }
}
