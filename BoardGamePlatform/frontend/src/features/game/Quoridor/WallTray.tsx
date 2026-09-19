import { useState } from 'react'
import { useI18n } from '@/i18n/I18nProvider'
import { Wall } from './Wall'

interface WallTrayProps {
  wallsRemaining: number
  onDragStart: (orientation: 'H' | 'V') => void
  isMyTurn: boolean
  disabled?: boolean
}

export function WallTray({
  wallsRemaining,
  onDragStart,
  isMyTurn,
  disabled = false,
}: WallTrayProps) {
  const { t } = useI18n()
  const [hoveredSlot, setHoveredSlot] = useState<'H' | 'V' | null>(null)
  const [pressedSlot, setPressedSlot] = useState<'H' | 'V' | null>(null)

  const canGrab = !disabled && isMyTurn && wallsRemaining > 0

  const handlePointerDown = (e: React.PointerEvent, slot: 'H' | 'V') => {
    if (!canGrab) return
    e.preventDefault()
    setPressedSlot(slot)
    onDragStart(slot)
  }

  const wallWidth = 64
  const wallHeight = 16

  const slotStyle = (slot: 'H' | 'V', w: number, h: number): React.CSSProperties => ({
    width: w,
    height: h,
    transform: pressedSlot === slot ? 'scale(0.95)' : hoveredSlot === slot ? 'scale(1.05)' : 'scale(1)',
    transition: 'transform 0.08s ease-out',
    filter: wallsRemaining > 0 ? 'none' : 'grayscale(0.8) opacity-40',
    cursor: canGrab ? 'grab' : 'default',
  })

  return (
    <div
      className="relative flex flex-col items-center gap-3 p-4"
      style={{
        minWidth: 200,
        background: 'linear-gradient(180deg, rgba(28,25,23,0.9), rgba(12,10,9,0.95))',
        border: '1px solid rgba(217,119,6,0.25)',
        borderRadius: 16,
        boxShadow: `
          0 8px 32px rgba(0,0,0,0.4),
          0 0 0 1px rgba(255,255,255,0.03) inset,
          0 1px 0 rgba(255,255,255,0.05) inset
        `,
      }}
    >
      <div className="flex items-center gap-2 text-center mb-2">
        <span
          className="text-lg font-bold tabular-nums px-3 py-1 rounded-lg"
          style={{
            color: wallsRemaining > 0 ? '#fef3c7' : '#57534e',
            background: wallsRemaining > 0
              ? 'linear-gradient(180deg, rgba(245,158,11,0.3), rgba(217,119,6,0.2))'
              : 'rgba(120,113,108,0.2)',
            border: wallsRemaining > 0 ? '1px solid rgba(245,158,11,0.4)' : '1px solid rgba(120,113,108,0.3)',
            boxShadow: wallsRemaining > 0 ? '0 0 16px rgba(245,158,11,0.3)' : 'none',
          }}
        >
          {wallsRemaining}
        </span>
        <span className="text-xs font-medium uppercase tracking-wider text-stone-400">
          {t('quoridor.wallsLeft', { n: wallsRemaining })}
        </span>
      </div>

      <div className="flex items-center gap-4">
        {/* Horizontal wall - grab to drag */}
        <div
          onPointerDown={(e) => handlePointerDown(e, 'H')}
          onPointerUp={() => setPressedSlot(null)}
          onMouseEnter={() => setHoveredSlot('H')}
          onMouseLeave={() => { setHoveredSlot(null); setPressedSlot(null) }}
          className="relative"
          style={slotStyle('H', wallWidth, wallHeight)}
        >
          <Wall
            orientation="H"
            style={{ width: wallWidth, height: wallHeight, left: 0, top: 0 }}
          />
          {hoveredSlot === 'H' && wallsRemaining > 0 && pressedSlot !== 'H' && (
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="5 9 2 12 5 15" />
                <polyline points="9 5 12 2 15 5" />
                <polyline points="15 19 12 22 9 19" />
                <polyline points="19 9 22 12 19 15" />
              </svg>
            </div>
          )}
        </div>

        {/* Vertical wall - grab to drag */}
        <div
          onPointerDown={(e) => handlePointerDown(e, 'V')}
          onPointerUp={() => setPressedSlot(null)}
          onMouseEnter={() => setHoveredSlot('V')}
          onMouseLeave={() => { setHoveredSlot(null); setPressedSlot(null) }}
          className="relative"
          style={slotStyle('V', wallHeight, wallWidth)}
        >
          <Wall
            orientation="V"
            style={{ width: wallHeight, height: wallWidth, left: 0, top: 0 }}
          />
          {hoveredSlot === 'V' && wallsRemaining > 0 && pressedSlot !== 'V' && (
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="5 9 2 12 5 15" />
                <polyline points="9 5 12 2 15 5" />
                <polyline points="15 19 12 22 9 19" />
                <polyline points="19 9 22 12 19 15" />
              </svg>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}