import type { GameState } from '@/shared/api/game'

/**
 * Client mirror of the backend `SplendorView` projection (PascalCase, exactly
 * as serialized). This is the ONLY Splendor data a client ever receives: all
 * public information, minus the three deck orders (only counts survive) and
 * minus the identities of blind reservations (`CardId: null`).
 */

export type GemColorName = 'diamond' | 'sapphire' | 'emerald' | 'ruby' | 'onyx'
export type GemKind = GemColorName | 'gold'

/** Canonical color index order: Diamond, Sapphire, Emerald, Ruby, Onyx. */
export const GEM_ORDER: GemColorName[] = ['diamond', 'sapphire', 'emerald', 'ruby', 'onyx']

/** Payload color names (engine parses case-insensitively). */
export const GEM_PAYLOAD_NAMES = ['Diamond', 'Sapphire', 'Emerald', 'Ruby', 'Onyx', 'Gold'] as const

export interface SplendorGems {
  Diamond: number
  Sapphire: number
  Emerald: number
  Ruby: number
  Onyx: number
  Gold: number
}

export type SplendorReservationSource = 'Market' | 'Deck'

export interface SplendorViewReservation {
  Tier: number
  Source: SplendorReservationSource
  /** Null for blind deck reservations - nobody may know the id before purchase. */
  CardId: string | null
}

export interface SplendorSeatView {
  Tokens: SplendorGems
  /** Bonus counts in GEM_ORDER. */
  Bonuses: number[]
  VictoryPoints: number
  Purchased: string[]
  NoblesOwned: string[]
  Reserved: SplendorViewReservation[]
}

export interface SplendorTimerConfig {
  BaseTurnSeconds: number
  MaxBankSeconds: number
  MaxOverrunSeconds: number
  MaxAfkTurns: number
  TotalGameTimeMinutes: number
}

export interface SplendorPlayerTimer {
  BankSeconds: number
  DeferredPenaltySeconds: number
  ConsecutiveTimeouts: number
}

export interface SplendorState {
  CurrentPlayerIndex: number
  TurnNumber: number
  Supply: SplendorGems
  DeckCounts: number[]
  Market: (string | null)[]
  NoblesInMarket: string[]
  Seats: SplendorSeatView[]
  FinalRoundTriggered: boolean
  TriggerSeat: number
  FinalTurnsRemaining: number
  EliminatedSeats: number[]
  EventLog: string[]
  ViewerSeat: number
  ViewerIsCurrentPlayer: boolean
  TimerConfig?: SplendorTimerConfig
  PlayerTimers?: SplendorPlayerTimer[]
  TurnStartUtc?: string
}

/** Extracts the nested projected SplendorState from the platform GameState. */
export function parseSplendorState(state: GameState | null | undefined): SplendorState | null {
  const raw = state?.data?.SplendorState
  if (typeof raw !== 'string') return null
  try {
    const parsed = JSON.parse(raw) as SplendorState
    // Only ever render the server-projected view: it carries DeckCounts +
    // Market and NEVER the authoritative `Decks` arrays (card-id sequences).
    // A raw authoritative payload (or an incompatible older schema) must never
    // reach the UI - reject it as "waiting for state" (platform 2026-09-12).
    if (
      !Array.isArray(parsed?.DeckCounts) ||
      !Array.isArray(parsed?.Market) ||
      !Array.isArray(parsed?.Seats) ||
      Array.isArray((parsed as unknown as { Decks?: unknown }).Decks)
    ) {
      return null
    }
    return parsed
  } catch {
    return null
  }
}

// ============================== card catalogue ==============================
// Static mirror of the backend SplendorCatalogue (docs/games/splendor.md §3.3,
// photo-verified 90 unique cards). Cost arrays are in GEM_ORDER.

export interface SplendorCardDef {
  id: string
  tier: 1 | 2 | 3
  /** Bonus color index into GEM_ORDER. */
  bonus: number
  points: number
  cost: number[]
}

type CardTuple = [string, 1 | 2 | 3, number, number, [number, number, number, number, number]]

