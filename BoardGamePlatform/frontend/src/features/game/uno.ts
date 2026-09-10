import type { GameState } from '@/shared/api/game'

export interface UnoCard {
  Color: number
  Value: number
}

export interface UnoPlayerHand {
  Cards: UnoCard[]
}

export interface UnoPlayerTimer {
  BankSeconds: number
  DeferredPenaltySeconds: number
  ConsecutiveTimeouts: number
}

export interface UnoTurnTimerConfig {
  BaseTurnSeconds: number
  MaxBankSeconds: number
  MaxOverrunSeconds: number
  MaxAfkTurns: number
}

export interface UnoState {
  DrawPile: { Cards?: UnoCard[]; Count?: number }
  DiscardPile: UnoCard[]
  PlayerHands: UnoPlayerHand[]
  CurrentColor?: number | null
  Direction: number
  CurrentPlayerIndex: number
  PendingDrawCount: number
  PendingDrawOffenderIndex?: number | null
  PendingDrawTargetIndex?: number | null
  NextPlayerSkipped: boolean
  UnoCalled: boolean
  UnoPendingPlayerIndex?: number | null
  DrawnThisTurn?: boolean
  EventLog: string[]
  TimerConfig?: UnoTurnTimerConfig
  PlayerTimers?: UnoPlayerTimer[]
  TurnStartUtc?: string
  EliminatedPlayerIndexes?: number[]
}

const WILD_COLOR = 4

/** Card color metadata for rendering. Names come from the colors.* dictionary.
 *  Tailwind classes are full literals so JIT keeps them. */
export const CARD_COLORS = [
  { id: 0, gradient: 'bg-gradient-to-br from-red-500 to-red-700', text: 'text-red-50', ring: 'ring-red-400' },
  { id: 1, gradient: 'bg-gradient-to-br from-blue-500 to-blue-700', text: 'text-blue-50', ring: 'ring-blue-400' },
  { id: 2, gradient: 'bg-gradient-to-br from-green-500 to-green-700', text: 'text-green-50', ring: 'ring-green-400' },
  { id: 3, gradient: 'bg-gradient-to-br from-yellow-400 to-yellow-600', text: 'text-yellow-50', ring: 'ring-yellow-400' },
  { id: 4, gradient: 'bg-gradient-to-br from-slate-600 to-slate-900', text: 'text-white', ring: 'ring-slate-500' },
]

export const COLOR_CHOICES = [
  { id: 0, swatch: 'bg-red-600' },
  { id: 1, swatch: 'bg-blue-600' },
  { id: 2, swatch: 'bg-green-600' },
  { id: 3, swatch: 'bg-yellow-400' },
]

export function cardColor(id?: number | null) {
  return CARD_COLORS.find((c) => c.id === id) ?? CARD_COLORS[0]
}

/** Hex color for card faces (index-aligned with CARD_COLORS). */
export const CARD_HEX = ['#DC2626', '#2563EB', '#16A34A', '#EAB308', '#1F2937']

/** Center/corner glyph for a card value; wild values render specially. */
export function valueGlyph(value: number): string {
  switch (value) {
    case 10: return '⊘'
    case 11: return '⇄'
    case 12: return '+2'
    case 14: return '+4'
    default: return String(value)
  }
}

/** Inline conic-gradient style for wild card faces. */
export const WILD_GRADIENT: React.CSSProperties = {
  background:
    'conic-gradient(from 45deg, #DC2626 0deg 90deg, #2563EB 90deg 180deg, #EAB308 180deg 270deg, #16A34A 270deg 360deg)',
}

/** Dictionary key under games.UNO.cardNames for a special value, or null for number cards. */
export function cardValueNameKey(value: number): string | null {
  switch (value) {
    case 10: return 'Skip'
    case 11: return 'Rev'
    case 12: return 'Draw2'
    case 13: return 'Wild'
    case 14: return 'WDraw4'
    default: return null
  }
}

export function isWild(card: UnoCard) {
  return card.Color === WILD_COLOR
}

/** A card is playable if it is wild, matches the active color, or matches the top card value. */
export function isPlayable(card: UnoCard, uno: UnoState): boolean {
  if (isWild(card)) return true
  const top = uno.DiscardPile[uno.DiscardPile.length - 1]
  if (!top) return true
  const activeColor = uno.CurrentColor ?? top.Color
  return card.Color === activeColor || card.Value === top.Value
}

/** Extracts the nested UnoState from the platform GameState. */
export function parseUnoState(state: GameState | null | undefined): UnoState | null {
  const raw = state?.data?.UnoState
  if (typeof raw !== 'string') return null
  try {
    return JSON.parse(raw) as UnoState
  } catch {
    return null
  }
}

/** Number-to-word color names used inside engine event params (0-4). */
const EVENT_COLOR_KEYS = ['colors.red', 'colors.blue', 'colors.green', 'colors.yellow', 'colors.wild'] as const

/** Rewrites a shorthand card code (R7, BDraw2, WDraw4, GRev) into readable parts. */
function parseCardCode(code: string): { colorKey?: string; valueLabel?: string } {
  if (code === 'Wild' || code === 'WDraw4') {
    return { colorKey: 'colors.wild', valueLabel: code }
  }
  const m = /^([RBGYW])(.+)$/.exec(code)
  if (!m) return {}
  const colorKey =
    m[1] === 'R' ? 'colors.red' :
    m[1] === 'B' ? 'colors.blue' :
    m[1] === 'G' ? 'colors.green' :
    m[1] === 'Y' ? 'colors.yellow' :
    m[1] === 'W' ? 'colors.wild' : undefined
  const raw = m[2]
  const valueLabel =
    raw === 'Skip' ? 'Skip' :
    raw === 'Rev' ? 'Rev' :
    raw === 'Draw2' ? 'Draw2' :
    raw === 'Wild' ? 'Wild' :
    raw === 'WDraw4' ? 'WDraw4' :
    raw
  return { colorKey, valueLabel }
}

/**
 * Renders an engine event-log entry in the active language.
 *
 * New entries are JSON envelopes: {"c":"code","d":{params}}. Numeric color
 * params render as localized color names; card codes ("R7", "BDraw2") render
 * as "Red 7" / "Blue Draw 2" style localized text. Legacy plain-text entries
 * (older games) are returned unchanged.
 */
export function formatEvent(
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
  if (!parsed.c) return entry

  const params: Record<string, string | number> = {}
  for (const [k, v] of Object.entries(parsed.d ?? {})) {
    if (k === 'color' && typeof v === 'number') {
      params[k] = t(EVENT_COLOR_KEYS[v] ?? 'colors.red')
    } else if (k === 'card' && typeof v === 'string') {
      const { colorKey, valueLabel } = parseCardCode(v)
      const color = colorKey ? t(colorKey) : ''
      const known = valueLabel === 'Skip' || valueLabel === 'Rev' || valueLabel === 'Draw2'
        || valueLabel === 'Wild' || valueLabel === 'WDraw4'
      const glyph = known ? t(`games.UNO.cardNames.${valueLabel}`) : valueLabel
      params[k] = color && !v.startsWith('W') ? `${color} ${glyph}` : glyph ?? v
    } else if (typeof v === 'number') {
      params[k] = v
    } else if (typeof v === 'string') {
      params[k] = v
    }
  }
  return t(`events.${parsed.c}`, params)
}
