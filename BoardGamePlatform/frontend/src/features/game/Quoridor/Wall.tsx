import { memo } from 'react'

interface WallProps {
  orientation: 'H' | 'V'
  style: React.CSSProperties
  isPreview?: boolean
  isValid?: boolean
}

export const Wall = memo(function Wall({ orientation, style, isPreview = false, isValid = true }: WallProps) {
  const baseStyle: React.CSSProperties = {
    ...style,
    position: 'absolute',
    borderRadius: 6,
    transform: isPreview ? 'scale(0.98)' : undefined,
    zIndex: isPreview ? 10 : 5,
    pointerEvents: 'none',
  }

  if (isPreview) {
    return (
      <div style={baseStyle}>
        <div
          className="absolute inset-0"
          style={{
            background: isValid
              ? 'linear-gradient(180deg, rgba(241,245,249,0.7), rgba(203,213,225,0.5))'
              : 'linear-gradient(180deg, rgba(239,68,68,0.4), rgba(185,28,28,0.3))',
            border: `1px dashed ${isValid ? 'rgba(248,250,252,0.6)' : 'rgba(239,68,68,0.7)'}`,
            borderRadius: 6,
          }}
        />
      </div>
    )
  }

  return (
    <div style={baseStyle}>
      {/* simple rounded bar with a subtle gradient + shadow */}
      <div
        className="absolute inset-0"
        style={{
          background: 'linear-gradient(180deg, #f8fafc 0%, #e2e8f0 55%, #cbd5e1 100%)',
          borderRadius: 6,
          opacity: 0.92,
          boxShadow:
            orientation === 'H'
              ? '0 4px 6px rgba(15,23,42,0.4), 0 1px 2px rgba(15,23,42,0.5), inset 0 1px 0 rgba(255,255,255,0.6)'
              : '4px 0 6px rgba(15,23,42,0.4), 1px 0 2px rgba(15,23,42,0.5), inset 1px 0 0 rgba(255,255,255,0.6)',
        }}
      />
      {/* subtle center seam to suggest a panel */}
      <div
        className="absolute inset-0"
        style={{
          background: 'linear-gradient(180deg, transparent 46%, rgba(100,116,139,0.28) 50%, transparent 54%)',
          borderRadius: 6,
        }}
      />
    </div>
  )
})

Wall.displayName = 'Wall'