const CARDS: CardTuple[] = [
  ['L1D01', 1, 0, 0, [0, 3, 0, 0, 0]],
  ['L1D02', 1, 0, 1, [0, 0, 4, 0, 0]],
  ['L1D03', 1, 0, 0, [0, 0, 0, 2, 1]],
  ['L1D04', 1, 0, 0, [0, 2, 0, 0, 2]],
  ['L1D05', 1, 0, 0, [3, 1, 0, 0, 1]],
  ['L1D06', 1, 0, 0, [0, 2, 2, 0, 1]],
  ['L1D07', 1, 0, 0, [0, 1, 1, 1, 1]],
  ['L1D08', 1, 0, 0, [0, 1, 2, 1, 1]],
  ['L1E01', 1, 2, 0, [0, 0, 0, 3, 0]],
  ['L1E02', 1, 2, 1, [0, 0, 0, 0, 4]],
  ['L1E03', 1, 2, 0, [2, 1, 0, 0, 0]],
  ['L1E04', 1, 2, 0, [0, 2, 0, 2, 0]],
  ['L1E05', 1, 2, 0, [1, 3, 1, 0, 0]],
  ['L1E06', 1, 2, 0, [0, 1, 0, 2, 2]],
  ['L1E07', 1, 2, 0, [1, 1, 0, 1, 1]],
  ['L1E08', 1, 2, 0, [1, 1, 0, 1, 2]],
  ['L1O01', 1, 4, 0, [0, 0, 3, 0, 0]],
  ['L1O02', 1, 4, 1, [0, 4, 0, 0, 0]],
  ['L1O03', 1, 4, 0, [0, 0, 2, 1, 0]],
  ['L1O04', 1, 4, 0, [2, 0, 2, 0, 0]],
  ['L1O05', 1, 4, 0, [0, 0, 1, 3, 1]],
  ['L1O06', 1, 4, 0, [2, 2, 0, 1, 0]],
  ['L1O07', 1, 4, 0, [1, 1, 1, 1, 0]],
  ['L1O08', 1, 4, 0, [1, 2, 1, 1, 0]],
  ['L1R01', 1, 3, 0, [3, 0, 0, 0, 0]],
  ['L1R02', 1, 3, 1, [4, 0, 0, 0, 0]],
  ['L1R03', 1, 3, 0, [0, 2, 1, 0, 0]],
  ['L1R04', 1, 3, 0, [2, 0, 0, 2, 0]],
  ['L1R05', 1, 3, 0, [1, 0, 0, 1, 3]],
  ['L1R06', 1, 3, 0, [2, 0, 1, 0, 2]],
  ['L1R07', 1, 3, 0, [1, 1, 1, 0, 1]],
  ['L1R08', 1, 3, 0, [2, 1, 1, 0, 1]],
  ['L1S01', 1, 1, 0, [0, 0, 0, 0, 3]],
  ['L1S02', 1, 1, 1, [0, 0, 0, 4, 0]],
  ['L1S03', 1, 1, 0, [1, 0, 0, 0, 2]],
  ['L1S04', 1, 1, 0, [0, 0, 2, 0, 2]],
  ['L1S05', 1, 1, 0, [0, 1, 3, 1, 0]],
  ['L1S06', 1, 1, 0, [1, 0, 2, 2, 0]],
  ['L1S07', 1, 1, 0, [1, 0, 1, 1, 1]],
  ['L1S08', 1, 1, 0, [1, 0, 1, 2, 1]],
  ['L2D01', 2, 0, 2, [0, 0, 0, 5, 0]],
  ['L2D02', 2, 0, 3, [6, 0, 0, 0, 0]],
  ['L2D03', 2, 0, 2, [0, 0, 0, 5, 3]],
  ['L2D04', 2, 0, 2, [0, 0, 1, 4, 2]],
  ['L2D05', 2, 0, 1, [0, 0, 3, 2, 2]],
  ['L2D06', 2, 0, 1, [2, 3, 0, 3, 0]],
  ['L2E01', 2, 2, 2, [0, 0, 5, 0, 0]],
  ['L2E02', 2, 2, 3, [0, 0, 6, 0, 0]],
  ['L2E03', 2, 2, 2, [0, 5, 3, 0, 0]],
  ['L2E04', 2, 2, 2, [4, 2, 0, 0, 1]],
  ['L2E05', 2, 2, 1, [2, 3, 0, 0, 2]],
  ['L2E06', 2, 2, 1, [3, 0, 2, 3, 0]],
  ['L2O01', 2, 4, 2, [5, 0, 0, 0, 0]],
  ['L2O02', 2, 4, 3, [0, 0, 0, 0, 6]],
  ['L2O03', 2, 4, 2, [0, 0, 5, 3, 0]],
  ['L2O04', 2, 4, 2, [0, 1, 4, 2, 0]],
  ['L2O05', 2, 4, 1, [3, 2, 2, 0, 0]],
  ['L2O06', 2, 4, 1, [3, 0, 3, 0, 2]],
  ['L2R01', 2, 3, 2, [0, 0, 0, 0, 5]],
  ['L2R02', 2, 3, 3, [0, 0, 0, 6, 0]],
  ['L2R03', 2, 3, 2, [3, 0, 0, 0, 5]],
  ['L2R04', 2, 3, 2, [1, 4, 2, 0, 0]],
  ['L2R05', 2, 3, 1, [2, 0, 0, 2, 3]],
  ['L2R06', 2, 3, 1, [0, 3, 0, 2, 3]],
  ['L2S01', 2, 1, 2, [0, 5, 0, 0, 0]],
  ['L2S02', 2, 1, 3, [0, 6, 0, 0, 0]],
  ['L2S03', 2, 1, 2, [5, 3, 0, 0, 0]],
  ['L2S04', 2, 1, 2, [2, 0, 0, 1, 4]],
  ['L2S05', 2, 1, 1, [0, 2, 2, 3, 0]],
  ['L2S06', 2, 1, 1, [0, 2, 3, 0, 3]],
  ['L3D01', 3, 0, 4, [0, 0, 0, 0, 7]],
  ['L3D02', 3, 0, 4, [3, 0, 0, 3, 6]],
  ['L3D03', 3, 0, 3, [0, 3, 3, 5, 3]],
  ['L3D04', 3, 0, 5, [3, 0, 0, 0, 7]],
  ['L3E01', 3, 2, 4, [0, 7, 0, 0, 0]],
  ['L3E02', 3, 2, 4, [3, 6, 3, 0, 0]],
  ['L3E03', 3, 2, 3, [5, 3, 0, 3, 3]],
  ['L3E04', 3, 2, 5, [0, 7, 3, 0, 0]],
  ['L3O01', 3, 4, 4, [0, 0, 0, 7, 0]],
  ['L3O02', 3, 4, 4, [0, 0, 3, 6, 3]],
  ['L3O03', 3, 4, 3, [3, 3, 5, 3, 0]],
  ['L3O04', 3, 4, 5, [0, 0, 0, 7, 3]],
  ['L3R01', 3, 3, 4, [0, 0, 7, 0, 0]],
  ['L3R02', 3, 3, 4, [0, 3, 6, 3, 0]],
  ['L3R03', 3, 3, 3, [3, 5, 3, 0, 3]],
  ['L3R04', 3, 3, 5, [0, 0, 7, 3, 0]],
  ['L3S01', 3, 1, 4, [7, 0, 0, 0, 0]],
  ['L3S02', 3, 1, 4, [6, 3, 0, 0, 3]],
  ['L3S03', 3, 1, 3, [3, 0, 3, 3, 5]],
  ['L3S04', 3, 1, 5, [7, 3, 0, 0, 0]],
]

