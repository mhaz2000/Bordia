using FluentValidation;

namespace Lobby.Application.Commands.LeaveRoom;

/// <summary>
/// Validates the <see cref="LeaveRoomCommand"/>.
/// </summary>
public class LeaveRoomCommandValidator : AbstractValidator<LeaveRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaveRoomCommandValidator"/> class.
    /// </summary>
    public LeaveRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();
    }
}