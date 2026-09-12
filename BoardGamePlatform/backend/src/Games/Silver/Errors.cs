namespace Silver;

/// <summary>
/// Central error codes for the Silver engine. Every user-facing failure carries
/// a stable code (+ positional args) so the Game Service can localize it at the
/// request boundary. Codes are part of the platform contract: a new code must
/// also be added to the server-side <c>ErrorCatalog</c>
/// (BuildingBlocks.Domain Localization/errors.en.json and errors.fa.json).
/// </summary>
internal static class Errors
{
    public const string GameOver = "silver.gameOver";
    public const string InvalidState = "silver.invalidState";
    public const string InvalidPlayer = "silver.invalidPlayer";
    public const string NotYourTurn = "silver.notYourTurn";
    public const string WrongPhase = "silver.wrongPhase";
    public const string UnknownActionType = "silver.unknownActionType";
    public const string UnknownAbility = "silver.unknownAbility";
    public const string PlayerEliminated = "silver.playerEliminated";
    public const string DeckEmpty = "silver.deckEmpty";
    public const string DiscardEmpty = "silver.discardEmpty";
    public const string DrawPending = "silver.drawPending";
    public const string NoPendingDraw = "silver.noPendingDraw";
    public const string MustExchange = "silver.mustExchange";
    public const string ExchangeNotAllowed = "silver.exchangeNotAllowed";
    public const string InvalidPayload = "silver.invalidPayload";
    public const string InvalidSlot = "silver.invalidSlot";
    public const string CardProtected = "silver.cardProtected";
    public const string InvalidTargetPlayer = "silver.invalidTargetPlayer";
    public const string AbilityNotAvailable = "silver.abilityNotAvailable";
    public const string AbilityAlreadyUsed = "silver.abilityAlreadyUsed";
    public const string PeekLimitReached = "silver.peekLimitReached";
    public const string VoteNotAllowed = "silver.voteNotAllowed";
    public const string AmuletNotAvailable = "silver.amuletNotAvailable";
    public const string TimerNotExpired = "silver.timerNotExpired";
    public const string TimeNotUp = "silver.timeNotUp";
    public const string GuardNotAttached = "silver.guardNotAttached";
}