export interface SplendorNobleDef {
  id: string
  /** Required bonuses in GEM_ORDER. */
  requirement: number[]
}

/** Canonical display order (§3.2) - also the engine's OD-5 auto-claim order. */
export const NOBLES: SplendorNobleDef[] = [
  { id: 'N-DS', requirement: [4, 4, 0, 0, 0] },
  { id: 'N-SE', requirement: [0, 4, 4, 0, 0] },
  { id: 'N-ER', requirement: [0, 0, 4, 4, 0] },
  { id: 'N-RO', requirement: [0, 0, 0, 4, 4] },
  { id: 'N-OD', requirement: [4, 0, 0, 0, 4] },
  { id: 'N-ERO', requirement: [0, 0, 3, 3, 3] },
  { id: 'N-DSO', requirement: [3, 3, 0, 0, 3] },
  { id: 'N-SER', requirement: [0, 3, 3, 3, 0] },
  { id: 'N-DRO', requirement: [3, 0, 0, 3, 3] },
  { id: 'N-DSE', requirement: [3, 3, 3, 0, 0] },
]

const NOBLE_INDEX: Record<string, SplendorNobleDef> = Object.fromEntries(NOBLES.map((n) => [n.id, n]))

export const CARD_INDEX: Record<string, SplendorCardDef> = Object.fromEntries(
  CARDS.map(([id, tier, bonus, points, cost]) => [id, { id, tier, bonus, points, cost }]),
)

export function cardById(id: string | null | undefined): SplendorCardDef | null {
  return id ? CARD_INDEX[id] ?? null : null
}

export function nobleById(id: string): SplendorNobleDef | null {
  return NOBLE_INDEX[id] ?? null
}

// ================================ constants ================================

export const MAX_TOKENS = 10
export const MAX_RESERVATIONS = 3
export const TRIGGER_POINTS = 15
export const NOBLE_POINTS = 3

