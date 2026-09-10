using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Lobby.Application.Commands.StartGame;

/// <summary>
/// Validates the <see cref="StartGameCommand"/>.
/// </summary>
public class StartGameCommandValidator : AbstractValidator<StartGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartGameCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public StartGameCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);
    }
}
