using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.SendRoomMessage;

/// <summary>
/// Validates the <see cref="SendRoomMessageCommand"/>.
/// </summary>
public class SendRoomMessageCommandValidator : AbstractValidator<SendRoomMessageCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendRoomMessageCommandValidator"/> class.
    /// </summary>
    public SendRoomMessageCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.ChatMessageRequired)
            .MaximumLength(SendRoomMessageCommand.MaxTextLength)
            .WithMessage(ErrorCodes.Validation.ChatMessageTooLong);
    }
}
