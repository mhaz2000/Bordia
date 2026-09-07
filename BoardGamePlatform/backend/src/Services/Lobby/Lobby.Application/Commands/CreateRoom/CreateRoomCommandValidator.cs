using FluentValidation;

namespace Lobby.Application.Commands.CreateRoom;

/// <summary>
/// Validates the <see cref="CreateRoomCommand"/>.
/// </summary>
public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoomCommandValidator"/> class.
    /// </summary>
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.GameType)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 8);

        RuleFor(x => x.SettingsJson)
            .MaximumLength(4000);
    }
}