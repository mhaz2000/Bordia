import { useEffect, useMemo, useRef, useState } from 'react'
import {
  CheckIcon,
  ClockIcon,
  HandThumbUpIcon,
  PlayIcon,
  QuestionMarkCircleIcon,
  SparklesIcon,
  TrashIcon,
  TrophyIcon,
} from '@heroicons/react/24/outline'
import type { GameSession, GameState } from '@/shared/api/game'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { useCountdown, useNow } from '@/shared/hooks/useCountdown'
import {
  BonusChip,
  GemChip,
  GemIcon,
  GemPile,
  SplendorCardBack,
  SplendorCardVisual,
  gemKind,
} from '@/shared/components/SplendorCardVisual'
import { CrownGlyph, SplendorNobleVisual } from '@/shared/components/SplendorNobleVisual'
import { flightAnchor, resetFlights, runFlights, type Flight } from './tokenFlight'
import { useI18n } from '@/i18n/I18nProvider'
import {
  canAfford,
  cardById,
  DEFAULT_TIMER,
  effectiveCost,
  eligibleNobles,
  emptyPayment,
  formatSplendorEvent,
  GEM_ORDER,
  GEM_PAYLOAD_NAMES,
  MAX_RESERVATIONS,
  MAX_TOKENS,
  TRIGGER_POINTS,
  nobleById,
  parseSplendorState,
  splendorActions,
  tokenTotal,
  type GemColorName,
  type GemKind,
  type GemReturn,
  type SplendorCardDef,
  type SplendorGems,
  type SplendorSeatView,
  type SplendorState,
} from './splendor'

interface SplendorGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

/** A staged take/reserve awaiting the return + noble-claim decisions. */
interface TurnFlow {
  actionType: string
  core: Record<string, unknown>
  /** Gained token counts per kind (GEM_ORDER + gold last). */
  gained: number[]
  summaryKey: string
  summaryParams?: Record<string, string | number>
}

type PurchaseTarget = { source: 'market'; cardId: string } | { source: 'reserved'; index: number }

const KINDS: GemKind[] = [...GEM_ORDER, 'gold']

/** Hand-placed wobble so market rows read as laid-out cards, not a UI grid. */
const MARKET_TILT = ['rotate-[-0.7deg]', 'rotate-[0.5deg]', 'rotate-[-0.4deg]', 'rotate-[0.9deg]']

type TFn = (key: string, params?: Record<string, string | number>) => string

