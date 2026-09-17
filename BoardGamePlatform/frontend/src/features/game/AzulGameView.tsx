import { useEffect, useMemo, useRef, useState } from 'react'
import {
  ClockIcon,
  HandThumbUpIcon,
  PlayIcon,
  QuestionMarkCircleIcon,
  TrophyIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline'
import type { GameSession, GameState } from '@/shared/api/game'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { useCountdown, useNow } from '@/shared/hooks/useCountdown'
import { AzulFactoryDisc, AzulMarker, AzulPlayerBoard, AzulTile } from '@/shared/components/AzulBoardVisuals'
import { flightAnchor, resetFlights } from './tokenFlight'
import { diffAzul, runAzulPlan } from './azulFlights'
import { useI18n } from '@/i18n/I18nProvider'
import {
  AZUL_FLOOR,
  COLOR_NAMES,
  DEFAULT_TIMER,
  colorCounts,
  formatAzulEvent,
  legalLines,
  mustFloor,
  parseAzulState,
  type AzulColor,
  type AzulState,
} from './azul'

interface AzulGameViewProps {
  state: GameState
  session: GameSession
  userId?: string
  onAction: (actionType: string, payload: Record<string, unknown>) => void
  isSending: boolean
}

type PickedSource = { kind: 'factory'; index: number } | { kind: 'center' }

const COLOR_SLUGS = ['blue', 'red', 'yellow', 'black', 'white'] as const

export function AzulGameView({ state, session, userId, onAction, isSending }: AzulGameViewProps) {
  const { t } = useI18n()
  const azul = useMemo(() => parseAzulState(state), [state])

  const [pickedSource, setPickedSource] = useState<PickedSource | null>(null)
  const [pickedColor, setPickedColor] = useState<AzulColor | null>(null)
  const [helpOpen, setHelpOpen] = useState(false)
  const [opponentModal, setOpponentModal] = useState<number | null>(null);

  // Any accepted action bumps the version; staged picks are stale.
  useEffect(() => {
    setPickedSource(null)
    setPickedColor(null)
  }, [state?.version])

  const now = useNow()
  const gameRemainingMs = useCountdown(state.gameEndsAtUtc)

  // ---------------------- §29 animation layer ----------------------
  const prevAzulRef = useRef<{ azul: AzulState; over: boolean } | null>(null)
  const [veilRound, setVeilRound] = useState<number | null>(null)
  const [penaltyFlash, setPenaltyFlash] = useState<number[]>([])

  // Esc clears a staged draft selection (spec §29: cancel is never a dialog).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setPickedSource(null)
        setPickedColor(null)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  // Reconcile broadcasts into tile flights, score floats, the round veil and
  // floor penalties by diffing consecutive projected states.
  useEffect(() => {
    if (!azul) return
    const before = prevAzulRef.current
    prevAzulRef.current = { azul, over: state.isOver }
    if (!before || before.azul === azul) return
    const plan = diffAzul(before.azul, azul, before.over, state.isOver)
    runAzulPlan(plan)
    if (plan.roundAdvanced) {
      setVeilRound(plan.newRound)
      window.setTimeout(() => setVeilRound(null), 950)
    }
    if (plan.penaltySeats.length > 0) {
      setPenaltyFlash(plan.penaltySeats)
      window.setTimeout(() => setPenaltyFlash([]), 900)
    }
  }, [azul])

  useEffect(() => () => resetFlights(), [])

  if (!azul) {
    return (
      <div className="rounded-3xl border border-sky-300/20 bg-gradient-to-b from-sky-950 to-slate-950 py-20 text-center text-sky-100/70">
        {t('azul.waitingState')}
      </div>
    )
  }

  const meIndex = state.players.findIndex((p) => p.userId === userId)
  const mySeat = meIndex >= 0 ? azul.Seats[meIndex] ?? null : null
  const isMyTurn = meIndex >= 0 && azul.CurrentPlayerIndex === meIndex && !state.isOver
  const eliminated = azul.EliminatedSeats
  const iWasRemoved = !state.isOver && meIndex >= 0 && eliminated.includes(meIndex)

  const seatName = (i: number): string => {
    const uid = state.players[i]?.userId
    if (uid === userId) return t('common.you')
    return session.players.find((p) => p.userId === uid)?.displayName ?? t('game.playerPrefix', { id: String(i + 1) })
  }
  const colorName = (c: AzulColor) => t(`azul.colors.${COLOR_SLUGS[c]}`)

  const send = (actionType: string, payload: Record<string, unknown>) => {
    if (isSending || !isMyTurn) return
    setPickedSource(null)
    setPickedColor(null)
    onAction(actionType, payload)
  }

  const sourceTiles =
    pickedSource === null ? [] : pickedSource.kind === 'center' ? azul.Center : azul.Factories[pickedSource.index] ?? []
  const chooser = pickedSource === null ? new Map<AzulColor, number>() : colorCounts(sourceTiles)

  const picking =
    isMyTurn && mySeat && pickedColor !== null
      ? { color: pickedColor, lines: legalLines(mySeat, pickedColor), forced: mustFloor(mySeat, pickedColor) }
      : null

  const draft = (lineIndex: number) => {
    if (pickedSource === null || pickedColor === null) return
    if (pickedSource.kind === 'factory') {
      send('DraftFromFactory', { FactoryIndex: pickedSource.index, Color: COLOR_NAMES[pickedColor], LineIndex: lineIndex })
    } else {
      send('DraftFromCenter', { Color: COLOR_NAMES[pickedColor], LineIndex: lineIndex })
    }
  }

  const markerInCenter = azul.MarkerSeat === -1

  // ------------------------------ timers ------------------------------

  const turnStartMs = azul.TurnStartUtc ? Date.parse(azul.TurnStartUtc) : Number.NaN
  const currentTimer = (azul.PlayerTimers ?? [])[azul.CurrentPlayerIndex]
  const baseSeconds = azul.TimerConfig?.BaseTurnSeconds ?? DEFAULT_TIMER.BaseTurnSeconds
  const maxBankSeconds = azul.TimerConfig?.MaxBankSeconds ?? DEFAULT_TIMER.MaxBankSeconds
  const graceSeconds = azul.TimerConfig?.MaxOverrunSeconds ?? DEFAULT_TIMER.MaxOverrunSeconds
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

  const standings = state.players
    .map((p, i) => ({
      userId: p.userId,
      name: seatName(i),
      seat: i,
      score: azul.Seats[i]?.Score ?? 0,
      floor: azul.Seats[i]?.Floor.length ?? 0,
    }))
    .sort((a, b) => b.score - a.score)
  const winnerId = state.isOver ? state.winner?.userId : undefined

  const centerChooser = pickedSource?.kind === 'center'

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <p role="status" className="sr-only">
        {state.isOver ? t('azul.gameOver') : isMyTurn ? t('azul.yourTurn') : t('azul.waitingFor', { name: seatName(azul.CurrentPlayerIndex) })}
      </p>

      {/* ============================ TABLE ============================ */}
      <div className="relative rounded-[2rem] bg-gradient-to-br from-slate-700 via-slate-900 to-black p-2.5 shadow-2xl sm:p-3.5">
        <div className="pointer-events-none absolute inset-0 rounded-[2rem] opacity-25 [background:repeating-linear-gradient(92deg,rgba(0,0,0,0.5)_0_3px,transparent_3px_12px)]" />
        <div className="pointer-events-none absolute inset-0 rounded-[2rem] shadow-[inset_0_1px_0_rgba(226,232,240,0.3),inset_0_-3px_8px_rgba(0,0,0,0.65)]" />
        {/* blue felt */}
        <div className="relative overflow-hidden rounded-[1.5rem] border border-sky-200/25 bg-gradient-to-b from-sky-900 via-slate-900 to-indigo-950 px-4 py-4 shadow-[inset_0_3px_22px_rgba(0,0,0,0.55)] sm:px-5">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(125,211,252,0.08),transparent_55%)]" />
          <div className="pointer-events-none absolute inset-0 opacity-[0.05] [background-image:radial-gradient(rgba(255,255,255,0.8)_0.5px,transparent_0.5px)] [background-size:7px_7px]" />
          <div className="pointer-events-none absolute inset-2 rounded-[1.25rem] border border-sky-200/10" />

          {/* round-transition veil (spec §29) */}
          {veilRound !== null && (
            <div
              key={veilRound}
              className="a-round-veil pointer-events-none absolute inset-0 z-20 flex flex-col items-center justify-center gap-1 bg-black/55 backdrop-blur-[2px]"
            >
              <span className="text-3xl font-black tracking-widest text-sky-100 drop-shadow-lg">{t('azul.roundLabel', { n: veilRound })}</span>
              <span className="text-xs font-semibold text-sky-200/70">{t('azul.bagLeft', { n: azul.BagCount })}</span>
            </div>
          )}

          {/* round banner */}
          <div className="relative flex flex-wrap items-center justify-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-widest text-sky-200/70">
              {t('azul.roundLabel', { n: azul.RoundNumber })}
            </span>
            <span className="rounded-full bg-black/35 px-2 py-0.5 text-[10px] font-bold tabular-nums text-sky-100/80 ring-1 ring-white/10">
              {t('azul.bagLeft', { n: azul.BagCount })}
            </span>
            {azul.DiscardCount > 0 && (
              <span className="rounded-full bg-black/35 px-2 py-0.5 text-[10px] font-bold tabular-nums text-white/60 ring-1 ring-white/10">
                {t('azul.discardLeft', { n: azul.DiscardCount })}
              </span>
            )}
            {!markerInCenter && (
              <span className="flex items-center gap-1 rounded-full bg-amber-500/15 px-2 py-0.5 text-[10px] font-bold text-amber-200 ring-1 ring-amber-300/30">
                <AzulMarker size="sm" /> {t('azul.markerWith', { player: seatName(azul.MarkerSeat) })}
              </span>
            )}
          </div>

          {/* factories ring + center bowl */}
          <div className="relative mt-4 flex flex-wrap items-center justify-center gap-3 sm:gap-5">
            {azul.Factories.map((tiles, i) => (
              <AzulFactoryDisc
                key={i}
                index={i}
                tiles={tiles}
                anchor={flightAnchor(`factory:${i}`)}
                selected={pickedSource?.kind === 'factory' && pickedSource.index === i}
                dimmed={tiles.length === 0}
                disabled={!isMyTurn || tiles.length === 0 || isSending}
                onSelect={() => {
                  setPickedColor(null)
                  setPickedSource(pickedSource?.kind === 'factory' && pickedSource.index === i ? null : { kind: 'factory', index: i })
                }}
              />
            ))}

            {/* the center pool bowl, in the middle of the ring */}
            <button
              ref={flightAnchor('center')}
              type="button"
              disabled={!isMyTurn || (azul.Center.length === 0 && !markerInCenter) || isSending}
              onClick={() => {
                setPickedColor(null)
                setPickedSource(pickedSource?.kind === 'center' ? null : azul.Center.length > 0 ? { kind: 'center' } : null)
              }}
              title={t('azul.centerHint')}
              aria-label={t('azul.centerLabel')}
              className={`relative flex min-h-24 min-w-28 flex-col items-center justify-center gap-1.5 rounded-full border-2 border-sky-200/25 bg-[radial-gradient(circle_at_50%_38%,rgba(14,165,233,0.16),rgba(2,6,23,0.85)_70%)] px-4 py-2 shadow-[inset_0_6px_14px_rgba(0,0,0,0.6)] transition-all ${
                pickedSource?.kind === 'center' ? 'ring-3 ring-emerald-300 shadow-[0_0_18px_rgba(52,211,153,0.7)]' : ''
              } ${isMyTurn && (azul.Center.length > 0 || markerInCenter) ? 'cursor-pointer hover:-translate-y-0.5' : 'cursor-default opacity-90'}`}
            >
              <span className="text-[9px] font-bold uppercase tracking-widest text-sky-200/60">{t('azul.centerLabel')}</span>
              {azul.Center.length === 0 ? (
                <span className="text-[10px] italic text-white/35">{t('azul.centerEmpty')}</span>
              ) : (
                <span className="flex flex-wrap items-center justify-center gap-1">
                  {[...colorCounts(azul.Center).entries()].map(([c, n]) => (
                    <span key={c} className="relative">
                      <AzulTile color={c} size="sm" />
                      <span className="absolute -end-1.5 -top-1.5 rounded-full bg-black/80 px-1 text-[8px] font-black tabular-nums text-white ring-1 ring-white/30">
                        {n}
                      </span>
                    </span>
                  ))}
                </span>
              )}
              {markerInCenter && <AzulMarker size="sm" className="absolute -bottom-2 end-2 shadow-lg" />}
            </button>
          </div>

          {/* the staging bar: pick a color, then a line */}
          <div className="relative mt-3 flex min-h-14 flex-wrap items-center justify-center gap-2 rounded-2xl border border-white/10 bg-black/30 px-3 py-2">
            {pickedSource === null ? (
              <p className="text-[11px] text-sky-100/60">
                {isMyTurn ? (markerInCenter ? t('azul.stagePickSourceOrMarker') : t('azul.stagePickSource')) : t('azul.waitingFor', { name: seatName(azul.CurrentPlayerIndex) })}
              </p>
            ) : pickedColor === null ? (
              <>
                <span className="text-[10px] font-semibold uppercase tracking-wide text-sky-200/70">{t('azul.pickColor')}</span>
                {[...chooser.entries()].map(([c, n]) => (
                  <button
                    key={c}
                    type="button"
                    disabled={isSending}
                    onClick={() => setPickedColor(c)}
                    className="flex items-center gap-1 rounded-full bg-white/10 py-1 ps-1 pe-2.5 text-xs font-bold text-white ring-1 ring-white/20 transition-all hover:-translate-y-0.5 hover:bg-white/20 hover:ring-emerald-300"
                    title={t('azul.takeN', { n, color: colorName(c) })}
                  >
                    <AzulTile color={c} size="xs" />
                    ×{n}
                    {pickedSource.kind === 'factory' && (
                      <span className="ms-1 text-[9px] font-medium text-white/50">
                        ({(azul.Factories[pickedSource.index]?.length ?? 0) - n} → {t('azul.centerLabel').toLowerCase()})
                      </span>
                    )}
                  </button>
                ))}
                <button
                  type="button"
                  onClick={() => setPickedSource(null)}
                  className="flex items-center gap-1 text-[10px] font-medium text-white/50 hover:text-white"
                >
                  <XMarkIcon className="h-3.5 w-3.5" /> {t('common.cancel')}
                </button>
              </>
            ) : (
              <>
                <span className="flex items-center gap-1.5 text-xs font-bold text-white">
                  <AzulTile color={pickedColor} size="xs" />
                  {t('azul.nowPickLine', { color: colorName(pickedColor) })}
                  {centerChooser && pickedSource.kind === 'center' && (
                    <span className="ms-1 text-[10px] font-medium text-white/50">· {t('azul.centerDraftMarkerNote')}</span>
                  )}
                </span>
                {picking && picking.forced && (
                  <span className="rounded-full bg-rose-500/20 px-2 py-0.5 text-[10px] font-bold text-rose-200 ring-1 ring-rose-400/40">
                    {t('azul.noLineAllFloor')}
                  </span>
                )}
                <button
                  type="button"
                  onClick={() => setPickedColor(null)}
                  className="flex items-center gap-1 text-[10px] font-medium text-white/50 hover:text-white"
                >
                  <XMarkIcon className="h-3.5 w-3.5" /> {t('common.cancel')}
                </button>
              </>
            )}
            {isMyTurn && markerInCenter && pickedSource === null && (
              <button
                type="button"
                disabled={isSending}
                onClick={() => send('TakeFirstPlayer', {})}
                className="flex items-center gap-1.5 rounded-full bg-amber-400/15 px-2.5 py-1 text-[11px] font-bold text-amber-200 ring-1 ring-amber-300/40 transition-all hover:-translate-y-0.5 hover:bg-amber-400/25"
                title={t('azul.takeMarkerHint')}
              >
                <HandThumbUpIcon className="h-3.5 w-3.5" />
                {t('azul.takeMarker')}
              </button>
            )}
          </div>

          {/* YOUR board first — full size, interactive */}
          {mySeat && meIndex >= 0 && (
            <div className="relative mx-auto mt-4 max-w-4xl">
              <p className="mb-1 text-[10px] font-semibold uppercase tracking-widest text-sky-200/50">{t('azul.yourBoard')}</p>
              <AzulPlayerBoard
                wall={mySeat.Wall}
                patternLines={mySeat.PatternLines}
                floor={mySeat.Floor}
                score={mySeat.Score}
                markerHere={azul.MarkerSeat === meIndex}
                picking={picking}
                onPickLine={(l) => draft(l)}
                onPickFloor={() => draft(AZUL_FLOOR)}
                anchorId={`seat:${meIndex}`}
                registerAnchor={flightAnchor}
                flashFloor={penaltyFlash.includes(meIndex)}
              />
            </div>
          )}

          {/* opponents below - summary cards with click-to-expand modal */}
          <div className="relative mt-3 flex flex-wrap justify-center gap-2 sm:gap-3">
            {state.players.map((player, i) => {
              if (i === meIndex) return null
              const seat = azul.Seats[i]
              if (!seat) return null
              const removed = eliminated.includes(i)
              const isTurn = azul.CurrentPlayerIndex === i
              return (
                <div
                  key={player.userId}
                  onClick={() => setOpponentModal(i)}
                  className={`relative w-48 rounded-2xl border p-3 backdrop-blur-sm transition-all cursor-pointer hover:shadow-xl hover:-translate-y-1 ${
                    removed
                      ? 'border-white/10 bg-white/5 opacity-50'
                      : isTurn
                        ? 'a-seat-turn border-amber-300/60 bg-white/10'
                        : 'border-white/15 bg-white/5'
                  }`}>
                  <div className="mb-2 flex items-center gap-2">
                    <div
                      className={`flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-full text-xs font-bold text-white shadow ${
                        removed ? 'bg-gray-500' : isTurn ? 'bg-amber-500' : 'bg-sky-700'
                      }`}>
                      {seatName(i).charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-xs font-semibold leading-tight text-white">{seatName(i)}</p>
                      <div className="flex items-center gap-1">
                        <span className="text-[9px] font-bold tabular-nums text-amber-200/90">{t('azul.vpN', { n: seat.Score })}</span>
                        {azul.MarkerSeat === i && <AzulMarker size="sm" />}
                        {removed && <span className="text-[9px] font-medium text-white/50">{t('azul.eliminated')}</span>}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center justify-center gap-2 text-[10px] text-white/50">
                    <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z" /><path d="M2.458 12C3.732 7.043 7.523 3 12 3c4.478 0 8.268 4.043 9.542 9-1.274 4.957-5.064 9-9.542 9" /></svg>
                    <span>{t('azul.viewBoard')}</span>
                  </div>
                </div>
              )
            })}
          </div>

          {meIndex < 0 && <p className="relative mt-4 text-center text-xs text-sky-100/60">{t('azul.spectator')}</p>}
        </div>
      </div>

      {/* ============================ STATUS STRIP ============================ */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-2xl border border-sky-200/15 bg-slate-900/80 px-4 py-3 shadow-lg backdrop-blur">
        {state.isOver ? (
          <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-sky-100/70">
            <TrophyIcon className="h-4 w-4 text-amber-300" /> {t('azul.gameOver')}
          </span>
        ) : isMyTurn ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-sky-400/15 px-3 py-1 text-sm font-semibold text-sky-200 ring-1 ring-sky-300/40">
            <PlayIcon className="h-4 w-4" /> {t('azul.yourTurn')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 text-sm text-sky-100/60">
            <ClockIcon className="h-4 w-4 text-sky-200/40" />
            {t('azul.waitingFor', { name: seatName(azul.CurrentPlayerIndex) })}
          </span>
        )}

        {!state.isOver && (
          <TimerRing seconds={remainingSeconds} overtimeSeconds={overtimeSeconds} fraction={timerFraction} critical={timerCritical} />
        )}

        {!state.isOver && gameClockLabel && (
          <div
            className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 ring-1 ${gameClockCritical ? 'bg-rose-600/80 ring-rose-300/40' : 'bg-white/5 ring-white/10'}`}
            title={t('azul.gameClockTitle')}
          >
            <ClockIcon className={`h-3.5 w-3.5 ${gameClockCritical ? 'text-white' : 'text-sky-200/60'}`} />
            <span className={`text-xs font-bold tabular-nums ${gameClockCritical ? 'text-white' : 'text-sky-100/70'}`}>{gameClockLabel}</span>
          </div>
        )}

        <div className="flex-1" />

        <button
          type="button"
          onClick={() => setHelpOpen(true)}
          className="inline-flex items-center gap-1.5 rounded-full bg-white/5 px-3 py-1.5 text-xs font-semibold text-sky-100/70 ring-1 ring-white/10 transition-colors hover:bg-sky-400/15 hover:text-sky-100"
        >
          <QuestionMarkCircleIcon className="h-4 w-4" />
          {t('azul.helpBtn')}
        </button>
      </div>

      {/* ============================ SCOREBOARD + LOG ============================ */}
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-sky-200/15 bg-slate-900/80 px-4 py-3 shadow-lg backdrop-blur">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-sky-200/50">{t('azul.scoreboard')}</p>
          <ul className="space-y-1.5">
            {standings.map((s) => (
              <li
                key={s.userId}
                className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm ${s.userId === userId ? 'bg-sky-400/10 ring-1 ring-sky-300/25' : ''} ${
                  eliminated.includes(s.seat) ? 'opacity-50' : ''
                }`}
              >
                <span className="flex h-6 w-6 items-center justify-center rounded-full bg-sky-800 text-[10px] font-bold text-sky-100">
                  {s.name.charAt(0).toUpperCase()}
                </span>
                <span className="min-w-0 flex-1 truncate font-medium text-sky-50/85">{s.name}</span>
                {azul.MarkerSeat === s.seat && <AzulMarker size="sm" />}
                <span className="w-10 text-end text-sm font-black tabular-nums text-amber-200">{s.score}</span>
              </li>
            ))}
          </ul>
        </div>

        <details className="group rounded-2xl border border-sky-200/15 bg-slate-900/80 shadow-lg backdrop-blur open:pb-2">
          <summary className="flex cursor-pointer list-none select-none items-center justify-between px-4 py-3">
            <span className="text-sm font-semibold text-sky-50/85">{t('azul.gameLog')}</span>
            <span className="text-xs text-sky-200/40 group-open:hidden">{t('azul.show')}</span>
            <span className="hidden text-xs text-sky-200/40 group-open:inline">{t('azul.hide')}</span>
          </summary>
          <ul className="max-h-64 space-y-1 overflow-y-auto px-4 pb-3">
            {azul.EventLog.length === 0 ? (
              <li className="py-4 text-center text-sm text-sky-200/40">{t('azul.noEvents')}</li>
            ) : (
              azul.EventLog.slice(-40)
                .reverse()
                .map((entry, i) => (
                  <li key={`${azul.EventLog.length - i}`} className="flex items-start gap-2 text-xs text-sky-100/65">
                    <span className="mt-1 h-1.5 w-1.5 flex-shrink-0 rounded-full bg-sky-400" />
                    <span>{formatAzulEvent(entry, t)}</span>
                  </li>
                ))
            )}
          </ul>
        </details>
      </div>

      {/* ============================ HELP ============================ */}
      <Modal dark isOpen={helpOpen} onClose={() => setHelpOpen(false)} title={t('azul.helpTitle')}>
        <ul className="space-y-3">
          {[1, 2, 3, 4, 5].map((n) => (
            <li key={n} className="flex items-start gap-3 text-sm text-sky-100/80">
              <span className="mt-1 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full bg-sky-400/20 text-[10px] font-black text-sky-200 ring-1 ring-sky-300/30">{n}</span>
              {t(`azul.rule${n}`)}
            </li>
          ))}
        </ul>
      </Modal>

      {/* ============================ GAME OVER ============================ */}
      <Modal dark isOpen={state.isOver} onClose={() => {}} title={t('azul.gameOverModalTitle')}>
        <div className="py-4 text-center">
          <TrophyIcon className="mx-auto h-12 w-12 text-amber-300" />
          <p className="mt-3 text-lg font-semibold text-sky-50">
            {winnerId ? t('azul.wins', { name: seatName(state.players.findIndex((p) => p.userId === winnerId)) }) : t('azul.tie')}
          </p>
          {!winnerId && <p className="mt-1 text-sm text-sky-100/65">{t('azul.tieDetail')}</p>}
          <p className="mt-4 text-xs font-semibold uppercase tracking-wide text-sky-200/45">{t('azul.standings')}</p>
          <ul className="mt-2 space-y-1">
            {standings.map((s, rank) => (
              <li
                key={s.userId}
                className={`flex items-center justify-between rounded-lg px-3 py-1.5 text-sm ${
                  s.userId === winnerId ? 'bg-amber-300/15 font-semibold text-amber-200 ring-1 ring-amber-300/30' : 'bg-white/5 text-sky-100/70'
                }`}
              >
                <span>
                  {rank + 1}. {s.name}
                </span>
                <span className="text-xs font-black tabular-nums">{s.score} VP</span>
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
      <Modal dark isOpen={iWasRemoved} onClose={() => {}} title={t('azul.removedFromGame')}>
        <div className="py-4 text-center">
          <ClockIcon className="mx-auto h-12 w-12 text-amber-300" />
          <p className="mt-3 text-lg font-semibold text-sky-50">{t('azul.afkTitle')}</p>
          <p className="mt-1 text-sm text-sky-100/65">{t('azul.afkDetail')}</p>
          <div className="mt-4">
            <Button variant="primary" asChild>
              <a href="/lobby">{t('common.backToLobby')}</a>
            </Button>
          </div>
        </div>
      </Modal>

      {/* ============================ OPPONENT BOARD MODAL ============================ */}
      <Modal dark isOpen={opponentModal !== null} onClose={() => setOpponentModal(null)} title={t('azul.opponentBoard', { name: opponentModal !== null ? seatName(opponentModal) : '' })} className="max-w-4xl">
        {opponentModal !== null && azul.Seats[opponentModal] && (
          <AzulPlayerBoard
            wall={azul.Seats[opponentModal].Wall}
            patternLines={azul.Seats[opponentModal].PatternLines}
            floor={azul.Seats[opponentModal].Floor}
            score={azul.Seats[opponentModal].Score}
            markerHere={azul.MarkerSeat === opponentModal}
            picking={null}
            anchorId={`seat:${opponentModal}`}
            registerAnchor={flightAnchor}
            flashFloor={false}
          />
        )}
      </Modal>
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
      title={overtimeSeconds > 0 ? t('azul.overtimeTooltip', { n: overtimeSeconds }) : t('azul.turnTooltip', { s: seconds })}
    >
      <svg viewBox="0 0 56 56" className="h-12 w-12 -rotate-90">
        <circle cx="28" cy="28" r={R} fill="none" strokeWidth="5" className="stroke-white/10" />
        <circle
          cx="28"
          cy="28"
          r={R}
          fill="none"
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={C}
          strokeDashoffset={C * (1 - fraction)}
          className={critical ? 'stroke-rose-400' : 'stroke-sky-400'}
          style={{ transition: 'stroke-dashoffset 0.25s linear' }}
        />
      </svg>
      <span
        className={`absolute inset-0 flex items-center justify-center text-sm font-bold tabular-nums ${
          overtimeSeconds > 0 ? 'text-rose-300' : critical ? 'text-rose-400' : 'text-sky-50'
        }`}
      >
        {overtimeSeconds > 0 ? `-${overtimeSeconds}` : seconds}
      </span>
    </div>
  )
}
