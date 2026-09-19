import { memo } from 'react'

interface PlayerPieceProps {
  seat: number
  eliminated: boolean
  isCurrentPlayer: boolean
}

const PLAYER_COLORS = [
  { base: 'rgb(239, 68, 68)', light: 'rgb(248, 113, 113)', dark: 'rgb(185, 28, 28)', glow: 'rgba(239, 68, 68, 0.6)' },   // Red - Player 1
  { base: 'rgb(59, 130, 246)', light: 'rgb(96, 165, 250)', dark: 'rgb(30, 64, 175)', glow: 'rgba(59, 130, 246, 0.6)' },  // Blue - Player 2
  { base: 'rgb(34, 197, 94)', light: 'rgb(74, 222, 128)', dark: 'rgb(21, 128, 61)', glow: 'rgba(34, 197, 94, 0.6)' },   // Green - Player 3
  { base: 'rgb(236, 72, 153)', light: 'rgb(244, 114, 182)', dark: 'rgb(190, 24, 93)', glow: 'rgba(236, 72, 153, 0.6)' }, // Pink - Player 4
]

export const PlayerPiece = memo(function PlayerPiece({ seat, eliminated, isCurrentPlayer }: PlayerPieceProps) {
  const color = PLAYER_COLORS[seat % PLAYER_COLORS.length]
  const size = 38

  if (eliminated) {
    return (
      <div
        className="relative"
        style={{ width: size, height: size }}
        title="Eliminated"
      >
        <div
          className="absolute inset-0 rounded-full"
          style={{
            background: 'radial-gradient(circle at 30% 30%, rgb(100, 100, 100), rgb(55, 55, 55))',
            boxShadow: `
              inset 0 -2px 4px rgba(0,0,0,0.5),
              inset 0 2px 4px rgba(255,255,255,0.05),
              0 4px 12px rgba(0,0,0,0.4),
              0 0 0 1px rgba(255,255,255,0.05)
            `,
            transform: 'scale(0.85)',
            opacity: 0.5,
          }}
        />
        <div
          className="absolute inset-0 rounded-full flex items-center justify-center"
          style={{
            background: 'linear-gradient(135deg, rgba(255,255,255,0.05), transparent)',
          }}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ color: 'rgba(255,255,255,0.3)' }}>
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </div>
      </div>
    )
  }

  return (
    <div
      className="relative"
      style={{ width: size, height: size }}
      title={isCurrentPlayer ? 'Your turn' : `Player ${seat + 1}`}
    >
      <div
        className="absolute -inset-1 rounded-full blur-[8px] opacity-0 transition-opacity duration-300"
        style={{
          background: `radial-gradient(circle, ${color.glow} 0%, transparent 70%)`,
          opacity: isCurrentPlayer ? 0.4 : 0,
        }}
      />
      
      <div
        className="absolute inset-0 rounded-full"
        style={{
          background: `radial-gradient(ellipse at 25% 25%, ${color.light} 0%, ${color.base} 55%, ${color.dark} 100%)`,
          boxShadow: `
            inset 0 -3px 6px rgba(0,0,0,0.4),
            inset 0 2px 4px rgba(255,255,255,0.15),
            0 6px 20px rgba(0,0,0,0.5),
            0 2px 8px rgba(0,0,0,0.3),
            0 0 0 1px rgba(255,255,255,0.08),
            0 0 0 2px ${color.dark}40
          `,
          transform: isCurrentPlayer ? 'scale(1.05)' : 'scale(1)',
          transition: 'transform 0.2s ease-out, box-shadow 0.2s ease-out',
        }}
      />
      
      <div
        className="absolute inset-0 rounded-full"
        style={{
          background: 'radial-gradient(ellipse at 20% 20%, rgba(255,255,255,0.25) 0%, transparent 50%)',
          pointerEvents: 'none',
        }}
      />
      
      <div
        className="absolute inset-0 rounded-full"
        style={{
          background: 'radial-gradient(ellipse at 80% 80%, rgba(0,0,0,0.2) 0%, transparent 50%)',
          pointerEvents: 'none',
        }}
      />
      
      <div
        className="absolute inset-0 rounded-full flex items-center justify-center"
        style={{
          background: 'radial-gradient(ellipse at 50% 50%, rgba(255,255,255,0.03) 0%, transparent 70%)',
        }}
      >
        <span
          className="text-sm font-black tabular-nums select-none"
          style={{
            color: '#fafafa',
            textShadow: `
              0 1px 2px rgba(0,0,0,0.6),
              0 0 8px rgba(0,0,0,0.4)
            `,
            fontSize: size * 0.5,
            lineHeight: 1,
          }}
        >
          {seat + 1}
        </span>
      </div>

      {isCurrentPlayer && (
        <div
          className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center animate-pulse"
          style={{
            background: `radial-gradient(circle, ${color.base} 0%, ${color.dark} 100%)`,
            boxShadow: `
              0 0 0 2px #0f172a,
              0 0 12px ${color.glow},
              0 4px 12px rgba(0,0,0,0.4)
            `,
            border: '2px solid #0f172a',
          }}
        >
          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="#fafafa" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
            <polygon points="5 3 19 12 5 21 5 3" />
          </svg>
        </div>
      )}
    </div>
  )
})

PlayerPiece.displayName = 'PlayerPiece'