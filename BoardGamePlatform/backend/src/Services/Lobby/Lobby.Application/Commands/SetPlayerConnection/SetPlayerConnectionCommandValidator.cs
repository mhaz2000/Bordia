using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.SetPlayerConnection;

/// <summary>
/// Validates the <see cref="SetPlayerConnectionCommand"/>.
/// </summary>
public class SetPlayerConnectionCommandValidator : AbstractValidator<SetPlayerConnectionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetPlayerConnectionCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public SetPlayerConnectionCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.ConnectionId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.ConnectionIdRequired);
    }
}
