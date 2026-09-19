namespace Quoridor;

/// <summary>
/// Central error codes for the Quoridor engine. Every user-facing failure carries a
/// stable code (+ positional args) so the Game Service can localize it at the
/// request boundary. Codes are part of the platform contract: a new code must
/// also be added to the server-side ErrorCatalog
/// (BuildingBlocks.Domain Localization/errors.en.json and errors.fa.json).
/// </summary>
internal static class Errors
{
    public const string GameOver = "quoridor.gameOver";
    public const string InvalidState = "quoridor.invalidState";
    public const string InvalidPlayer = "quoridor.invalidPlayer";
    public const string PlayerEliminated = "quoridor.playerEliminated";
    public const string NotYourTurn = "quoridor.notYourTurn";
    public const string UnknownActionType = "quoridor.unknownActionType";
    public const string InvalidPayload = "quoridor.invalidPayload";
    public const string OutOfBounds = "quoridor.outOfBounds";
    public const string CellOccupied = "quoridor.cellOccupied";
    public const string MoveBlockedByWall = "quoridor.moveBlockedByWall";
    public const string InvalidJump = "quoridor.invalidJump";
    public const string WallOverlap = "quoridor.wallOverlap";
    public const string WallOutOfBounds = "quoridor.wallOutOfBounds";
    public const string WallBlocksPath = "quoridor.wallBlocksPath";
    public const string NoWallsLeft = "quoridor.noWallsLeft";
    public const string TimerNotExpired = "quoridor.timerNotExpired";
    public const string TimeNotUp = "quoridor.timeNotUp";
    public const string PlayerCount = "quoridor.playerCount";
}