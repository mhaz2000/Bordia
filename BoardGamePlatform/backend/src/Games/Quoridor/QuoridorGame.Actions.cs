using Quoridor.Models;

namespace Quoridor;

/// <summary>
/// Player action processors (MovePawn, PlaceWall) and system actions
/// (TurnTimeout, GameTimeExpired). Every action validates its complete
/// decision set against the authoritative state before mutating anything.
/// </summary>
public sealed partial class QuoridorGame
{
    // ----- MovePawn -----

    private static QuoridorActionResult ProcessMovePawn(
        QuoridorState quoridor, int seat, MovePawnPayload? payload)
    {
        if (payload is null)
            return FailResult(Errors.InvalidPayload);

        var pawn = quoridor.Pawns[seat];
        var fromRow = pawn.Row;
        var fromCol = pawn.Col;
        int toRow = payload.Row, toCol = payload.Col;

        // Bounds
        if (toRow < 0 || toRow >= QuoridorCatalog.BoardSize ||
            toCol < 0 || toCol >= QuoridorCatalog.BoardSize)
        {
            return FailResult(Errors.OutOfBounds);
        }

        // Occupied?
        if (quoridor.Pawns.Any(p => p.Row == toRow && p.Col == toCol))
        {
            return FailResult(Errors.CellOccupied);
        }

        // Check legality
        var check = quoridor.CheckMove(seat, fromRow, fromCol, toRow, toCol);
        if (!check.legal)
        {
            if (check.isJump) return FailResult(Errors.InvalidJump);
            return FailResult(Errors.MoveBlockedByWall);
        }

        // Apply move
        quoridor.Pawns[seat] = new QuoridorPawn { Row = toRow, Col = toCol };
        var result = Ok();
        result.IsTurnAction = true;

        // Log event
        if (check.isJump)
        {
            result.Events.Add(QuoridorEvent.Jump(seat, fromRow, fromCol, toRow, toCol, check.jumpKind!));
        }
        else
        {
            result.Events.Add(QuoridorEvent.Move(seat, fromRow, fromCol, toRow, toCol));
        }

        // Check win
        if (QuoridorCatalog.IsGoalSquare(seat, quoridor.SeatCount, toRow, toCol))
        {
            result.GameEnded = true;
            result.WinnerSeat = seat;

            if (quoridor.SeatCount == 4)
            {
                // 4p: team win (OD-3)
                string team = QuoridorCatalog.TeamForSeat(seat, 4) == 0 ? "A" : "B";
                result.Events.Add(QuoridorEvent.TeamWin(seat, team));
            }
            else
            {
                result.Events.Add(QuoridorEvent.Win(seat));
            }
        }

        return result;
    }

    // ----- PlaceWall -----

    private static QuoridorActionResult ProcessPlaceWall(
        QuoridorState quoridor, int seat, PlaceWallPayload? payload)
    {
        if (payload is null)
            return FailResult(Errors.InvalidPayload);

        var wall = new QuoridorWall
        {
            Row = payload.Row,
            Col = payload.Col,
            Orientation = payload.Orientation
        };

        // Bounds
        if (!QuoridorState.IsWallSlotInBounds(wall))
            return FailResult(Errors.WallOutOfBounds);

        // Overlap
        if (quoridor.WallOverlaps(wall))
            return FailResult(Errors.WallOverlap);

        // Supply
        if (quoridor.WallsRemaining[seat] <= 0)
            return FailResult(Errors.NoWallsLeft);

        // Path preservation
        quoridor.Walls.Add(wall);
        bool allHavePaths = true;
        for (int s = 0; s < quoridor.SeatCount; s++)
        {
            if (!quoridor.HasPathToGoal(s))
            {
                allHavePaths = false;
                break;
            }
        }

        if (!allHavePaths)
        {
            quoridor.Walls.RemoveAt(quoridor.Walls.Count - 1);
            return FailResult(Errors.WallBlocksPath);
        }

        // Apply
        quoridor.WallsRemaining[seat]--;
        var result = Ok();
        result.IsTurnAction = true;
        result.Events.Add(QuoridorEvent.Wall(seat, wall.Row, wall.Col, wall.Orientation));

        return result;
    }

    // ----- System actions -----

    /// <summary>
    /// TurnTimeout: skip the turn (OD-8). Never fabricate a move.
    /// 2p: opponent instantly wins. 4p: seat eliminated, game continues.
    /// </summary>
    private static QuoridorActionResult ProcessTurnTimeout(QuoridorState quoridor, int seat)
    {
        // Platform convention: system actions don't check turn ownership here,
        // but we verify the deadline actually passed
        if (quoridor.NextActionDeadlineUtc is not { } deadline || DateTime.UtcNow < deadline)
        {
            return FailResult(Errors.TimerNotExpired);
        }

        var timer = quoridor.EnsureTimer(seat);
        var config = quoridor.TimerConfig ?? new QuoridorTimerConfig();

        // Charge overrun penalty (already accounted in timer via caller, but reset consecutive)
        timer.ConsecutiveTimeouts++;

        var result = Ok();
        result.Events.Add(QuoridorEvent.Skip(seat, "timeout"));

        // AFK elimination check
        if (timer.ConsecutiveTimeouts >= config.MaxAfkTurns)
        {
            quoridor.EliminatedSeats.Add(seat);
            result.Events.Add(QuoridorEvent.Eliminated(seat));

            if (quoridor.SeatCount == 2)
            {
                // 2p: other player instantly wins (platform 2p survivor rule)
                int survivor = quoridor.SeatCount == 2 ? 1 - seat : -1;
                if (survivor >= 0)
                {
                    result.GameEnded = true;
                    result.WinnerSeat = survivor;
                    result.Events.Add(QuoridorEvent.Win(survivor));
                }
            }
            else
            {
                // 4p: eliminated seat's pawn is removed from board (OD-9)
                // We mark it eliminated; AdvancePlayer will skip it.
                // The pawn position stays but is effectively inactive since seat is eliminated.
                // Note: path preservation for existing walls is not re-checked (I-6).
            }
        }

        return result;
    }

    /// <summary>
    /// GameTimeExpired: force-finish as a draw (OD-4).
    /// </summary>
    private static QuoridorActionResult ProcessGameTimeExpired(QuoridorState quoridor)
    {
        if (quoridor.GameEndsAtUtc is not { } deadline || DateTime.UtcNow < deadline)
        {
            return FailResult(Errors.TimeNotUp);
        }

        var result = Ok();
        result.GameEnded = true;
        result.SharedVictory = true; // draw
        result.WinnerSeat = -1;
        result.Events.Add(QuoridorEvent.Draw());
        result.Events.Add(QuoridorEvent.Skip(-1, "gameTimeExpired"));

        return result;
    }
}