// ============================== image assets ===============================
// Real photography / public-domain paintings served from `frontend/public`
// (sources + licenses recorded in public/splendor/CREDITS.md).

export const GEM_IMAGE: Record<GemKind, string> = {
  diamond: '/splendor/gems/diamond.jpg',
  sapphire: '/splendor/gems/sapphire.jpg',
  emerald: '/splendor/gems/emerald.jpg',
  ruby: '/splendor/gems/ruby.jpg',
  onyx: '/splendor/gems/onyx.jpg',
  gold: '/splendor/gems/gold.jpg',
}

/** Portrait per noble tile (anonymous Old Master portraits - no invented names). */
export const NOBLE_IMAGE: Record<string, string> = {
  'N-DS': '/splendor/nobles/n1.jpg',
  'N-SE': '/splendor/nobles/n2.jpg',
  'N-ER': '/splendor/nobles/n3.jpg',
  'N-RO': '/splendor/nobles/n4.jpg',
  'N-OD': '/splendor/nobles/n5.jpg',
  'N-ERO': '/splendor/nobles/n6.jpg',
  'N-DSO': '/splendor/nobles/n7.jpg',
  'N-SER': '/splendor/nobles/n8.jpg',
  'N-DRO': '/splendor/nobles/n9.jpg',
  'N-DSE': '/splendor/nobles/n10.jpg',
}

/** Level scene art: 1 mines, 2 aqueduct/transport, 3 jeweler's treasure. */
export const SCENE_IMAGE: Record<1 | 2 | 3, string> = {
  1: '/splendor/scenes/mine.jpg',
  2: '/splendor/scenes/aqueduct.jpg',
  3: '/splendor/scenes/jewels.jpg',
}

/** Engine defaults (§20) - configuration, not rules; the view falls back to these. */
export const DEFAULT_TIMER: SplendorTimerConfig = {
  BaseTurnSeconds: 60,
  MaxBankSeconds: 180,
  MaxOverrunSeconds: 15,
  MaxAfkTurns: 3,
  TotalGameTimeMinutes: 60,
}

// ============================ payment / gems math ==========================
// UX-only mirrors of the engine rules (§6/§9); the server re-validates all.

export const emptyPayment = () => ({ Diamond: 0, Sapphire: 0, Emerald: 0, Ruby: 0, Onyx: 0, Gold: 0 })

/** Per-color token cost after the seat's bonuses (never negative, §9). */
export function effectiveCost(card: SplendorCardDef, bonuses: number[]): number[] {
  return card.cost.map((c, i) => Math.max(0, c - (bonuses[i] ?? 0)))
}

export function gemsOf(bag: SplendorGems): number[] {
  return [bag.Diamond, bag.Sapphire, bag.Emerald, bag.Ruby, bag.Onyx]
}

export function tokenTotal(bag: SplendorGems): number {
  return gemsOf(bag).reduce((a, b) => a + b, 0) + bag.Gold
}

/** Minimum gold needed after spending as many held tokens as useful. */
export function minGoldNeeded(card: SplendorCardDef, seat: SplendorSeatView): number {
  const required = effectiveCost(card, seat.Bonuses)
  const held = gemsOf(seat.Tokens)
  return required.reduce((sum, r, i) => sum + Math.max(0, r - held[i]), 0)
}

export function canAfford(card: SplendorCardDef, seat: SplendorSeatView): boolean {
  return minGoldNeeded(card, seat) <= seat.Tokens.Gold
}

/** Greedy canonical payment: every held token up to the requirement, gold covers the rest. */
export function defaultPayment(card: SplendorCardDef, seat: SplendorSeatView): SplendorGems | null {
  const required = effectiveCost(card, seat.Bonuses)
  const held = gemsOf(seat.Tokens)
  const payment = emptyPayment()
  let gold = 0
  for (let i = 0; i < 5; i++) {
    const spend = Math.min(held[i], required[i])
    payment[GEM_PAYLOAD_NAMES[i] as 'Diamond'] = spend
    gold += required[i] - spend
  }
  if (gold > seat.Tokens.Gold) return null
  payment.Gold = gold
  return payment
}

/** Nobles the seat satisfies by bonuses (display order), restricted to tiles still in the market. */
export function eligibleNobles(splendor: SplendorState, seat: SplendorSeatView, bonusOverride?: number[]): string[] {
  const bonuses = bonusOverride ?? seat.Bonuses
  return NOBLES.filter(
    (n) =>
      splendor.NoblesInMarket.includes(n.id) &&
      n.requirement.every((r, i) => (bonuses[i] ?? 0) >= r),
  ).map((n) => n.id)
}

