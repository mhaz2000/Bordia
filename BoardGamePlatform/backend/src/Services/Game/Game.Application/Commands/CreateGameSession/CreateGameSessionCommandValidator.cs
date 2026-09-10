using BuildingBlocks.Domain.Localization;
using FluentValidation;

namespace Game.Application.Commands.CreateGameSession;

/// <summary>
/// Validates the <see cref="CreateGameSessionCommand"/>.
/// </summary>
public class CreateGameSessionCommandValidator : AbstractValidator<CreateGameSessionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateGameSessionCommandValidator"/> class.
    /// Validation failures carry catalog codes; the response middleware localizes them.
    /// </summary>
    public CreateGameSessionCommandValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.RoomIdRequired);

        RuleFor(x => x.GameType)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.GameTypeRequired);

        RuleFor(x => x.Players)
            .NotEmpty()
            .WithMessage(ErrorCodes.Validation.PlayersRequired)
            .Must(p => p.Count >= 2)
            .WithMessage(ErrorCodes.Validation.PlayersMinTwo);

        RuleForEach(x => x.Players)
            .Must(p => p.UserId != Guid.Empty)
            .WithMessage(ErrorCodes.Validation.PlayerUserIdRequired)
            .Must(p => !string.IsNullOrWhiteSpace(p.DisplayName))
            .WithMessage(ErrorCodes.Validation.PlayerDisplayNameRequired)
            .Must((command, player) => command.Players.Count(p => p.UserId == player.UserId) == 1)
            .WithMessage(ErrorCodes.Validation.PlayerDuplicate);
    }
}
