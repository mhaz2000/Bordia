import type { GameState } from '@/shared/api/game'

/**
 * Client mirror of the backend `SilverView` projection (PascalCase, exactly as
 * serialized). This is the ONLY Silver data a client ever receives: public
 * information plus the viewer's own private cards and knowledge. Hidden card
 * values arrive as `Value: null` and must never be inferred.
 */

export interface SilverViewCard {
  Id: string
  /** Werewolf count 0-13, or null when hidden from the viewer. */
  Value: number | null
  FaceUp: boolean
  /** Covered by a Guard or the Silver Amulet (untouchable by outsiders). */
  Protected: boolean
  /** Covered specifically by the Silver Amulet (untouchable by everyone). */
  AmuletProtected: boolean
  /** The Guard card covering this card, when a Guard does (public). */
  GuardedByCardId: string | null
}

export type SilverPhase =
  | 'TurnStart'
  | 'TricksterChoice'
  | 'DrawnDecision'
  | 'ExchangeDecision'
  | 'AbilityPending'

export type SilverPendingSource = 'Deck' | 'Discard' | 'Display'

export interface SilverTimerConfig {
  BaseTurnSeconds: number
  MaxBankSeconds: number
  MaxOverrunSeconds: number
  MaxAfkTurns: number
  TotalGameTimeMinutes: number
}

export interface SilverPlayerTimer {
  BankSeconds: number
  DeferredPenaltySeconds: number
  ConsecutiveTimeouts: number
}

export interface SilverState {
  Round: number
  CurrentPlayerIndex: number
  Phase: SilverPhase
  Villages: SilverViewCard[][]
  DeckSize: number
  RemovedCount: number
  DiscardTop: SilverViewCard | null
  DiscardSize: number
  DiscardPile: SilverViewCard[]
  Display: SilverViewCard[]
  PendingDraw: SilverViewCard[]
  PendingSource: SilverPendingSource
  WitchPeekPending: boolean
  /** The peeked deck-top value, present only for the Witch player's view. */
  WitchPeekedValue: number | null
  RevealerChooserSeat: number | null
  CensusCallerIndex: number | null
  RemainingCensusTurns: number
  AmuletHolderIndex: number | null
  AmuletPlacedCardId: string | null
  AmuletPlaceable: boolean
  CumulativeScores: number[]
  LastRoundScores: number[] | null
  LastRoundCensusCallerIndex: number | null
  EliminatedPlayerIndexes: number[]
  ViewerPeeksUsed: number
  ViewerIsCurrentPlayer: boolean
  AbilitiesUsedThisTurn: string[]
  ActedThisTurn: boolean
  EventLog: string[]
  TimerConfig?: SilverTimerConfig
  PlayerTimers?: SilverPlayerTimer[]
  TurnStartUtc?: string
}

/** Extracts the nested SilverState from the platform GameState. */
export function parseSilverState(state: GameState | null | undefined): SilverState | null {
  const raw = state?.data?.SilverState
  if (typeof raw !== 'string') return null
  try {
    const parsed = JSON.parse(raw) as SilverState
    // Only ever render the server-projected view: it always carries DeckSize
    // and DiscardPile. A raw authoritative payload (or an incompatible older
    // schema) must never reach the UI - reject it as "waiting for state".
    if (typeof parsed?.DeckSize !== 'number' || !Array.isArray(parsed?.DiscardPile)) return null
    return parsed
  } catch {
    return null
  }
}

// ============================== card metadata ==============================

export type SilverTier = 'always' | 'village' | 'discard' | 'wild'

export type SilverAbilityName =
  | 'Squire'
  | 'Enchanter'
  | 'Guard'
  | 'Exposer'
  | 'Revealer'
  | 'ApprenticeSeer'
  | 'Seer'
  | 'Beholder'
  | 'Master'
  | 'Witch'
  | 'Robber'

/** Ability enum name per card value (0 is automatic, 13 is passive). */
export const ABILITY_BY_VALUE: Record<number, SilverAbilityName> = {
  1: 'Squire',
  2: 'Enchanter',
  3: 'Guard',
  5: 'Exposer',
  6: 'Revealer',
  7: 'ApprenticeSeer',
  8: 'Seer',
  9: 'Beholder',
  10: 'Master',
  11: 'Witch',
  12: 'Robber',
}

/** Dictionary key under games.Silver.cardNames for a card value. */
export function cardNameKey(value: number): string {
  return `V${value}`
}

export function tierOf(value: number): SilverTier {
  if (value <= 1) return 'always'
  if (value <= 4) return 'village'
  if (value <= 12) return 'discard'
  return 'wild'
}