// =========================== payload builders ==============================
// Payloads are PascalCase (System.Text.Json default), like the UNO/Silver ones.

export type GemReturn = { Color: string; Count: number }

export const splendorActions = {
  takeThree: (colors: GemColorName[], ret?: GemReturn[], claimNoble?: string) => ({
    Colors: colors.map((c) => GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(c)]),
    ...(ret && ret.length ? { Return: ret } : {}),
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
  takeTwo: (color: GemColorName, ret?: GemReturn[], claimNoble?: string) => ({
    Color: GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(color)],
    ...(ret && ret.length ? { Return: ret } : {}),
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
  reserveMarket: (cardId: string, ret?: GemReturn[], claimNoble?: string) => ({
    CardId: cardId,
    ...(ret && ret.length ? { Return: ret } : {}),
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
  reserveDeck: (tier: number, ret?: GemReturn[], claimNoble?: string) => ({
    Tier: tier,
    ...(ret && ret.length ? { Return: ret } : {}),
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
  purchaseMarket: (cardId: string, payment: SplendorGems, claimNoble?: string) => ({
    CardId: cardId,
    Payment: payment,
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
  purchaseReserved: (index: number, payment: SplendorGems, claimNoble?: string) => ({
    ReservationIndex: index,
    Payment: payment,
    ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
  }),
}

// ================================ events ===================================

function gemListName(colors: unknown, t: (k: string) => string): string {
  if (!Array.isArray(colors)) return ''
  return colors
    .map((c) => (typeof c === 'string' ? t(`splendor.gems.${c}`) : ''))
    .filter(Boolean)
    .join(' · ')
}

function cardEventName(id: unknown, t: (k: string, p?: Record<string, string | number>) => string): string {
  const card = cardById(typeof id === 'string' ? id : null)
  if (!card) return typeof id === 'string' ? id : ''
  return t('splendor.cardName', {
    tier: t(`splendor.tierNames.T${card.tier}`),
    gem: t(`splendor.gems.${GEM_ORDER[card.bonus]}`),
  })
}

/**
 * Renders a Splendor event-log entry ({"c":"splendor.x","d":{...}}) in the
 * active language. Deck-reservation entries never carry a card id (spec §19);
 * legacy plain-text entries render unchanged.
 */
export function formatSplendorEvent(
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

  switch (code) {
    case 'splendor.gameStarted':
      return t('events.splendor.gameStarted', {
        players: Array.isArray(d.players) ? (d.players as string[]).join(' · ') : '',
      })
    case 'splendor.gemsTaken':
      return t('events.splendor.gemsTaken', { player, gems: gemListName(d.colors, t) })
    case 'splendor.tokensReturned':
      return t('events.splendor.tokensReturned', { player, count: Number(d.count ?? 0) })
    case 'splendor.goldTaken':
      return t('events.splendor.goldTaken', { player })
    case 'splendor.cardReserved':
      return d.source === 'Deck'
        ? t('events.splendor.cardReservedDeck', { player, tier: Number(d.tier ?? 0) })
        : t('events.splendor.cardReservedMarket', { player, card: cardEventName(d.card, t) })
    case 'splendor.cardPurchased':
      return t('events.splendor.cardPurchased', {
        player,
        card: cardEventName(d.card, t),
        points: Number(d.points ?? 0),
        score: Number(d.score ?? 0),
      })
    case 'splendor.nobleClaimed': {
      const noble = nobleById(typeof d.noble === 'string' ? d.noble : '')
      const gems = noble
        ? noble.requirement
            .map((r, i) => (r > 0 ? `${r} ${t(`splendor.gems.${GEM_ORDER[i]}`)}` : ''))
            .filter(Boolean)
            .join(' + ')
        : String(d.noble ?? '')
      return t('events.splendor.nobleClaimed', { player, noble: gems, score: Number(d.score ?? 0) })
    }
    case 'splendor.finalRoundTriggered':
      return t('events.splendor.finalRoundTriggered', { player, remaining: Number(d.remaining ?? 0) })
    case 'splendor.gameFinished':
      return typeof d.winner === 'string' && d.winner
        ? t('events.splendor.gameFinished', { winner: d.winner })
        : t('events.splendor.gameFinishedDraw')
    case 'splendor.gameTimeExpired':
      return t('events.splendor.gameTimeExpired')
    case 'splendor.playerEliminated':
      return t('events.splendor.playerEliminated', { player })
    default:
      return code
  }
}
