using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.JoinRoom;

/// <summary>
/// Validates the <see cref="JoinRoomCommand"/>.
/// </summary>
public class JoinRoomCommandValidator : AbstractValidator<JoinRoomCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoinRoomCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public JoinRoomCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.Code))
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.Code)
            .NotEmpty()
            .When(x => x.RoomId is null)
            .WithMessage(ErrorCodes.Validation.RoomCodeRequired);

        RuleFor(x => x.Code)
            .MaximumLength(12)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));
    }
}
