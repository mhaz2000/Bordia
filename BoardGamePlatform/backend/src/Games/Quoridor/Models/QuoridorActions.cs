using System.Text.Json.Serialization;

namespace Quoridor.Models;

/// <summary>
/// Action type identifiers matching the spec §16. Payloads are PascalCase
/// (System.Text.Json default) so they deserialize cleanly from client JSON.
/// </summary>
public enum QuoridorActionType
{
    MovePawn,
    PlaceWall,
    TurnTimeout,
    GameTimeExpired
}

/// <summary>
/// MovePawn payload: destination cell coordinates.
/// The engine derives whether this is a single step, straight jump, or aside jump
/// by comparing the destination against the current pawn position.
/// </summary>
public sealed class MovePawnPayload
{
    /// <summary>Target row (0-8).</summary>
    public int Row { get; set; }

    /// <summary>Target column (0-8).</summary>
    public int Col { get; set; }
}

/// <summary>
/// PlaceWall payload: canonical slot form per spec §3.
/// H(Row,Col) covers horizontal edges (Row,Col) and (Row,Col+1), Col <= 7.
/// V(Row,Col) covers vertical edges (Row,Col) and (Row+1,Col), Row <= 7.
/// </summary>
public sealed class PlaceWallPayload
{
    /// <summary>First/top/left unit edge coordinate (row-edge for H, row for V).</summary>
    public int Row { get; set; }

    /// <summary>First/left unit edge coordinate (col for H, col-edge for V).</summary>
    public int Col { get; set; }

    /// <summary>Orientation: "H" (horizontal) or "V" (vertical).</summary>
    public string Orientation { get; set; } = string.Empty;
}

/// <summary>
/// Event envelope codes for the game log / UI animations (spec §20).
/// These are emitted as JSON envelopes {"c":"code","d":{...}} in GameResult.Events.
/// </summary>
public static class QuoridorEventCode
{
    public const string GameStarted = "gameStarted";
    public const string Move = "move";
    public const string Jump = "jump";
    public const string Wall = "wall";
    public const string Win = "win";
    public const string TeamWin = "teamWin";
    public const string Draw = "draw";
    public const string Skip = "skip";
    public const string Eliminated = "eliminated";
}

/// <summary>
/// Event payload structures (for serialization into the event log).
/// </summary>
public sealed class MoveEventPayload
{
    public int Seat { get; set; }
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
}

public sealed class JumpEventPayload
{
    public int Seat { get; set; }
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
    public string Kind { get; set; } = string.Empty; // "straight" or "aside"
}

public sealed class WallEventPayload
{
    public int Seat { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string Orientation { get; set; } = string.Empty;
}

public sealed class WinEventPayload
{
    public int Seat { get; set; }
}

public sealed class TeamWinEventPayload
{
    public int Seat { get; set; }
    public string Team { get; set; } = string.Empty; // "A" or "B"
}

public sealed class SkipEventPayload
{
    public int Seat { get; set; }
    public string Reason { get; set; } = string.Empty; // "timeout" | "gameTimeExpired"
}

public sealed class EliminatedEventPayload
{
    public int Seat { get; set; }
    public string Reason { get; set; } = "afk";
}