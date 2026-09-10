using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.KickPlayer;

/// <summary>
/// Validates the <see cref="KickPlayerCommand"/>.
/// </summary>
public class KickPlayerCommandValidator : AbstractValidator<KickPlayerCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KickPlayerCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public KickPlayerCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PlayerIdRequired);
    }
}
