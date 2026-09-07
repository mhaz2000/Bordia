using FluentValidation;

namespace Game.Application.Commands.CreateGameSession;

/// <summary>
/// Validates the <see cref="CreateGameSessionCommand"/>.
/// </summary>
public class CreateGameSessionCommandValidator : AbstractValidator<CreateGameSessionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateGameSessionCommandValidator"/> class.
    /// </summary>
    public CreateGameSessionCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty();

        RuleFor(x => x.GameType)
            .NotEmpty();

        RuleFor(x => x.Players)
            .NotEmpty()
            .Must(p => p.Count >= 2)
            .WithMessage("At least two players are required.");

        RuleForEach(x => x.Players)
            .Must(p => p.UserId != Guid.Empty)
            .WithMessage("Every player must have a user id.")
            .Must(p => !string.IsNullOrWhiteSpace(p.DisplayName))
            .WithMessage("Every player must have a display name.")
            .Must((command, player) => command.Players.Count(p => p.UserId == player.UserId) == 1)
            .WithMessage("Duplicate players are not allowed.");
    }
}