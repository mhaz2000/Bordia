import { memo } from 'react'

interface WallPreviewProps {
  orientation: 'H' | 'V'
  style: React.CSSProperties
  isValid: boolean
}

// Inject keyframes once
if (typeof document !== 'undefined' && !document.getElementById('quoridor-wall-preview-styles')) {
  const style = document.createElement('style')
  style.id = 'quoridor-wall-preview-styles'
  style.textContent = `
    @keyframes pulse-glow {
      0%, 100% { opacity: 0.9; }
      50% { opacity: 0.6; }
    }
    @keyframes preview-pulse {
      0%, 100% { transform: scale(1); opacity: 1; }
      50% { transform: scale(1.05); opacity: 0.8; }
    }
  `
  document.head.appendChild(style)
}

export const WallPreview = memo(function WallPreview({ orientation, style, isValid }: WallPreviewProps) {
  return (
    <div
      style={{
        ...style,
        position: 'absolute',
        borderRadius: orientation === 'H' ? '0 0 8px 8px / 0 0 50% 50%' : '8px 0 0 8px / 50% 0 0 50%',
        pointerEvents: 'none',
        zIndex: 20,
        transform: 'scale(1)',
        transition: 'all 0.08s ease-out',
      }}
    >
      <div
        className="absolute inset-0"
        style={{
          borderRadius: orientation === 'H' ? '0 0 8px 8px / 0 0 50% 50%' : '8px 0 0 8px / 50% 0 0 50%',
          background: isValid
            ? 'linear-gradient(135deg, rgba(34,197,94,0.5) 0%, rgba(21,128,61,0.6) 50%, rgba(20,101,48,0.5) 100%)'
            : 'linear-gradient(135deg, rgba(239,68,68,0.5) 0%, rgba(185,28,28,0.6) 50%, rgba(127,29,29,0.5) 100%)',
          boxShadow: isValid
            ? `
              0 0 0 2px rgba(34,197,94,0.8),
              0 0 24px rgba(34,197,94,0.6),
              0 8px 32px rgba(34,197,94,0.3),
              inset 0 0 16px rgba(34,197,94,0.2),
              inset 0 2px 8px rgba(255,255,255,0.15)
            `
            : `
              0 0 0 2px rgba(239,68,68,0.8),
              0 0 24px rgba(239,68,68,0.5),
              0 8px 32px rgba(239,68,68,0.3),
              inset 0 0 16px rgba(239,68,68,0.2),
              inset 0 2px 8px rgba(255,255,255,0.1)
            `,
          border: isValid ? '2px solid rgba(34,197,94,0.9)' : '2px solid rgba(239,68,68,0.9)',
          opacity: 0.9,
        }}
      />
      
      <div
        className="absolute inset-0 flex items-center justify-center"
        style={{
          borderRadius: orientation === 'H' ? '0 0 8px 8px / 0 0 50% 50%' : '8px 0 0 8px / 50% 0 0 50%',
          background: isValid
            ? 'linear-gradient(90deg, transparent 30%, rgba(34,197,94,0.3) 45%, rgba(34,197,94,0.5) 50%, rgba(34,197,94,0.3) 55%, transparent 70%)'
            : 'linear-gradient(90deg, transparent 30%, rgba(239,68,68,0.3) 45%, rgba(239,68,68,0.5) 50%, rgba(239,68,68,0.3) 55%, transparent 70%)',
          pointerEvents: 'none',
          animation: 'pulse-glow 1.5s ease-in-out infinite',
        }}
      >
        <svg
          width={orientation === 'H' ? 32 : 18}
          height={orientation === 'H' ? 18 : 32}
          viewBox="0 0 24 24"
          fill="none"
          stroke={isValid ? '#22c55e' : '#ef4444'}
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{ 
            filter: isValid 
              ? 'drop-shadow(0 0 6px rgba(34,197,94,0.8))' 
              : 'drop-shadow(0 0 6px rgba(239,68,68,0.8))',
            animation: 'preview-pulse 1s ease-in-out infinite',
          }}
        >
          {orientation === 'H' ? (
            <>
              <line x1="3" y1="12" x2="21" y2="12" />
              <line x1="12" y1="3" x2="12" y2="21" />
            </>
          ) : (
            <>
              <line x1="12" y1="3" x2="12" y2="21" />
              <line x1="3" y1="12" x2="21" y2="12" />
            </>
          )}
        </svg>
      </div>
    </div>
  )
})

WallPreview.displayName = 'WallPreview'