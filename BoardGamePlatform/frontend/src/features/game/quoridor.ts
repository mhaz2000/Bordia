import type { GameState } from '@/shared/api/game'

/**
 * Client mirror of the backend `QuoridorState` (PascalCase, exactly as
 * serialized). This is the ONLY Quoridor data a client ever receives.
 * The client mirrors rules for HIGHLIGHTING only; the engine remains
 * the sole authority on legality.
 */

export type PlayerIndex = number

export interface QuoridorWall {
  Row: number
  Col: number
  Orientation: 'H' | 'V'
}

export interface QuoridorTimerConfig {
  BaseTurnSeconds: number
  MaxBankSeconds: number
  MaxOverrunSeconds: number
  MaxAfkTurns: number
  TotalGameTimeMinutes: number
}

export interface QuoridorPlayerTimer {
  BankSeconds: number
  DeferredPenaltySeconds: number
  ConsecutiveTimeouts: number
}

export interface QuoridorState {
  SeatCount: number
  CurrentPlayerIndex: number
  Pawns: { Row: number; Col: number }[]
  Walls: QuoridorWall[]
  WallsRemaining: number[]
  EliminatedSeats: number[]
  EventLog: string[]
  WinnerSeat: number
  Draw: boolean
  TurnStartUtc?: string
  TimerConfig?: QuoridorTimerConfig
  PlayerTimers?: QuoridorPlayerTimer[]
  GameEndsAtUtc?: string
}

/** Extracts the projected QuoridorState from the platform GameState. */
export function parseQuoridorState(state: GameState | null | undefined): QuoridorState | null {
  const raw = state?.data?.QuoridorState
  if (raw == null) return null
  
  // Handle both string (JSON string) and object (already parsed) cases
  let parsed: QuoridorState
  if (typeof raw === 'string') {
    try {
      parsed = JSON.parse(raw) as QuoridorState
    } catch {
      return null
    }
  } else if (typeof raw === 'object') {
    parsed = raw as QuoridorState
  } else {
    return null
  }
  
  // Basic shape validation (defensive, per platform 2026-09-12 leak-guard precedent)
  if (
    typeof parsed?.SeatCount !== 'number' ||
    !Array.isArray(parsed?.Pawns) ||
    !Array.isArray(parsed?.Walls) ||
    !Array.isArray(parsed?.WallsRemaining)
  ) {
    return null
  }
  return parsed
}

// ================================ actions ==================================
// Payloads are PascalCase (System.Text.Json default).
// One turn = one atomic MovePawn (to Row/Col) OR PlaceWall (Row/Col/Orientation).

export const quoridorActions = {
  movePawn: (row: number, col: number) => ({
    Row: row,
    Col: col,
  }),
  placeWall: (row: number, col: number, orientation: 'H' | 'V') => ({
    Row: row,
    Col: col,
    Orientation: orientation,
  }),
}

// ============================ client rule mirror ===========================
// Display aids ONLY (highlighting, previews). The engine re-validates all of it.

export const BOARD_SIZE = 9
export const TOTAL_WALLS = 20

export const DIRECTIONS = [
  { dr: -1, dc: 0 }, // North
  { dr: 1, dc: 0 },  // South
  { dr: 0, dc: -1 }, // West
  { dr: 0, dc: 1 },  // East
] as const

export function wallsPerPlayer(seatCount: number): number {
  return seatCount === 2 ? 10 : 5
}

export function startPosition(seat: number, seatCount: number): { row: number; col: number } {
  if (seatCount === 2) {
    return seat === 0 ? { row: 8, col: 4 } : { row: 0, col: 4 }
  }
  switch (seat) {
    case 0: return { row: 8, col: 4 } // South
    case 1: return { row: 4, col: 0 } // West
    case 2: return { row: 0, col: 4 } // North
    case 3: return { row: 4, col: 8 } // East
    default: throw new Error(`Invalid seat ${seat} for ${seatCount} players`)
  }
}

