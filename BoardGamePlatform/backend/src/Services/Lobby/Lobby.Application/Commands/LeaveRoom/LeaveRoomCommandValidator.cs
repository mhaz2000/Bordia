using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.LeaveRoom;

/// <summary>
/// Validates the <see cref="LeaveRoomCommand"/>.
/// </summary>
public class LeaveRoomCommandValidator : AbstractValidator<LeaveRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaveRoomCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public LeaveRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);
    }
}
