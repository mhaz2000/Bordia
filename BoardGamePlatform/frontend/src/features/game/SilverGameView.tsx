import { useEffect, useMemo, useRef, useState } from 'react'
import {
  CheckIcon,
  ClockIcon,
  EyeIcon,
  HandRaisedIcon,
  MoonIcon,
  PlayIcon,
  QuestionMarkCircleIcon,
  ShieldCheckIcon,
  TrophyIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline'
import { MoonIcon as MoonSolid } from '@heroicons/react/24/solid'
import type { GameSession, GameState } from '@/shared/api/game'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { useCountdown, useNow } from '@/shared/hooks/useCountdown'
import { SilverCardVisual, SilverCardBack } from '@/shared/components/SilverCardVisual'
import { useI18n } from '@/i18n/I18nProvider'
import {
  ABILITY_BY_VALUE,
  cardNameKey,
  countFaceUp,
  enchanterUsesLeft,
  formatSilverEvent,
  matchStatus,
  ownerMovableSlots,
  parseSilverState,
  silverActions,
  type SilverViewCard,
} from './silver'

interface SilverGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

type ExchangeMode = {
  kind: 'drawn' | 'taken' | 'witch'
  slots: number[]
  newAtEnd: boolean
  penaltyAtEnd: boolean
}

type Targeting =
  | { kind: 'amulet' }
  | { kind: 'enchanter' }
  | { kind: 'guard'; guardSlot: number | null }
  | { kind: 'exposer' }
  | { kind: 'revealer' }
  | { kind: 'apprenticeSeer'; slots: number[] }
  | { kind: 'seer' }
  | { kind: 'beholder' }
  | { kind: 'witchTarget' }
  | { kind: 'robber'; ownSlot: number | null }
  | { kind: 'master'; slots: number[] }

export function SilverGameView({ state, session, userId, onAction, isSending }: SilverGameViewProps) {
  const { t } = useI18n()
  const silver = useMemo(() => parseSilverState(state), [state])

  const [exchange, setExchange] = useState<ExchangeMode | null>(null)
  const [targeting, setTargeting] = useState<Targeting | null>(null)
  const [confirmDraw, setConfirmDraw] = useState(false)
  const [tricksterExtra, setTricksterExtra] = useState(0)
  const [confirmTake, setConfirmTake] = useState<{ kind: 'discard' } | { kind: 'squire'; index: number } | null>(null)
  const [confirmCensus, setConfirmCensus] = useState(false)
  const [masterModal, setMasterModal] = useState(false)
  const [helpOpen, setHelpOpen] = useState(false)
  // Round-summary modal: opens when the authoritative round counter rolls
  // over (scoring already happened and the next round is live underneath).
  const [summaryRound, setSummaryRound] = useState<number | null>(null)
  const seenRoundRef = useRef<number | null>(null)

  useEffect(() => {
    if (!silver) return
    const prev = seenRoundRef.current
    seenRoundRef.current = silver.Round
    if (prev !== null && silver.Round > prev && !state.isOver) {
      setSummaryRound(silver.Round - 1)
    }
  }, [silver, state.isOver])

  // Any accepted action produces a new state version; interaction modes are
  // then stale, so they reset.
  useEffect(() => {
    setExchange(null)
    setTargeting(null)
    setMasterModal(false)
  }, [state?.version])

  if (!silver) {
    return (
      <div className="rounded-3xl border border-indigo-300/30 bg-gradient-to-b from-indigo-950 to-slate-950 py-20 text-center text-indigo-200/70">
        {t('silver.waitingState')}
      </div>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const myVillage: SilverViewCard[] = meIndex >= 0 ? silver.Villages[meIndex] ?? [] : []
  const isMyTurn = meIndex >= 0 && meIndex === silver.CurrentPlayerIndex
  const eliminated = silver.EliminatedPlayerIndexes
  const iWasRemoved = !state.isOver && meIndex >= 0 && eliminated.includes(meIndex)
  const isChooserPrompt = meIndex >= 0 && silver.RevealerChooserSeat === meIndex && !state.isOver

  const seatName = (i: number): string => {
    const uid = state.players[i]?.userId
    if (uid === userId) return t('common.you')
    return session.players.find((p) => p.userId === uid)?.displayName ?? t('game.playerPrefix', { id: String(i + 1) })
  }
  const seatNames = state.players.map((_, i) => session.players.find((p) => p.userId === state.players[i].userId)?.displayName ?? `#${i + 1}`)

  const hasMovableSlot = ownerMovableSlots(myVillage).length > 0
  const canPeek =
    meIndex >= 0 &&
    !state.isOver &&
    silver.ViewerPeeksUsed < 2 &&
    myVillage.some((c) => !c.FaceUp && c.Value === null && !c.AmuletProtected)

  const canDraw =
    isMyTurn && silver.Phase === 'TurnStart' && silver.PendingDraw.length === 0 && silver.DeckSize > 0
  const tricksters = countFaceUp(myVillage, 4)
  const canTakeDiscard =
    isMyTurn &&
    silver.Phase === 'TurnStart' &&
    silver.PendingDraw.length === 0 &&
    silver.DiscardTop !== null &&
    hasMovableSlot
  const canTakeSquire =
    isMyTurn &&
    silver.Phase === 'TurnStart' &&
    silver.PendingDraw.length === 0 &&
    silver.Display.length > 0 &&
    hasMovableSlot
  const canCallCensus =
    isMyTurn &&
    silver.Phase === 'TurnStart' &&
    silver.CensusCallerIndex === null &&
    !silver.ActedThisTurn &&
    myVillage.length <= 4
  const canPlaceAmulet =
    isMyTurn &&
    meIndex === silver.AmuletHolderIndex &&
    silver.AmuletPlaceable &&
    silver.AmuletPlacedCardId === null &&
    myVillage.length > 0 &&
    silver.Phase !== 'TricksterChoice'

  const peekUsesLeft = enchanterUsesLeft(silver, myVillage)
  const enchanterAvailable =
    isMyTurn && peekUsesLeft > 0 && myVillage.some((c) => !c.FaceUp && !c.AmuletProtected)
  const usableGuards = isMyTurn
    ? myVillage.filter(
        (c) => c.FaceUp && c.Value === 3 && !c.AmuletProtected && !silver.AbilitiesUsedThisTurn.includes(`Guard:${c.Id}`),
      )
    : []
  const attachedGuardEntries = usableGuards.flatMap((guard) =>
    myVillage.some((t) => t.GuardedByCardId === guard.Id)
      ? [{ slot: myVillage.indexOf(guard), id: guard.Id }]
      : [],
  )

  /** The ability a specific own face-up card can play right now, if any. */
  const abilityReadyForCard = (card: SilverViewCard): 'Enchanter' | 'Guard' | null => {
    if (!isMyTurn || state.isOver || silver.Phase === 'TricksterChoice' || !card.FaceUp) return null
    if (card.Value === 2 && enchanterUsesLeft(silver, myVillage) > 0
      && myVillage.some((c) => !c.FaceUp && !c.AmuletProtected)) return 'Enchanter'
    if (card.Value === 3 && !card.AmuletProtected
      && !silver.AbilitiesUsedThisTurn.includes(`Guard:${card.Id}`)) return 'Guard'
    return null
  }
  const readyCount = myVillage.filter((c) => abilityReadyForCard(c) !== null).length

  const pendingAbilityCardValue =
    silver.Phase === 'AbilityPending' && silver.DiscardTop?.Value != null && silver.DiscardTop.Value >= 5
      ? silver.DiscardTop.Value
      : null
  const pendingAbilityName = pendingAbilityCardValue !== null ? ABILITY_BY_VALUE[pendingAbilityCardValue] : undefined
  const revealPromptPending = silver.Phase === 'AbilityPending' && pendingAbilityCardValue === 6 && silver.RevealerChooserSeat !== null
  const witchPeeked = silver.Phase === 'AbilityPending' && pendingAbilityCardValue === 11 && silver.WitchPeekPending

  // Turn timer: identical math to UNO (soft end = TurnStartUtc + allowance;
  // the stored deadline is the hard limit including the grace window).
  const now = useNow()
  const gameRemainingMs = useCountdown(state.gameEndsAtUtc)
  const turnStartMs = silver.TurnStartUtc ? Date.parse(silver.TurnStartUtc) : Number.NaN
  const currentTimer = (silver.PlayerTimers ?? [])[silver.CurrentPlayerIndex]
  const baseSeconds = silver.TimerConfig?.BaseTurnSeconds ?? 90
  const maxBankSeconds = silver.TimerConfig?.MaxBankSeconds ?? 180
  const graceSeconds = silver.TimerConfig?.MaxOverrunSeconds ?? 15
  const allowanceSeconds = Math.max(
    Math.max(0, baseSeconds - graceSeconds),
    Math.min(
      maxBankSeconds,
      baseSeconds + (currentTimer?.BankSeconds ?? 0) - (currentTimer?.DeferredPenaltySeconds ?? 0),
    ),
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

  const myCumulative = meIndex >= 0 ? silver.CumulativeScores[meIndex] ?? 0 : 0
  const discardTilt = ((silver.DiscardPile.length * 37) % 13) - 6

  const send = (actionType: string, payload: Record<string, unknown>) => {
    if (isSending) return
    onAction(actionType, payload)
  }

  /** Maps a 5-12 ability name to its targeting interaction mode. */
  const targetingForAbility = (name: string): Targeting | null => {
    switch (name) {
      case 'Exposer':
        return { kind: 'exposer' }
      case 'Revealer':
        return { kind: 'revealer' }
      case 'ApprenticeSeer':
        return { kind: 'apprenticeSeer', slots: [] }
      case 'Seer':
        return { kind: 'seer' }
      case 'Beholder':
        return { kind: 'beholder' }
      case 'Witch':
        // Handled directly in the window banner (peek, then own/opponent exchange).
        return null
      case 'Robber':
        return { kind: 'robber', ownSlot: null }
      case 'Master':
        return { kind: 'master', slots: [] }
      default:
        return null
    }
  }

  // ------------------------------ targeting ------------------------------

  const isTargetable = (seat: number, card: SilverViewCard): boolean => {
    if (state.isOver) return false
    if (isChooserPrompt) {
      return seat === meIndex && !card.FaceUp && !card.Protected
    }
    if (!targeting) return false
    const own = seat === meIndex
    switch (targeting.kind) {
      case 'amulet':
        return own && !card.Protected
      case 'enchanter':
      case 'exposer':
        return own && !card.FaceUp && !card.AmuletProtected
      case 'guard':
        return targeting.guardSlot === null
          ? own && card.FaceUp && card.Value === 3 && !card.AmuletProtected &&
            !silver.AbilitiesUsedThisTurn.includes(`Guard:${card.Id}`)
          : own && !card.Protected
      case 'revealer':
        return !own
      case 'apprenticeSeer':
        return own && !card.FaceUp && !card.AmuletProtected
      case 'seer':
        return !own && !card.FaceUp && !card.Protected
      case 'beholder':
        return !card.FaceUp && (own ? !card.AmuletProtected : !card.Protected)
      case 'witchTarget':
        return !own && !card.Protected
      case 'robber':
        return targeting.ownSlot === null
          ? own && !card.AmuletProtected
          : !own && !card.Protected
      case 'master':
        return own && !card.AmuletProtected
      default:
        return false
    }
  }

  const isSlotSelected = (seat: number, slot: number): boolean => {
    if (seat !== meIndex) return false
    if (exchange) return exchange.slots.includes(slot)
    if (targeting?.kind === 'apprenticeSeer') return targeting.slots.includes(slot)
    if (targeting?.kind === 'master') return targeting.slots.includes(slot)
    if (targeting?.kind === 'guard' && targeting.guardSlot === slot) return true
    if (targeting?.kind === 'robber' && targeting.ownSlot === slot) return true
    return false
  }

  const handleCardClick = (seat: number, slot: number, card: SilverViewCard) => {
    if (isSending || state.isOver) return

    // Revealer prompt: the targeted player picks one of their own cards.
    if (isChooserPrompt) {
      if (seat === meIndex && !card.FaceUp && !card.Protected) {
        send('ChooseRevealCard', silverActions.chooseReveal(slot))
      }
      return
    }

    if (targeting) {
      if (!isTargetable(seat, card)) return
      switch (targeting.kind) {
        case 'amulet':
          send('PlaceAmulet', silverActions.placeAmulet(slot))
          return
        case 'enchanter':
          send('UseAbility', silverActions.useAbility({ Ability: 'Enchanter', OwnSlot: slot }))
          return
        case 'exposer':
          send('UseAbility', silverActions.useAbility({ Ability: 'Exposer', OwnSlot: slot }))
          return
        case 'guard':
          if (targeting.guardSlot === null) {
            setTargeting({ kind: 'guard', guardSlot: slot })
          } else if (targeting.guardSlot === slot) {
            setTargeting({ kind: 'guard', guardSlot: null })
          } else {
            send('MoveGuard', silverActions.moveGuard(targeting.guardSlot, slot))
            setTargeting(null)
          }
          return
        case 'revealer':
          send('UseAbility', silverActions.useAbility({ Ability: 'Revealer', TargetPlayerIndex: seat }))
          setTargeting(null)
          return
        case 'apprenticeSeer': {
          const slots = targeting.slots.includes(slot)
            ? targeting.slots.filter((s) => s !== slot)
            : [...targeting.slots, slot]
          setTargeting({ kind: 'apprenticeSeer', slots: slots.slice(0, 2) })
          return
        }
        case 'seer':
          send('UseAbility', silverActions.useAbility({ Ability: 'Seer', TargetPlayerIndex: seat, TargetSlot: slot }))
          return
        case 'beholder':
          send('UseAbility', silverActions.useAbility({ Ability: 'Beholder', TargetPlayerIndex: seat, TargetSlot: slot }))
          return
        case 'witchTarget':
          send('UseAbility', silverActions.useAbility({ Ability: 'Witch', TargetPlayerIndex: seat, TargetSlot: slot }))
          return
        case 'robber':
          if (targeting.ownSlot === null) {
            setTargeting({ kind: 'robber', ownSlot: slot })
          } else {
            send('UseAbility', silverActions.useAbility({ Ability: 'Robber', OwnSlot: targeting.ownSlot, TargetPlayerIndex: seat, TargetSlot: slot }))
          }
          return
        case 'master': {
          const slots = targeting.slots.includes(slot)
            ? targeting.slots.filter((s) => s !== slot)
            : [...targeting.slots, slot].sort((a, b) => a - b)
          setTargeting({ kind: 'master', slots })
          return
        }
      }
    }

    if (exchange && seat === meIndex) {
      if (card.AmuletProtected) return
      setExchange({
        ...exchange,
        slots: exchange.slots.includes(slot)
          ? exchange.slots.filter((s) => s !== slot)
          : [...exchange.slots, slot].sort((a, b) => a - b),
      })
      return
    }

    // Clicking one of your own ready residents plays THAT card's ability,
    // so with several playable face-up cards you choose which to use.
    if (!modeActive && seat === meIndex && isMyTurn && !state.isOver) {
      const ready = abilityReadyForCard(card)
      if (ready === 'Enchanter') {
        setTargeting({ kind: 'enchanter' })
        return
      }
      if (ready === 'Guard') {
        setTargeting({ kind: 'guard', guardSlot: slot })
        return
      }
    }

    // Peek right: any time, up to two per round, on unknown own face-down cards.
    // (The owner may peek cards their own Guard covers.)
    if (
      seat === meIndex &&
      !card.FaceUp &&
      card.Value === null &&
      !card.AmuletProtected &&
      silver.ViewerPeeksUsed < 2
    ) {
      send('PeekVillageCard', silverActions.peek(slot))
    }
  }

  const confirmExchange = () => {
    if (!exchange || exchange.slots.length === 0) return
    if (exchange.kind === 'witch') {
      send(
        'UseAbility',
        silverActions.useAbility({
          Ability: 'Witch',
          OwnSlots: exchange.slots,
          PlacementSlot: exchange.slots[0],
          NewAtEnd: exchange.newAtEnd,
          PenaltyAtEnd: exchange.penaltyAtEnd,
        }),
      )
      return
    }
    send(
      exchange.kind === 'drawn' ? 'ExchangeWithDrawn' : 'ExchangeWithDiscard',
      silverActions.exchange(exchange.slots, exchange.slots[0], exchange.newAtEnd, exchange.penaltyAtEnd),
    )
  }

  const targetingHintKey: string | null = (() => {
    if (isChooserPrompt) return 'silver.chooserHint'
    if (!targeting) return null
    switch (targeting.kind) {
      case 'amulet':
        return 'silver.targetAmulet'
      case 'enchanter':
        return 'silver.targetEnchanter'
      case 'guard':
        return targeting.guardSlot === null ? 'silver.targetGuard1' : 'silver.targetGuard2'
      case 'exposer':
        return 'silver.targetExposer'
      case 'revealer':
        return 'silver.targetRevealer'
      case 'apprenticeSeer':
        return 'silver.targetApprenticeSeer'
      case 'seer':
        return 'silver.targetSeer'
      case 'beholder':
        return 'silver.targetBeholder'
      case 'witchTarget':
        return 'silver.targetWitch'
      case 'robber':
        return targeting.ownSlot === null ? 'silver.targetRobber1' : 'silver.targetRobber2'
      case 'master':
        return 'silver.targetMaster'
      default:
        return null
    }
  })()

  const drawnCard = silver.PendingDraw[0] ?? null
  const exchangeSlots = exchange?.slots ?? (targeting?.kind === 'master' ? targeting.slots : [])
  const exchangeValues = exchangeSlots.map((s) => myVillage[s]?.Value ?? null)
  const exchangeVerdict = exchangeSlots.length >= 2 ? matchStatus(exchangeValues) : null
  const modeActive = exchange !== null || targeting !== null

  const winnerId = state.isOver ? state.winner?.userId : undefined
  const winnerName = winnerId === undefined ? undefined : seatName(state.players.findIndex((p) => p.userId === winnerId))
  const standings = state.players
    .map((p, i) => ({
      name: seatName(i),
      score: silver.CumulativeScores[i] ?? 0,
      roundScore: silver.LastRoundScores?.[i],
      userId: p.userId,
    }))
    .sort((a, b) => a.score - b.score)

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      {/* ============================ TABLE ============================ */}
      <div className="relative rounded-3xl border border-indigo-950/60 bg-gradient-to-b from-indigo-950 via-slate-900 to-slate-950 p-4 sm:p-6 shadow-2xl overflow-hidden">
        {/* moonlit sheen + stars */}
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(165,180,252,0.16),transparent_58%)]" />
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_bottom,rgba(15,23,42,0.55),transparent_60%)]" />
        <span className="pointer-events-none absolute end-[8%] top-[6%] h-1 w-1 rounded-full bg-slate-300/80" />
        <span className="pointer-events-none absolute end-[18%] top-[14%] h-0.5 w-0.5 rounded-full bg-slate-400/70" />
        <span className="pointer-events-none absolute start-[12%] top-[10%] h-0.5 w-0.5 rounded-full bg-slate-300/70" />
        <span className="pointer-events-none absolute start-[24%] top-[22%] h-1 w-1 rounded-full bg-slate-400/50" />

        {/* round tracker */}
        <div className="relative flex items-center justify-center gap-2">
          <span className="text-[11px] font-semibold uppercase tracking-widest text-indigo-200/60">
            {t('silver.roundOf', { n: silver.Round })}
          </span>
          <span className="flex items-center gap-1">
            {[1, 2, 3, 4].map((r) => (
              <MoonSolid
                key={r}
                className={`h-3.5 w-3.5 ${r < silver.Round ? 'text-amber-300/90' : r === silver.Round ? 'text-indigo-200' : 'text-slate-600/70'}`}
              />
            ))}
          </span>
        </div>

        {/* opponent seats */}
        <div className="relative mt-4 flex flex-wrap justify-center gap-3">
          {state.players.map((player, i) => {
            if (i === meIndex) return null
            const isTurn = silver.CurrentPlayerIndex === i
            const removed = eliminated.includes(i)
            const amuletHolder = silver.AmuletHolderIndex === i
            return (
              <div
                key={player.userId}
                className={`rounded-2xl border px-3 py-2 backdrop-blur-sm transition-all ${
                  removed
                    ? 'border-white/10 bg-white/5 opacity-50'
                    : isTurn
                      ? 'border-amber-300/70 bg-white/10 ring-2 ring-amber-300/40 scale-[1.02]'
                      : 'border-white/15 bg-white/5'
                }`}
              >
                <div className="flex items-center gap-2.5">
                  <div
                    className={`flex h-9 w-9 items-center justify-center rounded-full text-sm font-bold text-white shadow ${
                      removed ? 'bg-gray-500' : isTurn ? 'bg-amber-500' : 'bg-indigo-600'
                    }`}
                  >
                    {seatName(i).charAt(0).toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-1.5">
                      <p className="max-w-24 truncate text-xs font-semibold leading-tight text-white">{seatName(i)}</p>
                      {amuletHolder && (
                        <MoonSolid className="h-3.5 w-3.5 text-slate-200 drop-shadow-[0_0_5px_rgba(226,232,240,0.8)]" aria-label={t('silver.amuletHolder')} />
                      )}
                      {removed && <span className="text-[10px] font-medium text-white/50">{t('silver.eliminated')}</span>}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] font-bold tabular-nums text-amber-200/90">
                        {silver.CumulativeScores[i] ?? 0} {t('silver.points')}
                      </span>
                      <span className="text-[10px] font-medium tabular-nums text-white/50">
                        {t('silver.cards', { n: silver.Villages[i]?.length ?? 0 })}
                      </span>
                    </div>
                  </div>
                </div>
                {/* mini village */}
                <div className="mt-2 flex items-end justify-center gap-0.5">
                  {(silver.Villages[i] ?? []).map((card, slot) => (
                    <button
                      key={card.Id}
                      type="button"
                      disabled={!isTargetable(i, card) || isSending}
                      onClick={() => handleCardClick(i, slot, card)}
                      className={`relative rounded-md transition-transform ${
                        isTargetable(i, card)
                          ? 'cursor-pointer ring-2 ring-rose-400 ring-offset-1 ring-offset-slate-900 animate-pulse hover:-translate-y-1'
                          : 'cursor-default'
                      } ${isSlotSelected(i, slot) ? '-translate-y-1' : ''}`}
                      aria-label={t('silver.villageCardAria', { player: seatName(i) })}
                    >
                      <SilverCardVisual card={card} size="xs" />
                    </button>
                  ))}
                </div>
              </div>
            )
          })}
        </div>

        {/* center: deck + discard + squire display */}
        <div className="relative mt-6 flex flex-wrap items-center justify-center gap-8 sm:gap-12">
          {/* Deck */}
          <button
            type="button"
            onClick={() => canDraw && setConfirmDraw(true)}
            disabled={!canDraw || isSending}
            aria-label={t('silver.deckAria', { n: silver.DeckSize })}
            className={`group relative flex flex-col items-center gap-2 ${canDraw ? 'cursor-pointer' : 'cursor-default'}`}
          >
            <div className="relative">
              <SilverCardBack size="xl" className="absolute inset-0 translate-x-1.5 translate-y-1.5 rotate-3 opacity-60" />
              <SilverCardBack size="xl" className="absolute inset-0 translate-x-0.5 translate-y-0.5 -rotate-2 opacity-80" />
              <SilverCardBack
                size="xl"
                className={`relative transition-transform ${canDraw ? 'group-hover:-translate-y-3 group-hover:shadow-[0_0_25px_rgba(165,180,252,0.5)]' : ''}`}
              />
            </div>
            <span
              className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold tabular-nums ${
                silver.DeckSize <= 5 ? 'animate-pulse bg-amber-500/90 text-white' : 'bg-black/40 text-white'
              }`}
            >
              {t('silver.deckLeft', { n: silver.DeckSize })}
            </span>
            {canDraw && (
              <span className="absolute -bottom-8 text-[11px] font-semibold text-indigo-200 opacity-0 transition-opacity group-hover:opacity-100">
                {t('silver.drawHover')}
              </span>
            )}
          </button>

          {/* Discard */}
          <div className="relative flex flex-col items-center" aria-label={t('silver.discardAria', { label: silver.DiscardTop?.Value ?? '' })}>
            <div className="relative h-36 w-24 sm:h-40 sm:w-28">
              {silver.DiscardTop ? (
                <div className="absolute inset-0" style={{ transform: `rotate(${discardTilt}deg)` }}>
                  <SilverCardVisual card={silver.DiscardTop} size="xl" />
                </div>
              ) : (
                <SilverCardBack size="xl" className="opacity-40" />
              )}
              {silver.DiscardSize > 0 && (
                <div className="absolute -top-2 -end-3 z-10 rounded-full bg-indigo-500 px-2 py-0.5 text-[10px] font-black tabular-nums text-white shadow ring-2 ring-white/70">
                  +{silver.DiscardSize}
                </div>
              )}
            </div>
            <span className="mt-2 text-[11px] font-medium text-white/50">{t('silver.discardLabel')}</span>
            {canTakeDiscard && (
              <button
                type="button"
                onClick={() => setConfirmTake({ kind: 'discard' })}
                disabled={isSending}
                className="mt-1 text-[11px] font-semibold text-amber-300 underline decoration-dotted underline-offset-2 hover:text-amber-200"
              >
                {t('silver.takeHover')}
              </button>
            )}
          </div>

          {/* Squire display area */}
          {silver.Display.length > 0 && (
            <div className="relative flex flex-col items-center">
              <div className="flex items-end gap-1">
                {silver.Display.map((card, index) => (
                  <button
                    key={card.Id}
                    type="button"
                    disabled={!canTakeSquire || isSending}
                    onClick={() => setConfirmTake({ kind: 'squire', index })}
                    className={`rounded-lg transition-transform ${
                      canTakeSquire
                        ? 'cursor-pointer ring-2 ring-sky-300/70 hover:-translate-y-2 hover:shadow-[0_0_16px_rgba(125,211,252,0.5)]'
                        : 'cursor-default'
                    }`}
                    aria-label={t('silver.takeSquireAria')}
                  >
                    <SilverCardVisual card={card} size="sm" className="opacity-95" />
                  </button>
                ))}
              </div>
              <span className="mt-2 text-[11px] font-medium text-white/50">{t('silver.displayLabel')}</span>
            </div>
          )}
        </div>

        {/* decision banners */}
        <div className="relative mt-5 space-y-3">
          {isMyTurn && silver.Phase === 'TricksterChoice' && (
            <div className="mx-auto max-w-lg rounded-2xl border border-violet-300/30 bg-violet-950/60 p-4 text-center backdrop-blur-sm">
              <p className="text-sm font-semibold text-violet-100">{t('silver.tricksterTitle')}</p>
              <p className="mt-0.5 text-[11px] text-violet-200/70">{t('silver.tricksterHint')}</p>
              <div className="mt-3 flex items-center justify-center gap-4">
                {silver.PendingDraw.map((card) => (
                  <button
                    key={card.Id}
                    type="button"
                    disabled={isSending}
                    onClick={() => send('ChooseDrawnCard', silverActions.chooseDrawn(card.Id))}
                    className="rounded-xl transition-transform hover:-translate-y-2 hover:shadow-[0_0_18px_rgba(196,181,253,0.55)]"
                  >
                    <SilverCardVisual card={card} size="lg" />
                  </button>
                ))}
              </div>
            </div>
          )}

          {isChooserPrompt && (
            <div className="mx-auto max-w-lg rounded-2xl border border-rose-300/40 bg-rose-950/70 p-4 text-center backdrop-blur-sm">
              <p className="text-sm font-semibold text-rose-100">{t('silver.chooserTitle', { name: seatName(silver.CurrentPlayerIndex) })}</p>
              <p className="mt-0.5 text-[11px] text-rose-200/70">{t('silver.chooserHint')}</p>
            </div>
          )}

          {isMyTurn && silver.Phase === 'DrawnDecision' && drawnCard && (
            <div className="mx-auto max-w-lg rounded-2xl border border-indigo-300/30 bg-indigo-950/70 p-4 text-center backdrop-blur-sm">
              <p className="text-sm font-semibold text-indigo-100">
                {t('silver.drawnTitle', { card: cardLabel(drawnCard, t) })}
              </p>
              <p className="mt-0.5 text-[11px] text-indigo-200/70">{t('silver.drawnHint')}</p>
              <div className="mt-3 flex items-center justify-center gap-5">
                <SilverCardVisual card={drawnCard} size="lg" />
                <div className="flex flex-col gap-2">
                  <Button size="sm" onClick={() => send('DiscardDrawnCard', silverActions.discardDrawn())} isLoading={isSending}>
                    {t('silver.discardBtn')}
                  </Button>
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => setExchange({ kind: 'drawn', slots: [], newAtEnd: true, penaltyAtEnd: true })}
                    isLoading={isSending}
                    disabled={!hasMovableSlot}
                  >
                    {t('silver.exchangeBtn')}
                  </Button>
                </div>
              </div>
            </div>
          )}

          {isMyTurn && silver.Phase === 'ExchangeDecision' && drawnCard && (
            <div className="mx-auto max-w-lg rounded-2xl border border-amber-300/30 bg-amber-950/60 p-4 text-center backdrop-blur-sm">
              <p className="text-sm font-semibold text-amber-100">
                {t('silver.tookTitle', { card: cardLabel(drawnCard, t) })}
              </p>
              <p className="mt-0.5 text-[11px] text-amber-200/70">{t('silver.tookHint')}</p>
              <div className="mt-3 flex items-center justify-center gap-5">
                <SilverCardVisual card={drawnCard} size="lg" />
                <Button
                  size="sm"
                  onClick={() => setExchange({ kind: 'taken', slots: [], newAtEnd: true, penaltyAtEnd: true })}
                  isLoading={isSending}
                  disabled={!hasMovableSlot}
                >
                  {t('silver.exchangeBtn')}
                </Button>
              </div>
            </div>
          )}

          {isMyTurn && silver.Phase === 'AbilityPending' && pendingAbilityName && (
            <div className="mx-auto max-w-lg rounded-2xl border border-emerald-300/30 bg-emerald-950/60 p-4 text-center backdrop-blur-sm">
              <p className="text-sm font-semibold text-emerald-100">
                {t('silver.abilityWindow', { card: silver.DiscardTop ? cardLabel(silver.DiscardTop, t) : '' })}
              </p>
              <p className="mt-0.5 text-[11px] text-emerald-200/70">{t('silver.abilityHint')}</p>
              <div className="mt-3 flex justify-center gap-2">
                {revealPromptPending ? (
                  <p className="text-xs font-semibold text-emerald-100">
                    {t('silver.waitingForChoice', { name: seatName(silver.RevealerChooserSeat ?? 0) })}
                  </p>
                ) : witchPeeked ? (
                  <>
                    <div className="flex items-center gap-2 rounded-xl bg-emerald-900/50 px-3 py-1">
                      <span className="text-[11px] text-emerald-100">{t('silver.witchPeeked')}</span>
                      {silver.WitchPeekedValue !== null && (
                        <SilverCardVisual
                          card={{
                            Id: 'witch-peek',
                            Value: silver.WitchPeekedValue,
                            FaceUp: true,
                            Protected: false,
                            AmuletProtected: false,
                            GuardedByCardId: null,
                          }}
                          size="md"
                        />
                      )}
                    </div>
                    <Button size="sm" onClick={() => setTargeting({ kind: 'witchTarget' })} isLoading={isSending}>
                      {t('silver.witchSwapOpponent')}
                    </Button>
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => setExchange({ kind: 'witch', slots: [], newAtEnd: true, penaltyAtEnd: true })}
                      isLoading={isSending}
                      disabled={!hasMovableSlot}
                    >
                      {t('silver.witchSwapOwn')}
                    </Button>
                  </>
                ) : (
                  <>
                    <Button
                      size="sm"
                      onClick={() => {
                        if (pendingAbilityName === 'Witch') {
                          send('UseAbility', silverActions.useAbility({ Ability: 'Witch' }))
                        } else {
                          const mode = targetingForAbility(pendingAbilityName)
                          if (mode) setTargeting(mode)
                        }
                      }}
                      isLoading={isSending}
                    >
                      {pendingAbilityName === 'Witch' ? t('silver.witchPeekBtn') : t('silver.useAbility')}
                    </Button>
                    {pendingAbilityName === 'Master' && (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => send('UseAbility', silverActions.useAbility({ Ability: 'Master' }))}
                        isLoading={isSending}
                      >
                        {t('silver.masterDecline')}
                      </Button>
                    )}
                  </>
                )}
                <Button size="sm" variant="secondary" onClick={() => send('SkipAbility', silverActions.skipAbility())} isLoading={isSending}>
                  {t('silver.skipAbility')}
                </Button>
              </div>
            </div>
          )}

          {exchange && (
            <div className="mx-auto max-w-xl rounded-2xl border border-emerald-300/30 bg-emerald-950/70 p-4 backdrop-blur-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-semibold text-emerald-100">
                  {exchange.slots.length === 0
                    ? t('silver.chooseReplace')
                    : exchange.slots.length === 1
                      ? t('silver.exchangeSingle')
                      : t('silver.exchangeTitle', { n: exchange.slots.length })}
                </p>
                <button
                  type="button"
                  onClick={() => setExchange(null)}
                  className="flex items-center gap-1 text-xs font-medium text-emerald-200/70 hover:text-white"
                >
                  <XMarkIcon className="h-3.5 w-3.5" /> {t('common.cancel')}
                </button>
              </div>
              <p className="mt-0.5 text-[11px] text-emerald-200/70">{t('silver.selectCards')}</p>
              {exchangeVerdict && (
                <p
                  className={`mt-2 rounded-lg px-3 py-1.5 text-center text-xs font-semibold ${
                    exchangeVerdict === 'mismatch' || exchangeVerdict === 'wildcard'
                      ? 'bg-rose-900/70 text-rose-200'
                      : exchangeVerdict === 'match'
                        ? 'bg-emerald-900/70 text-emerald-200'
                        : 'bg-amber-900/70 text-amber-200'
                  }`}
                >
                  {exchangeVerdict === 'mismatch'
                    ? t('silver.matchMismatch')
                    : exchangeVerdict === 'wildcard'
                      ? t('silver.matchWildcardLimit')
                      : exchangeVerdict === 'match'
                        ? t('silver.matchLikely')
                        : t('silver.matchUncertain')}
                </p>
              )}
              {exchange.slots.length >= 2 && (
                <div className="mt-2 flex flex-wrap items-center justify-center gap-4 text-[11px] text-emerald-100">
                  <span className="flex items-center gap-1.5">
                    {t('silver.newCardPos')}
                    <span className="flex overflow-hidden rounded-md ring-1 ring-emerald-300/40">
                      <button
                        type="button"
                        onClick={() => setExchange({ ...exchange, newAtEnd: false })}
                        className={`px-2 py-0.5 ${!exchange.newAtEnd ? 'bg-emerald-500 font-bold text-white' : 'bg-emerald-900/60'}`}
                      >
                        {t('silver.atStart')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setExchange({ ...exchange, newAtEnd: true })}
                        className={`px-2 py-0.5 ${exchange.newAtEnd ? 'bg-emerald-500 font-bold text-white' : 'bg-emerald-900/60'}`}
                      >
                        {t('silver.atEnd')}
                      </button>
                    </span>
                  </span>
                  {exchange.slots.length >= 3 && (
                    <span className="flex items-center gap-1.5">
                      {t('silver.penaltyPos')}
                      <span className="flex overflow-hidden rounded-md ring-1 ring-emerald-300/40">
                        <button
                          type="button"
                          onClick={() => setExchange({ ...exchange, penaltyAtEnd: false })}
                          className={`px-2 py-0.5 ${!exchange.penaltyAtEnd ? 'bg-emerald-500 font-bold text-white' : 'bg-emerald-900/60'}`}
                        >
                          {t('silver.atStart')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setExchange({ ...exchange, penaltyAtEnd: true })}
                          className={`px-2 py-0.5 ${exchange.penaltyAtEnd ? 'bg-emerald-500 font-bold text-white' : 'bg-emerald-900/60'}`}
                        >
                          {t('silver.atEnd')}
                        </button>
                      </span>
                    </span>
                  )}
                </div>
              )}
              <div className="mt-3 flex justify-center">
                <Button
                  size="sm"
                  onClick={confirmExchange}
                  isLoading={isSending}
                  disabled={exchange.slots.length === 0}
                >
                  {t('silver.confirmExchange', { n: exchange.slots.length })}
                </Button>
              </div>
            </div>
          )}

          {targeting && targeting.kind === 'master' && targeting.slots.length > 0 && (
            <div className="mx-auto flex max-w-xl items-center justify-center gap-3">
              <Button size="sm" onClick={() => setMasterModal(true)} isLoading={isSending}>
                {t('silver.masterChooseDiscard', { n: targeting.slots.length })}
              </Button>
            </div>
          )}

          {targeting && targetingHintKey && !isChooserPrompt && (
            <div className="mx-auto flex max-w-xl items-center justify-between gap-3 rounded-2xl border border-rose-300/30 bg-rose-950/60 px-4 py-3 backdrop-blur-sm">
              <p className="flex items-center gap-2 text-sm font-semibold text-rose-100">
                <HandRaisedIcon className="h-4 w-4 shrink-0 animate-pulse" />
                {t(targetingHintKey)}
              </p>
              <div className="flex items-center gap-2">
                {targeting.kind === 'apprenticeSeer' && targeting.slots.length > 0 && (
                  <Button
                    size="sm"
                    onClick={() =>
                      send('UseAbility', silverActions.useAbility({ Ability: 'ApprenticeSeer', PeekSlots: targeting.slots }))
                    }
                    isLoading={isSending}
                  >
                    {t('silver.apprenticeSeerConfirm', { n: targeting.slots.length })}
                  </Button>
                )}
                <button
                  type="button"
                  onClick={() => setTargeting(null)}
                  className="flex items-center gap-1 text-xs font-medium text-rose-200/70 hover:text-white"
                >
                  <XMarkIcon className="h-3.5 w-3.5" /> {t('common.cancel')}
                </button>
              </div>
            </div>
          )}

          {silver.CensusCallerIndex !== null && !state.isOver && (
            <div className="mx-auto flex max-w-md items-center justify-center gap-2 rounded-2xl border border-amber-300/30 bg-amber-950/50 px-4 py-2 text-center backdrop-blur-sm">
              <MoonIcon className="h-4 w-4 shrink-0 text-amber-300" />
              <p className="text-xs font-semibold text-amber-100">
                {t('silver.censusBanner', { name: seatName(silver.CensusCallerIndex) })}
                {' · '}
                {t('silver.finalTurns', { n: silver.RemainingCensusTurns })}
              </p>
            </div>
          )}
        </div>

        {/* own village */}
        {meIndex >= 0 && (
          <div className="relative mt-6">
            <div className="flex items-center justify-between px-1">
              <p className="text-[11px] font-semibold uppercase tracking-widest text-indigo-200/60">
                {t('silver.yourVillage')}
              </p>
              <div className="flex items-center gap-3 text-[11px] text-indigo-200/60">
                {canPeek && <span className="flex items-center gap-1"><EyeIcon className="h-3.5 w-3.5" /> {t('silver.peekHint', { n: 2 - silver.ViewerPeeksUsed })}</span>}
                {canPlaceAmulet && !targeting && (
                  <button
                    type="button"
                    onClick={() => setTargeting({ kind: 'amulet' })}
                    className="flex items-center gap-1 font-semibold text-slate-200 underline decoration-dotted underline-offset-2 hover:text-white"
                  >
                    <MoonSolid className="h-3.5 w-3.5" /> {t('silver.placeAmulet')}
                  </button>
                )}
              </div>
            </div>
            <div className="overflow-x-auto px-2 pb-3 pt-6">
              <div className="mx-auto flex w-max items-end px-6">
                {myVillage.map((card, slot) => {
                  const n = myVillage.length
                  const mid = (n - 1) / 2
                  const rot = (slot - mid) * 2.2
                  const arc = Math.min(Math.abs(slot - mid) * 4, 18)
                  const overlap = n <= 5 ? 10 : n <= 6 ? 26 : 38
                  const interactive =
                    (isChooserPrompt && !card.FaceUp && !card.Protected) ||
                    (targeting !== null && isTargetable(meIndex, card)) ||
                    (exchange !== null && !card.AmuletProtected) ||
                    (!modeActive && canPeek && !card.FaceUp && card.Value === null && !card.AmuletProtected) ||
                    (!modeActive && abilityReadyForCard(card) !== null)
                  return (
                    <div
                      key={card.Id}
                      className="relative hover:z-40"
                      style={{
                        marginInlineStart: slot === 0 ? 0 : `-${overlap}px`,
                        transform: `rotate(${rot}deg)`,
                        marginTop: arc,
                        zIndex: slot,
                      }}
                    >
                      <button
                        type="button"
                        disabled={isSending || (!interactive && !isSlotSelected(meIndex, slot))}
                        onClick={() => handleCardClick(meIndex, slot, card)}
                        className={`relative block rounded-xl transition-all duration-150 ${
                          isTargetable(meIndex, card) || (isChooserPrompt && !card.FaceUp && !card.Protected)
                            ? 'cursor-pointer ring-2 ring-rose-400 ring-offset-2 ring-offset-slate-950 hover:-translate-y-3 hover:scale-105 shadow-[0_0_16px_rgba(251,113,133,0.5)] animate-pulse'
                            : ''
                        } ${
                          isSlotSelected(meIndex, slot)
                            ? 'cursor-pointer -translate-y-3 ring-2 ring-emerald-300 shadow-[0_0_16px_rgba(110,231,183,0.6)]'
                            : ''
                        } ${
                          !targeting && !exchange && !isChooserPrompt && canPeek && !card.FaceUp && card.Value === null && !card.AmuletProtected
                            ? 'cursor-pointer hover:-translate-y-2 hover:shadow-[0_0_14px_rgba(165,180,252,0.55)]'
                            : ''
                        } ${!interactive && !isSlotSelected(meIndex, slot) ? 'cursor-default' : ''}`}
                        aria-label={t('silver.villageAria', { n: slot + 1 })}
                      >
                        <SilverCardVisual card={card} size="lg" actionable={abilityReadyForCard(card) !== null} />
                        {isSlotSelected(meIndex, slot) && (
                          <span className="absolute -top-2 -end-2 flex h-6 w-6 items-center justify-center rounded-full bg-emerald-400 text-slate-900 shadow ring-2 ring-white">
                            <CheckIcon className="h-4 w-4" />
                          </span>
                        )}
                      </button>
                    </div>
                  )
                })}
              </div>
            </div>
          </div>
        )}
        {meIndex < 0 && (
          <p className="relative mt-6 text-center text-xs text-indigo-200/60">{t('silver.spectator')}</p>
        )}
      </div>

      {/* ============================ STATUS STRIP ============================ */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-2xl border border-gray-200 bg-white px-4 py-3 shadow-sm">
        {state.isOver ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-gray-500">
            <TrophyIcon className="h-4 w-4 text-amber-400" /> {t('silver.gameOver')}
          </span>
        ) : isMyTurn ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1 text-sm font-semibold text-indigo-700">
            <PlayIcon className="h-4 w-4" /> {t('silver.yourTurn')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 text-sm text-gray-600">
            <ClockIcon className="h-4 w-4 text-gray-400" />
            {t('silver.waitingFor', { name: seatName(silver.CurrentPlayerIndex) })}
          </span>
        )}

        {!state.isOver && (
          <TimerRing
            seconds={remainingSeconds}
            overtimeSeconds={overtimeSeconds}
            fraction={timerFraction}
            critical={timerCritical}
          />
        )}

        {!state.isOver && gameClockLabel && (
          <div
            className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 ${
              gameClockCritical ? 'bg-rose-600/80' : 'bg-gray-100'
            }`}
            title={t('silver.gameClockTitle')}
          >
            <ClockIcon className={`h-3.5 w-3.5 ${gameClockCritical ? 'text-white' : 'text-gray-500'}`} />
            <span className={`text-xs font-bold tabular-nums ${gameClockCritical ? 'text-white' : 'text-gray-500'}`}>
              {gameClockLabel}
            </span>
          </div>
        )}

        <div className="flex-1" />

        <button
          type="button"
          onClick={() => setHelpOpen(true)}
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
            readyCount > 0
              ? 'animate-pulse bg-amber-100 text-amber-800 ring-2 ring-amber-400 hover:bg-amber-200'
              : 'bg-gray-100 text-gray-600 hover:bg-indigo-50 hover:text-indigo-700'
          }`}
          title={readyCount > 0 ? t('silver.abilityReady') : t('silver.helpTitle')}
        >
          <QuestionMarkCircleIcon className="h-4 w-4" />
          {t('silver.helpBtn')}
        </button>

        {enchanterAvailable && (
          <Button size="sm" variant="secondary" onClick={() => setTargeting({ kind: 'enchanter' })} isLoading={isSending}>
            <EyeIcon className="h-4 w-4" />
            <span className="ms-1">
              {t('silver.enchanterBtn')}
              {peekUsesLeft > 1 ? ` ×${peekUsesLeft}` : ''}
            </span>
          </Button>
        )}
        {isMyTurn && usableGuards.length > 0 && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => setTargeting({ kind: 'guard', guardSlot: null })}
            isLoading={isSending}
          >
            <ShieldCheckIcon className="h-4 w-4" />
            <span className="ms-1">{t('silver.guardBtn')}</span>
          </Button>
        )}
        {attachedGuardEntries.map((entry) => (
          <Button
            key={entry.id}
            size="sm"
            variant="secondary"
            onClick={() => send('RemoveGuard', silverActions.removeGuard(entry.slot))}
            isLoading={isSending}
          >
            {t('silver.guardRemove')}
          </Button>
        ))}
        {canPlaceAmulet && !targeting && (
          <Button size="sm" variant="secondary" onClick={() => setTargeting({ kind: 'amulet' })} isLoading={isSending}>
            <MoonSolid className="h-4 w-4 text-slate-500" />
            <span className="ms-1">{t('silver.placeAmulet')}</span>
          </Button>
        )}
        {canCallCensus && (
          <Button size="sm" variant="primary" onClick={() => setConfirmCensus(true)} isLoading={isSending}>
            {t('silver.callCensus')}
          </Button>
        )}
        {meIndex >= 0 && !state.isOver && (
          <span className="text-xs font-medium tabular-nums text-gray-400">
            {t('silver.yourScore', { n: myCumulative })}
            {' · '}
            {t('silver.peeksLeft', { n: silver.ViewerPeeksUsed })}
          </span>
        )}
      </div>

      {/* ============================ SCOREBOARD + LOG ============================ */}
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-gray-200 bg-white px-4 py-3 shadow-sm">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">{t('silver.scoreboard')}</p>
          <ul className="space-y-1.5">
            {state.players.map((player, i) => {
              const lastRound = silver.LastRoundScores?.[i]
              const isMe = i === meIndex
              return (
                <li
                  key={player.userId}
                  className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm ${
                    isMe ? 'bg-indigo-50' : ''
                  } ${eliminated.includes(i) ? 'opacity-50' : ''}`}
                >
                  <span className="flex h-6 w-6 items-center justify-center rounded-full bg-indigo-100 text-[10px] font-bold text-indigo-700">
                    {seatName(i).charAt(0).toUpperCase()}
                  </span>
                  <span className="min-w-0 flex-1 truncate font-medium text-gray-700">{seatName(i)}</span>
                  {silver.AmuletHolderIndex === i && (
                    <MoonSolid className="h-3.5 w-3.5 text-slate-400" aria-label={t('silver.amuletHolder')} />
                  )}
                  {silver.LastRoundCensusCallerIndex === i && (
                    <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[9px] font-bold uppercase text-amber-700">
                      {t('silver.callerBadge')}
                    </span>
                  )}
                  {lastRound !== undefined && (
                    <span className="text-[11px] tabular-nums text-gray-400">
                      {t('silver.lastRound', { n: lastRound })}
                    </span>
                  )}
                  <span className="w-10 text-end text-sm font-black tabular-nums text-gray-800">
                    {silver.CumulativeScores[i] ?? 0}
                  </span>
                </li>
              )
            })}
          </ul>
        </div>

        <details className="group rounded-2xl border border-gray-200 bg-white shadow-sm open:pb-2">
          <summary className="flex cursor-pointer list-none select-none items-center justify-between px-4 py-3">
            <span className="text-sm font-semibold text-gray-700">{t('silver.gameLog')}</span>
            <span className="text-xs text-gray-400 group-open:hidden">{t('silver.show')}</span>
            <span className="hidden text-xs text-gray-400 group-open:inline">{t('silver.hide')}</span>
          </summary>
          <ul className="max-h-64 space-y-1 overflow-y-auto px-4 pb-3">
            {silver.EventLog.length === 0 ? (
              <li className="py-4 text-center text-sm text-gray-400">{t('silver.noEvents')}</li>
            ) : (
              silver.EventLog.slice(-40).reverse().map((entry, i) => (
                <li key={`${silver.EventLog.length - i}`} className="flex items-start gap-2 text-xs text-gray-600">
                  <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-indigo-400" />
                  <span>{formatSilverEvent(entry, t, seatNames)}</span>
                </li>
              ))
            )}
          </ul>
        </details>
      </div>

      {/* ============================ MODALS ============================ */}

      {/* Draw confirmation (with optional Trickster extras) */}
      <Modal isOpen={confirmDraw} onClose={() => setConfirmDraw(false)} title={t('silver.drawTitle')}>
        <p className="text-sm text-gray-600">{t('silver.drawConfirm')}</p>
        {tricksters > 0 && (
          <div className="mt-3 rounded-lg bg-violet-50 p-2.5">
            <p className="text-sm text-violet-800">{t('silver.useTrickster')}</p>
            <div className="mt-2 flex gap-2">
              {Array.from({ length: Math.min(tricksters, Math.max(0, silver.DeckSize - 1)) + 1 }, (_, k) => (
                <button
                  key={k}
                  type="button"
                  onClick={() => setTricksterExtra(k)}
                  className={`flex-1 rounded-md px-2 py-1 text-sm font-semibold ${
                    tricksterExtra === k ? 'bg-violet-600 text-white' : 'bg-violet-100 text-violet-800 hover:bg-violet-200'
                  }`}
                >
                  {t('silver.tricksterDrawN', { n: 1 + k })}
                </button>
              ))}
            </div>
          </div>
        )}
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" className="flex-1" onClick={() => setConfirmDraw(false)}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            className="flex-1"
            isLoading={isSending}
            onClick={() => {
              setConfirmDraw(false)
              send('DrawFromDeck', silverActions.draw(tricksterExtra))
              setTricksterExtra(0)
            }}
          >
            {t('silver.drawBtn')}
          </Button>
        </div>
      </Modal>

      {/* Take discard / squire confirmation */}
      <Modal isOpen={confirmTake !== null} onClose={() => setConfirmTake(null)} title={t('silver.takeTitle')}>
        <div className="flex items-center gap-4">
          {confirmTake?.kind === 'squire'
            ? silver.Display[confirmTake.index] && <SilverCardVisual card={silver.Display[confirmTake.index]} size="lg" />
            : silver.DiscardTop && <SilverCardVisual card={silver.DiscardTop} size="lg" />}
          <p className="text-sm text-gray-600">{t('silver.takeConfirm')}</p>
        </div>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" className="flex-1" onClick={() => setConfirmTake(null)}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            className="flex-1"
            isLoading={isSending}
            onClick={() => {
              const take = confirmTake
              setConfirmTake(null)
              if (take?.kind === 'squire') send('TakeSquireCard', silverActions.takeSquire(take.index))
              else send('TakeDiscard', {})
            }}
          >
            {t('silver.takeBtn')}
          </Button>
        </div>
      </Modal>

      {/* Census confirmation */}
      <Modal isOpen={confirmCensus} onClose={() => setConfirmCensus(false)} title={t('silver.callCensusTitle')}>
        <p className="text-sm text-gray-600">{t('silver.callCensusConfirm')}</p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" className="flex-1" onClick={() => setConfirmCensus(false)}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="danger"
            className="flex-1"
            isLoading={isSending}
            onClick={() => {
              setConfirmCensus(false)
              send('CallCensus', silverActions.callCensus())
            }}
          >
            {t('silver.callCensus')}
          </Button>
        </div>
      </Modal>

      {/* Master discard picker */}
      <Modal isOpen={masterModal} onClose={() => setMasterModal(false)} title={t('silver.masterTitle')}>
        <p className="mb-3 text-sm text-gray-600">{t('silver.masterPick')}</p>
        <div className="flex max-h-64 flex-wrap items-center justify-center gap-2 overflow-y-auto">
          {silver.DiscardPile.map((card, index) => (
            <button
              key={card.Id}
              type="button"
              disabled={isSending}
              onClick={() => {
                if (targeting?.kind !== 'master' || targeting.slots.length === 0) return
                send(
                  'UseAbility',
                  silverActions.useAbility({
                    Ability: 'Master',
                    OwnSlots: targeting.slots,
                    PlacementSlot: targeting.slots[0],
                    DiscardIndex: index,
                  }),
                )
                setMasterModal(false)
                setTargeting(null)
              }}
              className="relative rounded-xl transition-transform hover:-translate-y-1.5 hover:shadow-[0_0_14px_rgba(16,185,129,0.5)]"
              title={t('silver.masterPickAria', { n: index + 1 })}
            >
              <SilverCardVisual card={card} size="sm" />
            </button>
          ))}
        </div>
      </Modal>

      {/* Card abilities guide */}
      <Modal isOpen={helpOpen} onClose={() => setHelpOpen(false)} title={t('silver.helpTitle')}>
        <ul className="max-h-[60vh] space-y-3 overflow-y-auto pe-1">
          {Array.from({ length: 14 }, (_, v) => v).map((v) => (
            <li key={v} className="flex items-start gap-3">
              <SilverCardVisual
                card={{
                  Id: `help-${v}`,
                  Value: v,
                  FaceUp: true,
                  Protected: false,
                  AmuletProtected: false,
                  GuardedByCardId: null,
                }}
                size="xs"
              />
              <div className="min-w-0">
                <p className="text-sm font-bold text-gray-900">
                  {v}. {t(`games.Silver.cardNames.${cardNameKey(v)}`)}
                </p>
                <p className="text-xs leading-relaxed text-gray-600">{t(`games.Silver.cardAbilities.${cardNameKey(v)}`)}</p>
              </div>
            </li>
          ))}
        </ul>
      </Modal>

      {/* Round summary (scoring happened; the next round already runs live) */}
      <Modal
        isOpen={summaryRound !== null && silver.LastRoundScores !== null}
        onClose={() => setSummaryRound(null)}
        title={t('silver.roundSummaryTitle', { n: summaryRound ?? 0 })}
      >
        {summaryRound !== null && silver.LastRoundScores && (() => {
          const scores = silver.LastRoundScores!
          const min = Math.min(...scores)
          const winners = state.players
            .map((_, i) => i)
            .filter((i) => !eliminated.includes(i) && scores[i] === min)
            .map((i) => seatName(i))
          return (
            <div>
              <p className="text-sm font-semibold text-gray-900">
                {winners.length === 1
                  ? t('silver.roundSummaryWin', { name: winners[0], score: min })
                  : t('silver.roundSummaryTie', { names: winners.join(' & '), score: min })}
              </p>
              <p className="mt-1 text-xs text-gray-500">{t('silver.roundSummaryAmulet', { name: silver.AmuletHolderIndex !== null ? seatName(silver.AmuletHolderIndex) : '' })}</p>
              <ul className="mt-4 space-y-1.5">
                {state.players.map((player, i) => (
                  <li key={player.userId} className="flex items-center gap-2 rounded-lg bg-gray-50 px-3 py-1.5 text-sm">
                    <span className="min-w-0 flex-1 truncate font-medium text-gray-700">{seatName(i)}</span>
                    {silver.AmuletHolderIndex === i && <MoonSolid className="h-3.5 w-3.5 text-slate-400" aria-label={t('silver.amuletHolder')} />}
                    {silver.LastRoundCensusCallerIndex === i && (
                      <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[9px] font-bold uppercase text-amber-700">
                        {t('silver.callerBadge')}
                      </span>
                    )}
                    <span className="tabular-nums text-gray-500">{t('silver.roundSummaryRoundPts', { n: scores[i] ?? 0 })}</span>
                    <span className="w-14 text-end font-black tabular-nums text-gray-900">
                      {t('silver.roundSummaryTotal', { n: silver.CumulativeScores[i] ?? 0 })}
                    </span>
                  </li>
                ))}
              </ul>
              <p className="mt-4 text-center text-xs font-medium text-indigo-600">
                {!state.isOver ? t('silver.roundSummaryContinue', { n: silver.Round }) : t('silver.gameOver')}
              </p>
              <div className="flex justify-center pt-3">
                <Button variant="primary" onClick={() => setSummaryRound(null)}>
                  {t('common.close')}
                </Button>
              </div>
            </div>
          )
        })()}
      </Modal>

      {/* Winner / draw overlay */}
      <Modal isOpen={state.isOver} onClose={() => {}} title={t('silver.gameOverModalTitle')}>
        <div className="py-4 text-center">
          <TrophyIcon className="mx-auto h-12 w-12 text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">
            {winnerId ? t('silver.wins', { name: winnerName ?? '' }) : t('silver.tie')}
          </p>
          {!winnerId && <p className="mt-1 text-sm text-gray-600">{t('silver.tieDetail')}</p>}
          <p className="mt-4 text-xs font-semibold uppercase tracking-wide text-gray-400">{t('silver.standings')}</p>
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
                <span className="flex items-center gap-2">
                  {s.roundScore !== undefined && (
                    <span className="text-xs text-gray-400">{t('silver.finalRoundPts', { n: s.roundScore })}</span>
                  )}
                  <span className="tabular-nums">{s.score}</span>
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

      {/* AFK removal overlay */}
      <Modal isOpen={iWasRemoved} onClose={() => {}} title={t('silver.removedFromGame')}>
        <div className="py-4 text-center">
          <ClockIcon className="mx-auto h-12 w-12 text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">{t('silver.afkTitle')}</p>
          <p className="mt-1 text-sm text-gray-600">{t('silver.afkDetail')}</p>
          <p className="mt-1 text-xs text-gray-400">{t('silver.afkScoring')}</p>
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

/** Localized readable label of a visible card, for banners and aria labels. */
function cardLabel(card: SilverViewCard, t: (k: string) => string): string {
  if (card.Value === null) return t('silver.unknownCard')
  return t(`games.Silver.cardNames.${cardNameKey(card.Value)}`)
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
      title={overtimeSeconds > 0 ? t('silver.overtimeTooltip', { n: overtimeSeconds }) : t('silver.turnTooltip', { s: seconds })}
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
          className={critical ? 'stroke-red-500' : 'stroke-indigo-500'}
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
