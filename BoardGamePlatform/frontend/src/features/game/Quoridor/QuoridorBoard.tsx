import { useMemo, useCallback, useEffect, useState, useRef } from 'react'
import type { QuoridorState } from '../quoridor'
import { BOARD_SIZE } from '../quoridor'
import { PlayerPiece } from './PlayerPiece'
import { Wall } from './Wall'

export interface QuoridorBoardProps {
  quoridor: QuoridorState
  mode: 'move' | 'wallH' | 'wallV'
  hoverCell: { r: number; c: number } | null
  isMyTurn: boolean
  mySeat: number
  legalMoveSet: Set<string>
  legalWallSet: Set<string>
  onCellClick: (r: number, c: number) => void
  onSlotHover: (r: number, c: number) => void
  onWallDragEnd: (slot: { r: number; c: number; orientation: 'H' | 'V' } | null) => void
  draggedWallOrientation: 'H' | 'V' | null
}

const CELL_SIZE_PX = 56
const GAP_SIZE_PX = 12
const BOARD_PADDING = 16

function BoardCoordinateSystem() {
  const cellSize = CELL_SIZE_PX
  const gapSize = GAP_SIZE_PX
  
  const cellToScreen = useCallback((row: number, col: number) => {
    const x = BOARD_PADDING + col * (cellSize + gapSize)
    const y = BOARD_PADDING + row * (cellSize + gapSize)
    return { x, y }
  }, [])

  const wallToScreen = useCallback((row: number, col: number, orientation: 'H' | 'V') => {
    if (orientation === 'H') {
      const x = BOARD_PADDING + col * (cellSize + gapSize)
      const y = BOARD_PADDING + (row + 1) * cellSize + row * gapSize - gapSize / 2
      return { x, y, width: cellSize * 2 + gapSize, height: gapSize }
    } else {
      const x = BOARD_PADDING + (col + 1) * cellSize + col * gapSize - gapSize / 2
      const y = BOARD_PADDING + row * (cellSize + gapSize)
      return { x, y, width: gapSize, height: cellSize * 2 + gapSize }
    }
  }, [])

  const wallSlotToScreen = useCallback((row: number, col: number, orientation: 'H' | 'V') => {
    if (orientation === 'H') {
      const x = BOARD_PADDING + col * (cellSize + gapSize)
      const y = BOARD_PADDING + (row + 1) * cellSize + row * gapSize - gapSize / 2
      return { x, y, width: cellSize * 2 + gapSize, height: gapSize }
    } else {
      const x = BOARD_PADDING + (col + 1) * cellSize + col * gapSize - gapSize / 2
      const y = BOARD_PADDING + row * (cellSize + gapSize)
      return { x, y, width: gapSize, height: cellSize * 2 + gapSize }
    }
  }, [])

  const boardWidth = BOARD_PADDING * 2 + BOARD_SIZE * cellSize + (BOARD_SIZE - 1) * gapSize
  const boardHeight = boardWidth

  return {
    cellToScreen,
    wallToScreen,
    wallSlotToScreen,
    boardWidth,
    boardHeight,
    cellSize,
    gapSize,
  }
}

