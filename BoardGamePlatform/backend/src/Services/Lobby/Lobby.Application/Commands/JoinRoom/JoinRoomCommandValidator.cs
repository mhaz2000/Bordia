using FluentValidation;

namespace Lobby.Application.Commands.JoinRoom;

/// <summary>
/// Validates the <see cref="JoinRoomCommand"/>.
/// </summary>
public class JoinRoomCommandValidator : AbstractValidator<JoinRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoinRoomCommandValidator"/> class.
    /// </summary>
    public JoinRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();
    }
}