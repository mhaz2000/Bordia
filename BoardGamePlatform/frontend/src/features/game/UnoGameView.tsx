import { useMemo, useState } from 'react'
import { ArrowPathIcon, ClockIcon, PlayIcon, TrophyIcon } from '@heroicons/react/24/outline'
import type { GameSession, GameState } from '@/shared/api/game'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { useCountdown, useNow } from '@/shared/hooks/useCountdown'
import {
  CARD_HEX,
  COLOR_CHOICES,
  cardValueLabel,
  isPlayable,
  isWild,
  parseUnoState,
  type UnoCard,
} from './uno'
import { UnoCardVisual, UnoCardBackVisual } from '@/shared/components/UnoCardVisual'

interface UnoGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

export function UnoGameView({ state, session, userId, onAction, isSending }: UnoGameViewProps) {
  const [wildPick, setWildPick] = useState<UnoCard | null>(null)
  const [confirmDraw, setConfirmDraw] = useState(false)

  const uno = useMemo(() => parseUnoState(state), [state])

  if (!uno) {
    return (
      <div className="rounded-3xl border border-emerald-200 bg-gradient-to-b from-emerald-900 to-emerald-950 py-20 text-center text-emerald-200/70">
        Waiting for the first game state...
      </div>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const myCards: UnoCard[] = meIndex >= 0 ? uno.PlayerHands[meIndex]?.Cards ?? [] : []
  const isMyTurn = meIndex >= 0 && meIndex === uno.CurrentPlayerIndex
  const topCard = uno.DiscardPile[uno.DiscardPile.length - 1]
  const activeColor = uno.CurrentColor ?? topCard?.Color ?? 0
  const clockwise = uno.Direction >= 1
  const eliminated = uno.EliminatedPlayerIndexes ?? []
  const iWasRemoved = !state.isOver && meIndex >= 0 && eliminated.includes(meIndex)

  // Turn timer: signed countdown with an overtime grace window. The state's
  // deadline is the HARD limit (allowance + grace); the soft end is
  // TurnStartUtc + allowance. Acting inside the grace window is allowed and
  // the overshoot shortens the player's next allowance.
  const now = useNow()
  const gameRemainingMs = useCountdown(state.gameEndsAtUtc)
  const turnStartMs = uno.TurnStartUtc ? Date.parse(uno.TurnStartUtc) : Number.NaN
  const currentTimer = (uno.PlayerTimers ?? [])[uno.CurrentPlayerIndex]
  const timerConfig = uno.TimerConfig
  const baseSeconds = timerConfig?.BaseTurnSeconds ?? 30
  const maxBankSeconds = timerConfig?.MaxBankSeconds ?? 120
  const graceSeconds = timerConfig?.MaxOverrunSeconds ?? 15
  const allowanceSeconds = Math.max(
    Math.max(0, baseSeconds - graceSeconds),
    Math.min(
      maxBankSeconds,
      baseSeconds + (currentTimer?.BankSeconds ?? 0) - (currentTimer?.DeferredPenaltySeconds ?? 0)
    )
  )
  const softEndMs = Number.isNaN(turnStartMs) ? Number.NaN : turnStartMs + allowanceSeconds * 1000
  const signedRemainingMs = Number.isNaN(softEndMs) ? 0 : softEndMs - now
  const remainingSeconds = Math.max(0, Math.ceil(signedRemainingMs / 1000))
  const overtimeSeconds = signedRemainingMs < 0 ? Math.min(graceSeconds, Math.floor(-signedRemainingMs / 1000)) : 0
  const timerFraction = Math.max(0, Math.min(1, signedRemainingMs / 1000 / Math.max(1, allowanceSeconds)))
  const timerCritical = signedRemainingMs <= 0

  // Game clock: overall time limit before the game is force-finished.
  const gameRemainingMinutes = Math.floor(gameRemainingMs / 60000)
  const gameRemainingSeconds = Math.floor((gameRemainingMs % 60000) / 1000)
  const gameClockLabel =
    state.isOver || gameRemainingMs <= 0
      ? null
      : `${gameRemainingMinutes}:${String(gameRemainingSeconds).padStart(2, '0')}`
  const gameClockCritical = gameRemainingMs <= 5 * 60 * 1000

  const handCounts = state.players.map((_, i) => uno.PlayerHands[i]?.Cards?.length ?? 0)
  const drawPileCount = uno.DrawPile?.Cards?.length ?? uno.DrawPile?.Count ?? 0
  const winnerId = state.isOver ? state.winner?.userId : undefined
  const winnerName =
    winnerId === undefined ? undefined
    : session.players.find((p) => p.userId === winnerId)?.displayName
    ?? (winnerId === userId ? 'You' : 'Player')

  const playCard = (card: UnoCard, chosenColor?: number) => {
    const payload: Record<string, unknown> = { Card: { Color: card.Color, Value: card.Value } }
    if (chosenColor !== undefined) payload.ChosenColor = chosenColor
    onAction('PlayCard', payload)
  }

  const handleCardClick = (card: UnoCard) => {
    // While MY draw penalty is pending, playing is forbidden. (A debt belonging
    // to another player - e.g. they were skipped - does not block me.)
    if (!isMyTurn || isSending || iOweDraw || !isPlayable(card, uno)) return
    if (isWild(card)) setWildPick(card)
    else playCard(card)
  }

  // A pending draw debt belongs to a specific player (it survives their skipped
  // turn). Only that player is locked out of playing; everyone else acts normal.
  const iOweDraw =
    uno.PendingDrawCount > 0 &&
    (uno.PendingDrawTargetIndex == null || uno.PendingDrawTargetIndex === meIndex)
  const pendingDraw = iOweDraw && isMyTurn
  const isWildDrawFour = !!topCard && isWild(topCard) && topCard.Value === 14
  const challengeResolved = !!uno.PendingDrawOffenderIndex
  const canChallenge = pendingDraw && isWildDrawFour && uno.PendingDrawCount === 4 && !challengeResolved
  const canDraw = isMyTurn && !iOweDraw && !uno.DrawnThisTurn
  // Pass appears whenever a voluntary draw has not been followed by a play.
  // Normally the backend auto-passes an unplayable drawn card, so this only
  // shows to decline a playable drawn card - but it also guarantees the player
  // can never get stuck (e.g. a backend/frontend version mismatch).
  const playableCount = pendingDraw ? 0 : myCards.filter((c) => isPlayable(c, uno)).length
  const canPass = isMyTurn && !iOweDraw && !!uno.DrawnThisTurn
  const canCallUno = isMyTurn && myCards.length === 1 && !uno.UnoCalled && uno.UnoPendingPlayerIndex === meIndex

  const currentPlayerUserId = state.players[uno.CurrentPlayerIndex]?.userId
  const activePlayerName =
    currentPlayerUserId === userId
      ? 'You'
      : session.players.find((p) => p.userId === currentPlayerUserId)?.displayName ?? 'Player'

  const offenderName =
    uno.PendingDrawOffenderIndex == null
      ? null
      : uno.PendingDrawOffenderIndex === meIndex
        ? 'you'
        : session.players.find((p) => p.userId === state.players[uno.PendingDrawOffenderIndex!].userId)?.displayName ?? 'the offender'

  // Deterministic tilt for the discard top card so the pile looks "played".
  const discardTilt = ((uno.DiscardPile.length * 37) % 13) - 6

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      {/* ============================ TABLE ============================ */}
      <div className="relative rounded-3xl border border-emerald-950/50 bg-gradient-to-b from-emerald-800 via-emerald-900 to-emerald-950 p-4 sm:p-6 shadow-2xl overflow-hidden">
        {/* subtle felt texture highlight */}
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(255,255,255,0.08),transparent_60%)]" />

        {/* Opponent seats */}
        <div className="relative flex flex-wrap gap-3 justify-center">
          {state.players.map((player, i) => {
            if (i === meIndex) return null
            const isTurn = uno.CurrentPlayerIndex === i
            const removed = eliminated.includes(i)
            const displayName = session.players.find((p) => p.userId === player.userId)?.displayName ?? 'Player'
            return (
              <div
                key={player.userId}
                className={`flex items-center gap-2.5 rounded-2xl px-3 py-2 border backdrop-blur-sm transition-all ${
                  removed
                    ? 'border-white/10 bg-white/5 opacity-50'
                    : isTurn
                      ? 'border-amber-300/70 bg-white/10 ring-2 ring-amber-300/40 scale-[1.03]'
                      : 'border-white/15 bg-white/5'
                }`}
              >
                <div className={`w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold text-white shadow ${removed ? 'bg-gray-500' : isTurn ? 'bg-amber-500' : 'bg-emerald-600'}`}>
                  {displayName.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-white max-w-24 truncate leading-tight">{displayName}</p>
                  {removed ? (
                    <span className="text-[10px] font-medium text-white/50">Removed (AFK)</span>
                  ) : (
                    <div className="flex items-center gap-1.5">
                      <MiniCardStack count={handCounts[i]} />
                      {handCounts[i] === 1 && (
                        <span className="text-[10px] font-black text-amber-300 animate-pulse">UNO!</span>
                      )}
                    </div>
                  )}
                </div>
              </div>
            )
          })}
        </div>

        {/* Center: draw pile + discard + color/direction */}
        <div className="relative mt-6 flex items-center justify-center gap-10 sm:gap-16">
          {/* Draw pile */}
          <button
            type="button"
            onClick={() => setConfirmDraw(true)}
            disabled={!canDraw || isSending}
            aria-label={`Draw pile, ${drawPileCount} cards left`}
            className={`group relative flex flex-col items-center gap-2 ${canDraw ? 'cursor-pointer' : 'cursor-default'}`}
          >
            <div className="relative">
              {/* stacked backs */}
              <UnoCardBackVisual className="absolute inset-0 translate-x-1.5 translate-y-1.5 rotate-3 opacity-60" size="xl" />
              <UnoCardBackVisual className="absolute inset-0 translate-x-0.5 translate-y-0.5 -rotate-2 opacity-80" size="xl" />
              <UnoCardBackVisual
                className={`relative transition-transform ${canDraw ? 'group-hover:-translate-y-3 group-hover:shadow-[0_0_25px_rgba(251,191,36,0.45)]' : ''}`}
                size="xl"
              />
            </div>
            <span className="rounded-full bg-black/40 px-2.5 py-0.5 text-[11px] font-bold text-white tabular-nums">
              {drawPileCount} left
            </span>
            {canDraw && (
              <span className="absolute -bottom-9 text-[11px] font-semibold text-amber-200 opacity-0 group-hover:opacity-100 transition-opacity">
                Draw a card
              </span>
            )}
          </button>

          {/* Discard pile */}
          <div className="relative flex flex-col items-center">
            <div className="relative w-24 h-36 sm:w-28 sm:h-40">
              {uno.DiscardPile.slice(0, -1).slice(-2).map((c, i) => (
                <div
                  key={`under-${i}`}
                  className="absolute inset-0 opacity-45"
                  style={{ transform: `rotate(${(i - 1) * 7}deg) translate(${(i - 1) * 4}px, ${(i - 1) * 3}px)` }}
                >
                  <UnoCardVisual card={c} size="xl" />
                </div>
              ))}
              {topCard && (
                <div className="absolute inset-0" style={{ transform: `rotate(${discardTilt}deg)` }}>
                  <UnoCardVisual card={topCard} size="xl" />
                </div>
              )}
              {uno.PendingDrawCount > 0 && (
                <div className="absolute -top-3 -right-4 z-10 animate-bounce rounded-full bg-rose-600 px-2.5 py-1 text-xs font-black text-white shadow-lg ring-2 ring-white/70">
                  +{uno.PendingDrawCount}
                </div>
              )}
            </div>
            <span className="mt-2 text-[11px] font-medium text-white/50">Discard pile</span>
          </div>
        </div>

        {/* Active color + direction */}
        <div className="relative mt-5 flex items-center justify-center gap-3">
          <div className="flex items-center gap-2 rounded-full bg-black/30 px-4 py-1.5 backdrop-blur-sm">
            <span
              className="h-4 w-4 rounded-full border-2 border-white shadow"
              style={{ background: CARD_HEX[activeColor] ?? CARD_HEX[0] }}
              aria-label={`Active color ${activeColor}`}
            />
            <span className="text-xs font-semibold text-white/80 capitalize">{colorName(activeColor)}</span>
          </div>
          <div
            className="flex items-center gap-1.5 rounded-full bg-black/30 px-3 py-1.5 backdrop-blur-sm"
            title={clockwise ? 'Play direction: clockwise' : 'Play direction: counter-clockwise'}
          >
            <ArrowPathIcon className={`h-3.5 w-3.5 text-white/70 ${clockwise ? '' : '-scale-x-100'}`} />
            <span className="text-xs font-medium text-white/60">{clockwise ? 'CW' : 'CCW'}</span>
          </div>
          {gameClockLabel && (
            <div
              className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 backdrop-blur-sm ${
                gameClockCritical ? 'bg-rose-600/80' : 'bg-black/30'
              }`}
              title="Time left before the game is force-finished (fewest cards wins)"
            >
              <ClockIcon className={`h-3.5 w-3.5 ${gameClockCritical ? 'text-white' : 'text-white/70'}`} />
              <span className={`text-xs font-bold tabular-nums ${gameClockCritical ? 'text-white' : 'text-white/60'}`}>
                {gameClockLabel}
              </span>
            </div>
          )}
        </div>

        {/* Pending draw banner */}
        {pendingDraw && (
          <div className="relative mt-5 mx-auto max-w-md rounded-2xl border border-rose-300/30 bg-rose-950/60 p-3 text-center backdrop-blur-sm">
            {challengeResolved ? (
              <>
                <p className="text-sm font-semibold text-rose-100">
                  Challenge successful - {offenderName} must draw {uno.PendingDrawCount}
                </p>
                <p className="mt-0.5 text-[11px] text-rose-200/70">Take the cards into the offender's hand, then play or draw.</p>
              </>
            ) : (
              <>
                <p className="text-sm font-semibold text-rose-100">
                  You must draw {uno.PendingDrawCount} card{uno.PendingDrawCount > 1 ? 's' : ''}
                  {isWildDrawFour && uno.PendingDrawCount === 4 ? ' or challenge the Wild Draw Four' : ''}
                </p>
                {!isWildDrawFour && (
                  <p className="mt-0.5 text-[11px] text-rose-200/70">Playing a card is not allowed while a draw penalty is pending.</p>
                )}
              </>
            )}
            <div className="mt-2 flex justify-center gap-2">
              {canChallenge && (
                <Button size="sm" onClick={() => onAction('ChallengeWildDrawFour', {})} isLoading={isSending}>
                  Challenge +4
                </Button>
              )}
              <Button size="sm" onClick={() => onAction('AcceptDraw', {})} isLoading={isSending}>
                {challengeResolved ? 'Apply draw & continue' : 'Accept draw'}
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* ============================ STATUS STRIP ============================ */}
      <div className="rounded-2xl bg-white border border-gray-200 px-4 py-3 flex flex-wrap items-center gap-x-4 gap-y-2 shadow-sm">
        {state.isOver ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-gray-500">
            <TrophyIcon className="h-4 w-4 text-amber-400" /> Game over.
          </span>
        ) : isMyTurn ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-sm font-semibold text-emerald-700">
            <PlayIcon className="h-4 w-4" /> Your turn
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 text-sm text-gray-600">
            <ClockIcon className="h-4 w-4 text-gray-400" />
            Waiting for <strong className="mx-0.5">{activePlayerName}</strong>
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

        <div className="flex-1" />

        {canCallUno && (
          <Button size="sm" variant="secondary" onClick={() => onAction('CallUno', {})} isLoading={isSending}>
            Call UNO!
          </Button>
        )}
        {canPass && (
          <Button size="sm" variant="secondary" onClick={() => onAction('Pass', {})} isLoading={isSending}>
            Pass
          </Button>
        )}
        {meIndex >= 0 && !state.isOver && (
          <span className="text-xs font-medium text-gray-400 tabular-nums">You: {myCards.length} cards</span>
        )}
      </div>

      {/* ============================ HAND ============================ */}
      <div className="rounded-2xl bg-white border border-gray-200 px-4 pt-5 pb-4 shadow-sm">
        <div className="flex items-center justify-between mb-1">
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400">
            Your hand {playableCount > 0 && isMyTurn && (
              <span className="ml-1 normal-case text-emerald-600">- {playableCount} playable</span>
            )}
          </p>
          {isMyTurn && playableCount === 0 && !iOweDraw && !state.isOver && (
            <p className="text-xs text-gray-400">{uno.DrawnThisTurn ? 'Drew a card - play it if it fits, or pass' : 'No playable card - draw from the pile'}</p>
          )}
          {pendingDraw && (
            <p className="text-xs font-semibold text-rose-600">You owe {uno.PendingDrawCount} card{uno.PendingDrawCount > 1 ? 's' : ''} - accept the draw first</p>
          )}
        </div>
        <div className="overflow-x-auto pb-3 pt-10">
          {myCards.length === 0 ? (
            <p className="text-center text-gray-400 py-8 text-sm">No cards in hand.</p>
          ) : (
            <div className="flex w-max mx-auto items-end px-6">
              {myCards.map((card, idx) => {
                const n = myCards.length
                const mid = (n - 1) / 2
                const rot = (idx - mid) * 2.5
                const arc = Math.min(Math.abs(idx - mid) * 5, 24)
                const overlap = n <= 6 ? 14 : n <= 9 ? 30 : 42
                const playable = !iOweDraw && isPlayable(card, uno)
                return (
                  <div
                    key={`${idx}-${card.Color}-${card.Value}`}
                    className="relative hover:z-50"
                    style={{
                      marginLeft: idx === 0 ? 0 : `-${overlap}px`,
                      transform: `rotate(${rot}deg)`,
                      marginTop: arc,
                      zIndex: idx,
                    }}
                  >
                    <button
                      type="button"
                      aria-label={`Play ${cardValueLabel(card.Value)}`}
                      disabled={!isMyTurn || isSending || !playable}
                      onClick={() => handleCardClick(card)}
                      className={`relative block rounded-xl transition-all duration-150 ${
                        isMyTurn && playable
                          ? 'cursor-pointer hover:-translate-y-4 hover:scale-105 shadow-[0_0_14px_rgba(16,185,129,0.55)] ring-2 ring-emerald-300'
                          : ''
                      } ${isMyTurn && !playable ? 'opacity-40 saturate-50 cursor-not-allowed' : ''} ${
                        !isMyTurn && !state.isOver ? 'opacity-80 cursor-default' : ''
                      }`}
                    >
                      <UnoCardVisual card={card} size="lg" />
                    </button>
                  </div>
                )
              })}
            </div>
          )}
        </div>
        {isMyTurn && !state.isOver && playableCount > 0 && (
          <p className="text-center text-[11px] text-gray-400 -mt-1">
            Click a glowing card to play it
          </p>
        )}
      </div>

      {/* ============================ GAME LOG ============================ */}
      <details className="group rounded-2xl bg-white border border-gray-200 shadow-sm open:pb-2">
        <summary className="flex cursor-pointer list-none items-center justify-between px-4 py-3 select-none">
          <span className="text-sm font-semibold text-gray-700">Game log</span>
          <span className="text-xs text-gray-400 group-open:hidden">show</span>
          <span className="hidden text-xs text-gray-400 group-open:inline">hide</span>
        </summary>
        <ul className="max-h-64 overflow-y-auto space-y-1 px-4 pb-3">
          {uno.EventLog.length === 0 ? (
            <li className="text-sm text-gray-400 text-center py-4">No events yet</li>
          ) : (
            uno.EventLog.slice(-40).reverse().map((entry, i) => (
              <li key={`${uno.EventLog.length - i}`} className="flex items-start gap-2 text-xs text-gray-600">
                <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-emerald-400" />
                <span>{entry}</span>
              </li>
            ))
          )}
        </ul>
      </details>

      {/* Wild color picker */}
      <Modal isOpen={!!wildPick} onClose={() => setWildPick(null)} title="Choose a color">
        <div className="flex items-center gap-4 mb-4">
          {wildPick && <UnoCardVisual card={wildPick} size="xl" />}
          <p className="text-sm text-gray-600">
            Play this wild card and pick the color to continue.
          </p>
        </div>
        <div className="grid grid-cols-4 gap-2 sm:gap-3">
          {COLOR_CHOICES.map((choice) => (
            <button
              key={choice.id}
              type="button"
              aria-label={`Choose ${choice.name}`}
              disabled={isSending}
              onClick={() => {
                if (wildPick) playCard(wildPick, choice.id)
                setWildPick(null)
              }}
              className={`group flex flex-col items-center gap-1 rounded-xl ${choice.swatch} p-0.5 shadow-md transition-transform hover:scale-105 hover:shadow-lg disabled:opacity-50`}
            >
              <span className="h-12 w-full rounded-lg sm:h-14" />
              <span className="pb-1 text-[11px] font-bold text-white drop-shadow">{choice.name}</span>
            </button>
          ))}
        </div>
      </Modal>

      {/* Draw confirmation */}
      <Modal isOpen={confirmDraw} onClose={() => setConfirmDraw(false)} title="Draw a card">
        <p className="text-sm text-gray-600">Draw 1 card from the draw pile?</p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" className="flex-1" onClick={() => setConfirmDraw(false)}>
            Cancel
          </Button>
          <Button
            variant="primary"
            className="flex-1"
            isLoading={isSending}
            onClick={() => {
              setConfirmDraw(false)
              onAction('DrawCard', { Count: 1 })
            }}
          >
            Draw card
          </Button>
        </div>
      </Modal>

      {/* Winner / draw overlay */}
      <Modal isOpen={state.isOver && (!!winnerName || !!winnerId)} onClose={() => {}} title="Game over">
        <div className="text-center py-4">
          <TrophyIcon className="w-12 h-12 mx-auto text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">
            {winnerId ? `${winnerName} wins!` : "It's a tie!"}
          </p>
          {!winnerId && (
            <p className="mt-1 text-sm text-gray-600">
              The time limit was reached and players had the same number of cards.
            </p>
          )}
          <div className="mt-4">
            <Button variant="primary" asChild>
              <a href="/lobby">Back to Lobby</a>
            </Button>
          </div>
        </div>
      </Modal>

      {/* AFK removal overlay */}
      <Modal isOpen={iWasRemoved} onClose={() => {}} title="Removed from game">
        <div className="text-center py-4">
          <ClockIcon className="w-12 h-12 mx-auto text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">You were inactive for too long</p>
          <p className="mt-1 text-sm text-gray-600">You have been removed from this game.</p>
          <div className="mt-4">
            <Button variant="primary" asChild>
              <a href="/lobby">Back to Lobby</a>
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}

/* ============================ CARD VISUALS ============================ */

function colorName(id: number): string {
  return ['Red', 'Blue', 'Green', 'Yellow', 'Wild'][id] ?? 'Red'
}

/** Small card-stack icon with count for opponent seats. */
function MiniCardStack({ count }: { count: number }) {
  return (
    <span className="inline-flex items-center gap-1">
      <span className="relative inline-block h-4 w-3">
        <span className="absolute inset-0 translate-x-[2px] translate-y-[1px] rounded-[2px] border border-white/60 bg-red-500/80" />
        <span className="absolute inset-0 rounded-[2px] border border-white bg-red-400" />
      </span>
      <span className="text-[10px] font-bold text-white/80 tabular-nums">{count}</span>
    </span>
  )
}

/* ============================ STATUS WIDGETS ============================ */

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
  const R = 22
  const C = 2 * Math.PI * R
  return (
    <div
      className={`relative h-12 w-12 flex-shrink-0 ${overtimeSeconds > 0 ? 'animate-pulse' : ''}`}
      title={overtimeSeconds > 0 ? `${overtimeSeconds}s into overtime - act now or the turn is skipped` : `${seconds}s left this turn`}
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
