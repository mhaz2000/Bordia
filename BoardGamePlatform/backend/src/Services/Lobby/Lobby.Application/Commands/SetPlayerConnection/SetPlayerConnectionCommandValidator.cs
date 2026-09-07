using FluentValidation;

namespace Lobby.Application.Commands.SetPlayerConnection;

/// <summary>
/// Validates the <see cref="SetPlayerConnectionCommand"/>.
/// </summary>
public class SetPlayerConnectionCommandValidator : AbstractValidator<SetPlayerConnectionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetPlayerConnectionCommandValidator"/> class.
    /// </summary>
    public SetPlayerConnectionCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();

        RuleFor(x => x.ConnectionId)
            .NotEmpty();
    }
}