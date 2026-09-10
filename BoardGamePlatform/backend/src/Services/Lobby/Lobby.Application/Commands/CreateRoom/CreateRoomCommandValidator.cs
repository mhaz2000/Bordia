using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.CreateRoom;

/// <summary>
/// Validates the <see cref="CreateRoomCommand"/>.
/// </summary>
public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoomCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomNameRequired)
            .MaximumLength(100)
            .WithMessage(ErrorCodes.Validation.RoomNameTooLong);

        RuleFor(x => x.GameType)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.GameTypeRequired)
            .MaximumLength(50)
            .WithMessage(ErrorCodes.Validation.GameTypeTooLong);

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 8)
            .WithMessage(ErrorCodes.Validation.MaxPlayersRange);

        RuleFor(x => x.SettingsJson)
            .MaximumLength(4000)
            .WithMessage(ErrorCodes.Validation.SettingsTooLong);
    }
}