export function SplendorGameView({ state, session, userId, onAction, isSending }: SplendorGameViewProps) {
  const { t } = useI18n()
  const splendor = useMemo(() => parseSplendorState(state), [state])

  // Gems selected for the take being staged: color -> 1 (distinct take) or 2 (pair).
  const [sel, setSel] = useState<Partial<Record<GemColorName, number>>>({})
  const [hint, setHint] = useState<string | null>(null)
  const [cardModal, setCardModal] = useState<string | null>(null)
  const [resModal, setResModal] = useState<number | null>(null)
  const [turnFlow, setTurnFlow] = useState<TurnFlow | null>(null)
  const [returns, setReturns] = useState<number[]>([0, 0, 0, 0, 0, 0])
  const [purchase, setPurchase] = useState<PurchaseTarget | null>(null)
  const [payGems, setPayGems] = useState<number[]>([0, 0, 0, 0, 0])
  const [claim, setClaim] = useState<string | null>(null)
  const [helpOpen, setHelpOpen] = useState(false)

  // Timer hooks run unconditionally (Rules of Hooks): the projected state may
  // arrive on a later render and the waiting screen must not change the hook count.
  const now = useNow()
  const gameRemainingMs = useCountdown(state.gameEndsAtUtc)

  // Any accepted action bumps the state version; staged interactions are then
  // stale, so they all reset (Silver precedent).
  useEffect(() => {
    setSel({})
    setHint(null)
    setCardModal(null)
    setResModal(null)
    setTurnFlow(null)
    setPurchase(null)
    setClaim(null)
  }, [state?.version])

  // Motion: after each resolved action, diff the projected state and fly the
  // changed tokens / cards / nobles from their source pile to their new home.
  const prevViewRef = useRef<SplendorState | null>(null)
  useEffect(() => {
    prevViewRef.current = null
    resetFlights()
  }, [session.id])
  useEffect(() => {
    const prev = prevViewRef.current
    prevViewRef.current = splendor
    if (!prev || !splendor) return
    const tok = (s: SplendorState, seat: number, k: GemKind): number => {
      const seatView = s.Seats[seat]
      if (!seatView) return 0
      return k === 'gold' ? seatView.Tokens.Gold : seatView.Tokens[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(k as GemColorName)] as 'Diamond']
    }
    const sup = (s: SplendorState, k: GemKind): number =>
      k === 'gold' ? s.Supply.Gold : s.Supply[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(k as GemColorName)] as 'Diamond']

    const flights: Flight[] = []
    for (const k of KINDS) {
      let delta = sup(splendor, k) - sup(prev, k)
      for (let i = 0; i < splendor.Seats.length; i++) {
        const seatDelta = tok(splendor, i, k) - tok(prev, i, k)
        if (seatDelta > 0 && delta < 0) {
          const n = Math.min(seatDelta, -delta)
          for (let j = 0; j < n; j++) flights.push({ kind: 'token', gem: k, fromKey: `supply:${k}`, toKey: `seat:${i}:tokens` })
          delta += n
        }
      }
      for (let i = 0; i < splendor.Seats.length; i++) {
        const seatDelta = tok(splendor, i, k) - tok(prev, i, k)
        if (seatDelta < 0 && delta > 0) {
          const n = Math.min(-seatDelta, delta)
          for (let j = 0; j < n; j++) flights.push({ kind: 'token', gem: k, fromKey: `seat:${i}:tokens`, toKey: `supply:${k}` })
          delta -= n
        }
      }
    }

    const prevSlot: Record<string, number> = {}
    prev.Market.forEach((id, idx) => {
      if (id) prevSlot[id] = idx
    })
    for (let i = 0; i < splendor.Seats.length; i++) {
      const prevPurchased = new Set(prev.Seats[i]?.Purchased ?? [])
      for (const id of splendor.Seats[i].Purchased) {
        if (prevPurchased.has(id)) continue
        const card = cardById(id)
        if (!card) continue
        const from = prevSlot[id] !== undefined ? `market:${prevSlot[id]}` : `deck:${card.tier}`
        flights.push({ kind: 'card', tier: card.tier, fromKey: from, toKey: `seat:${i}:tableau`, toFallback: `seat:${i}` })
      }
      const prevRes = prev.Seats[i]?.Reserved ?? []
      const nextRes = splendor.Seats[i].Reserved
      if (nextRes.length > prevRes.length) {
        const added = nextRes[nextRes.length - 1]
        const from =
          added.Source === 'Market' && added.CardId && prevSlot[added.CardId] !== undefined
            ? `market:${prevSlot[added.CardId]}`
            : `deck:${added.Tier}`
        flights.push({ kind: 'card', tier: added.Tier as 1 | 2 | 3, fromKey: from, toKey: `seat:${i}:reserved` })
      }
      const prevNobles = new Set(prev.Seats[i]?.NoblesOwned ?? [])
      for (const id of splendor.Seats[i].NoblesOwned) {
        if (prevNobles.has(id)) continue
        flights.push({ kind: 'noble', fromKey: `noble:${id}`, toKey: `seat:${i}:nobles`, toFallback: `seat:${i}` })
      }
    }
    runFlights(flights)
  }, [splendor])

  if (!splendor) {
    return (
      <div className="rounded-3xl border border-emerald-300/20 bg-gradient-to-b from-emerald-950 to-slate-950 py-20 text-center text-emerald-100/70">
        {t('splendor.waitingState')}
      </div>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const mySeat = meIndex >= 0 ? splendor.Seats[meIndex] ?? null : null
  const isMyTurn = meIndex >= 0 && splendor.CurrentPlayerIndex === meIndex && !state.isOver
  const eliminated = splendor.EliminatedSeats
  const iWasRemoved = !state.isOver && meIndex >= 0 && eliminated.includes(meIndex)

  const seatName = (i: number): string => {
    const uid = state.players[i]?.userId
    if (uid === userId) return t('common.you')
    return session.players.find((p) => p.userId === uid)?.displayName ?? t('game.playerPrefix', { id: String(i + 1) })
  }
  const gemName = (k: GemKind) => t(`splendor.gems.${k}`)
  const cardName = (id: string | null) => {
    const card = cardById(id)
    if (!card) return id ?? ''
    return t('splendor.cardName', { tier: t(`splendor.tierNames.T${card.tier}`), gem: gemName(gemKind(card.bonus)) })
  }
  const nobleName = (id: string) => {
    const noble = nobleById(id)
    if (!noble) return id
    return noble.requirement
      .map((r, i) => (r > 0 ? `${r} ${gemName(GEM_ORDER[i])}` : ''))
      .filter(Boolean)
      .join(' + ')
  }

  const send = (actionType: string, payload: Record<string, unknown>) => {
    if (isSending || !isMyTurn) return
    onAction(actionType, payload)
  }

  // --------------------------- take-gem staging ----------------------------
  // The action type is DERIVED from what the player taps: three distinct gems
  // stage TakeThreeGems, a second tap on one gem stages TakeTwoGems. Anything
  // the engine would reject is blocked here with a hint (display aid only -
  // the engine stays the sole enforcer).

  const supplyOf = (color: GemColorName) => splendor.Supply[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(color)] as 'Diamond']
  const colorsWithSupply = GEM_ORDER.filter((c) => supplyOf(c) > 0)
  const requiredTakeCount = Math.min(3, colorsWithSupply.length)
  const selColors = GEM_ORDER.filter((c) => (sel[c] ?? 0) > 0)
  const pairColor = GEM_ORDER.find((c) => (sel[c] ?? 0) === 2) ?? null
  const takeComplete = pairColor !== null || (selColors.length === requiredTakeCount && requiredTakeCount > 0)

  const clickColor = (color: GemColorName) => {
    setHint(null)
    const cur = sel[color] ?? 0
    if (cur === 0) {
      if (pairColor) {
        setHint(t('splendor.blockMixed'))
        return
      }
      if (selColors.length >= requiredTakeCount) {
        setHint(t('splendor.blockMax'))
        return
      }
      setSel({ ...sel, [color]: 1 })
    } else if (cur === 1) {
      if (selColors.length > 1) {
        setHint(t('splendor.blockMixed'))
        return
      }
      if (supplyOf(color) < 4) {
        setHint(t('splendor.needs4'))
        return
      }
      setSel({ [color]: 2 })
    } else {
      setSel({ ...sel, [color]: 1 })
    }
  }

  const beginTake = () => {
    if (!mySeat || !takeComplete) return
    if (pairColor) {
      const gained = [0, 0, 0, 0, 0, 0]
      gained[GEM_ORDER.indexOf(pairColor)] = 2
      setReturns([0, 0, 0, 0, 0, 0])
      setClaim(null)
      setTurnFlow({
        actionType: 'TakeTwoGems',
        core: splendorActions.takeTwo(pairColor),
        gained,
        summaryKey: 'splendor.summaryTakeTwo',
        summaryParams: { gem: gemName(pairColor), n: 2 },
      })
      return
    }
    const gained = [0, 0, 0, 0, 0, 0]
    selColors.forEach((c) => (gained[GEM_ORDER.indexOf(c)] += 1))
    setReturns([0, 0, 0, 0, 0, 0])
    setClaim(null)
    setTurnFlow({
      actionType: 'TakeThreeGems',
      core: splendorActions.takeThree(selColors),
      gained,
      summaryKey: 'splendor.summaryTakeThree',
      summaryParams: { gems: selColors.map(gemName).join(' + ') },
    })
  }

  const beginReserve = (target: { market: string } | { deck: number }) => {
    if (!mySeat) return
    const gained = [0, 0, 0, 0, 0, 0]
    if (splendor.Supply.Gold > 0) gained[5] = 1
    setReturns([0, 0, 0, 0, 0, 0])
    setClaim(null)
    if ('market' in target) {
      setTurnFlow({
        actionType: 'ReserveMarketCard',
        core: splendorActions.reserveMarket(target.market),
        gained,
        summaryKey: 'splendor.summaryReserveMarket',
        summaryParams: { card: cardName(target.market) },
      })
    } else {
      setTurnFlow({
        actionType: 'ReserveDeckCard',
        core: splendorActions.reserveDeck(target.deck),
        gained,
        summaryKey: 'splendor.summaryReserveDeck',
        summaryParams: { tier: t(`splendor.tierNames.T${target.deck}`) },
      })
    }
  }

  // Take/reserve actions leave bonuses untouched: eligibility is the viewer's
  // current one (carried-over eligibility included, spec §10).
  const flowEligible = mySeat ? eligibleNobles(splendor, mySeat) : []
  const flowOverflow = (() => {
    if (!turnFlow || !mySeat) return 0
    const gainedTotal = turnFlow.gained.reduce((a, b) => a + b, 0)
    return Math.max(0, tokenTotal(mySeat.Tokens) + gainedTotal - MAX_TOKENS)
  })()
  const projected = (() => {
    const held = mySeat
      ? [...GEM_ORDER.map((c) => mySeat.Tokens[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(c)] as 'Diamond']), mySeat.Tokens.Gold]
      : [0, 0, 0, 0, 0, 0]
    if (!turnFlow) return held
    return held.map((h, i) => h + turnFlow.gained[i])
  })()
  const returnsTotal = returns.reduce((a, b) => a + b, 0)
  const returnsOk = returnsTotal === flowOverflow && returns.every((r, i) => r >= 0 && r <= projected[i])

  const submitTurnFlow = () => {
    if (!turnFlow || !mySeat) return
    const ret: GemReturn[] | undefined =
      flowOverflow > 0
        ? returns.map((count, i) => ({ Color: GEM_PAYLOAD_NAMES[i], Count: count })).filter((r) => r.Count > 0)
        : undefined
    const claimNoble = flowEligible.length >= 2 ? claim ?? undefined : undefined
    send(turnFlow.actionType, {
      ...turnFlow.core,
      ...(ret ? { Return: ret } : {}),
      ...(claimNoble ? { ClaimNoble: claimNoble } : {}),
    })
    setTurnFlow(null)
  }

  // ----------------------------- purchase flow -----------------------------

  const purchaseCardId =
    purchase === null
      ? null
      : purchase.source === 'market'
        ? purchase.cardId
        : mySeat?.Reserved[purchase.index]?.Source === 'Market'
          ? mySeat.Reserved[purchase.index].CardId
          : null
  const purchaseCard = cardById(purchaseCardId)
  const purchaseRequired = purchaseCard && mySeat ? effectiveCost(purchaseCard, mySeat.Bonuses) : null
  const purchaseHeld = mySeat
    ? [...GEM_ORDER.map((c) => mySeat.Tokens[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(c)] as 'Diamond'])]
    : [0, 0, 0, 0, 0]
  const purchaseGoldShort = purchaseRequired
    ? purchaseRequired.reduce((sum, r, i) => sum + Math.max(0, r - payGems[i]), 0)
    : 0
  const purchaseValid =
    !!purchaseRequired &&
    purchaseGoldShort <= (mySeat?.Tokens.Gold ?? 0) &&
    payGems.every((p, i) => p >= 0 && p <= (purchaseRequired?.[i] ?? 0) && p <= purchaseHeld[i])

  const purchaseEligible = (() => {
    if (!mySeat || !purchaseCard) return []
    const override = [...mySeat.Bonuses]
    override[purchaseCard.bonus] += 1
    return eligibleNobles(splendor, mySeat, override)
  })()

  const openPurchase = (target: PurchaseTarget) => {
    if (!mySeat) return
    const card = cardById(
      target.source === 'market'
        ? target.cardId
        : mySeat.Reserved[target.index]?.Source === 'Market'
          ? mySeat.Reserved[target.index].CardId
          : null,
    )
    if (card) {
      const required = effectiveCost(card, mySeat.Bonuses)
      setPayGems(required.map((r, i) => Math.min(r, purchaseHeld[i])))
    } else {
      setPayGems([0, 0, 0, 0, 0])
    }
    setClaim(null)
    setPurchase(target)
  }

  const submitPurchase = () => {
    if (!purchase || !purchaseValid) return
    const payment: SplendorGems = { ...emptyPayment() }
    payGems.forEach((p, i) => {
      payment[GEM_PAYLOAD_NAMES[i] as 'Diamond'] = p
    })
    payment.Gold = purchaseGoldShort
    const claimNoble = purchaseEligible.length >= 2 ? claim ?? undefined : undefined
    if (purchase.source === 'market') {
      send('PurchaseMarketCard', splendorActions.purchaseMarket(purchase.cardId, payment, claimNoble))
    } else {
      send('PurchaseReservedCard', splendorActions.purchaseReserved(purchase.index, payment, claimNoble))
    }
    setPurchase(null)
  }

  const submitBlindAttempt = (index: number) => {
    // No cost preview exists for a blind reservation (spec §12): a payment of
    // all zeros succeeds exactly when the hidden effective cost is 0 (bonuses
    // cover it). A mismatch is rejected without revealing anything.
    send('PurchaseReservedCard', splendorActions.purchaseReserved(index, emptyPayment()))
  }

  const autoFill = () => {
    if (!purchaseCard || !mySeat || !purchaseRequired) return
    const payment = emptyPayment()
    let gold = 0
    purchaseRequired.forEach((r, i) => {
      const spend = Math.min(r, purchaseHeld[i])
      payment[GEM_PAYLOAD_NAMES[i] as 'Diamond'] = spend
      gold += r - spend
    })
    if (gold > mySeat.Tokens.Gold) return
    setPayGems(purchaseRequired.map((r, i) => Math.min(r, purchaseHeld[i])))
  }

  // ------------------------------ timers ------------------------------

  const turnStartMs = splendor.TurnStartUtc ? Date.parse(splendor.TurnStartUtc) : Number.NaN
  const currentTimer = (splendor.PlayerTimers ?? [])[splendor.CurrentPlayerIndex]
  const baseSeconds = splendor.TimerConfig?.BaseTurnSeconds ?? DEFAULT_TIMER.BaseTurnSeconds
  const maxBankSeconds = splendor.TimerConfig?.MaxBankSeconds ?? DEFAULT_TIMER.MaxBankSeconds
  const graceSeconds = splendor.TimerConfig?.MaxOverrunSeconds ?? DEFAULT_TIMER.MaxOverrunSeconds
  const allowanceSeconds = Math.max(
    Math.max(0, baseSeconds - graceSeconds),
    Math.min(maxBankSeconds, baseSeconds + (currentTimer?.BankSeconds ?? 0) - (currentTimer?.DeferredPenaltySeconds ?? 0)),
  )
  const softEndMs = Number.isNaN(turnStartMs) ? Number.NaN : turnStartMs + allowanceSeconds * 1000
  const signedRemainingMs = Number.isNaN(softEndMs) ? 0 : softEndMs - now
  const remainingSeconds = Math.max(0, Math.ceil(signedRemainingMs / 1000))
  const overtimeSeconds = signedRemainingMs < 0 ? Math.min(graceSeconds, Math.floor(-signedRemainingMs / 1000)) : 0
  const timerFraction = Math.max(0, Math.min(1, signedRemainingMs / 1000 / Math.max(1, allowanceSeconds)))
  const timerCritical = signedRemainingMs <= 0
  const gameRemainingMinutes = Math.floor(gameRemainingMs / 60000)
  const gameRemainingSeconds = Math.floor((gameRemainingMs % 60000) / 1000)
  const gameClockLabel =
    state.isOver || gameRemainingMs <= 0 ? null : `${gameRemainingMinutes}:${String(gameRemainingSeconds).padStart(2, '0')}`
  const gameClockCritical = gameRemainingMs <= 5 * 60 * 1000

  // ------------------------------ standings ------------------------------

  const winnerId = state.isOver ? state.winner?.userId : undefined
  const winnerSeat = state.players.findIndex((p) => p.userId === state.winner?.userId)
  const standings = state.players
    .map((p, i) => ({
      userId: p.userId,
      name: seatName(i),
      seat: i,
      vp: splendor.Seats[i]?.VictoryPoints ?? 0,
      cards: splendor.Seats[i]?.Purchased.length ?? 0,
      reserved: splendor.Seats[i]?.Reserved.length ?? 0,
      nobles: splendor.Seats[i]?.NoblesOwned.length ?? 0,
    }))
    .sort((a, b) => b.vp - a.vp || a.cards - b.cards || a.reserved - b.reserved)

  const viewerEligible = mySeat ? eligibleNobles(splendor, mySeat) : []
  const canReserve = !!mySeat && mySeat.Reserved.length < MAX_RESERVATIONS
  const myTurnIdle = isMyTurn && !turnFlow && !purchase && cardModal === null && resModal === null

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      {/* ============================ TABLE ============================ */}
      {/* Wooden rim */}
      <div className="relative rounded-[2rem] bg-gradient-to-br from-amber-800 via-yellow-950 to-stone-950 p-2.5 shadow-2xl sm:p-3.5">
        <div className="pointer-events-none absolute inset-0 rounded-[2rem] opacity-25 [background:repeating-linear-gradient(92deg,rgba(0,0,0,0.5)_0_3px,transparent_3px_12px)]" />
        <div className="pointer-events-none absolute inset-0 rounded-[2rem] shadow-[inset_0_1px_0_rgba(255,222,160,0.4),inset_0_-3px_8px_rgba(0,0,0,0.65)]" />
        {/* Felt playing surface */}
        <div className="relative overflow-hidden rounded-[1.5rem] border border-amber-200/25 bg-gradient-to-b from-emerald-950 via-teal-950 to-slate-950 px-4 py-4 shadow-[inset_0_3px_26px_rgba(0,0,0,0.7)] sm:px-5">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(251,191,36,0.07),transparent_55%)]" />
          <div className="pointer-events-none absolute inset-0 opacity-[0.05] [background-image:radial-gradient(rgba(255,255,255,0.8)_0.5px,transparent_0.5px)] [background-size:7px_7px]" />
          <div className="pointer-events-none absolute inset-2 rounded-[1.25rem] border border-amber-200/10" />

        {/* turn banner */}
        <div className="relative flex flex-wrap items-center justify-center gap-2">
          <span className="text-[11px] font-semibold uppercase tracking-widest text-amber-200/60">
            {t('splendor.turnOf', { n: splendor.TurnNumber })}
          </span>
          {splendor.FinalRoundTriggered && (
            <span className="rounded-full bg-amber-500/90 px-2.5 py-0.5 text-[11px] font-bold text-amber-950 shadow">
              {t('splendor.finalRound', { n: splendor.FinalTurnsRemaining })}
            </span>
          )}
        </div>

        {/* market + nobles side by side, like the physical setup */}
        <div className="relative mt-4 flex flex-col items-center justify-center gap-4 xl:flex-row xl:items-stretch">
          {/* market: level 3 at the back, level 1 nearest the player (rulebook stacking) */}
          <div className="relative space-y-2">
            {[3, 2, 1].map((tier) => {
            const deckCount = splendor.DeckCounts[tier - 1] ?? 0
            const slots = splendor.Market.slice((tier - 1) * 4, tier * 4)
            return (
              <div key={tier} className="flex items-center gap-2 sm:gap-4">
                <button
                  type="button"
                  ref={flightAnchor(`deck:${tier}`)}
                  disabled={!isMyTurn || !canReserve || deckCount === 0 || isSending}
                  onClick={() => beginReserve({ deck: tier })}
                  className={`group relative flex h-[10.7rem] w-[7.5rem] flex-col items-center justify-center ${
                    isMyTurn && canReserve && deckCount > 0 ? 'cursor-pointer' : 'cursor-default'
                  }`}
                  title={
                    deckCount === 0
                      ? t('splendor.deckEmpty')
                      : !canReserve
                        ? t('splendor.reservedFull')
                        : t('splendor.reserveDeckHint', { tier: t(`splendor.tierNames.T${tier}`) })
                  }
                  aria-label={t('splendor.reserveDeckHint', { tier: t(`splendor.tierNames.T${tier}`) })}
                >
                  <span className="relative block h-[10.1rem] w-[7.25rem]">
                    <span
                      aria-hidden
                      className="absolute inset-0 rounded-[10px] bg-slate-400"
                      style={{ transform: 'translate(2.5px, 4px)', boxShadow: '0 2px 3px rgba(0,0,0,0.55)' }}
                    />
                    <span
                      aria-hidden
                      className="absolute inset-0 rounded-[10px] border border-white/70 bg-slate-200"
                      style={{ transform: 'translate(1.2px, 2px)' }}
                    />
                    <SplendorCardBack
                      size="md"
                      tier={tier}
                      className={`relative transition-transform duration-150 ${
                        isMyTurn && canReserve && deckCount > 0
                          ? 'group-hover:-translate-y-1 group-hover:shadow-[0_0_16px_rgba(251,191,36,0.45)]'
                          : deckCount === 0
                            ? 'opacity-30 grayscale'
                            : ''
                      }`}
                    />
                    <span className="absolute -bottom-1.5 start-1/2 z-10 -translate-x-1/2 whitespace-nowrap rounded-full bg-black/75 px-1.5 py-px text-[9px] font-bold tabular-nums text-white">
                      {t('splendor.deckLeft', { n: deckCount })}
                    </span>
                  </span>
                </button>

                <div className="flex flex-1 flex-wrap items-center justify-center gap-2 sm:gap-2.5">
                  {slots.map((id, slot) => {
                    const card = cardById(id)
                    if (!card) {
                      return (
                        <div
                          key={`x${slot}`}
                          className="flex h-[10.1rem] w-[7.25rem] items-center justify-center rounded-lg border border-dashed border-white/15 text-[9px] text-white/25"
                          title={t('splendor.slotSpent')}
                        >
                          {t('splendor.slotSpent')}
                        </div>
                      )
                    }
                    const affordable = !!mySeat && canAfford(card, mySeat)
                    const isTarget = purchase?.source === 'market' && purchase.cardId === id
                    return (
                      <button
                        key={id}
                        type="button"
                        ref={flightAnchor(`market:${slot}`)}
                        style={{ animation: 'deal-pop 0.35s ease-out' }}
                        onClick={() => setCardModal(id)}
                        title={affordable && isMyTurn ? t('splendor.canAfford') : undefined}
                        className={`relative rounded-lg transition-transform duration-150 hover:-translate-y-1 hover:[transform:perspective(700px)_rotateX(7deg)_translateY(-4px)] ${
                          isMyTurn && affordable
                            ? 'a-card-glow ring-2 ring-emerald-300'
                            : isMyTurn && canReserve
                              ? 'ring-1 ring-amber-300/40'
                              : ''
                        } ${isTarget ? '-translate-y-1 ring-2 ring-amber-300' : ''}`}
                      >
                        <SplendorCardVisual card={card} size="md" className={MARKET_TILT[slot % 4]} />
                        {isMyTurn && affordable && (
                          <>
                            <span className="a-shine rounded-[12px]" />
                            <span className="absolute -start-1.5 -top-1.5 z-10 flex h-5 w-5 items-center justify-center rounded-full bg-emerald-500 text-white shadow ring-2 ring-white">
                              <CheckIcon className="h-3.5 w-3.5" />
                            </span>
                          </>
                        )}
                      </button>
                    )
                  })}
                </div>
              </div>
            )
          })}
          </div>

          {/* nobles: their own strip beside the market, as on the real table */}
          <div className="flex flex-shrink-0 flex-col items-center xl:justify-center">
            <p className="mb-2 text-[10px] font-semibold uppercase tracking-widest text-amber-200/50">
              {t('splendor.noblesTitle')}
            </p>
            <div className="flex flex-wrap items-center justify-center gap-2 xl:flex-col xl:items-center xl:gap-2.5">
              {splendor.NoblesInMarket.length === 0 ? (
                <span className="py-2 text-xs text-white/40">{t('splendor.noNoblesLeft')}</span>
              ) : (
                splendor.NoblesInMarket.map((id) => {
                  const noble = nobleById(id)
                  if (!noble) return null
                  return (
                    <span key={id} ref={flightAnchor(`noble:${id}`)} className="inline-block">
                      <SplendorNobleVisual noble={noble} size="sm" eligible={viewerEligible.includes(id)} />
                    </span>
                  )
                })
              )}
            </div>
          </div>
        </div>

        {/* token bank: recessed felt tray */}
        <div className="relative mt-5 rounded-[1.3rem] border border-amber-200/15 bg-black/25 p-3 shadow-[inset_0_3px_14px_rgba(0,0,0,0.6)]">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-[10px] font-semibold uppercase tracking-widest text-amber-200/50">{t('splendor.gemSupply')}</p>
          </div>
          <div className="mt-4 flex flex-wrap items-end justify-center gap-4 pb-1 sm:gap-7">
            {GEM_ORDER.map((color) => {
              const supply = supplyOf(color)
              const selected = sel[color] ?? 0
              const blockedByPair = pairColor !== null && selected === 0
              return (
                <button
                  key={color}
                  type="button"
                  ref={flightAnchor(`supply:${color}`)}
                  disabled={!myTurnIdle || supply === 0}
                  onClick={() => clickColor(color)}
                  className={`group flex flex-col items-center gap-1.5 transition-transform duration-150 ${
                    myTurnIdle && supply > 0 && !blockedByPair ? 'cursor-pointer hover:-translate-y-1.5' : 'cursor-default'
                  } ${selected > 0 ? '-translate-y-1' : ''}`}
                  title={
                    selected === 1 && selColors.length === 1 && supply >= 4 && requiredTakeCount > 1
                      ? t('splendor.pairOffer')
                      : supply > 0 && supply < 4
                        ? t('splendor.needs4')
                        : undefined
                  }
                >
                  <span
                    className={`relative block transition-shadow ${
                      selected === 2
                        ? 'drop-shadow-[0_0_12px_rgba(251,191,36,0.8)]'
                        : selected === 1
                          ? 'drop-shadow-[0_0_10px_rgba(110,231,183,0.7)]'
                          : ''
                    } ${blockedByPair ? 'opacity-40' : ''}`}
                  >
                    <GemPile color={color} count={supply} selected={selected === 2 ? 2 : selected === 1 ? 1 : 0} />
                    {selected === 2 && (
                      <span className="absolute -end-2 -top-1 z-10 flex h-5 w-5 items-center justify-center rounded-full bg-amber-400 text-[10px] font-black text-amber-950 shadow ring-2 ring-white">
                        x2
                      </span>
                    )}
                  </span>
                  <span className={`text-[11px] font-medium ${selected > 0 ? 'text-amber-100' : 'text-white/60'}`}>{gemName(color)}</span>
                </button>
              )
            })}
            <div ref={flightAnchor('supply:gold')} className="flex flex-col items-center gap-1.5 opacity-90" title={t('splendor.goldHint')}>
              <GemPile color="gold" count={splendor.Supply.Gold} />
              <span className="text-[11px] font-medium text-white/60">{gemName('gold')}</span>
            </div>
          </div>

            {myTurnIdle && selColors.length === 0 && !hint && (
              <p className="mt-2.5 text-center text-[11px] text-amber-100/60">{t('splendor.takeHint')}</p>
            )}

            {myTurnIdle && (selColors.length > 0 || hint) && (
              <div className="mt-3 flex flex-wrap items-center justify-center gap-3 border-t border-white/10 pt-3">
                {hint ? (
                  <p className="text-xs font-semibold text-rose-300">{hint}</p>
                ) : pairColor ? (
                  <p className="text-xs font-semibold text-amber-100">{t('splendor.pairSelected', { gem: gemName(pairColor) })}</p>
                ) : (
                  <p className="text-xs font-medium text-amber-100/80">
                    {t('splendor.selectedColors', { n: selColors.length, m: requiredTakeCount })}
                    {requiredTakeCount < 3 && <span className="ms-1.5 text-amber-200/50">{t('splendor.exactTake', { n: requiredTakeCount })}</span>}
                  </p>
                )}
                {takeComplete ? (
                  <Button size="sm" onClick={beginTake} isLoading={isSending}>
                    {t('splendor.continueBtn')}
                  </Button>
                ) : (
                  <span className="text-[11px] font-medium text-amber-200/60">
                    {selColors.length === 1 && selColors.every((c) => supplyOf(c) >= 4) && requiredTakeCount > 1
                      ? t('splendor.pairOffer')
                      : t('splendor.pickMore', { n: requiredTakeCount - selColors.length })}
                  </span>
                )}
              {selColors.length > 0 &&
                (() => {
                  const gained = pairColor ? 2 : selColors.length
                  const after = (mySeat ? tokenTotal(mySeat.Tokens) : 0) + gained
                  const over = Math.max(0, after - MAX_TOKENS)
                  return (
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold tabular-nums ring-1 ${
                        over > 0
                          ? 'bg-rose-500/15 text-rose-300 ring-rose-400/40'
                          : 'bg-emerald-500/10 text-emerald-200 ring-emerald-400/30'
                      }`}
                    >
                      {after}/{MAX_TOKENS}
                      {over > 0 && ` · ${t('splendor.mustReturn', { n: over })}`}
                    </span>
                  )
                })()}
              {selColors.length > 0 && (
                <button
                  type="button"
                  onClick={() => {
                    setSel({})
                    setHint(null)
                  }}
                  className="text-[11px] font-medium text-amber-200/60 underline decoration-dotted hover:text-white"
                >
                  {t('common.cancel')}
                </button>
              )}
            </div>
          )}
        </div>

        {/* own area first - no scrolling to reach your own deck */}
        {mySeat && meIndex >= 0 && (
          <SeatPanel
            seatIndex={meIndex}
            name={seatName(meIndex)}
            data={mySeat}
            isTurn={isMyTurn}
            removed={eliminated.includes(meIndex)}
            trigger={splendor.FinalRoundTriggered && splendor.TriggerSeat === meIndex}
            own
            cardName={cardName}
            gemName={gemName}
            t={t}
            onReservedClick={!state.isOver ? (index) => setResModal(index) : null}
          />
        )}

        {/* opponents: compact */}
        <div className="relative mt-3 flex flex-wrap justify-center gap-2 sm:gap-3">
          {state.players.map((player, i) => {
            if (i === meIndex) return null
            const seat = splendor.Seats[i]
            if (!seat) return null
            return (
              <SeatPanel
                key={player.userId}
                seatIndex={i}
                name={seatName(i)}
                data={seat}
                isTurn={splendor.CurrentPlayerIndex === i}
                removed={eliminated.includes(i)}
                trigger={splendor.FinalRoundTriggered && splendor.TriggerSeat === i}
                cardName={cardName}
                gemName={gemName}
                t={t}
                onReservedClick={null}
              />
            )
          })}
        </div>
        {meIndex < 0 && <p className="relative mt-6 text-center text-xs text-amber-100/60">{t('splendor.spectator')}</p>}
        </div>
      </div>

      {/* ============================ STATUS STRIP ============================ */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-2xl border border-gray-200 bg-white px-4 py-3 shadow-sm">
        {state.isOver ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-gray-500">
            <TrophyIcon className="h-4 w-4 text-amber-400" /> {t('splendor.gameOver')}
          </span>
        ) : isMyTurn ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-sm font-semibold text-emerald-700">
            <PlayIcon className="h-4 w-4" /> {t('splendor.yourTurn')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 text-sm text-gray-600">
            <ClockIcon className="h-4 w-4 text-gray-400" />
            {t('splendor.waitingFor', { name: seatName(splendor.CurrentPlayerIndex) })}
          </span>
        )}

        {!state.isOver && (
          <TimerRing seconds={remainingSeconds} overtimeSeconds={overtimeSeconds} fraction={timerFraction} critical={timerCritical} />
        )}

        {!state.isOver && gameClockLabel && (
          <div
            className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 ${gameClockCritical ? 'bg-rose-600/80' : 'bg-gray-100'}`}
            title={t('splendor.gameClockTitle')}
          >
            <ClockIcon className={`h-3.5 w-3.5 ${gameClockCritical ? 'text-white' : 'text-gray-500'}`} />
            <span className={`text-xs font-bold tabular-nums ${gameClockCritical ? 'text-white' : 'text-gray-500'}`}>{gameClockLabel}</span>
          </div>
        )}

        <div className="flex-1" />

        {mySeat && !state.isOver && (
          <span className="flex items-center gap-2" title={t('splendor.vpProgress')}>
            <span className="text-xs font-semibold tabular-nums text-gray-600">{t('splendor.vp', { n: mySeat.VictoryPoints })}</span>
            <span className="h-1.5 w-24 overflow-hidden rounded-full bg-gray-200">
              <span
                className="block h-full rounded-full bg-gradient-to-r from-emerald-400 to-teal-500 transition-all duration-500"
                style={{ width: `${Math.min(100, (mySeat.VictoryPoints / TRIGGER_POINTS) * 100)}%` }}
              />
            </span>
            <span className="text-[10px] font-medium tabular-nums text-gray-400">{TRIGGER_POINTS}</span>
          </span>
        )}

        <button
          type="button"
          onClick={() => setHelpOpen(true)}
          className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-600 transition-colors hover:bg-emerald-50 hover:text-emerald-700"
        >
          <QuestionMarkCircleIcon className="h-4 w-4" />
          {t('splendor.helpBtn')}
        </button>
      </div>

      {/* ============================ SCOREBOARD + LOG ============================ */}
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-gray-200 bg-white px-4 py-3 shadow-sm">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">{t('splendor.scoreboard')}</p>
          <ul className="space-y-1.5">
            {standings.map((s) => (
              <li
                key={s.userId}
                className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm ${s.userId === userId ? 'bg-emerald-50' : ''} ${
                  eliminated.includes(s.seat) ? 'opacity-50' : ''
                }`}
              >
                <span className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-100 text-[10px] font-bold text-emerald-700">
                  {s.name.charAt(0).toUpperCase()}
                </span>
                <span className="min-w-0 flex-1 truncate font-medium text-gray-700">{s.name}</span>
                {s.nobles > 0 && (
                  <span className="flex items-center gap-0.5 text-[10px] font-semibold text-amber-600" title={t('splendor.noblesOwned', { n: s.nobles })}>
                    <CrownGlyph className="h-3 w-3" />
                    {s.nobles}
                  </span>
                )}
                <span className="text-[11px] tabular-nums text-gray-400">{t('splendor.cards', { n: s.cards })}</span>
                <span className="text-[11px] tabular-nums text-gray-400">{t('splendor.reservedN', { n: s.reserved })}</span>
                <span className="w-8 text-end text-sm font-black tabular-nums text-gray-800">{s.vp}</span>
              </li>
            ))}
          </ul>
        </div>

        <details className="group rounded-2xl border border-gray-200 bg-white shadow-sm open:pb-2">
          <summary className="flex cursor-pointer list-none select-none items-center justify-between px-4 py-3">
            <span className="text-sm font-semibold text-gray-700">{t('splendor.gameLog')}</span>
            <span className="text-xs text-gray-400 group-open:hidden">{t('splendor.show')}</span>
            <span className="hidden text-xs text-gray-400 group-open:inline">{t('splendor.hide')}</span>
          </summary>
          <ul className="max-h-64 space-y-1 overflow-y-auto px-4 pb-3">
            {splendor.EventLog.length === 0 ? (
              <li className="py-4 text-center text-sm text-gray-400">{t('splendor.noEvents')}</li>
            ) : (
              splendor.EventLog.slice(-40)
                .reverse()
                .map((entry, i) => (
                  <li key={`${splendor.EventLog.length - i}`} className="flex items-start gap-2 text-xs text-gray-600">
                    <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-emerald-400" />
                    <span>{formatSplendorEvent(entry, t)}</span>
                  </li>
                ))
            )}
          </ul>
        </details>
      </div>

      {/* ============================ CARD DETAIL (market) ============================ */}
      <Modal isOpen={cardModal !== null} onClose={() => setCardModal(null)} title={t('splendor.marketCard')} className="max-w-xl">
        {(() => {
          const card = cardById(cardModal)
          if (!card || !mySeat) return null
          const affordable = canAfford(card, mySeat)
          const required = effectiveCost(card, mySeat.Bonuses)
          return (
            <div className="space-y-4">
              <div className="flex items-start gap-4">
                <SplendorCardVisual card={card} size="lg" />
                <div className="min-w-0 flex-1 space-y-2 text-sm text-gray-700">
                  <p className="font-semibold text-gray-900">{cardName(card.id)}</p>
                  <div className="flex flex-wrap gap-1.5">
                    {card.cost.map(
                      (c, i) =>
                        c > 0 && (
                          <span
                            key={i}
                            className={`flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ${
                              required[i] === 0
                                ? 'bg-emerald-100 text-emerald-700 line-through decoration-emerald-500/70'
                                : required[i] < c
                                  ? 'bg-amber-100 text-amber-800'
                                  : 'bg-gray-100 text-gray-700'
                            }`}
                          >
                            <GemIcon color={gemKind(i)} className="h-3.5 w-3.5" />
                            {required[i] === 0 ? c : required[i]}
                            {required[i] > 0 && required[i] < c && <span className="text-gray-400 line-through">/{c}</span>}
                          </span>
                        ),
                    )}
                    {card.cost.every((c) => c === 0) && <span className="text-xs font-semibold text-emerald-700">{t('splendor.freeCard')}</span>}
                  </div>
                  <p className={`text-xs font-semibold ${affordable ? 'text-emerald-700' : 'text-rose-600'}`}>
                    {affordable ? t('splendor.canAfford') : t('splendor.cannotAfford')}
                  </p>
                  <p className="text-xs text-gray-500">{t('splendor.reserveGoldNote', { n: splendor.Supply.Gold > 0 ? 1 : 0 })}</p>
                </div>
              </div>
              <div className="flex gap-3">
                <Button
                  variant="secondary"
                  className="flex-1"
                  disabled={!isMyTurn || !canReserve || eliminated.includes(meIndex)}
                  title={!canReserve ? t('splendor.reservedFull') : undefined}
                  onClick={() => {
                    setCardModal(null)
                    beginReserve({ market: card.id })
                  }}
                >
                  {t('splendor.reserveBtn')}
                </Button>
                <Button
                  variant="primary"
                  className="flex-1"
                  disabled={!isMyTurn || !affordable || eliminated.includes(meIndex)}
                  onClick={() => {
                    setCardModal(null)
                    openPurchase({ source: 'market', cardId: card.id })
                  }}
                >
                  {t('splendor.buyBtn')}
                </Button>
              </div>
              {!canReserve && isMyTurn && <p className="text-center text-xs text-amber-600">{t('splendor.reservedFull')}</p>}
            </div>
          )
        })()}
      </Modal>

      {/* ============================ RESERVED SLOT DETAIL ============================ */}
      <Modal isOpen={resModal !== null} onClose={() => setResModal(null)} title={t('splendor.reservationTitle')}>
        {(() => {
          if (resModal === null || !mySeat) return null
          const res = mySeat.Reserved[resModal]
          if (!res) return null
          const blind = res.Source === 'Deck'
          const card = cardById(res.CardId)
          const affordable = !!card && canAfford(card, mySeat)
          return (
            <div className="space-y-4">
              <div className="flex items-start gap-4">
                {card ? <SplendorCardVisual card={card} size="lg" /> : <SplendorCardBack size="lg" tier={res.Tier} />}
                <div className="min-w-0 flex-1 space-y-1.5 text-sm text-gray-700">
                  <p className="font-semibold text-gray-900">{blind ? t(`splendor.tierNames.T${res.Tier}`) : cardName(card?.id ?? null)}</p>
                  <p className="text-xs text-gray-500">{blind ? t('splendor.blindDetail') : t('splendor.publicReserved')}</p>
                  {card && (
                    <p className={`text-xs font-semibold ${affordable ? 'text-emerald-700' : 'text-rose-600'}`}>
                      {affordable ? t('splendor.canAfford') : t('splendor.cannotAfford')}
                    </p>
                  )}
                </div>
              </div>
              <Button
                variant={blind ? 'secondary' : 'primary'}
                className="w-full"
                disabled={!isMyTurn || eliminated.includes(meIndex) || (!blind && !affordable)}
                onClick={() => {
                  const index = resModal
                  setResModal(null)
                  if (blind) submitBlindAttempt(index)
                  else openPurchase({ source: 'reserved', index })
                }}
              >
                {blind ? t('splendor.buyBlindBtn') : t('splendor.buyBtn')}
              </Button>
              {blind && <p className="text-center text-xs text-gray-500">{t('splendor.blindBuyWarn')}</p>}
            </div>
          )
        })()}
      </Modal>

      {/* ============================ PURCHASE COMPOSER ============================ */}
      <Modal
        isOpen={purchase !== null && purchaseRequired !== null}
        onClose={() => setPurchase(null)}
        title={t('splendor.purchaseTitle', { card: cardName(purchaseCardId) })}
        className="max-w-2xl"
      >
        {purchaseRequired && purchaseCard && mySeat && (
          <div className="space-y-4">
            <div className="flex flex-col items-center gap-4 sm:flex-row sm:items-start">
              <div className="flex flex-col items-center gap-1.5">
                <SplendorCardVisual card={purchaseCard} size="lg" />
                <p className="text-center text-xs font-semibold text-gray-700">{cardName(purchaseCard.id)}</p>
              </div>
              <div className="min-w-0 flex-1 space-y-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-400">{t('splendor.paymentTitle')}</p>
                {purchaseRequired.some((r) => r > 0) ? (
                  purchaseRequired.map(
                    (r, i) =>
                      r > 0 && (
                        <TokenMoveRow
                          key={i}
                          kind={gemKind(i)}
                          caption={t('splendor.needLabel', { n: r })}
                          inHand={purchaseHeld[i]}
                          moved={payGems[i]}
                          max={Math.min(r, purchaseHeld[i])}
                          disabled={isSending}
                          onMove={(v) => {
                            const next = [...payGems]
                            next[i] = v
                            setPayGems(next)
                          }}
                        />
                      ),
                  )
                ) : (
                  <p className="rounded-xl bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700 ring-1 ring-emerald-200">
                    {t('splendor.freeCard')}
                  </p>
                )}
                {purchaseRequired.some((r) => r > 0) &&
                  purchaseRequired.map(
                    (r, i) =>
                      r === 0 &&
                      purchaseCard.cost[i] > 0 && (
                        <p key={i} className="px-1 text-[10px] font-medium text-emerald-600">
                          {gemName(gemKind(i))}: {t('splendor.bonusCovered')}
                        </p>
                      ),
                  )}
                {purchaseGoldShort > 0 && (
                  <div className="flex items-center gap-2 rounded-xl bg-amber-50 px-2.5 py-2 ring-1 ring-amber-200">
                    <GemIcon color="gold" className="h-6 w-6" />
                    <p className="flex-1 text-xs font-semibold text-amber-800">{t('splendor.goldWillCover', { n: purchaseGoldShort })}</p>
                    <GemChip color="gold" count={mySeat.Tokens.Gold} size="sm" dimmed={purchaseGoldShort > mySeat.Tokens.Gold} />
                  </div>
                )}
                <p className={`px-1 text-xs font-semibold ${purchaseValid ? 'text-emerald-700' : 'text-rose-600'}`}>
                  {purchaseValid
                    ? t('splendor.canAfford')
                    : `${t('splendor.cannotAfford')}${
                        purchaseGoldShort > (mySeat?.Tokens.Gold ?? 0)
                          ? ` · ${t('splendor.shortBy', { n: purchaseGoldShort - (mySeat?.Tokens.Gold ?? 0) })}`
                          : ''
                      }`}
                </p>
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={autoFill}
                    className="text-xs font-semibold text-emerald-700 underline decoration-dotted underline-offset-2 hover:text-emerald-900"
                  >
                    {t('splendor.autoFill')}
                  </button>
                  <button
                    type="button"
                    onClick={() => setPayGems([0, 0, 0, 0, 0])}
                    className="text-xs font-medium text-gray-400 underline decoration-dotted underline-offset-2 hover:text-rose-600"
                  >
                    {t('splendor.resetBtn')}
                  </button>
                  <span className="text-[11px] text-gray-400">{t('splendor.tapToPay')}</span>
                </div>
              </div>
            </div>

            {purchaseEligible.length >= 2 && <NoblePicker nobles={purchaseEligible} value={claim} onChange={setClaim} nobleName={nobleName} t={t} required />}
            {purchaseEligible.length === 1 && (
              <p className="rounded-lg bg-amber-50 px-3 py-2 text-center text-xs font-medium text-amber-800">
                {t('splendor.nobleAuto', { noble: nobleName(purchaseEligible[0]) })}
              </p>
            )}

            <div className="flex gap-3">
              <Button variant="secondary" className="flex-1" onClick={() => setPurchase(null)}>
                {t('common.cancel')}
              </Button>
              <Button
                variant="primary"
                className="flex-1"
                isLoading={isSending}
                disabled={!purchaseValid || (purchaseEligible.length >= 2 && !claim)}
                onClick={submitPurchase}
              >
                {t('splendor.buyBtn')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {/* ============================ TAKE/RESERVE CONFIRM ============================ */}
      <Modal isOpen={turnFlow !== null} onClose={() => setTurnFlow(null)} title={t('splendor.confirmTurnTitle')} className="max-w-xl">
        {turnFlow && mySeat && (
          <div className="space-y-4">
            <p className="rounded-lg bg-emerald-50 px-3 py-2 text-center text-sm font-semibold text-emerald-800">
              {t(turnFlow.summaryKey, turnFlow.summaryParams)}
              {turnFlow.gained[5] > 0 && <span className="ms-1 text-amber-600">· +1 {gemName('gold')}</span>}
              <span
                className={`ms-2 rounded-full px-2 py-0.5 text-[11px] font-bold tabular-nums ${
                  flowOverflow > 0 ? 'bg-rose-100 text-rose-700' : 'bg-emerald-100 text-emerald-700'
                }`}
              >
                {(mySeat ? tokenTotal(mySeat.Tokens) : 0) + turnFlow.gained.reduce((a, b) => a + b, 0)}/{MAX_TOKENS}
              </span>
              {turnFlow.gained[5] === 0 && turnFlow.actionType.startsWith('Reserve') && (
                <span className="ms-1 text-[11px] font-medium text-gray-400">({t('splendor.noGold')})</span>
              )}
            </p>

            {flowOverflow > 0 && (
              <div>
                <p className="mb-2 flex items-center justify-between text-xs font-semibold text-gray-500">
                  <span>{t('splendor.returnTitle', { n: flowOverflow })}</span>
                  <span className={`font-bold tabular-nums ${returnsTotal === flowOverflow ? 'text-emerald-600' : 'text-rose-600'}`}>
                    ({returnsTotal}/{flowOverflow})
                  </span>
                </p>
                <div className="space-y-1.5">
                  {KINDS.map(
                    (kind, i) =>
                      projected[i] > 0 && (
                        <TokenMoveRow
                          key={kind}
                          kind={kind}
                          caption={t('splendor.upToLabel', { n: Math.min(projected[i], flowOverflow) })}
                          inHand={projected[i]}
                          moved={returns[i]}
                          max={Math.min(projected[i], flowOverflow - returnsTotal + returns[i])}
                          disabled={isSending}
                          trayRing="amber"
                          onMove={(v) => {
                            const next = [...returns]
                            next[i] = v
                            setReturns(next)
                          }}
                        />
                      ),
                  )}
                </div>
                <div className="mt-1.5 flex items-center justify-between">
                  <p className="text-[10px] text-gray-400">{t('splendor.tapToReturn')}</p>
                  <button
                    type="button"
                    onClick={() => setReturns([0, 0, 0, 0, 0, 0])}
                    className="flex items-center gap-1 text-[11px] font-medium text-gray-400 hover:text-rose-600"
                  >
                    <TrashIcon className="h-3.5 w-3.5" /> {t('splendor.clearReturns')}
                  </button>
                </div>
              </div>
            )}

            {flowEligible.length >= 2 && <NoblePicker nobles={flowEligible} value={claim} onChange={setClaim} nobleName={nobleName} t={t} required />}
            {flowEligible.length === 1 && (
              <p className="rounded-lg bg-amber-50 px-3 py-2 text-center text-xs font-medium text-amber-800">
                {t('splendor.nobleAuto', { noble: nobleName(flowEligible[0]) })}
              </p>
            )}

            <div className="flex gap-3">
              <Button variant="secondary" className="flex-1" onClick={() => setTurnFlow(null)}>
                {t('common.cancel')}
              </Button>
              <Button
                variant="primary"
                className="flex-1"
                isLoading={isSending}
                disabled={(flowOverflow > 0 && !returnsOk) || (flowEligible.length >= 2 && !claim)}
                onClick={submitTurnFlow}
              >
                {t('splendor.endTurnBtn')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {/* ============================ HELP ============================ */}
      <Modal isOpen={helpOpen} onClose={() => setHelpOpen(false)} title={t('splendor.helpTitle')}>
        <ul className="space-y-3">
          {[
            { icon: <GemIcon color="emerald" className="h-5 w-5" />, text: t('splendor.rule1') },
            { icon: <SparklesIcon className="h-5 w-5 text-amber-500" />, text: t('splendor.rule2') },
            { icon: <HandThumbUpIcon className="h-5 w-5 text-emerald-600" />, text: t('splendor.rule3') },
            { icon: <CrownGlyph className="h-5 w-5 text-amber-600" />, text: t('splendor.rule4') },
            { icon: <TrophyIcon className="h-5 w-5 text-amber-500" />, text: t('splendor.rule5') },
          ].map((row, i) => (
            <li key={i} className="flex items-start gap-3 text-sm text-gray-700">
              <span className="mt-0.5 flex-shrink-0">{row.icon}</span>
              {row.text}
            </li>
          ))}
        </ul>
      </Modal>

      {/* ============================ GAME OVER ============================ */}
      <Modal isOpen={state.isOver} onClose={() => {}} title={t('splendor.gameOverModalTitle')}>
        <div className="py-4 text-center">
          <TrophyIcon className="mx-auto h-12 w-12 text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">
            {winnerId ? t('splendor.wins', { name: seatName(winnerSeat) }) : t('splendor.tie')}
          </p>
          {!winnerId && <p className="mt-1 text-sm text-gray-600">{t('splendor.tieDetail')}</p>}
          <p className="mt-4 text-xs font-semibold uppercase tracking-wide text-gray-400">{t('splendor.standings')}</p>
          <ul className="mt-2 space-y-1">
            {standings.map((s, rank) => (
              <li
                key={s.userId}
                className={`flex items-center justify-between rounded-lg px-3 py-1.5 text-sm ${
                  s.userId === winnerId ? 'bg-amber-50 font-semibold text-amber-800' : 'bg-gray-50 text-gray-600'
                }`}
              >
                <span>
                  {rank + 1}. {s.name}
                </span>
                <span className="flex items-center gap-2 text-xs tabular-nums">
                  <span className="text-gray-400">{t('splendor.cards', { n: s.cards })}</span>
                  <span className="font-black text-gray-800">{s.vp} VP</span>
                </span>
              </li>
            ))}
          </ul>
          <div className="mt-4">
            <Button variant="primary" asChild>
              <a href="/lobby">{t('common.backToLobby')}</a>
            </Button>
          </div>
        </div>
      </Modal>

      {/* ============================ AFK ============================ */}
      <Modal isOpen={iWasRemoved} onClose={() => {}} title={t('splendor.removedFromGame')}>
        <div className="py-4 text-center">
          <ClockIcon className="mx-auto h-12 w-12 text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">{t('splendor.afkTitle')}</p>
          <p className="mt-1 text-sm text-gray-600">{t('splendor.afkDetail')}</p>
          <div className="mt-4">
            <Button variant="primary" asChild>
              <a href="/lobby">{t('common.backToLobby')}</a>
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}

// ================================ pieces ================================

/**
 * Treasury picker row: the left chip is what's still in your pocket, the
 * right chip is what you've moved into the payment/return tray - tap either
 * side to move one token across.
 */
function TokenMoveRow({
  kind,
  caption,
  inHand,
  moved,
  max,
  onMove,
  disabled,
  trayRing = 'emerald',
}: {
  kind: GemKind
  caption: string
  inHand: number
  moved: number
  max: number
  onMove: (v: number) => void
  disabled?: boolean
  trayRing?: 'emerald' | 'amber'
}) {
  const { t } = useI18n()
  const canAdd = !disabled && moved < max
  const trayCls = trayRing === 'amber' ? 'ring-2 ring-amber-400' : 'ring-2 ring-emerald-400'
  return (
    <div className="flex items-center gap-2 rounded-xl bg-gray-50 px-2.5 py-1.5 ring-1 ring-gray-200">
      <div className="min-w-0 flex-1 leading-tight">
        <p className="text-xs font-bold text-gray-800">{t(`splendor.gems.${kind}`)}</p>
        <p className="text-[10px] tabular-nums text-gray-500">{caption}</p>
      </div>
      <button
        type="button"
        disabled={!canAdd}
        onClick={() => onMove(moved + 1)}
        title={t('splendor.tapToPay')}
        aria-label={t('splendor.tapToPay')}
        className={`rounded-full ${canAdd ? 'transition-transform hover:-translate-y-0.5 hover:shadow-[0_0_10px_rgba(16,185,129,0.4)]' : ''}`}
      >
        <GemChip color={kind} count={inHand - moved} size="sm" className={canAdd ? '' : 'opacity-40'} />
      </button>
      <span className="text-xs text-gray-300" aria-hidden="true">
        |
      </span>
      <button
        type="button"
        disabled={moved === 0 || disabled}
        onClick={() => onMove(moved - 1)}
        title={t('splendor.tapToUndo')}
        aria-label={t('splendor.tapToUndo')}
        className={`rounded-full ${moved > 0 ? 'transition-transform hover:-translate-y-0.5' : ''}`}
      >
        <GemChip color={kind} count={moved} size="sm" className={moved > 0 ? trayCls : 'opacity-40'} />
      </button>
    </div>
  )
}

function NoblePicker({
  nobles,
  value,
  onChange,
  nobleName,
  t,
  required,
}: {
  nobles: string[]
  value: string | null
  onChange: (id: string | null) => void
  nobleName: (id: string) => string
  t: TFn
  required?: boolean
}) {
  return (
    <div className={`rounded-xl p-3 ${required ? 'bg-amber-50 ring-1 ring-amber-200' : 'bg-gray-50'}`}>
      <p className="mb-2 text-xs font-semibold text-amber-800">{t('splendor.chooseNobleTitle')}</p>
      <div className="flex flex-wrap gap-2">
        {nobles.map((id) => {
          const noble = nobleById(id)
          if (!noble) return null
          const active = value === id
          return (
            <button
              key={id}
              type="button"
              onClick={() => onChange(active ? null : id)}
              className={`relative rounded-xl transition-transform hover:-translate-y-0.5 ${
                active ? 'ring-2 ring-amber-500 shadow-[0_0_14px_rgba(245,158,11,0.5)]' : ''
              }`}
              title={nobleName(id)}
            >
              <SplendorNobleVisual noble={noble} size="sm" eligible />
              {active && (
                <span className="absolute -end-1.5 -top-1.5 z-10 flex h-5 w-5 items-center justify-center rounded-full bg-amber-500 text-white shadow ring-2 ring-white">
                  <CheckIcon className="h-3.5 w-3.5" />
                </span>
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}

/** One seat's public area: identity, tokens, bonuses, reservations and tableau. */
function SeatPanel({
  seatIndex,
  name,
  data,
  isTurn,
  removed,
  trigger,
  own = false,
  cardName,
  gemName,
  t,
  onReservedClick,
}: {
  seatIndex: number
  name: string
  data: SplendorSeatView
  isTurn: boolean
  removed: boolean
  trigger: boolean
  own?: boolean
  cardName: (id: string | null) => string
  gemName: (k: GemKind) => string
  t: TFn
  onReservedClick: ((index: number) => void) | null
}) {
  const held = [...GEM_ORDER.map((c) => data.Tokens[GEM_PAYLOAD_NAMES[GEM_ORDER.indexOf(c)] as 'Diamond']), data.Tokens.Gold]
  const byColor: Map<number, SplendorCardDef[]> = new Map()
  for (const id of data.Purchased) {
    const card = cardById(id)
    if (!card) continue
    const list = byColor.get(card.bonus) ?? []
    list.push(card)
    byColor.set(card.bonus, list)
  }

  return (
    <div
      ref={flightAnchor(`seat:${seatIndex}`)}
      className={`relative w-full rounded-2xl border p-3 backdrop-blur-sm transition-all sm:w-auto ${
        removed
          ? 'border-white/10 bg-white/5 opacity-50'
          : isTurn
            ? 'border-amber-300/70 bg-white/10 shadow-[0_0_22px_rgba(251,191,36,0.25)] ring-2 ring-amber-300/40'
            : 'border-white/15 bg-white/5'
      } ${own ? 'mt-4 border-amber-200/30 bg-white/[0.07]' : 'min-w-[12rem] flex-1 sm:flex-none'}`}
    >
      {trigger && (
        <span className="absolute -top-2 start-3 z-10 rounded-full bg-amber-500 px-2 py-px text-[9px] font-bold uppercase text-amber-950 shadow">
          {t('splendor.triggerSeat')}
        </span>
      )}
      <div className="flex items-center gap-2.5">
        <div
          className={`flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full text-sm font-bold text-white shadow ${
            removed ? 'bg-gray-500' : isTurn ? 'bg-amber-500' : 'bg-emerald-700'
          }`}
        >
          {name.charAt(0).toUpperCase()}
        </div>
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <p className="max-w-32 truncate text-xs font-semibold leading-tight text-white">{name}</p>
            {data.NoblesOwned.length > 0 && (
              <span
                ref={flightAnchor(`seat:${seatIndex}:nobles`)}
                className="flex items-center gap-0.5 text-[10px] font-bold text-amber-300"
                title={t('splendor.noblesOwned', { n: data.NoblesOwned.length })}
              >
                <CrownGlyph className="h-3 w-3" />
                {data.NoblesOwned.length}
              </span>
            )}
            {removed && <span className="text-[10px] font-medium text-white/50">{t('splendor.eliminated')}</span>}
          </div>
          <div className="flex items-center gap-2">
            <span className="text-[10px] font-bold tabular-nums text-amber-200/90">{t('splendor.vp', { n: data.VictoryPoints })}</span>
            <span className="text-[10px] font-medium tabular-nums text-white/50">{t('splendor.cards', { n: data.Purchased.length })}</span>
          </div>
        </div>
        <div className="ms-auto flex flex-wrap items-center justify-end gap-1">
          {data.Bonuses.map((b, i) =>
            b > 0 ? (
              <span key={i} className="flex items-center gap-0.5" title={`${gemName(GEM_ORDER[i])} x${b}`}>
                <BonusChip colorIndex={i} />
                <span className="text-[9px] font-bold tabular-nums text-white/80">{b}</span>
              </span>
            ) : null,
          )}
        </div>
      </div>

      {/* tokens */}
      <div ref={flightAnchor(`seat:${seatIndex}:tokens`)} className="mt-2 flex flex-wrap items-center gap-1.5">
        {KINDS.map((kind, i) =>
          held[i] > 0 ? <GemChip key={kind} color={kind} count={held[i]} size={own ? 'lg' : 'sm'} /> : null,
        )}
        {held.every((n) => n === 0) && <span className="text-[10px] text-white/35">{t('splendor.noTokens')}</span>}
      </div>

      {/* reservations */}
      <div ref={flightAnchor(`seat:${seatIndex}:reserved`)} className="mt-2 flex items-center gap-1">
        <span className="me-1 text-[9px] font-semibold uppercase tracking-wide text-white/40">{t('splendor.reservedLabel')}</span>
        {Array.from({ length: MAX_RESERVATIONS }, (_, i) => {
          const res = data.Reserved[i]
          if (!res) {
            return (
              <span
                key={i}
                className={`flex items-center justify-center rounded-md border border-dashed border-white/15 ${
                  own ? 'h-[6.7rem] w-[4.8rem]' : 'h-[4.2rem] w-12'
                }`}
                title={t('splendor.emptyReservation')}
              >
                <span className="h-px w-3 bg-white/30" />
              </span>
            )
          }
          const card = cardById(res.CardId)
          const buyable = !!card && !!onReservedClick && isTurn && canAfford(card, data)
          return (
            <button
              key={i}
              type="button"
              disabled={onReservedClick === null}
              onClick={() => onReservedClick?.(i)}
              title={buyable ? t('splendor.canAfford') : card ? cardName(card.id) : t('splendor.blindReserved')}
              className={`relative rounded-md transition-transform ${
                onReservedClick ? 'hover:-translate-y-0.5' : ''
              } ${buyable ? 'a-card-glow ring-2 ring-emerald-300' : ''}`}
            >
              {card ? (
                <SplendorCardVisual card={card} size={own ? 'sm' : 'xs'} />
              ) : (
                <SplendorCardBack size={own ? 'sm' : 'xs'} tier={res.Tier} />
              )}
              {buyable && (
                <>
                  <span className="a-shine rounded-[10px]" />
                  <span className="absolute -end-1 -top-1 z-10 flex h-4 w-4 items-center justify-center rounded-full bg-emerald-500 text-white shadow ring-2 ring-white">
                    <CheckIcon className="h-2.5 w-2.5" />
                  </span>
                </>
              )}
            </button>
          )
        })}
      </div>

      {/* tableau: full-size cascaded stacks for the viewer, compact per-color chips for opponents */}
      {data.Purchased.length > 0 && own && (
        <div ref={flightAnchor(`seat:${seatIndex}:tableau`)} className="mt-3 flex flex-wrap items-start gap-x-3 gap-y-2">
          {[...byColor.entries()]
            .sort((a, b) => a[0] - b[0])
            .map(([bonusIdx, group]) => {
            const off = own ? 16 : 11
            return (
              <div key={bonusIdx} className="flex flex-col items-center gap-1">
                <div
                  className="relative"
                  style={{ width: 116, height: 162 + (group.length - 1) * off }}
                >
                  {group.map((c, j) => (
                    <div
                      key={c.id}
                      className="absolute left-0"
                      style={{ top: j * off, zIndex: j, transform: `rotate(${j % 2 === 0 ? -0.6 : 0.6}deg)` }}
                      title={cardName(c.id)}
                    >
                      <SplendorCardVisual card={c} size="md" />
                    </div>
                  ))}
                </div>
                <span
                  className="flex items-center gap-1 rounded-full bg-black/45 px-2 py-0.5 text-[10px] font-bold tabular-nums text-white ring-1 ring-white/10"
                  title={`${gemName(GEM_ORDER[bonusIdx])} x${group.length}`}
                >
                  <BonusChip colorIndex={bonusIdx} />
                  x{group.length}
                  <span className="text-amber-300 font-black">{group.reduce((s, c) => s + c.points, 0)}</span>
                </span>
              </div>
            )
          })}
        </div>
      )}
      {data.Purchased.length > 0 && !own && (
        <div ref={flightAnchor(`seat:${seatIndex}:tableau`)} className="mt-2 flex flex-wrap gap-1">
          {[...byColor.entries()]
            .sort((a, b) => a[0] - b[0])
            .map(([bonusIdx, group]) => (
              <span
                key={bonusIdx}
                className="flex items-center gap-1 rounded-md bg-black/25 px-1 py-0.5"
                title={`${gemName(GEM_ORDER[bonusIdx])} x${group.length}`}
              >
                <BonusChip colorIndex={bonusIdx} />
                <span className="flex flex-wrap gap-px">
                  {group.map((c) => (
                    <span key={c.id} title={cardName(c.id)}>
                      <SplendorCardVisual card={c} size="xs" dimmed />
                    </span>
                  ))}
                </span>
              </span>
            ))}
        </div>
      )}
    </div>
  )
}

/** Circular countdown ring for the current turn; shows overtime as negative. */
function TimerRing({
  seconds,
  overtimeSeconds,
  fraction,
  critical,
}: {
  seconds: number
  overtimeSeconds: number
  fraction: number
  critical: boolean
}) {
  const { t } = useI18n()
  const R = 22
  const C = 2 * Math.PI * R
  return (
    <div
      className={`relative h-12 w-12 flex-shrink-0 ${overtimeSeconds > 0 ? 'animate-pulse' : ''}`}
      title={overtimeSeconds > 0 ? t('splendor.overtimeTooltip', { n: overtimeSeconds }) : t('splendor.turnTooltip', { s: seconds })}
    >
      <svg viewBox="0 0 56 56" className="h-12 w-12 -rotate-90">
        <circle cx="28" cy="28" r={R} fill="none" strokeWidth="5" className="stroke-gray-200" />
        <circle
          cx="28"
          cy="28"
          r={R}
          fill="none"
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={C}
          strokeDashoffset={C * (1 - fraction)}
          className={critical ? 'stroke-red-500' : 'stroke-emerald-500'}
          style={{ transition: 'stroke-dashoffset 0.25s linear' }}
        />
      </svg>
      <span
        className={`absolute inset-0 flex items-center justify-center text-sm font-bold tabular-nums ${
          overtimeSeconds > 0 ? 'text-red-600' : critical ? 'text-red-500' : 'text-gray-700'
        }`}
      >
        {overtimeSeconds > 0 ? `-${overtimeSeconds}` : seconds}
      </span>
    </div>
  )
}
