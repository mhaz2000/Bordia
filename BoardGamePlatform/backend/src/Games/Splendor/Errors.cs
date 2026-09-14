namespace Splendor;

/// <summary>
/// Central error codes for the Splendor engine. Every user-facing failure carries
/// a stable code (+ positional args) so the Game Service can localize it at the
/// request boundary. Codes are part of the platform contract: a new code must
/// also be added to the server-side <c>ErrorCatalog</c>
/// (BuildingBlocks.Domain Localization/errors.en.json and errors.fa.json).
/// </summary>
internal static class Errors
{
    public const string GameOver = "splendor.gameOver";
    public const string InvalidState = "splendor.invalidState";
    public const string InvalidPlayer = "splendor.invalidPlayer";
    public const string NotYourTurn = "splendor.notYourTurn";
    public const string UnknownActionType = "splendor.unknownActionType";
    public const string InvalidPayload = "splendor.invalidPayload";
    public const string InvalidGemColor = "splendor.invalidGemColor";
    public const string DuplicateColor = "splendor.duplicateColor";
    public const string InsufficientSupply = "splendor.insufficientSupply";
    public const string CannotTakeTwo = "splendor.cannotTakeTwo";
    public const string ExactGemsRequired = "splendor.exactGemsRequired";
    public const string TokenLimitExceeded = "splendor.tokenLimitExceeded";
    public const string InvalidReturn = "splendor.invalidReturn";
    public const string ReservationLimit = "splendor.reservationLimit";
    public const string DeckEmpty = "splendor.deckEmpty";
    public const string CardUnavailable = "splendor.cardUnavailable";
    public const string ReservationNotFound = "splendor.reservationNotFound";
    public const string InsufficientFunds = "splendor.insufficientFunds";
    public const string InvalidPayment = "splendor.invalidPayment";
    public const string NobleUnavailable = "splendor.nobleUnavailable";
    public const string NobleChoiceRequired = "splendor.nobleChoiceRequired";
    public const string NobleChoiceInvalid = "splendor.nobleChoiceInvalid";
    public const string TimerNotExpired = "splendor.timerNotExpired";
    public const string TimeNotUp = "splendor.timeNotUp";
    public const string PlayerEliminated = "splendor.playerEliminated";
}
