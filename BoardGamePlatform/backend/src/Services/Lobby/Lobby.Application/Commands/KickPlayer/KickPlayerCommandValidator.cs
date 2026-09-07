using FluentValidation;

namespace Lobby.Application.Commands.KickPlayer;

/// <summary>
/// Validates the <see cref="KickPlayerCommand"/>.
/// </summary>
public class KickPlayerCommandValidator : AbstractValidator<KickPlayerCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KickPlayerCommandValidator"/> class.
    /// </summary>
    public KickPlayerCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();

        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}