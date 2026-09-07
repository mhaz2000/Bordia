import { useMemo, useState } from 'react'
import { ArrowPathIcon, ClockIcon, PlayIcon, TrophyIcon } from '@heroicons/react/24/outline'
import type { GameSession, GameState } from '@/shared/api/game'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { useCountdown } from '@/shared/hooks/useCountdown'
import {
  COLOR_CHOICES,
  cardColor,
  cardValueLabel,
  isPlayable,
  isWild,
  parseUnoState,
  type UnoCard,
} from './uno'

interface UnoGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

export function UnoGameView({ state, session, userId, onAction, isSending }: UnoGameViewProps) {
  const [colorForCard, setColorForCard] = useState<UnoCard | null>(null)
  const [confirmDraw, setConfirmDraw] = useState(false)

  const uno = useMemo(() => parseUnoState(state), [state])

  if (!uno) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-gray-500">
          Waiting for the first game state...
        </CardContent>
      </Card>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const myCards: UnoCard[] = meIndex >= 0 ? uno.PlayerHands[meIndex]?.Cards ?? [] : []
  const isMyTurn = meIndex >= 0 && meIndex === uno.CurrentPlayerIndex
  const topCard = uno.DiscardPile[uno.DiscardPile.length - 1]
  const activeColor = uno.CurrentColor ?? topCard?.Color ?? 0
  const activeColorMeta = cardColor(activeColor)
  const clockwise = uno.Direction >= 1

  const eliminated = uno.EliminatedPlayerIndexes ?? []
  const iWasRemoved = !state.isOver && meIndex >= 0 && eliminated.includes(meIndex)

  // Turn timer countdown driven by the platform deadline on the state root.
  const remainingMs = useCountdown(state.nextActionDeadlineUtc)
  const remainingSeconds = Math.max(0, Math.ceil(remainingMs / 1000))
  const currentTimer = (uno.PlayerTimers ?? [])[uno.CurrentPlayerIndex]
  const timerConfig = uno.TimerConfig
  const baseSeconds = timerConfig?.BaseTurnSeconds ?? 30
  const overrunCeiling = timerConfig?.MaxOverrunSeconds ?? 15
  const allottedSeconds = Math.max(
    Math.max(0, baseSeconds - overrunCeiling),
    baseSeconds + (currentTimer?.BankSeconds ?? 0) - (currentTimer?.DeferredPenaltySeconds ?? 0)
  )
  const timerFraction =
    allottedSeconds > 0 && remainingMs > 0 ? Math.min(1, remainingMs / 1000 / allottedSeconds) : 0
  const timerCritical = remainingSeconds <= 5

  const handCounts = state.players.map((_, i) => uno.PlayerHands[i]?.Cards?.length ?? 0)
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

  const requestDraw = () => setConfirmDraw(true)
  const confirmDrawAction = () => {
    setConfirmDraw(false)
    onAction('DrawCard', { Count: 1 })
  }
  const callUno = () => onAction('CallUno', {})
  const acceptDraw = () => onAction('AcceptDraw', {})
  const challenge = () => onAction('ChallengeWildDrawFour', {})

  const canDraw = isMyTurn && uno.PendingDrawCount === 0
  const canCallUno = isMyTurn && myCards.length === 1 && !uno.UnoCalled && uno.UnoPendingPlayerIndex === meIndex
  const pendingDraw = uno.PendingDrawCount > 0 && isMyTurn
  const isWildDrawFour = !!topCard && isWild(topCard) && topCard.Value === 14

  const currentPlayerUserId = state.players[uno.CurrentPlayerIndex]?.userId
  const activePlayerName =
    currentPlayerUserId === userId
    ? 'You'
    : session.players.find((p) => p.userId === currentPlayerUserId)?.displayName ?? 'Player'

  const handleCardClick = (card: UnoCard) => {
    if (!isMyTurn || isSending || !isPlayable(card, uno)) return
    if (isWild(card)) setColorForCard(card)
    else playCard(card)
  }

  const handZero = myCards.length === 0
  const canPlaceOnPile = !state.isOver

  return (
    <div className="grid gap-6 lg:grid-cols-4">
      <div className="lg:col-span-3 space-y-6">
        <div className="rounded-2xl bg-gradient-to-br from-emerald-100 via-white to-emerald-50 border border-emerald-200 p-4 sm:p-6 shadow-sm">
          {/* Opponent seats */}
          <div className="flex flex-wrap gap-4 justify-center lg:justify-start">
            {state.players.map((player, i) => {
              if (i === meIndex) return null
              const isTurn = uno.CurrentPlayerIndex === i
              const removed = eliminated.includes(i)
              const displayName = session.players.find((p) => p.userId === player.userId)?.displayName ?? 'Player'
              return (
                <div
                  key={player.userId}
                  className={`flex flex-col items-center gap-1.5 px-3 py-2 rounded-xl bg-white/80 border transition-all ${
                    removed
                      ? 'border-gray-200 opacity-50'
                      : isTurn
                        ? 'border-emerald-400 ring-2 ring-emerald-200'
                        : 'border-gray-200'
                  }`}
                >
                  <div className={`w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold text-white shadow ${removed ? 'bg-gray-400' : 'bg-blue-500'}`}>
                    {displayName.charAt(0).toUpperCase()}
                  </div>
                  <p className="text-xs font-medium text-gray-700 max-w-20 truncate">{displayName}</p>
                  {removed ? (
                    <span className="text-xs font-semibold text-gray-500">Removed (AFK)</span>
                  ) : (
                    <div className="flex items-center gap-1">
                      <span className="text-xs font-semibold text-gray-500">
                        {handCounts[i]} {handCounts[i] === 1 ? 'card' : 'cards'}
                      </span>
                      {handCounts[i] === 1 && <span className="text-xs text-amber-500">Uno!</span>}
                    </div>
                  )}
                </div>
              )
            })}
          </div>

          {/* Table status: current color + direction + pending draw */}
          <div className="flex flex-wrap items-center justify-center gap-3 my-4">
            <div className="flex items-center gap-2 rounded-full bg-white px-4 py-2 shadow-sm border border-gray-200">
              <span
                className={`w-5 h-5 rounded-full border-2 border-white shadow ${activeColorMeta.gradient}`}
                aria-label={`Active color ${activeColorMeta.name}`}
              />
              <span className="text-sm font-medium text-gray-700">{activeColorMeta.name}</span>
            </div>
            <div className="flex items-center gap-1.5 rounded-full bg-white px-4 py-2 shadow-sm border border-gray-200">
              <ArrowPathIcon className={`w-4 h-4 text-gray-600 ${clockwise ? '' : 'scale-x-[-1]'}`} />
              <span className="text-sm font-medium text-gray-700">{clockwise ? 'Clockwise' : 'Counter-clockwise'}</span>
            </div>
            {uno.PendingDrawCount > 0 && (
              <div className="rounded-full bg-rose-50 border border-rose-200 px-4 py-2">
                <span className="text-sm font-semibold text-rose-700">{uno.PendingDrawCount} cards to draw</span>
              </div>
            )}
          </div>

          {/* Table: discard + draw piles */}
          <div className="flex items-center justify-center gap-8">
            <button
              type="button"
              onClick={requestDraw}
              disabled={!canDraw || isSending}
              className={`flex flex-col items-center gap-1 ${
                canDraw ? 'cursor-pointer' : 'cursor-default'
              }`}
              aria-label="Draw pile"
            >
              <div
                className={`w-14 h-20 sm:w-16 sm:h-24 rounded-xl bg-gradient-to-br from-violet-500 to-violet-700 border-2 border-white/40 shadow-lg relative transition-transform ${
                  canDraw ? 'hover:-translate-y-2 hover:shadow-2xl' : ''
                }`}
              >
                <div className="absolute inset-2 rounded-lg bg-white/20 border border-white/30 flex items-center justify-center text-white text-xs font-bold">
                  DRAW
                </div>
              </div>
              <span className="text-xs text-gray-500">{uno.DrawPile?.Count ?? 0} left</span>
            </button>

            <div className="flex flex-col items-center">
              {topCard ? (
                <CardFaceVisual card={topCard} />
              ) : (
                <div className="w-16 h-24 sm:w-20 sm:h-28 rounded-xl bg-gray-200 flex items-center justify-center text-gray-400">
                  ?
                </div>
              )}
              <span className="mt-1 text-xs text-gray-500">Discard pile</span>
            </div>
          </div>
        </div>

        {/* Your hand */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between w-full">
              <CardTitle>Your hand ({myCards.length})</CardTitle>
              {isMyTurn && myCards.length === 1 && (
                <span className="text-sm font-semibold text-amber-500">Uno!</span>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {handZero ? (
              <p className="text-center text-gray-500 py-6">No cards in hand.</p>
            ) : (
              <div className="flex flex-wrap justify-center items-end pt-6 pb-2 px-2 min-h-[7.5rem]">
                {myCards.map((card, idx) => (
                  <div key={idx} className="-ml-8 first:ml-0 z-10 hover:z-20 transition-all">
                    <UnoCardFace
                      card={card}
                      clickable={canPlaceOnPile && isMyTurn && !isSending}
                      playable={isPlayable(card, uno)}
                      onClick={() => handleCardClick(card)}
                    />
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Right rail: status + actions + log */}
      <div className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Turn</CardTitle>
          </CardHeader>
          <CardContent>
            {state.isOver ? (
              <p className="text-sm text-gray-600">Game over.</p>
            ) : isMyTurn ? (
              <div className="space-y-2">
                <div className="flex items-center gap-2 text-emerald-700">
                  <PlayIcon className="w-4 h-4" />
                  <span className="font-medium">Your turn</span>
                </div>
                <TurnTimer
                  seconds={remainingSeconds}
                  fraction={timerFraction}
                  critical={timerCritical}
                />
              </div>
            ) : (
              <div className="space-y-2">
                <p className="text-sm text-gray-600">Waiting for {activePlayerName}...</p>
                <TurnTimer
                  seconds={remainingSeconds}
                  fraction={timerFraction}
                  critical={timerCritical}
                />
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Actions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {!isMyTurn && !state.isOver && (
              <p className="text-sm text-gray-500 text-center py-4">Take your turn when it arrives.</p>
            )}

            {pendingDraw && (
              <>
                {isWildDrawFour && uno.PendingDrawCount === 4 && (
                  <Button variant="secondary" className="w-full" onClick={challenge} isLoading={isSending}>
                    Challenge Wild Draw 4
                  </Button>
                )}
                <Button variant="danger" className="w-full" onClick={acceptDraw} isLoading={isSending}>
                  Accept draw {uno.PendingDrawCount}
                </Button>
              </>
            )}

            {canDraw && (
              <Button variant="primary" className="w-full" onClick={requestDraw} isLoading={isSending}>
                Draw card
              </Button>
            )}

            {canCallUno && (
              <Button variant="secondary" className="w-full" onClick={callUno} isLoading={isSending}>
                Call Uno!
              </Button>
            )}

            {!isMyTurn && uno.NextPlayerSkipped && (
              <p className="text-xs text-center text-gray-400">Next player is skipped.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Game log</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="max-h-72 overflow-y-auto space-y-1.5">
              {uno.EventLog.length === 0 ? (
                <li className="text-sm text-gray-400 text-center py-4">No events yet</li>
              ) : (
                uno.EventLog.slice(-40).reverse().map((entry, i) => (
                  <li key={`${uno.EventLog.length - i}`} className="text-xs text-gray-600 p-2 bg-gray-50 rounded">
                    {entry}
                  </li>
                ))
              )}
            </ul>
          </CardContent>
        </Card>
      </div>

      {/* Wild color picker */}
      <Modal isOpen={!!colorForCard} onClose={() => setColorForCard(null)} title="Choose a color">
        <p className="text-sm text-gray-600 mb-4">Wild card played — pick the color to continue.</p>
        <div className="grid grid-cols-2 gap-3">
          {COLOR_CHOICES.map((choice) => (
            <Button
              key={choice.id}
              variant="secondary"
              className={`${choice.swatch} text-white hover:opacity-90 w-full`}
              onClick={() => {
                if (colorForCard) playCard(colorForCard, choice.id)
                setColorForCard(null)
              }}
              disabled={isSending}
            >
              {choice.name}
            </Button>
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
          <Button variant="primary" className="flex-1" onClick={confirmDrawAction} isLoading={isSending}>
            Draw card
          </Button>
        </div>
      </Modal>

      {/* Winner overlay */}
      <Modal isOpen={state.isOver && !!winnerName} onClose={() => {}} title="Game over">
        <div className="text-center py-4">
          <TrophyIcon className="w-12 h-12 mx-auto text-amber-400" />
          <p className="mt-3 text-lg font-semibold text-gray-900">{winnerName} wins!</p>
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

interface UnoCardFaceProps {
  card: UnoCard
  clickable: boolean
  playable: boolean
  onClick: () => void
}

function UnoCardFace({ card, clickable, playable, onClick }: UnoCardFaceProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!clickable || !playable}
      aria-label={`Play ${cardValueLabel(card.Value)}`}
      className={`
        w-16 h-24 sm:w-20 sm:h-28 rounded-xl border-2 border-white/30 shadow-md transition-all
        ${clickable && playable
          ? 'cursor-pointer hover:-translate-y-3 hover:shadow-2xl ring-2 ring-emerald-300 ring-offset-2 ring-offset-slate-100'
          : clickable
            ? 'cursor-not-allowed opacity-40'
            : 'opacity-90'}
      `}
    >
      <CardFaceVisual card={card} />
    </button>
  )
}

function CardFaceVisual({ card }: { card: UnoCard }) {
  const color = cardColor(card.Color)
  const label = cardValueLabel(card.Value)
  return (
    <div className={`w-16 h-24 sm:w-20 sm:h-28 rounded-xl border-2 border-white/30 shadow-md overflow-hidden flex items-center justify-center ${color.gradient} ${color.text}`}>
      <div className="rounded-lg bg-white/20 border border-white/40 w-[calc(100%-0.75rem)] h-[calc(100%-0.75rem)] flex items-center justify-center">
        <span className="font-black text-lg sm:text-2xl">{label}</span>
      </div>
    </div>
  )
}

function TurnTimer({ seconds, fraction, critical }: { seconds: number; fraction: number; critical: boolean }) {
  if (seconds <= 0) return null
  return (
    <div className="flex items-center gap-2">
      <ClockIcon className={`w-4 h-4 ${critical ? 'text-red-500' : 'text-gray-500'}`} />
      <div className="flex-1 h-1.5 bg-gray-200 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${critical ? 'bg-red-500' : 'bg-emerald-500'}`}
          style={{ width: `${Math.round(fraction * 100)}%` }}
        />
      </div>
      <span className={`text-sm font-semibold tabular-nums ${critical ? 'text-red-600' : 'text-gray-700'}`}>
        {seconds}s
      </span>
    </div>
  )
}