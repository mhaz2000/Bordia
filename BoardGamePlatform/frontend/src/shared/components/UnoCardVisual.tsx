import type { CSSProperties } from 'react'
import { CARD_HEX, valueGlyph, WILD_GRADIENT, isWild, type UnoCard } from '@/features/game/uno'

export type UnoCardVisualSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl'

const DIMS: Record<UnoCardVisualSize, string> = {
  xs: 'w-8 h-12',
  sm: 'w-12 h-[4.5rem]',
  md: 'w-16 h-24',
  lg: 'w-20 h-[7.5rem] sm:w-24 sm:h-32',
  xl: 'w-24 h-36 sm:w-28 sm:h-40',
}

const CENTER_TEXT: Record<UnoCardVisualSize, string> = {
  xs: 'text-[11px]',
  sm: 'text-base',
  md: 'text-2xl',
  lg: 'text-3xl sm:text-4xl',
  xl: 'text-4xl sm:text-5xl',
}

const CORNER_TEXT: Record<UnoCardVisualSize, string> = {
  xs: 'text-[7px]',
  sm: 'text-[8px]',
  md: 'text-[10px]',
  lg: 'text-[10px] sm:text-xs',
  xl: 'text-xs sm:text-sm',
}

/**
 * A real UNO-style card face: colored body, white central oval with the big
 * value, and small corner glyphs. Shared between the game view and game pages.
 */
export function UnoCardVisual({
  card,
  size = 'md',
  className = '',
  style,
}: {
  card: UnoCard
  size?: UnoCardVisualSize
  className?: string
  style?: CSSProperties
}) {
  const wild = isWild(card)
  const hex = CARD_HEX[card.Color] ?? CARD_HEX[0]
  const glyph = valueGlyph(card.Value)

  return (
    <div
      className={`${DIMS[size]} relative select-none rounded-xl border-[3px] border-white shadow-lg overflow-hidden ${className}`}
      style={{ background: wild ? undefined : `linear-gradient(145deg, ${hex}, ${shade(hex)})`, ...style }}
    >
      {wild && <div className="absolute inset-0" style={WILD_GRADIENT} />}
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-white/15 to-black/10" />
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="flex h-[62%] w-[82%] -rotate-[28deg] items-center justify-center rounded-[50%] bg-white shadow-sm">
          <div className="rotate-[28deg] text-center leading-none">
            {wild && card.Value === 13 ? (
              <div className="grid h-7 w-7 grid-cols-2 gap-px overflow-hidden rounded-full ring-2 ring-gray-800 sm:h-9 sm:w-9">
                <span className="bg-red-600" />
                <span className="bg-blue-600" />
                <span className="bg-yellow-400" />
                <span className="bg-green-600" />
              </div>
            ) : (
              <span
                className={`${CENTER_TEXT[size]} font-black tracking-tighter`}
                style={{ color: wild ? '#111827' : hex, textShadow: '0 1px 0 rgba(0,0,0,0.08)' }}
              >
                {glyph}
                </span>
            )}
          </div>
        </div>
      </div>
      <span className={`${CORNER_TEXT[size]} absolute left-1 top-0.5 font-black text-white drop-shadow-[0_1px_1px_rgba(0,0,0,0.6)]`}>
        {wild && card.Value === 13 ? 'W' : glyph}
      </span>
      <span className={`${CORNER_TEXT[size]} absolute bottom-0.5 right-1 rotate-180 font-black text-white drop-shadow-[0_1px_1px_rgba(0,0,0,0.6)]`}>
        {wild && card.Value === 13 ? 'W' : glyph}
      </span>
    </div>
  )
}

/** The back of a UNO card, used for decorative stacks. */
export function UnoCardBackVisual({
  className = '',
  size = 'md',
}: {
  className?: string
  size?: UnoCardVisualSize
}) {
  return (
    <div className={`${DIMS[size]} relative rounded-xl border-[3px] border-white shadow-lg overflow-hidden bg-gradient-to-br from-red-500 to-red-800 ${className}`}>
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="flex h-[62%] w-[82%] -rotate-[28deg] items-center justify-center rounded-[50%] bg-white">
          <span className={`rotate-[28deg] font-black italic tracking-tighter text-red-600 ${size === 'xl' ? 'text-xl sm:text-2xl' : 'text-sm'}`}>
            UNO
          </span>
        </div>
      </div>
    </div>
  )
}

/** Darkens a hex color for the card body gradient. */
function shade(hex: string): string {
  const n = parseInt(hex.slice(1), 16)
  const r = Math.max(0, ((n >> 16) & 255) - 60)
  const g = Math.max(0, ((n >> 8) & 255) - 60)
  const b = Math.max(0, (n & 255) - 60)
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`
}