export function QuoridorBoard({
  quoridor,
  mode,
  hoverCell,
  isMyTurn,
  mySeat,
  legalMoveSet,
  legalWallSet,
  onCellClick,
  onSlotHover,
  onWallDragEnd,
  draggedWallOrientation,
}: QuoridorBoardProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const boardRef = useRef<HTMLDivElement>(null)
  const [fit, setFit] = useState(1)
  const [ghost, setGhost] = useState<{ r: number; c: number; orientation: 'H' | 'V'; legal: boolean } | null>(null)
  const ghostRef = useRef<{ r: number; c: number; orientation: 'H' | 'V'; legal: boolean } | null>(null)
  
  const {
    wallToScreen,
    wallSlotToScreen,
    boardWidth,
    boardHeight,
    cellSize,
  } = BoardCoordinateSystem()

  // Responsive scaling so the 1:1 board fits its container width.
  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const update = () => setFit(Math.min(1, el.clientWidth / boardWidth, el.clientHeight / boardHeight))
    update()
    const ro = new ResizeObserver(update)
    ro.observe(el)
    return () => ro.disconnect()
  }, [boardWidth, boardHeight])

  const goalRows = useMemo(() => [0, BOARD_SIZE - 1], [])

  const myPawn = mySeat >= 0 ? quoridor.Pawns[mySeat] : null

  // Drag manager: while a wall is being dragged, track the pointer over the
  // board, snap to the nearest slot, and report the slot on release.
  useEffect(() => {
    if (!draggedWallOrientation) {
      setGhost(null)
      ghostRef.current = null
      return
    }
    const orientation = draggedWallOrientation

    const onMove = (e: PointerEvent) => {
      const el = boardRef.current
      if (!el) {
        setGhost(null)
        ghostRef.current = null
        return
      }
      const rect = el.getBoundingClientRect()
      const x = (e.clientX - rect.left) / fit
      const y = (e.clientY - rect.top) / fit

      const maxR = 7
      const maxC = 7
      let best: { r: number; c: number } | null = null
      let bestDist = Infinity
      for (let r = 0; r <= maxR; r++) {
        for (let c = 0; c <= maxC; c++) {
          const pos = wallSlotToScreen(r, c, orientation)
          const d = Math.hypot(x - (pos.x + pos.width / 2), y - (pos.y + pos.height / 2))
          if (d < bestDist) {
            bestDist = d
            best = { r, c }
          }
        }
      }

      // Only snap when the pointer is meaningfully close to a slot; dropping
      // far from any slot (e.g. back on the tray) cancels the placement.
      if (!best || bestDist > cellSize * 1.1) {
        setGhost(null)
        ghostRef.current = null
        return
      }

      const legal = legalWallSet.has(`${best.r},${best.c},${orientation}`)
      const g = { r: best.r, c: best.c, orientation, legal }
      setGhost(g)
      ghostRef.current = g
    }

    const onUp = () => {
      const g = ghostRef.current
      onWallDragEnd(g ? { r: g.r, c: g.c, orientation: g.orientation } : null)
      ghostRef.current = null
      setGhost(null)
    }

    document.addEventListener('pointermove', onMove)
    document.addEventListener('pointerup', onUp)
    document.addEventListener('pointercancel', onUp)
    const prevUserSelect = document.body.style.userSelect
    const prevTouchAction = document.body.style.touchAction
    document.body.style.userSelect = 'none'
    document.body.style.touchAction = 'none'

    return () => {
      document.removeEventListener('pointermove', onMove)
      document.removeEventListener('pointerup', onUp)
      document.removeEventListener('pointercancel', onUp)
      document.body.style.userSelect = prevUserSelect
      document.body.style.touchAction = prevTouchAction
    }
  }, [draggedWallOrientation, fit, legalWallSet, onWallDragEnd, wallSlotToScreen, cellSize])

  return (
    <div
      ref={containerRef}
      dir="ltr"
      className="relative w-full select-none"
      style={{ aspectRatio: '1 / 1' }}
    >
      <div
        ref={boardRef}
        className="relative select-none"
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          width: boardWidth,
          height: boardHeight,
          transform: `scale(${fit})`,
          transformOrigin: 'top left',
        }}
      >
        <div className="absolute inset-0 rounded-2xl bg-stone-950 shadow-[0_8px_32px_rgba(0,0,0,0.4),_0_0_0_1px_rgba(255,255,255,0.05),_inset_0_1px_0_rgba(255,255,255,0.03)]" />

        <div className="absolute inset-0 rounded-2xl bg-gradient-to-br from-stone-900/50 to-stone-950/50 pointer-events-none" />

        <div className="absolute inset-0 rounded-2xl bg-[radial-gradient(ellipse_at_center,rgba(255,255,255,0.02)_0%,transparent_70%)] pointer-events-none" />

        <div className="absolute inset-0" style={{
          display: 'grid',
          gridTemplateColumns: `repeat(${BOARD_SIZE}, ${cellSize}px)`,
          gridTemplateRows: `repeat(${BOARD_SIZE}, ${cellSize}px)`,
          gap: `${GAP_SIZE_PX}px`,
          padding: `${BOARD_PADDING}px`,
        }}>
          {Array.from({ length: BOARD_SIZE * BOARD_SIZE }).map((_, idx) => {
            const r = Math.floor(idx / BOARD_SIZE)
            const c = idx % BOARD_SIZE
            const isGoal = goalRows.includes(r)
            const pawnSeat = quoridor.Pawns.findIndex((p) => p.Row === r && p.Col === c)
            const isLegalMove = legalMoveSet.has(`${r},${c}`)
            const isHover = hoverCell?.r === r && hoverCell?.c === c

            return (
              <div
                key={idx}
                onClick={() => onCellClick(r, c)}
                onMouseEnter={() => onSlotHover(r, c)}
                className={[
                  'relative flex items-center justify-center rounded-lg transition-all duration-150',
                  isGoal
                    ? (r === 0
                      ? 'bg-gradient-to-br from-red-600/10 via-stone-900 to-red-600/5 border border-red-500/20'
                      : 'bg-gradient-to-br from-blue-600/10 via-stone-900 to-blue-600/5 border border-blue-500/20')
                    : 'bg-stone-800/60 border border-stone-700/50',
                  isLegalMove && isMyTurn && mode === 'move'
                    ? 'bg-amber-500/20 border-amber-500/40 shadow-[0_0_12px_rgba(245,158,11,0.3)]'
                    : '',
                  isHover && isLegalMove && isMyTurn && mode === 'move'
                    ? 'bg-amber-500/30 scale-[1.02] z-10'
                    : '',
                  !isLegalMove && !isGoal && isMyTurn && mode === 'move'
                    ? 'hover:bg-stone-700/60'
                    : '',
                  myPawn && myPawn.Row === r && myPawn.Col === c && isMyTurn && mode === 'move'
                    ? 'ring-2 ring-inset ring-amber-500/60'
                    : '',
                ].join(' ')}
                style={{ width: cellSize, height: cellSize }}
              >
                {pawnSeat >= 0 && (
                  <PlayerPiece
                    seat={pawnSeat}
                    eliminated={quoridor.EliminatedSeats.includes(pawnSeat)}
                    isCurrentPlayer={quoridor.CurrentPlayerIndex === pawnSeat}
                  />
                )}
              </div>
            )
          })}
        </div>

        <div className="absolute inset-0 pointer-events-none" style={{ padding: BOARD_PADDING }}>
          {quoridor.Walls.map((w, i) => {
            const pos = wallToScreen(w.Row, w.Col, w.Orientation)
            return (
              <Wall
                key={i}
                orientation={w.Orientation}
                style={{
                  left: pos.x,
                  top: pos.y,
                  width: pos.width,
                  height: pos.height,
                }}
              />
            )
          })}

          {/* Single ghost wall snapped to the nearest slot while dragging. */}
          {ghost && (
            <div
              className="pointer-events-none"
              style={{
                position: 'absolute',
                left: wallSlotToScreen(ghost.r, ghost.c, ghost.orientation).x,
                top: wallSlotToScreen(ghost.r, ghost.c, ghost.orientation).y,
                width: wallSlotToScreen(ghost.r, ghost.c, ghost.orientation).width,
                height: wallSlotToScreen(ghost.r, ghost.c, ghost.orientation).height,
                zIndex: 25,
              }}
            >
              <Wall
                orientation={ghost.orientation}
                isPreview
                isValid={ghost.legal}
                style={{ left: 0, top: 0, width: '100%', height: '100%' }}
              />
            </div>
          )}
        </div>

        <div className="absolute inset-0 pointer-events-none">
          {Array.from({ length: BOARD_SIZE }).map((_, r) => (
            <span
              key={`row-${r}`}
              className="absolute text-[10px] font-semibold tabular-nums text-stone-500"
              style={{
                left: BOARD_PADDING / 2,
                top: BOARD_PADDING + r * (cellSize + GAP_SIZE_PX) + cellSize / 2,
                transform: 'translate(-50%, -50%)',
              }}
            >
              {String.fromCharCode(97 + r)}
            </span>
          ))}
          {Array.from({ length: BOARD_SIZE }).map((_, c) => (
            <span
              key={`col-${c}`}
              className="absolute text-[10px] font-semibold tabular-nums text-stone-500"
              style={{
                left: BOARD_PADDING + c * (cellSize + GAP_SIZE_PX) + cellSize / 2,
                top: BOARD_PADDING / 2,
                transform: 'translate(-50%, -50%)',
              }}
            >
              {c + 1}
            </span>
          ))}
        </div>

        <div className="absolute inset-0 pointer-events-none" style={{ padding: BOARD_PADDING }}>
          {[0, BOARD_SIZE - 1].map((row) => (
            <div
              key={`goal-${row}`}
              className="absolute left-0 right-0 h-1.5 opacity-60"
              style={{
                top: row === 0 ? 0 : 'calc(100% - 6px)',
                background: row === 0
                  ? 'linear-gradient(90deg, transparent, rgba(239,68,68,0.4), transparent)'
                  : 'linear-gradient(90deg, transparent, rgba(59,130,246,0.4), transparent)',
              }}
            />
          ))}
        </div>
      </div>
    </div>
  )
}