export function goalSquares(seat: number, seatCount: number): { row: number; col: number }[] {
  const goals: { row: number; col: number }[] = []
  if (seatCount === 2) {
    if (seat === 0) {
      for (let c = 0; c < BOARD_SIZE; c++) goals.push({ row: 0, col: c })
    } else {
      for (let c = 0; c < BOARD_SIZE; c++) goals.push({ row: BOARD_SIZE - 1, col: c })
    }
  } else {
    switch (seat) {
      case 0: // South -> North edge
        for (let c = 0; c < BOARD_SIZE; c++) goals.push({ row: 0, col: c })
        break
      case 1: // West -> East edge
        for (let r = 0; r < BOARD_SIZE; r++) goals.push({ row: r, col: BOARD_SIZE - 1 })
        break
      case 2: // North -> South edge
        for (let c = 0; c < BOARD_SIZE; c++) goals.push({ row: BOARD_SIZE - 1, col: c })
        break
      case 3: // East -> West edge
        for (let r = 0; r < BOARD_SIZE; r++) goals.push({ row: r, col: 0 })
        break
    }
  }
  return goals
}

export function isGoalSquare(seat: number, seatCount: number, row: number, col: number): boolean {
  if (seatCount === 2) {
    return seat === 0 ? row === 0 : row === BOARD_SIZE - 1
  }
  switch (seat) {
    case 0: return row === 0
    case 1: return col === BOARD_SIZE - 1
    case 2: return row === BOARD_SIZE - 1
    case 3: return col === 0
    default: return false
  }
}

export function teamForSeat(seat: number, seatCount: number): number {
  return seatCount === 2 ? seat : seat % 2
}

/** Check if a cell is in bounds. */
export function inBounds(row: number, col: number): boolean {
  return row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE
}

/** Check if a wall slot is in bounds. Walls are 2 cells long and may sit flush with the rim. */
export function isWallSlotInBounds(wall: QuoridorWall): boolean {
  if (wall.Orientation === 'H') {
    return wall.Row >= 0 && wall.Row <= 7 && wall.Col >= 0 && wall.Col <= 7
  } else {
    return wall.Row >= 0 && wall.Row <= 7 && wall.Col >= 0 && wall.Col <= 7
  }
}

/** Check if a wall overlaps another wall. Same-orientation walls with shared unit
 * edges, and perpendicular walls that cross through each other's middle ("+"),
 * are forbidden; corner touches and end-on-side "T" contacts are legal so lines
 * never cross over each other.
 */
export function wallsOverlap(a: QuoridorWall, b: QuoridorWall): boolean {
  if (a.Orientation === b.Orientation) {
    if (a.Orientation === 'H') {
      return a.Row === b.Row && (a.Col === b.Col || a.Col === b.Col + 1 || a.Col + 1 === b.Col)
    } else {
      return a.Col === b.Col && (a.Row === b.Row || a.Row === b.Row + 1 || a.Row + 1 === b.Row)
    }
  }
  // Cross-orientation: forbid only the true "+" crossing — identical slot.
  return wallsCross(a, b)
}

/** True when H and V walls cross through each other's middle ("+"). An H wall at
 * slot (rw, c) and a V wall at slot (r, cw) each pass through the other exactly
 * when the shared grid vertex is an interior point of both spans, i.e. rw == r
 * and cw == c — the two slots coincide.
 */
function wallsCross(a: QuoridorWall, b: QuoridorWall): boolean {
  const h = a.Orientation === 'H' ? a : b
  const v = a.Orientation === 'H' ? b : a
  return h.Row === v.Row && h.Col === v.Col
}

/** Check if ANY wall in the list overlaps the candidate. */
export function wallOverlapsAny(candidate: QuoridorWall, walls: QuoridorWall[]): boolean {
  return walls.some(w => wallsOverlap(candidate, w))
}

