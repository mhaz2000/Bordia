using FluentValidation;

namespace Lobby.Application.Commands.StartGame;

/// <summary>
/// Validates the <see cref="StartGameCommand"/>.
/// </summary>
public class StartGameCommandValidator : AbstractValidator<StartGameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartGameCommandValidator"/> class.
    /// </summary>
    public StartGameCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();
    }
}