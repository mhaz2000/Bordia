using FluentValidation;

namespace Lobby.Application.Commands.CloseRoom;

/// <summary>
/// Validates the <see cref="CloseRoomCommand"/>.
/// </summary>
public class CloseRoomCommandValidator : AbstractValidator<CloseRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloseRoomCommandValidator"/> class.
    /// </summary>
    public CloseRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();
    }
}