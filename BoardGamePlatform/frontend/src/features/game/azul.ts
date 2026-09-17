import type { GameState } from '@/shared/api/game'

/**
 * Client mirror of the backend `AzulView` projection (PascalCase, exactly as
 * serialized). This is the ONLY Azul data a client ever receives: the full
 * public board minus the bag's draw ORDER (only `BagCount` survives) — the one
 * secret Azul has (spec §16). The client mirrors rules for HIGHLIGHTING only;
 * the engine remains the sole authority on legality.
 */

export type AzulColor = 0 | 1 | 2 | 3 | 4

export const AZUL_COLORS = ['Blue', 'Red', 'Yellow', 'Black', 'White'] as const
export const COLOR_NAMES: Record<AzulColor, string> = {
  0: 'Blue',
  1: 'Red',
  2: 'Yellow',
  3: 'Black',
  4: 'White',
}

/** Forced-floor destination sentinel (spec §8.3): legal only when no line can take the color. */
export const AZUL_FLOOR = -1

export interface AzulSeatView {
  /** 5 rows x 5 columns wall; -1 empty, else the tile color. Row index == pattern line index. */
  Wall: number[][]
  /** Five pattern lines (line l capacity l+1), all tiles one color. */
  PatternLines: number[][]
  /** Floor tiles (max 7). */
  Floor: number[]
  Score: number
}

export interface AzulTimerConfig {
  BaseTurnSeconds: number
  MaxBankSeconds: number
  MaxOverrunSeconds: number
  MaxAfkTurns: number
  TotalGameTimeMinutes: number
}

export interface AzulPlayerTimer {
  BankSeconds: number
  DeferredPenaltySeconds: number
  ConsecutiveTimeouts: number
}

export interface AzulState {
  RoundNumber: number
  CurrentPlayerIndex: number
  FactoryCount: number
  BagCount: number
  Factories: number[][]
  Center: number[]
  DiscardCount: number
  /** -1 = marker in the center; else the seat holding it this round. */
  MarkerSeat: number
  FirstPlayerSeat: number
  Seats: AzulSeatView[]
  EliminatedSeats: number[]
  EventLog: string[]
  ViewerSeat: number
  ViewerIsCurrentPlayer: boolean
  TimerConfig?: AzulTimerConfig
  PlayerTimers?: AzulPlayerTimer[]
  TurnStartUtc?: string
}

/** Extracts the projected AzulState from the platform GameState. */
export function parseAzulState(state: GameState | null | undefined): AzulState | null {
  const raw = state?.data?.AzulState
  if (typeof raw !== 'string') return null
  try {
    const parsed = JSON.parse(raw) as AzulState
    // Only ever render the server-projected view: it carries BagCount and never
    // the authoritative `Bag` array (whose order is secret). A leaked raw
    // payload must render the waiting screen, never the bag (platform 2026-09-12).
    if (
      !Array.isArray(parsed?.Factories) ||
      !Array.isArray(parsed?.Seats) ||
      typeof parsed?.BagCount !== 'number' ||
      Array.isArray((parsed as unknown as { Bag?: unknown }).Bag)
    ) {
      return null
    }
    return parsed
  } catch {
    return null
  }
}

// ================================ actions ==================================
// Payloads are PascalCase (System.Text.Json default), colors are the canonical
// names the engine parses. One turn = one atomic draft (source + color +
// destination line), mirroring spec §19.

export const azulActions = {
  draftFactory: (factoryIndex: number, color: AzulColor, lineIndex: number) => ({
    FactoryIndex: factoryIndex,
    Color: COLOR_NAMES[color],
    LineIndex: lineIndex,
  }),
  draftCenter: (color: AzulColor, lineIndex: number) => ({
    Color: COLOR_NAMES[color],
    LineIndex: lineIndex,
  }),
  takeFirstPlayer: () => ({}),
}

// ============================ client rule mirror ===========================
// Display aids ONLY (highlighting, previews). The engine re-validates all of it.

/** Pattern line capacity (line l holds l+1 tiles). */
export const lineCapacity = (line: number) => line + 1

/** Line color of a non-empty pattern line, or -1. */
export function lineColor(line: number[]): number {
  return line.length > 0 ? line[0] : -1
}

/** A line can receive `color`: not full, empty-or-same color, wall row lacks that color (spec §9/§8.3). */
export function canPlaceLine(seat: AzulSeatView, line: number, color: AzulColor): boolean {
  const tiles = seat.PatternLines[line] ?? []
  if (tiles.length >= lineCapacity(line)) return false
  if (tiles.length > 0 && lineColor(tiles) !== color) return false
  return !(seat.Wall[line] ?? []).includes(color)
}

