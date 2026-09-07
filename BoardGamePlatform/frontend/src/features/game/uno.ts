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
  NextPlayerSkipped: boolean
  UnoCalled: boolean
  UnoPendingPlayerIndex?: number | null
  EventLog: string[]
  TimerConfig?: UnoTurnTimerConfig
  PlayerTimers?: UnoPlayerTimer[]
  EliminatedPlayerIndexes?: number[]
}

const WILD_COLOR = 4

/** Card color metadata for rendering. Tailwind classes are full literals so JIT keeps them. */
export const CARD_COLORS = [
  { id: 0, name: 'Red', gradient: 'bg-gradient-to-br from-red-500 to-red-700', text: 'text-red-50', ring: 'ring-red-400' },
  { id: 1, name: 'Blue', gradient: 'bg-gradient-to-br from-blue-500 to-blue-700', text: 'text-blue-50', ring: 'ring-blue-400' },
  { id: 2, name: 'Green', gradient: 'bg-gradient-to-br from-green-500 to-green-700', text: 'text-green-50', ring: 'ring-green-400' },
  { id: 3, name: 'Yellow', gradient: 'bg-gradient-to-br from-yellow-400 to-yellow-600', text: 'text-yellow-50', ring: 'ring-yellow-400' },
  { id: 4, name: 'Wild', gradient: 'bg-gradient-to-br from-slate-600 to-slate-900', text: 'text-white', ring: 'ring-slate-500' },
]

export const COLOR_CHOICES = [
  { id: 0, name: 'Red', swatch: 'bg-red-600' },
  { id: 1, name: 'Blue', swatch: 'bg-blue-600' },
  { id: 2, name: 'Green', swatch: 'bg-green-600' },
  { id: 3, name: 'Yellow', swatch: 'bg-yellow-400' },
]

export function cardColor(id?: number | null) {
  return CARD_COLORS.find((c) => c.id === id) ?? CARD_COLORS[0]
}

export function cardValueLabel(value: number): string {
  switch (value) {
    case 10: return 'Skip'
    case 11: return 'Rev'
    case 12: return '+2'
    case 13: return 'Wild'
    case 14: return '+4'
    default: return String(value)
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