/** Check if a wall blocks the edge between two orthogonal neighbor cells. */
export function isWallBetween(
  walls: QuoridorWall[],
  r1: number, c1: number,
  r2: number, c2: number
): boolean {
  if (r1 === r2) { // horizontal move (left/right) -> check vertical wall
    // The vertical groove between columns min(c1,c2) and min(c1,c2)+1 (spec §3).
    // A V wall V(r, cw) covers unit edges (r, cw) and (r+1, cw), so the edge at
    // row r1 is covered by a wall whose first unit edge is r1 OR r1-1 (spec §8).
    const cw = Math.min(c1, c2)
    const r = r1
    return walls.some(w =>
      w.Orientation === 'V' && w.Col === cw && (w.Row === r || w.Row === r - 1)
    )
  } else { // vertical move (up/down) -> check horizontal wall
    // The horizontal groove between rows min(r1,r2) and min(r1,r2)+1 (spec §3).
    // An H wall H(rw, c) covers unit edges (rw, c) and (rw, c+1), so the edge at
    // column c1 is covered by a wall whose first unit edge is c1 OR c1-1 (spec §8).
    const rw = Math.min(r1, r2)
    const c = c1
    return walls.some(w =>
      w.Orientation === 'H' && w.Row === rw && (w.Col === c || w.Col === c - 1)
    )
  }
}

/**
 * Path-preservation BFS (spec §9): from a pawn's current cell, can it reach
 * ANY of its goal squares? Pawns are passable; only walls block.
 */
export function hasPathToGoal(
  quoridor: QuoridorState,
  seat: number
): boolean {
  const start = quoridor.Pawns[seat]
  const goals = goalSquares(seat, quoridor.SeatCount)

  // If already on goal, trivially reachable
  if (goals.some(g => g.row === start.Row && g.col === start.Col)) return true

  const visited = Array.from({ length: BOARD_SIZE }, () =>
    Array(BOARD_SIZE).fill(false)
  )
  const queue: { r: number; c: number }[] = [{ r: start.Row, c: start.Col }]
  visited[start.Row][start.Col] = true

  while (queue.length > 0) {
    const { r, c } = queue.shift()!
    if (goals.some(g => g.row === r && g.col === c)) return true

    for (const { dr, dc } of DIRECTIONS) {
      const nr = r + dr, nc = c + dc
      if (!inBounds(nr, nc)) continue
      if (visited[nr][nc]) continue
      if (isWallBetween(quoridor.Walls, r, c, nr, nc)) continue
      visited[nr][nc] = true
      queue.push({ r: nr, c: nc })
    }
  }
  return false
}

/**
 * Simulate placing a wall and verify path preservation for ALL seats.
 * Returns true if legal.
 */
export function isWallLegal(
  quoridor: QuoridorState,
  wall: QuoridorWall
): boolean {
  if (!isWallSlotInBounds(wall)) return false
  if (wallOverlapsAny(wall, quoridor.Walls)) return false

  quoridor.Walls.push(wall)
  const ok = quoridor.Pawns.every((_, s) => hasPathToGoal(quoridor, s))
  quoridor.Walls.pop()
  return ok
}

/** Check if a move from (fromRow,fromCol) to (toRow,toCol) is legal.
 * Returns (legal, isJump, jumpKind). jumpKind is "straight" or "aside" or null.
 * This mirrors the backend CheckMove logic (spec §6-7).
 */
