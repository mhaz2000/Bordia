using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.SetReady;

/// <summary>
/// Validates the <see cref="SetReadyCommand"/>.
/// </summary>
public class SetReadyCommandValidator : AbstractValidator<SetReadyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetReadyCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public SetReadyCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);
    }
}