export function legalLines(seat: AzulSeatView, color: AzulColor): number[] {
  const lines: number[] = []
  for (let l = 0; l < 5; l++) {
    if (canPlaceLine(seat, l, color)) lines.push(l)
  }
  return lines
}

/** True when NO line can legally take the color: the draft is floor-only (sentinel). */
export function mustFloor(seat: AzulSeatView, color: AzulColor): boolean {
  return legalLines(seat, color).length === 0
}

/** Group a source's tiles by color: which colors are draftable and how many. */
export function colorCounts(tiles: number[]): Map<AzulColor, number> {
  const map = new Map<AzulColor, number>()
  for (const t of tiles) {
    map.set(t as AzulColor, (map.get(t as AzulColor) ?? 0) + 1)
  }
  return map
}

/** Preview of a draft: tiles landing in the line vs spilling to floor/discard (spec §8.3). */
export function placementPreview(seat: AzulSeatView, line: number, count: number) {
  if (line === AZUL_FLOOR) {
    const freeFloor = 7 - seat.Floor.length
    return { placed: 0, toFloor: Math.min(count, freeFloor), discarded: Math.max(0, count - freeFloor) }
  }
  const placed = Math.min(count, lineCapacity(line) - (seat.PatternLines[line]?.length ?? 0))
  const spill = count - placed
  const freeFloor = 7 - seat.Floor.length
  return { placed, toFloor: Math.min(spill, freeFloor), discarded: Math.max(0, spill - freeFloor) }
}

export const DEFAULT_TIMER: AzulTimerConfig = {
  BaseTurnSeconds: 60,
  MaxBankSeconds: 180,
  MaxOverrunSeconds: 15,
  MaxAfkTurns: 3,
  TotalGameTimeMinutes: 60,
}

// ================================= events ==================================

/**
 * Renders a Splendor/Silver-style event-log envelope ({"c":"azul.x","d":{...}})
 * in the active language. Legacy plain-text entries render unchanged.
 */
export function formatAzulEvent(
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
  const color =
    typeof d.color === 'string' ? t(`azul.colors.${d.color.toLowerCase()}`) : ''
  const count = typeof d.count === 'number' ? d.count : 0
  const name = (k: string) => (typeof d[k] === 'string' || typeof d[k] === 'number' ? (d[k] as string | number) : '')

  switch (code) {
    case 'azul.gameStarted':
      return t('events.azul.gameStarted', { players: Array.isArray(d.players) ? (d.players as string[]).join(' · ') : '' })
    case 'azul.tileDrafted':
      return typeof d.factory === 'number'
        ? t('events.azul.tileDraftedFactory', { player, count, color, factory: (d.factory as number) + 1 })
        : t('events.azul.tileDraftedCenter', { player, count, color })
    case 'azul.tilesMovedToCenter':
      return t('events.azul.tilesMovedToCenter', { count, factory: typeof d.factory === 'number' ? (d.factory as number) + 1 : '' })
    case 'azul.firstPlayerTaken':
      return t('events.azul.firstPlayerTaken', { player })
    case 'azul.tilesPlaced':
      return t('events.azul.tilesPlaced', { player, line: typeof d.line === 'number' ? (d.line as number) + 1 : name('line'), count, color })
    case 'azul.tilesDroppedFloor':
      return t('events.azul.tilesDroppedFloor', { player, count, color })
    case 'azul.tileOnWall':
      return t('events.azul.tileOnWall', { player, color, points: name('points') })
    case 'azul.floorPenalty':
      return t('events.azul.floorPenalty', { player, penalty: name('penalty') })
    case 'azul.scoreUpdated':
      return t('events.azul.scoreUpdated', { player, score: name('score') })
    case 'azul.roundFinished':
      return t('events.azul.roundFinished', { round: name('round'), bagCount: name('bagCount') })
    case 'azul.gameFinished':
      return typeof d.winner === 'string' && d.winner
        ? t('events.azul.gameFinished', { winner: d.winner })
        : t('events.azul.gameFinishedDraw')
    case 'azul.gameTimeExpired':
      return t('events.azul.gameTimeExpired')
    case 'azul.playerEliminated':
      return t('events.azul.playerEliminated', { player })
    default:
      return code
  }
}