export function checkMove(
  quoridor: QuoridorState,
  _seat: number,
  fromRow: number, fromCol: number,
  toRow: number, toCol: number
): { legal: boolean; isJump: boolean; jumpKind: 'straight' | 'aside' | null } {
  // Bounds
  if (!inBounds(toRow, toCol)) return { legal: false, isJump: false, jumpKind: null }

  // Occupied?
  if (quoridor.Pawns.some(p => p.Row === toRow && p.Col === toCol))
    return { legal: false, isJump: false, jumpKind: null }

  const dr = toRow - fromRow
  const dc = toCol - fromCol
  const absDr = Math.abs(dr), absDc = Math.abs(dc)

  // Single orthogonal step
  if ((absDr === 1 && absDc === 0) || (absDr === 0 && absDc === 1)) {
    if (isWallBetween(quoridor.Walls, fromRow, fromCol, toRow, toCol))
      return { legal: false, isJump: false, jumpKind: null }
    return { legal: true, isJump: false, jumpKind: null }
  }

  // Straight jump (2 cells orthogonally)
  if ((absDr === 2 && absDc === 0) || (absDr === 0 && absDc === 2)) {
    const midRow = fromRow + dr / 2
    const midCol = fromCol + dc / 2
    const pawnBetween = quoridor.Pawns.some(p => p.Row === midRow && p.Col === midCol)
    if (!pawnBetween) return { legal: false, isJump: false, jumpKind: null }
    if (isWallBetween(quoridor.Walls, fromRow, fromCol, midRow, midCol))
      return { legal: false, isJump: false, jumpKind: null }
    if (isWallBetween(quoridor.Walls, midRow, midCol, toRow, toCol))
      return { legal: false, isJump: false, jumpKind: null }
    return { legal: true, isJump: true, jumpKind: 'straight' }
  }

  // Aside jump (diagonal 1,1) when straight jump blocked (OD-5)
  if (absDr === 1 && absDc === 1) {
    const straightRow = fromRow + 2 * Math.sign(dr)
    const straightCol = fromCol + 2 * Math.sign(dc)

    // Adjacent pawn must exist
    const midRow = fromRow + Math.sign(dr)
    const midCol = fromCol + Math.sign(dc)
    const pawnBeside = quoridor.Pawns.some(p => p.Row === midRow && p.Col === midCol)
    if (!pawnBeside) return { legal: false, isJump: false, jumpKind: null }

    // Straight must be impossible
    let straightPossible = true
    if (!inBounds(straightRow, straightCol)) straightPossible = false
    else if (quoridor.Pawns.some(p => p.Row === straightRow && p.Col === straightCol)) straightPossible = false
    else if (isWallBetween(quoridor.Walls, midRow, midCol, straightRow, straightCol)) straightPossible = false

    if (straightPossible) return { legal: false, isJump: false, jumpKind: null }

    // Check both L-shaped paths around the corner for walls
    const corner1 = { r: toRow, c: fromCol }
    const corner2 = { r: fromRow, c: toCol }
    if (isWallBetween(quoridor.Walls, fromRow, fromCol, corner1.r, corner1.c)) return { legal: false, isJump: false, jumpKind: null }
    if (isWallBetween(quoridor.Walls, corner1.r, corner1.c, toRow, toCol)) return { legal: false, isJump: false, jumpKind: null }
    if (isWallBetween(quoridor.Walls, fromRow, fromCol, corner2.r, corner2.c)) return { legal: false, isJump: false, jumpKind: null }
    if (isWallBetween(quoridor.Walls, corner2.r, corner2.c, toRow, toCol)) return { legal: false, isJump: false, jumpKind: null }

    return { legal: true, isJump: true, jumpKind: 'aside' }
  }

  return { legal: false, isJump: false, jumpKind: null }
}

/** Enumerate all legal MovePawn targets for the current player. */
export function getLegalMoves(quoridor: QuoridorState, seat: number): { row: number; col: number; isJump: boolean; jumpKind: 'straight' | 'aside' | null }[] {
  const moves: { row: number; col: number; isJump: boolean; jumpKind: 'straight' | 'aside' | null }[] = []
  const { Row: r, Col: c } = quoridor.Pawns[seat]

  for (const { dr, dc } of DIRECTIONS) {
    // Single step
    const nr = r + dr, nc = c + dc
    const check = checkMove(quoridor, seat, r, c, nr, nc)
    if (check.legal) moves.push({ row: nr, col: nc, isJump: false, jumpKind: null })

    // Straight jump
    const jr = r + 2 * dr, jc = c + 2 * dc
    const jcheck = checkMove(quoridor, seat, r, c, jr, jc)
    if (jcheck.legal && jcheck.isJump && jcheck.jumpKind === 'straight')
      moves.push({ row: jr, col: jc, isJump: true, jumpKind: 'straight' })

    // Aside jumps (diagonal) when straight is blocked
    const adjR = r + dr, adjC = c + dc
    if (quoridor.Pawns.some(p => p.Row === adjR && p.Col === adjC)) {
      const diagR1 = adjR + dc, diagC1 = adjC - dr
      const diagR2 = adjR - dc, diagC2 = adjC + dr
      for (const [dR, dC] of [[diagR1, diagC1], [diagR2, diagC2]]) {
        const acheck = checkMove(quoridor, seat, r, c, dR, dC)
        if (acheck.legal && acheck.isJump && acheck.jumpKind === 'aside')
          moves.push({ row: dR, col: dC, isJump: true, jumpKind: 'aside' })
      }
    }
  }
  return moves
}