/** Card face palette per tier: gradient body + accent text color. */
export const TIER_STYLES: Record<SilverTier, { gradient: string; accent: string; abilityKey: string }> = {
  always: {
    gradient: 'from-slate-500 via-slate-700 to-indigo-950',
    accent: 'text-slate-100',
    abilityKey: 'silver.tierAlways',
  },
  village: {
    gradient: 'from-emerald-700 via-emerald-800 to-teal-950',
    accent: 'text-emerald-50',
    abilityKey: 'silver.tierVillage',
  },
  discard: {
    gradient: 'from-amber-700 via-amber-800 to-orange-950',
    accent: 'text-amber-50',
    abilityKey: 'silver.tierDiscard',
  },
  wild: {
    gradient: 'from-violet-600 via-purple-800 to-fuchsia-950',
    accent: 'text-violet-50',
    abilityKey: 'silver.tierWild',
  },
}

// =========================== payload builders ==============================
// Payloads are case-sensitive PascalCase (System.Text.Json default), exactly
// like the UNO client payloads.

export const silverActions = {
  peek: (slotIndex: number) => ({ SlotIndex: slotIndex }),
  draw: (tricksterExtra: number) => ({ TricksterExtra: tricksterExtra }),
  takeSquire: (displayIndex: number) => ({ DisplayIndex: displayIndex }),
  chooseDrawn: (cardId: string) => ({ CardId: cardId }),
  discardDrawn: () => ({}),
  exchange: (slots: number[], placementSlot: number, newAtEnd: boolean, penaltyAtEnd: boolean) => ({
    Slots: slots,
    PlacementSlot: placementSlot,
    NewAtEnd: newAtEnd,
    PenaltyAtEnd: penaltyAtEnd,
  }),
  useAbility: (payload: {
    Ability: SilverAbilityName
    OwnSlot?: number
    OwnSlots?: number[]
    TargetPlayerIndex?: number
    TargetSlot?: number
    DiscardIndex?: number
    PeekSlots?: number[]
    PlacementSlot?: number
    NewAtEnd?: boolean
    PenaltyAtEnd?: boolean
  }) => ({ ...payload }),
  moveGuard: (guardSlot: number, targetSlot: number) => ({ GuardSlot: guardSlot, TargetSlot: targetSlot }),
  removeGuard: (guardSlot: number) => ({ GuardSlot: guardSlot }),
  chooseReveal: (slotIndex: number) => ({ SlotIndex: slotIndex }),
  skipAbility: () => ({}),
  callCensus: () => ({}),
  placeAmulet: (slotIndex: number) => ({ SlotIndex: slotIndex }),
}

// ============================ client rule mirror ===========================
// UX-only convenience; the server re-validates everything.

export function hasFaceUp(village: SilverViewCard[], value: number): boolean {
  return village.some((c) => c.FaceUp && c.Value === value)
}

export function countFaceUp(village: SilverViewCard[], value: number): number {
  return village.filter((c) => c.FaceUp && c.Value === value).length
}

/** Slots the owner may exchange away (the Amulet binds even its owner). */
export function ownerMovableSlots(village: SilverViewCard[]): number[] {
  return village.map((c, i) => ({ c, i })).filter(({ c }) => !c.AmuletProtected).map(({ i }) => i)
}

export function faceDownSlots(village: SilverViewCard[]): number[] {
  return village.map((c, i) => ({ c, i })).filter(({ c }) => !c.FaceUp && !c.Protected).map(({ i }) => i)
}

/** Slots the owner may look at (Guard-covered cards remain theirs to peek). */
export function ownViewableSlots(village: SilverViewCard[]): number[] {
  return village.map((c, i) => ({ c, i })).filter(({ c }) => !c.FaceUp && !c.AmuletProtected).map(({ i }) => i)
}

/** Multi-replacement match verdict from the viewer's knowledge (one wild max). */
export function matchStatus(values: (number | null)[]): 'match' | 'mismatch' | 'uncertain' | 'wildcard' {
  const known = values.filter((v): v is number => v !== null)
  const wilds = known.filter((v) => v === 13).length
  const others = new Set(known.filter((v) => v !== 13))
  if (others.size > 1) return 'mismatch'
  if (wilds >= 2 && others.size === 1) return 'wildcard'
  if (wilds > 2) return 'mismatch'
  return known.length === values.length ? 'match' : 'uncertain'
}

/** The value of the card whose 5-12 ability window is open, or null. */
export function pendingAbilityValue(silver: SilverState): number | null {
  if (silver.Phase !== 'AbilityPending' || !silver.DiscardTop) return null
  const card = silver.DiscardPile.find((c) => c.Id === silver.DiscardTop?.Id)
  if (!card || card.Value === null) return null
  return card.Value >= 5 && card.Value <= 12 ? card.Value : null
}

