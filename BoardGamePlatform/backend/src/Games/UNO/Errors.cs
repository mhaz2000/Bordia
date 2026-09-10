using GameEngine.Core.Models;
using UNO.Models;

namespace UNO;

/// <summary>
/// Central error codes and helpers for the UNO engine.
/// Every user-facing failure carries a stable code + positional args so the
/// Game service can localize it at the request boundary (Accept-Language).
/// Codes are part of the platform contract: a new code must also be added to
/// the server-side <c>ErrorCatalog</c> (BuildingBlocks.Domain.Localization).
/// </summary>
internal static class Errors
{
    public const string CardRequired = "uno.cardRequired";
    public const string CardNotInHand = "uno.cardNotInHand";
    public const string CannotPlayOnTop = "uno.cannotPlayOnTop";
    public const string ChooseWildColor = "uno.chooseWildColor";
    public const string InvalidState = "uno.invalidState";
    public const string InvalidPlayer = "uno.invalidPlayer";
    public const string NotYourTurn = "uno.notYourTurn";
    public const string UnknownActionType = "uno.unknownActionType";
    public const string GameOver = "uno.gameOver";
    public const string MustDraw = "uno.mustDraw";
    public const string AcceptOrChallenge = "uno.acceptOrChallenge";
    public const string UseAcceptDraw = "uno.useAcceptDraw";
    public const string OneDrawPerTurn = "uno.oneDrawPerTurn";
    public const string PassAfterDraw = "uno.passAfterDraw";
    public const string NoCardsLeft = "uno.noCardsLeft";
    public const string NoPendingDraw = "uno.noPendingDraw";
    public const string NotYourPenalty = "uno.notYourPenalty";
    public const string NoWdfToChallenge = "uno.noWdfToChallenge";
    public const string NotChallengeable = "uno.notChallengeable";
    public const string ChallengeResolved = "uno.challengeResolved";
    public const string NotYourChallenge = "uno.notYourChallenge";
    public const string LastNotWdf = "uno.lastNotWdf";
    public const string NoChallengeCard = "uno.noChallengeCard";
    public const string TimerNotExpired = "uno.timerNotExpired";
    public const string TimeNotUp = "uno.timeNotUp";
    public const string NotOneCard = "uno.notOneCard";
    public const string UnoAlreadyCalled = "uno.unoAlreadyCalled";
    public const string NotYourUnoTurn = "uno.notYourUnoTurn";
}
