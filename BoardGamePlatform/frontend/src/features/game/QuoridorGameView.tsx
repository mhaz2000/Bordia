import { useMemo, useState, useEffect, useCallback } from 'react'
import type { GameSession, GameState } from '@/shared/api/game'
import { useI18n } from '@/i18n/I18nProvider'
import { useNow, useCountdown } from '@/shared/hooks/useCountdown'
import {
  parseQuoridorState,
  getLegalMoves,
  getLegalWalls,
  quoridorActions,
  formatQuoridorEvent,
  teamForSeat,
} from './quoridor'
import { QuoridorBoard } from './Quoridor/QuoridorBoard'
import { WallTray } from './Quoridor/WallTray'
import { Wall } from './Quoridor/Wall'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { ClockIcon, PlayIcon, QuestionMarkCircleIcon, TrophyIcon } from '@heroicons/react/24/outline'

interface QuoridorGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

const PLAYER_AVATARS = ['bg-red-400', 'bg-blue-400', 'bg-emerald-400', 'bg-pink-400']

export function QuoridorGameView({ state, session, userId, onAction, isSending }: QuoridorGameViewProps) {
  const { t } = useI18n()
  const quoridor = useMemo(() => parseQuoridorState(state), [state])

  const [mode, setMode] = useState<'move' | 'wallH' | 'wallV'>('move')
  const [hoverCell, setHoverCell] = useState<{ r: number; c: number } | null>(null)
  const [helpOpen, setHelpOpen] = useState(false)
  const [draggedWallOrientation, setDraggedWallOrientation] = useState<'H' | 'V' | null>(null)

  useEffect(() => {
    setMode('move')
    setHoverCell(null)
    setDraggedWallOrientation(null)
  }, [state.version])

  if (!quoridor) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-stone-950">
        <div className="rounded-3xl border border-stone-700/50 bg-stone-900/80 py-20 text-center text-stone-300">
          {t('quoridor.waitingState')}
        </div>
      </div>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const mySeat = meIndex >= 0 ? meIndex : -1
  const isMyTurn = mySeat >= 0 && quoridor.CurrentPlayerIndex === mySeat && !state.isOver
  const isEliminated = mySeat >= 0 && quoridor.EliminatedSeats.includes(mySeat)
  const iWasRemoved = !state.isOver && isEliminated

  const seatName = (i: number): string => {
    const uid = state.players[i]?.userId
    if (uid === userId) return t('common.you')
    return session.players.find((p) => p.userId === uid)?.displayName ?? t('game.playerPrefix', { id: String(i + 1) })
  }

  const timerConfig = quoridor.TimerConfig ?? { BaseTurnSeconds: 90, MaxBankSeconds: 180, MaxOverrunSeconds: 15, MaxAfkTurns: 3, TotalGameTimeMinutes: 60 }
  const now = useNow()
  const gameRemainingMs = useCountdown(state.gameEndsAtUtc)

  const myTimer = mySeat >= 0 ? quoridor.PlayerTimers?.[mySeat] : null
  const bankSeconds = myTimer?.BankSeconds ?? 0
  const deferredPenalty = myTimer?.DeferredPenaltySeconds ?? 0
  const rawAllowance = timerConfig.BaseTurnSeconds + bankSeconds - deferredPenalty
  const floor = Math.max(0, timerConfig.BaseTurnSeconds - timerConfig.MaxOverrunSeconds)
  const allowance = Math.max(floor, Math.min(timerConfig.MaxBankSeconds, rawAllowance))
  const hardDeadline = state.nextActionDeadlineUtc ? new Date(state.nextActionDeadlineUtc).getTime() : null
  const secondsLeft = hardDeadline ? Math.ceil((hardDeadline - now) / 1000) : 0
  const fraction = Math.max(0, Math.min(1, allowance > 0 ? Math.max(0, secondsLeft) / allowance : 0))
  const critical = secondsLeft <= 10 && secondsLeft > -timerConfig.MaxOverrunSeconds
  const overtime = secondsLeft < 0 ? Math.min(Math.abs(secondsLeft), timerConfig.MaxOverrunSeconds) : 0
  const gameClockSeconds = gameRemainingMs > 0 ? Math.ceil(gameRemainingMs / 1000) : 0
  const gameClockCritical = gameClockSeconds > 0 && gameClockSeconds <= 60

  const legalMoves = useMemo(() => (isMyTurn && mode === 'move' && mySeat >= 0) ? getLegalMoves(quoridor, mySeat) : [], [quoridor, mySeat, isMyTurn, mode])
  const legalWalls = useMemo(() => (isMyTurn && (mode === 'wallH' || mode === 'wallV') && mySeat >= 0) ? getLegalWalls(quoridor, mySeat) : [], [quoridor, mySeat, isMyTurn, mode])

  const legalMoveSet = useMemo(() => new Set(legalMoves.map(m => `${m.row},${m.col}`)), [legalMoves])
  const legalWallSet = useMemo(() => new Set(legalWalls.map(w => `${w.Row},${w.Col},${w.Orientation}`)), [legalWalls])

  // --------------------------- winner / standings ---------------------------
  // The authoritative winner lives on the root GameState (a PlayerId); the
  // QuoridorState payload mirrors it as WinnerSeat/Draw from the engine. 2p:
  // the single seat whose pawn crossed. 4p: the whole team (seats with the
  // same team parity) shares the victory. Game over with no winner = draw.
  const winnerSeats = useMemo(() => {
    if (!state.isOver || !quoridor) return []
    const winnerUid = state.winner?.userId
    const winnerSeat =
      winnerUid != null && winnerUid.length > 0
        ? state.players.findIndex((p) => p.userId === winnerUid)
        : quoridor.WinnerSeat
    if (winnerSeat < 0) return []
    if (quoridor.SeatCount === 4) {
      const team = teamForSeat(winnerSeat, 4)
      return state.players.map((_, i) => i).filter((i) => teamForSeat(i, 4) === team)
    }
    return [winnerSeat]
  }, [state.isOver, state.winner, state.players, quoridor])

  const winnerNames = winnerSeats.map((s) => seatName(s))

  // Standings for the modal: winning seats first, then the rest in seat order.
  const standings = useMemo(() => {
    if (winnerSeats.length === 0) {
      return state.players.map((_, i) => i).map((seat) => ({ seat, winner: false }))
    }
    const rest = state.players.map((_, i) => i).filter((i) => !winnerSeats.includes(i))
    return [...winnerSeats, ...rest].map((seat) => ({ seat, winner: winnerSeats.includes(seat) }))
  }, [state.players, winnerSeats])

  const handleCellClick = useCallback((r: number, c: number) => {
    if (!isMyTurn || isSending) return
    if (mode === 'move' && legalMoveSet.has(`${r},${c}`)) {
      onAction('MovePawn', quoridorActions.movePawn(r, c))
    }
  }, [isMyTurn, isSending, mode, legalMoveSet, onAction])

  const handleSlotHover = useCallback((r: number, c: number) => {
    setHoverCell({ r, c })
  }, [])

  const handleWallDragStart = useCallback((orientation: 'H' | 'V') => {
    setDraggedWallOrientation(orientation)
    setMode(orientation === 'H' ? 'wallH' : 'wallV')
  }, [])

  const handleWallDragEnd = useCallback((slot: { r: number; c: number; orientation: 'H' | 'V' } | null) => {
    if (slot) {
      const key = `${slot.r},${slot.c},${slot.orientation}`
      if (legalWallSet.has(key)) {
        onAction('PlaceWall', quoridorActions.placeWall(slot.r, slot.c, slot.orientation))
      }
    }
    setDraggedWallOrientation(null)
    setMode('move')
    setHoverCell(null)
  }, [legalWallSet, onAction])

  const canPlaceWall = isMyTurn && !state.isOver && !isEliminated && quoridor.WallsRemaining[mySeat] > 0

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <p role="status" className="sr-only">
        {state.isOver ? t('quoridor.gameOver') : isMyTurn ? t('quoridor.yourTurn') : t('quoridor.waitingFor', { name: seatName(quoridor.CurrentPlayerIndex) })}
      </p>

      {/* ============================ STATUS STRIP ============================ */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-2xl border border-stone-700/50 bg-stone-900/80 px-4 py-3 shadow-lg backdrop-blur">
        {state.isOver ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-stone-100/70">
            <TrophyIcon className="h-4 w-4 text-amber-300" /> {t('quoridor.gameOver')}
          </span>
        ) : isEliminated ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-rose-300">
            <ClockIcon className="h-4 w-4 text-rose-300/60" /> {t('quoridor.removedFromGame')}
          </span>
        ) : isMyTurn ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-400/15 px-3 py-1 text-sm font-semibold text-amber-200 ring-1 ring-amber-300/40">
            <PlayIcon className="h-4 w-4" /> {t('quoridor.yourTurn')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 text-sm text-stone-100/60">
            <ClockIcon className="h-4 w-4 text-amber-200/40" />
            {t('quoridor.waitingFor', { name: seatName(quoridor.CurrentPlayerIndex) })}
          </span>
        )}

        {!state.isOver && isMyTurn && !isEliminated && (
          <TimerRing seconds={Math.max(0, secondsLeft)} overtimeSeconds={overtime} fraction={fraction} critical={critical} />
        )}

        {!state.isOver && gameClockSeconds > 0 && (
          <div
            className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 ring-1 ${gameClockCritical ? 'bg-rose-600/80 ring-rose-300/40' : 'bg-white/5 ring-white/10'}`}
            title={t('quoridor.gameClockTitle')}
          >
            <ClockIcon className={`h-3.5 w-3.5 ${gameClockCritical ? 'text-white' : 'text-stone-200/60'}`} />
            <span className={`text-xs font-bold tabular-nums ${gameClockCritical ? 'text-white' : 'text-stone-100/70'}`}>
              {Math.floor(gameClockSeconds / 60)}:{String(gameClockSeconds % 60).padStart(2, '0')}
            </span>
          </div>
        )}

        <div className="flex-1" />

        <button
          type="button"
          onClick={() => setHelpOpen(true)}
          className="inline-flex items-center gap-1.5 rounded-full bg-white/5 px-3 py-1.5 text-xs font-semibold text-stone-100/70 ring-1 ring-white/10 transition-colors hover:bg-amber-400/15 hover:text-amber-100"
        >
          <QuestionMarkCircleIcon className="h-4 w-4" />
          {t('quoridor.helpBtn')}
        </button>
      </div>

      {/* ============================ BOARD + WALL SUPPLY ============================ */}
      <div className="flex flex-col items-center justify-center gap-4 lg:flex-row lg:items-start">
        <div className="w-full max-w-[640px] rounded-2xl border border-stone-700/50 bg-stone-900/80 p-4 shadow-[0_16px_48px_rgba(0,0,0,0.4)]">
          <QuoridorBoard
            quoridor={quoridor}
            mode={mode}
            hoverCell={hoverCell}
            isMyTurn={isMyTurn && !isEliminated}
            mySeat={mySeat}
            legalMoveSet={legalMoveSet}
            legalWallSet={legalWallSet}
            onCellClick={handleCellClick}
            onSlotHover={handleSlotHover}
            onWallDragEnd={handleWallDragEnd}
            draggedWallOrientation={draggedWallOrientation}
          />
        </div>

        <div className="lg:sticky lg:top-4">
          <WallTray
            wallsRemaining={quoridor.WallsRemaining[mySeat] ?? 0}
            onDragStart={handleWallDragStart}
            isMyTurn={isMyTurn}
            disabled={!canPlaceWall}
          />
        </div>
      </div>

      {/* ============================ SCOREBOARD + LOG ============================ */}
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-stone-700/50 bg-stone-900/80 px-4 py-3 shadow-lg backdrop-blur">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-amber-200/60">{t('quoridor.scoreboard')}</p>
          <ul className="space-y-1.5">
            {state.players.map((player, index) => {
              const eliminated = quoridor.EliminatedSeats.includes(index)
              const isCurrent = index === quoridor.CurrentPlayerIndex && !state.isOver && !eliminated
              return (
                <li
                  key={player.userId}
                  className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm ${
                    player.userId === userId ? 'bg-amber-400/10 ring-1 ring-amber-300/20' : ''
                  } ${eliminated ? 'opacity-50' : ''}`}
                >
                  <span className={`flex h-6 w-6 items-center justify-center rounded-full text-[10px] font-bold text-stone-950 ${
                    isCurrent ? 'ring-2 ring-amber-400 animate-pulse' : ''
                  } ${PLAYER_AVATARS[index % PLAYER_AVATARS.length]}`}>
                    {index + 1}
                  </span>
                  <span className="min-w-0 flex-1 truncate font-medium text-stone-100">{seatName(index)}</span>
                  <span className="flex items-center gap-1 text-[10px] font-semibold tabular-nums text-stone-400">
                    <Wall orientation="H" style={{ width: 14, height: 4 }} />
                    {quoridor.WallsRemaining[index]}
                  </span>
                  {eliminated && <span className="text-[10px] font-semibold text-rose-400">{t('quoridor.eliminated')}</span>}
                  {isCurrent && <span className="animate-pulse text-[10px] font-bold text-amber-300">{t('quoridor.yourTurn')}</span>}
                </li>
              )
            })}
          </ul>
        </div>

        <details className="group h-fit self-start rounded-2xl border border-stone-700/50 bg-stone-900/80 shadow-lg backdrop-blur open:pb-2">
          <summary className="flex cursor-pointer list-none select-none items-center justify-between px-4 py-3">
            <span className="text-sm font-semibold text-stone-100">{t('quoridor.gameLog')}</span>
            <span className="text-xs text-stone-400 group-open:hidden">{t('quoridor.show')}</span>
            <span className="hidden text-xs text-stone-400 group-open:inline">{t('quoridor.hide')}</span>
          </summary>
          <ul className="max-h-64 space-y-1 overflow-y-auto px-4 pb-3">
            {quoridor.EventLog.length === 0 ? (
              <li className="py-4 text-center text-sm text-stone-400">{t('quoridor.noEvents')}</li>
            ) : (
              quoridor.EventLog.slice(-40)
                .reverse()
                .map((entry, i) => (
                  <li key={`${quoridor.EventLog.length - i}`} className="flex items-start gap-2 text-xs text-stone-300">
                    <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-amber-400" />
                    <span>{formatQuoridorEvent(entry, t)}</span>
                  </li>
                ))
            )}
          </ul>
        </details>
      </div>

      {/* ============================ HELP ============================ */}
      <Modal dark isOpen={helpOpen} onClose={() => setHelpOpen(false)} title={t('quoridor.helpTitle')}>
        <ul className="space-y-3">
          {[1, 2, 3, 4, 5, 6].map((n) => (
            <li key={n} className="flex items-start gap-3 text-sm text-stone-100/80">
              <span className="mt-1 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full bg-amber-400/20 text-[10px] font-black text-amber-200 ring-1 ring-amber-300/30">{n}</span>
              {t(`quoridor.rule${n}`)}
            </li>
          ))}
        </ul>
      </Modal>

      {/* ============================ GAME OVER ============================ */}
      <Modal dark isOpen={state.isOver} onClose={() => {}} title={t('quoridor.gameOverModalTitle')}>
        <div className="py-4 text-center">
          <TrophyIcon className="mx-auto h-12 w-12 text-amber-300" />
          <p className="mt-3 text-lg font-semibold text-stone-50">
            {quoridor.Draw
              ? t('quoridor.tie')
              : winnerSeats.length > 1
                ? t('quoridor.teamWins', { name: winnerNames.join(' & ') })
                : winnerSeats.length === 1
                  ? t('quoridor.wins', { name: winnerNames[0] })
                  : t('quoridor.tie')}
          </p>
          {quoridor.Draw && <p className="mt-1 text-sm text-stone-100/65">{t('quoridor.tieDetail')}</p>}
          <p className="mt-4 text-xs font-semibold uppercase tracking-wide text-stone-200/45">{t('quoridor.standings')}</p>
          <ul className="mt-2 space-y-1">
            {standings.map((s, rank) => (
              <li
                key={state.players[s.seat]?.userId}
                className={`flex items-center justify-between rounded-lg px-3 py-1.5 text-sm ${
                  s.winner ? 'bg-amber-300/15 font-semibold text-amber-200 ring-1 ring-amber-300/30' : 'bg-white/5 text-stone-100/70'
                }`}
              >
                <span>
                  {rank + 1}. {seatName(s.seat)}
                </span>
                <span className="flex items-center gap-1 text-xs font-black tabular-nums">
                  <Wall orientation="H" style={{ width: 14, height: 4 }} />
                  {quoridor.WallsRemaining[s.seat]}
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
      <Modal dark isOpen={iWasRemoved} onClose={() => {}} title={t('quoridor.removedFromGame')}>
        <div className="py-4 text-center">
          <ClockIcon className="mx-auto h-12 w-12 text-amber-300" />
          <p className="mt-3 text-lg font-semibold text-stone-50">{t('quoridor.afkTitle')}</p>
          <p className="mt-1 text-sm text-stone-100/65">{t('quoridor.afkDetail')}</p>
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

function TimerRing({ seconds, overtimeSeconds, fraction, critical }: { seconds: number; overtimeSeconds: number; fraction: number; critical: boolean }) {
  const { t } = useI18n()
  const R = 22
  const C = 2 * Math.PI * R
  return (
    <div className={`relative h-12 w-12 flex-shrink-0 ${overtimeSeconds > 0 ? 'animate-pulse' : ''}`} title={overtimeSeconds > 0 ? t('quoridor.overtimeTooltip', { n: overtimeSeconds }) : t('quoridor.turnTooltip', { s: seconds })}>
      <svg viewBox="0 0 56 56" className="h-12 w-12 -rotate-90">
        <circle cx="28" cy="28" r={R} fill="none" strokeWidth="5" className="stroke-white/10" />
        <circle cx="28" cy="28" r={R} fill="none" strokeWidth="5" strokeLinecap="round" strokeDasharray={C} strokeDashoffset={C * (1 - fraction)} className={critical ? 'stroke-rose-400' : 'stroke-amber-400'} style={{ transition: 'stroke-dashoffset 0.25s linear' }} />
      </svg>
      <span className={`absolute inset-0 flex items-center justify-center text-sm font-bold tabular-nums ${overtimeSeconds > 0 ? 'text-rose-300' : critical ? 'text-rose-400' : 'text-amber-50'}`}>
        {overtimeSeconds > 0 ? `-${overtimeSeconds}` : seconds}
      </span>
    </div>
  )
}