/** Enumerate all legal PlaceWall slots for the current player. */
export function getLegalWalls(quoridor: QuoridorState, seat: number): QuoridorWall[] {
  const walls: QuoridorWall[] = []
  if (quoridor.WallsRemaining[seat] <= 0) return walls

  // Horizontal: Row 0..7, Col 0..7
  for (let rw = 0; rw <= 7; rw++) {
    for (let col = 0; col <= 7; col++) {
      const wall = { Row: rw, Col: col, Orientation: 'H' as const }
      if (isWallLegal(quoridor, wall)) walls.push(wall)
    }
  }
  // Vertical: Row 0..7, Col 0..7
  for (let row = 0; row <= 7; row++) {
    for (let cw = 0; cw <= 7; cw++) {
      const wall = { Row: row, Col: cw, Orientation: 'V' as const }
      if (isWallLegal(quoridor, wall)) walls.push(wall)
    }
  }
  return walls
}

// ================================= events ==================================

/**
 * Cell notation: rows are letters a-i, columns are numbers 1-9, so cell
 * (row 0, col 0) is "a1". Used for coordinates in the event-log text.
 */
export function quoridorCell(row: number, col: number): string {
  return `${String.fromCharCode(97 + row)}${col + 1}`
}

/**
 * Renders a Quoridor event-log envelope ({"c":"quoridor.x","d":{...}})
 * in the active language. Legacy plain-text entries render unchanged.
 */
export function formatQuoridorEvent(
  entry: string,
  t: (key: string, params?: Record<string, string | number>) => string,
): string {
  if (!entry.startsWith('{')) return entry
  let parsed: { c?: string; d?: Record<string, unknown> }
  try {
    parsed = JSON.parse(entry)
  } catch {
    return entry
  }
  const code = parsed.c
  if (!code) return entry
  const d = parsed.d ?? {}

  const player = typeof d.player === 'string' ? d.player : ''
  const seat = typeof d.seat === 'number' ? d.seat : -1
  const fromRow = typeof d.fromRow === 'number' ? d.fromRow : -1
  const fromCol = typeof d.fromCol === 'number' ? d.fromCol : -1
  const toRow = typeof d.toRow === 'number' ? d.toRow : -1
  const toCol = typeof d.toCol === 'number' ? d.toCol : -1
  const kind = typeof d.kind === 'string' ? d.kind : ''
  const wallRow = typeof d.row === 'number' ? d.row : -1
  const wallCol = typeof d.col === 'number' ? d.col : -1
  const orientation = typeof d.orientation === 'string' ? d.orientation : ''
  const team = typeof d.team === 'string' ? d.team : ''
  const reason = typeof d.reason === 'string' ? d.reason : ''

  switch (code) {
    case 'move':
      return t('events.quoridor.move', { player, from: quoridorCell(fromRow, fromCol), to: quoridorCell(toRow, toCol) })
    case 'jump':
      return t('events.quoridor.jump', { player, from: quoridorCell(fromRow, fromCol), to: quoridorCell(toRow, toCol), kind })
    case 'wall':
      return t('events.quoridor.wall', { player, cell: quoridorCell(wallRow, wallCol), orientation })
    case 'win':
      return t('events.quoridor.win', { player })
    case 'teamWin':
      return t('events.quoridor.teamWin', { player, team })
    case 'draw':
      return t('events.quoridor.draw')
    case 'skip':
      return t('events.quoridor.skip', { seat, reason })
    case 'eliminated':
      return t('events.quoridor.eliminated', { seat })
    default:
      return code
  }
}

export const DEFAULT_TIMER: QuoridorTimerConfig = {
  BaseTurnSeconds: 90,
  MaxBankSeconds: 180,
  MaxOverrunSeconds: 15,
  MaxAfkTurns: 3,
  TotalGameTimeMinutes: 60,
}