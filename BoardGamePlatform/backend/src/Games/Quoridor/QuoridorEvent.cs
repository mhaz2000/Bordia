using System.Text.Json;
using Quoridor.Models;

namespace Quoridor;

/// <summary>
/// Event envelope builder. Events are emitted as JSON envelopes
/// {"c":"code","d":{...}} in GameResult.Events and stored in QuoridorState.EventLog.
/// This matches the platform convention (spec §20).
/// </summary>
internal static class QuoridorEvent
{
    public static string Build(string code, object payload)
    {
        return JsonSerializer.Serialize(new { c = code, d = payload });
    }

    public static string GameStarted(object payload) => Build(QuoridorEventCode.GameStarted, payload);
    public static string Move(int seat, int fromRow, int fromCol, int toRow, int toCol)
        => Build(QuoridorEventCode.Move, new MoveEventPayload { Seat = seat, FromRow = fromRow, FromCol = fromCol, ToRow = toRow, ToCol = toCol });
    public static string Jump(int seat, int fromRow, int fromCol, int toRow, int toCol, string kind)
        => Build(QuoridorEventCode.Jump, new JumpEventPayload { Seat = seat, FromRow = fromRow, FromCol = fromCol, ToRow = toRow, ToCol = toCol, Kind = kind });
    public static string Wall(int seat, int row, int col, string orientation)
        => Build(QuoridorEventCode.Wall, new WallEventPayload { Seat = seat, Row = row, Col = col, Orientation = orientation });
    public static string Win(int seat)
        => Build(QuoridorEventCode.Win, new WinEventPayload { Seat = seat });
    public static string TeamWin(int seat, string team)
        => Build(QuoridorEventCode.TeamWin, new TeamWinEventPayload { Seat = seat, Team = team });
    public static string Draw()
        => Build(QuoridorEventCode.Draw, new { });
    public static string Skip(int seat, string reason)
        => Build(QuoridorEventCode.Skip, new SkipEventPayload { Seat = seat, Reason = reason });
    public static string Eliminated(int seat)
        => Build(QuoridorEventCode.Eliminated, new EliminatedEventPayload { Seat = seat, Reason = "afk" });
}