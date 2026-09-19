import { memo } from 'react'

interface WallSlotProps {
  orientation: 'H' | 'V'
  isActive: boolean
  isLegal: boolean
  isPreview: boolean
  onClick?: () => void
  onHover: () => void
  style: React.CSSProperties
}

export const WallSlot = memo(function WallSlot({ 
  orientation, 
  isActive, 
  isLegal, 
  isPreview, 
  onClick, 
  onHover,
  style 
}: WallSlotProps) {
  const baseStyle: React.CSSProperties = {
    ...style,
    position: 'absolute',
    borderRadius: orientation === 'H' ? '0 0 6px 6px / 0 0 50% 50%' : '6px 0 0 6px / 50% 0 0 50%',
    cursor: 'pointer',
    transition: 'all 0.12s ease-out',
    zIndex: isPreview ? 15 : isActive ? 10 : 5,
  }

  if (isPreview) {
    return (
      <div
        onClick={onClick}
        onMouseEnter={onHover}
        style={{
          ...baseStyle,
          background: 'radial-gradient(ellipse at center, rgba(34,197,94,0.4) 0%, rgba(34,197,94,0.15) 50%, transparent 70%)',
          boxShadow: `
            0 0 0 2px rgba(34,197,94,0.6),
            0 0 20px rgba(34,197,94,0.4),
            inset 0 0 12px rgba(34,197,94,0.2)
          `,
          border: '2px solid rgba(34,197,94,0.8)',
          transform: 'scale(1.02)',
        }}
      >
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            borderRadius: orientation === 'H' ? '0 0 6px 6px / 0 0 50% 50%' : '6px 0 0 6px / 50% 0 0 50%',
            background: 'radial-gradient(ellipse at center, rgba(34,197,94,0.2) 0%, transparent 60%)',
            pointerEvents: 'none',
          }}
        >
          <svg
            width={orientation === 'H' ? 28 : 16}
            height={orientation === 'H' ? 16 : 28}
            viewBox="0 0 24 24"
            fill="none"
            stroke="#22c55e"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ opacity: 0.9, filter: 'drop-shadow(0 0 4px rgba(34,197,94,0.6))' }}
          >
            {orientation === 'H' ? (
              <>
                <line x1="4" y1="12" x2="20" y2="12" />
                <line x1="12" y1="4" x2="12" y2="20" />
              </>
            ) : (
              <>
                <line x1="12" y1="4" x2="12" y2="20" />
                <line x1="4" y1="12" x2="20" y2="12" />
              </>
            )}
          </svg>
        </div>
      </div>
    )
  }

  if (isActive && isLegal) {
    return (
      <div
        onClick={onClick}
        onMouseEnter={onHover}
        style={{
          ...baseStyle,
          background: 'radial-gradient(ellipse at center, rgba(59,130,246,0.25) 0%, rgba(59,130,246,0.08) 50%, transparent 70%)',
          boxShadow: `
            0 0 0 1px rgba(59,130,246,0.4),
            0 0 12px rgba(59,130,246,0.2),
            inset 0 0 8px rgba(59,130,246,0.1)
          `,
          border: '1px solid rgba(59,130,246,0.3)',
        }}
      >
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            borderRadius: orientation === 'H' ? '0 0 6px 6px / 0 0 50% 50%' : '6px 0 0 6px / 50% 0 0 50%',
            background: 'radial-gradient(ellipse at center, rgba(59,130,246,0.1) 0%, transparent 60%)',
            pointerEvents: 'none',
          }}
        >
          <svg
            width={orientation === 'H' ? 20 : 12}
            height={orientation === 'H' ? 12 : 20}
            viewBox="0 0 24 24"
            fill="none"
            stroke="#3b82f6"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ opacity: 0.6 }}
          >
            {orientation === 'H' ? (
              <line x1="4" y1="12" x2="20" y2="12" />
            ) : (
              <line x1="12" y1="4" x2="12" y2="20" />
            )}
          </svg>
        </div>
      </div>
    )
  }

  if (isActive) {
    return (
      <div
        onClick={onClick}
        onMouseEnter={onHover}
        style={{
          ...baseStyle,
          background: 'radial-gradient(ellipse at center, rgba(239,68,68,0.15) 0%, rgba(239,68,68,0.05) 50%, transparent 70%)',
          boxShadow: `
            0 0 0 1px rgba(239,68,68,0.3),
            0 0 8px rgba(239,68,68,0.1),
            inset 0 0 6px rgba(239,68,68,0.05)
          `,
          border: '1px dashed rgba(239,68,68,0.4)',
        }}
      >
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            borderRadius: orientation === 'H' ? '0 0 6px 6px / 0 0 50% 50%' : '6px 0 0 6px / 50% 0 0 50%',
            pointerEvents: 'none',
          }}
        >
          <svg
            width={orientation === 'H' ? 16 : 10}
            height={orientation === 'H' ? 10 : 16}
            viewBox="0 0 24 24"
            fill="none"
            stroke="#ef4444"
            strokeWidth="1"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ opacity: 0.4 }}
          >
            {orientation === 'H' ? (
              <line x1="4" y1="12" x2="20" y2="12" strokeDasharray="4,4" />
            ) : (
              <line x1="12" y1="4" x2="12" y2="20" strokeDasharray="4,4" />
            )}
          </svg>
        </div>
      </div>
    )
  }

  return (
    <div
      onClick={onClick}
      onMouseEnter={onHover}
      style={{
        ...baseStyle,
        background: 'transparent',
        border: '1px dashed transparent',
      }}
    >
      <div
        className="absolute inset-0 flex items-center justify-center"
        style={{
          borderRadius: orientation === 'H' ? '0 0 6px 6px / 0 0 50% 50%' : '6px 0 0 6px / 50% 0 0 50%',
          background: 'radial-gradient(ellipse at center, rgba(255,255,255,0.02) 0%, transparent 70%)',
          pointerEvents: 'none',
          opacity: 0,
          transition: 'opacity 0.15s ease-out',
        }}
      >
        <svg
          width={orientation === 'H' ? 14 : 8}
          height={orientation === 'H' ? 8 : 14}
          viewBox="0 0 24 24"
          fill="none"
          stroke="#64748b"
          strokeWidth="1"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{ opacity: 0.3 }}
        >
          {orientation === 'H' ? (
            <line x1="4" y1="12" x2="20" y2="12" strokeDasharray="3,3" />
          ) : (
            <line x1="12" y1="4" x2="12" y2="20" strokeDasharray="3,3" />
          )}
        </svg>
      </div>
    </div>
  )
})

WallSlot.displayName = 'WallSlot'