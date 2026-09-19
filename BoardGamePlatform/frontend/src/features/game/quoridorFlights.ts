import type { QuoridorState } from './quoridor'

/**
 * Quoridor's animation layer (§29): after each accepted broadcast the view diffs
 * the previous and current projected state and replays what physically moved as
 * flying pieces (pawn glide, wall set-down). Purely cosmetic - the engine
 * already applied everything; the view component handles the actual animation
 * via the anchor system (like Azul).
 */

export type QuoridorFlight =
  | { kind: 'pawn'; seat: number; fromKey: string; toKey: string; delay: number }
  | { kind: 'wall'; seat: number; row: number; col: number; orientation: 'H' | 'V'; delay: number }

export interface QuoridorEffectPlan {
  flights: QuoridorFlight[]
  /** Game ended with win/draw. */
  gameOver: boolean
  winnerSeat: number
  /** Seats that were just eliminated (AFK). */
  eliminatedSeats: number[]
}

export function diffQuoridor(prev: QuoridorState, cur: QuoridorState, curOver: boolean): QuoridorEffectPlan {
  const plan: QuoridorEffectPlan = {
    flights: [],
    gameOver: curOver,
    winnerSeat: curOver ? cur.WinnerSeat : -1,
    eliminatedSeats: [],
  }

  // Eliminated seats (AFK)
  if (prev.EliminatedSeats.length !== cur.EliminatedSeats.length) {
    for (const seat of cur.EliminatedSeats) {
      if (!prev.EliminatedSeats.includes(seat)) {
        plan.eliminatedSeats.push(seat)
      }
    }
  }

  // Pawn moves (find the single seat that moved)
  for (let s = 0; s < Math.min(prev.Pawns.length, cur.Pawns.length); s++) {
    const prevPos = prev.Pawns[s]
    const curPos = cur.Pawns[s]
    if (prevPos.Row !== curPos.Row || prevPos.Col !== curPos.Col) {
      plan.flights.push({
        kind: 'pawn',
        seat: s,
        fromKey: `seat:${s}:pawn`,
        toKey: `seat:${s}:pawn`,
        delay: 0,
      })
    }
  }

  // New walls
  if (prev.Walls.length !== cur.Walls.length) {
    for (const wall of cur.Walls) {
      if (!prev.Walls.some(w => w.Row === wall.Row && w.Col === wall.Col && w.Orientation === wall.Orientation)) {
        let placer = -1
        for (let s = 0; s < cur.SeatCount; s++) {
          if (cur.WallsRemaining[s] < prev.WallsRemaining[s]) {
            placer = s
            break
          }
        }
        plan.flights.push({
          kind: 'wall',
          seat: placer,
          row: wall.Row,
          col: wall.Col,
          orientation: wall.Orientation,
          delay: 0,
        })
      }
    }
  }

  return plan
}

// Placeholder - the view component handles actual animation via anchor system
export function runQuoridorPlan(_plan: QuoridorEffectPlan): void {
  // The view component handles actual animations via the anchor system
  // This is a no-op for now - actual animation is handled in the view component
}