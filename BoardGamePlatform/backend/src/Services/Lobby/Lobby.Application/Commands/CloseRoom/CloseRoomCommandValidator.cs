using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.CloseRoom;

/// <summary>
/// Validates the <see cref="CloseRoomCommand"/>.
/// </summary>
public class CloseRoomCommandValidator : AbstractValidator<CloseRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloseRoomCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public CloseRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);
    }
}
