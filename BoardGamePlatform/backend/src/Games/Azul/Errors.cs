namespace Azul;

/// <summary>
/// Central error codes for the Azul engine. Every user-facing failure carries a
/// stable code (+ positional args) so the Game Service can localize it at the
/// request boundary. Codes are part of the platform contract: a new code must
/// also be added to the server-side <c>ErrorCatalog</c>
/// (BuildingBlocks.Domain Localization/errors.en.json and errors.fa.json).
/// </summary>
internal static class Errors
{
    public const string GameOver = "azul.gameOver";
    public const string InvalidState = "azul.invalidState";
    public const string InvalidPlayer = "azul.invalidPlayer";
    public const string PlayerEliminated = "azul.playerEliminated";
    public const string NotYourTurn = "azul.notYourTurn";
    public const string UnknownActionType = "azul.unknownActionType";
    public const string InvalidPayload = "azul.invalidPayload";
    public const string InvalidFactory = "azul.invalidFactory";
    public const string NoTilesOfColor = "azul.noTilesOfColor";
    public const string CenterEmpty = "azul.centerEmpty";
    public const string LineFull = "azul.lineFull";
    public const string LineColorMismatch = "azul.lineColorMismatch";
    public const string WallRowHasColor = "azul.wallRowHasColor";
    public const string FloorNotOptional = "azul.floorNotOptional";
    public const string FirstPlayerNotAvailable = "azul.firstPlayerNotAvailable";
    public const string TimerNotExpired = "azul.timerNotExpired";
    public const string TimeNotUp = "azul.timeNotUp";
}