/** How many Enchanter peeks remain this turn for the viewer. */
export function enchanterUsesLeft(silver: SilverState, myVillage: SilverViewCard[]): number {
  if (!silver.ViewerIsCurrentPlayer) return 0
  const rights = countFaceUp(myVillage, 2)
  const used = silver.AbilitiesUsedThisTurn.filter((e) => e === 'Enchanter').length
  return Math.max(0, rights - used)
}

// ================================ events ===================================

type SilverEventParams = Record<string, unknown>

function localizedCardName(value: number, t: (k: string) => string): string {
  return t(`games.Silver.cardNames.${cardNameKey(value)}`)
}

/**
 * Renders a Silver event-log entry ({"c":"silver.x","d":{...}}) in the active
 * language. Card values and ability names resolve through the localized card
 * names; per-seat score arrays render with seat display names when available.
 */
export function formatSilverEvent(
  entry: string,
  t: (key: string, params?: Record<string, string | number>) => string,
  playerNames?: (string | undefined)[],
): string {
  if (!entry.startsWith('{')) return entry
  let parsed: { c?: string; d?: SilverEventParams }
  try {
    parsed = JSON.parse(entry)
  } catch {
    return entry
  }
  const code = parsed.c
  if (!code) return entry
  const d = parsed.d ?? {}

  const player = typeof d.player === 'string' ? d.player : ''
  const value = typeof d.value === 'number' ? d.value : null
  const card = value !== null ? localizedCardName(value, t) : ''
  const abilityValue = typeof d.ability === 'string'
    ? Object.entries(ABILITY_BY_VALUE).find(([, name]) => name === d.ability)?.[0]
    : undefined
  const ability = typeof d.ability === 'string'
    ? abilityValue !== undefined
      ? localizedCardName(Number(abilityValue), t)
      : d.ability
    : ''
  const count = typeof d.count === 'number' ? d.count : 0
  const round = typeof d.round === 'number' ? d.round : 0

  const params: Record<string, string | number> = {}
  for (const [k, v] of Object.entries(d)) {
    if (k === 'value' || k === 'count' || k === 'round' || k === 'player' || k === 'ability' || k === 'scores' || k === 'winner') continue
    if (typeof v === 'number' || typeof v === 'string') params[k] = v
  }
  if (player) params.player = player
  if (card) params.card = card
  if (ability) params.ability = ability

  switch (code) {
    case 'silver.roundStarted':
      return t('events.silver.roundStarted', { round })
    case 'silver.cardDrawn':
      return count >= 2
        ? t('events.silver.cardDrawnMany', { player, count })
        : t('events.silver.cardDrawn', { player })
    case 'silver.cardDiscarded':
      return t('events.silver.cardDiscarded', { player, card })
    case 'silver.discardTaken':
      return t('events.silver.discardTaken', { player, card })
    case 'silver.squireRevealed':
      return t('events.silver.squireRevealed', { count })
    case 'silver.squireCardTaken':
      return t('events.silver.squireCardTaken', { player, card })
    case 'silver.exchanged':
      return t('events.silver.exchanged', { player, count })
    case 'silver.exchangeFailed':
      return t('events.silver.exchangeFailed', { player, count })
    case 'silver.penaltyCard':
      return t('events.silver.penaltyCard', { player })
    case 'silver.cardRevealed':
      return t('events.silver.cardRevealed', { player, card })
    case 'silver.abilityUsed':
      return t('events.silver.abilityUsed', { player, ability })
    case 'silver.amuletPlaced':
      return t('events.silver.amuletPlaced', { player })
    case 'silver.voteCalled':
      return t('events.silver.voteCalled', { player })
    case 'silver.roundScored': {
      const scores = Array.isArray(d.scores) ? (d.scores as number[]) : []
      const rendered = scores
        .map((s, i) => `${playerNames?.[i] ?? `#${i + 1}`}: ${s}`)
        .join(' · ')
      return t('events.silver.roundScored', { scores: rendered })
    }
    case 'silver.roundEnded':
      return t('events.silver.roundEnded', { round })
    case 'silver.gameFinished':
      return typeof d.winner === 'string' && d.winner
        ? t('events.silver.gameFinished', { winner: d.winner })
        : t('events.silver.gameFinishedDraw')
    case 'silver.gameTimeExpired':
      return t('events.silver.gameTimeExpired')
    case 'silver.playerEliminated':
      return t('events.silver.playerEliminated', { player })
    default:
      return code
  }
}
