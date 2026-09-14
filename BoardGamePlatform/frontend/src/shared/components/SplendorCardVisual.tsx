import type { CSSProperties, ReactNode } from 'react'
import type { GemColorName, GemKind, SplendorCardDef } from '@/features/game/splendor'
import { GEM_IMAGE, GEM_ORDER, SCENE_IMAGE } from '@/features/game/splendor'
import { useI18n } from '@/i18n/I18nProvider'

/**
 * Original Splendor visuals: faceted gem icons, glossy 3D token chips printed
 * with real gemstone photography, photographic development-card scenes and
 * ornate card backs. Image sources + licenses: public/splendor/CREDITS.md.
 */

export type SplendorVisualSize = 'xs' | 'sm' | 'md' | 'lg'

const CARD_DIMS: Record<SplendorVisualSize, string> = {
  xs: 'w-12 h-[4.2rem]',
  sm: 'w-[4.8rem] h-[6.7rem]',
  md: 'w-[7.25rem] h-[10.1rem]',
  lg: 'w-[9.5rem] h-[13.3rem]',
}

/** Physical token shading: radial highlight + dark underside edge. `zoom`
 * crops each source photo (many have white backgrounds) to the disc face. */
export const GEM_STYLE: Record<
  GemKind,
  { hi: string; mid: string; lo: string; edge: string; text: string; icon: string; face: string; ring: string; zoom: number }
> = {
  diamond: {
    hi: '#ffffff', mid: '#e2e8f0', lo: '#94a3b8', edge: '#64748b',
    text: 'text-slate-700', icon: '#e8edf4', face: 'from-white to-slate-300', ring: 'ring-slate-400/70', zoom: 1.7,
  },
  sapphire: {
    hi: '#bae6fd', mid: '#38bdf8', lo: '#1d4ed8', edge: '#1e3a8a',
    text: 'text-white', icon: '#3b82f6', face: 'from-sky-400 to-blue-700', ring: 'ring-blue-900/50', zoom: 1.3,
  },
  emerald: {
    hi: '#a7f3d0', mid: '#10b981', lo: '#047857', edge: '#064e3b',
    text: 'text-white', icon: '#10b981', face: 'from-emerald-400 to-green-700', ring: 'ring-green-900/50', zoom: 1.25,
  },
  ruby: {
    hi: '#fecdd3', mid: '#f43f5e', lo: '#be123c', edge: '#881339',
    text: 'text-white', icon: '#ef4444', face: 'from-rose-400 to-red-700', ring: 'ring-red-900/50', zoom: 1.6,
  },
  onyx: {
    hi: '#a1a1aa', mid: '#3f3f46', lo: '#09090b', edge: '#000000',
    text: 'text-white', icon: '#3f3f46', face: 'from-zinc-500 to-zinc-900', ring: 'ring-black/60', zoom: 1.15,
  },
  gold: {
    hi: '#fde68a', mid: '#f59e0b', lo: '#b45309', edge: '#78350f',
    text: 'text-amber-950', icon: '#f59e0b', face: 'from-amber-300 to-yellow-600', ring: 'ring-amber-800/60', zoom: 1.35,
  },
}

export function gemKind(colorIndex: number): GemColorName {
  return GEM_ORDER[Math.max(0, Math.min(4, colorIndex))]
}

// ================================ gem icons ================================

const GEM_SHAPES: Record<GemKind, ReactNode> = {
  // Brilliant cut
  diamond: (
    <g>
      <polygon points="12,3 19,9 12,21 5,9" fill="currentColor" />
      <polyline points="5,9 12,9 19,9" stroke="rgba(255,255,255,0.65)" strokeWidth="1" fill="none" />
      <polyline points="12,3 9.5,9 12,21" stroke="rgba(255,255,255,0.5)" strokeWidth="0.8" fill="none" />
      <polyline points="12,3 14.5,9 12,21" stroke="rgba(0,0,0,0.25)" strokeWidth="0.8" fill="none" />
    </g>
  ),
  // Oval cut
  sapphire: (
    <g>
      <ellipse cx="12" cy="12" rx="7" ry="9" fill="currentColor" />
      <ellipse cx="12" cy="12" rx="4" ry="6" stroke="rgba(255,255,255,0.45)" strokeWidth="0.8" fill="none" />
      <path d="M8 7c2-2.5 6-2.5 8 0" stroke="rgba(255,255,255,0.7)" strokeWidth="1" fill="none" />
    </g>
  ),
  // Emerald (step) cut
  emerald: (
    <g>
      <polygon points="8,3 16,3 21,8 21,16 16,21 8,21 3,16 3,8" fill="currentColor" />
      <polygon points="9,6 15,6 18,9 18,15 15,18 9,18 6,15 6,9" stroke="rgba(255,255,255,0.45)" strokeWidth="0.8" fill="none" />
      <path d="M6.5 6.5l5-3.2M17.5 6.5l3-1.5" stroke="rgba(255,255,255,0.55)" strokeWidth="0.8" />
    </g>
  ),
  // Cushion cut
  ruby: (
    <g>
      <rect x="4" y="4" width="16" height="16" rx="5" transform="rotate(45 12 12)" fill="currentColor" />
      <rect x="7.5" y="7.5" width="9" height="9" rx="2.5" transform="rotate(45 12 12)" stroke="rgba(255,255,255,0.45)" strokeWidth="0.8" fill="none" />
      <path d="M12 2.8v3M2.8 12h3" stroke="rgba(255,255,255,0.6)" strokeWidth="0.8" />
    </g>
  ),
  // Square cut
  onyx: (
    <g>
      <rect x="4" y="4" width="16" height="16" rx="2.5" fill="currentColor" />
      <rect x="8" y="8" width="8" height="8" rx="1" stroke="rgba(255,255,255,0.4)" strokeWidth="0.8" fill="none" />
      <path d="M4 4l4 4M20 4l-4 4" stroke="rgba(255,255,255,0.5)" strokeWidth="0.8" />
    </g>
  ),
  // Joker coin
  gold: (
    <g>
      <circle cx="12" cy="12" r="9" fill="currentColor" />
      <circle cx="12" cy="12" r="6.2" stroke="rgba(255,255,255,0.55)" strokeWidth="0.9" fill="none" />
      <path d="M12 7.2l1.3 2.8 3 .4-2.2 2.1.6 3-2.7-1.5-2.7 1.5.6-3-2.2-2.1 3-.4z" fill="rgba(255,255,255,0.85)" stroke="none" />
    </g>
  ),
}

/** A faceted gem icon in the given color (currentColor-driven, RTL-neutral). */
export function GemIcon({ color, className = '', style }: { color: GemKind; className?: string; style?: CSSProperties }) {
  return (
    <svg viewBox="0 0 24 24" className={`${className} shrink-0`} style={{ color: GEM_STYLE[color].icon, ...style }} aria-hidden="true">
      {GEM_SHAPES[color]}
    </svg>
  )
}

// =============================== token chips ===============================

const CHIP_DIMS: Record<SplendorVisualSize, string> = {
  xs: 'h-6 w-6 text-[10px]',
  sm: 'h-8 w-8 text-[13px]',
  md: 'h-10 w-10 text-base',
  lg: 'h-14 w-14 text-xl',
}

/**
 * A real molded gem token: solid saturated plastic disc, thin molded ring,
 * the stone printed in the center, domed light and a cast shadow.
 */
export function GemChip({
  color,
  count,
  size = 'md',
  className = '',
  style,
  dimmed = false,
}: {
  color: GemKind
  count?: number
  size?: SplendorVisualSize
  className?: string
  style?: CSSProperties
  dimmed?: boolean
}) {
  const { t } = useI18n()
  const s = GEM_STYLE[color]
  const edge = size === 'lg' ? 4 : size === 'md' ? 3 : 2
  return (
    <span
      className={`relative inline-flex ${CHIP_DIMS[size]} select-none items-center justify-center overflow-hidden rounded-full font-black tabular-nums ${s.text} ${className}`}
      style={{
        background: `radial-gradient(circle at 50% 38%, ${s.mid} 0%, ${s.lo} 92%)`,
        boxShadow: `inset 0 2px 3px rgba(255,255,255,0.4), inset 0 -3px 5px rgba(0,0,0,0.5), 0 0 0 1px rgba(0,0,0,0.35), 0 ${edge}px 0 ${s.edge}, 0 ${edge + 4}px ${edge * 3}px rgba(0,0,0,0.45)`,
        filter: dimmed ? 'saturate(0.25) brightness(0.6)' : undefined,
        ...style,
      }}
      title={t(`splendor.gems.${color}`)}
    >
      {/* molded plastic rim keeps the color identity */}
      <span className="pointer-events-none absolute inset-[6%] rounded-full shadow-[inset_0_2px_2px_rgba(0,0,0,0.5),inset_0_-2px_1px_rgba(255,255,255,0.3)]" />
      {/* the stone photo printed over the face, zoomed past its own margins */}
      <span className="pointer-events-none absolute inset-[10%] overflow-hidden rounded-full">
        <img
          src={GEM_IMAGE[color]}
          alt=""
          draggable={false}
          className="h-full w-full object-cover"
          style={{ transform: `scale(${s.zoom})` }}
        />
      </span>
      {/* domed specular */}
      <span className="pointer-events-none absolute left-[12%] top-[5%] h-[32%] w-[60%] rounded-[50%] bg-white/25 blur-[2px]" />
      {count !== undefined && (
        <span
          className="relative z-10 text-white"
          style={{ textShadow: '0 1px 2px rgba(0,0,0,1), 0 0 6px rgba(0,0,0,0.9)' }}
        >
          {count}
        </span>
      )}
    </span>
  )
}

/** A physical pile of tokens: `count` discs stacked with visible rims. */
export function GemPile({
  color,
  count,
  className = '',
  selected = 0,
}: {
  color: GemKind
  count: number
  className?: string
  /** 0 none, 1 staged single, 2 staged pair. */
  selected?: 0 | 1 | 2
}) {
  const px = 56
  const step = 10
  const layers = Math.max(0, Math.min(count, 7))
  const s = GEM_STYLE[color]
  return (
    <span
      className={`relative block ${className}`}
      style={{ width: px, height: px + layers * step }}
    >
      {Array.from({ length: layers }, (_, i) => (
        <span
          key={i}
          className="absolute left-0 rounded-full"
          style={{
            bottom: i * step,
            width: px,
            height: px,
            background: `radial-gradient(circle at 50% 32%, ${s.mid} 0%, ${s.lo} 95%)`,
            boxShadow: `inset 0 2px 2px rgba(255,255,255,0.3), inset 0 -3px 4px rgba(0,0,0,0.55), 0 ${step - 7}px 1px rgba(0,0,0,0.3)`,
          }}
        />
      ))}
      <GemChip
        color={color}
        count={count}
        size="lg"
        dimmed={count === 0}
        className={`absolute left-0 ${selected > 0 ? 'ring-2 ring-amber-300' : ''}`}
        style={{ bottom: layers * step }}
      />
    </span>
  )
}

/** Small square-cut bonus chip with a photographic face (tableau / counters). */
export function BonusChip({ colorIndex, className = '' }: { colorIndex: number; className?: string }) {
  const kind = gemKind(colorIndex)
  const { t } = useI18n()
  return (
    <span
      className={`relative inline-flex h-4 w-4 items-center justify-center overflow-hidden rounded-[5px] shadow-[inset_0_0_0_1px_rgba(255,255,255,0.35),0_1px_2px_rgba(0,0,0,0.6)] ${className}`}
      title={t(`splendor.gems.${kind}`)}
    >
      <img src={GEM_IMAGE[kind]} alt="" draggable={false} className="h-full w-full object-cover" />
      <span className="pointer-events-none absolute inset-0 bg-[linear-gradient(145deg,rgba(255,255,255,0.35),transparent_45%)]" />
    </span>
  )
}

// ================================ card face ================================

const ART_INSET: Record<SplendorVisualSize, string> = { xs: '2px', sm: '3px', md: '5px', lg: '6px' }
const COST_PX: Record<SplendorVisualSize, number> = { xs: 14, sm: 20, md: 26, lg: 32 }
const CORNER_PX: Record<SplendorVisualSize, number> = { xs: 20, sm: 24, md: 30, lg: 40 }

/**
 * A development card: paper-white edge, a frame printed in the card's BONUS
 * gem color (same plastic palette as the tokens - the frame itself tells you
 * what the card discounts), the scene inset with a gold hairline, VP and
 * bonus tokens embossed on the frame corners, and the cost printed as real
 * mini gem tokens along the bottom of the art.
 */
export function SplendorCardVisual({
  card,
  size = 'md',
  className = '',
  style,
  dimmed = false,
}: {
  card: SplendorCardDef
  size?: SplendorVisualSize
  className?: string
  style?: CSSProperties
  dimmed?: boolean
}) {
  const bonus = GEM_STYLE[gemKind(card.bonus)]
  const corner = CORNER_PX[size]
  const costPx = COST_PX[size]
  return (
    <div
      className={`${CARD_DIMS[size]} relative flex-shrink-0 select-none overflow-hidden rounded-[10px] border border-white/85 ${
        dimmed ? 'opacity-85' : ''
      } ${className}`}
      style={{
        background: `linear-gradient(160deg, ${bonus.mid} 0%, ${bonus.lo} 78%)`,
        boxShadow: 'inset 0 0 0 1px rgba(0,0,0,0.35), 0 6px 14px rgba(0,0,0,0.5), 0 2px 3px rgba(0,0,0,0.4)',
        ...style,
      }}
      aria-hidden="true"
    >
      {/* printed art inside the frame */}
      <div className="absolute overflow-hidden rounded-[6px]" style={{ inset: ART_INSET[size] }}>
        <img src={SCENE_IMAGE[card.tier]} alt="" draggable={false} className="h-full w-full object-cover" />
        <div className="pointer-events-none absolute inset-0 shadow-[inset_0_0_0_1px_rgba(212,160,23,0.7),inset_0_1px_4px_rgba(0,0,0,0.5)]" />
        <div className="pointer-events-none absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-black/65 to-transparent" />
        {/* cost printed as gem tokens */}
        <span
          className="absolute inset-x-0.5 flex flex-wrap items-center justify-center"
          style={{ gap: size === 'xs' ? 2 : 4, bottom: size === 'xs' ? 3 : 4 }}
        >
          {card.cost.map(
            (c, i) =>
              c > 0 && (
                <GemChip
                  key={i}
                  color={gemKind(i)}
                  count={c}
                  size="xs"
                  style={{ width: costPx, height: costPx, fontSize: Math.round(costPx * 0.45) }}
                />
              ),
          )}
        </span>
        {/* gloss sweep */}
        <div className="pointer-events-none absolute -inset-y-6 -left-1/3 w-1/3 rotate-12 bg-gradient-to-r from-transparent via-white/10 to-transparent" />
      </div>

      {/* VP star embossed on the frame */}
      {card.points > 0 && (
        <span
          className="absolute flex items-center justify-center rounded-full font-black text-slate-900"
          style={{
            insetInlineStart: 2,
            top: 2,
            width: corner,
            height: corner,
            fontSize: Math.round(corner * 0.52),
            background: 'radial-gradient(circle at 34% 28%, #fff 0%, #e2e8f0 55%, #94a3b8 100%)',
            boxShadow: 'inset 0 1px 1px rgba(255,255,255,0.9), inset 0 -2px 3px rgba(0,0,0,0.25), 0 2px 3px rgba(0,0,0,0.5)',
          }}
        >
          {card.points}
        </span>
      )}

      {/* bonus token embossed on the frame */}
      <span
        className="absolute flex items-center justify-center rounded-full bg-white/95"
        style={{
          insetInlineEnd: 2,
          top: 2,
          width: corner,
          height: corner,
          boxShadow: 'inset 0 1px 1px rgba(255,255,255,0.9), inset 0 -2px 3px rgba(0,0,0,0.2), 0 2px 3px rgba(0,0,0,0.5)',
        }}
      >
        <GemIcon color={gemKind(card.bonus)} style={{ width: corner * 0.62, height: corner * 0.62 }} />
      </span>
    </div>
  )
}

// ================================ card back ================================

/** Deck / blind-reservation back: night-indigo damask with a gem rosette. */
export function SplendorCardBack({
  size = 'md',
  tier,
  className = '',
  style,
}: {
  size?: SplendorVisualSize
  /** Optional level badge drawn on deck stacks. */
  tier?: number
  className?: string
  style?: CSSProperties
}) {
  return (
    <div
      className={`${CARD_DIMS[size]} relative flex-shrink-0 select-none overflow-hidden rounded-[10px] border border-white/85 bg-gradient-to-br from-indigo-950 via-slate-900 to-violet-950 ${className}`}
      style={{ boxShadow: 'inset 0 0 0 1px rgba(0,0,0,0.4), 0 6px 14px rgba(0,0,0,0.5), 0 2px 3px rgba(0,0,0,0.4)', ...style }}
      aria-hidden="true"
    >
      <div className="pointer-events-none absolute inset-[4px] rounded-[6px] border border-amber-300/30" />
      {/* damask lattice */}
      <svg viewBox="0 0 48 64" preserveAspectRatio="none" className="absolute inset-0 h-full w-full opacity-[0.14]" aria-hidden="true">
        <path d="M0 0l48 64M-16 0l48 64M16 0l48 64M32 0l48 64M48 0L0 64M64 0L16 64M32 0L-16 64M16 0L-32 64" stroke="#c7d2fe" strokeWidth="0.7" fill="none" />
        <path d="M24 14l6 6-6 6-6-6zM24 38l6 6-6 6-6-6z" stroke="#fde68a" strokeWidth="0.8" fill="none" />
      </svg>
      {/* gold corner filigree */}
      <svg viewBox="0 0 48 64" preserveAspectRatio="none" className="absolute inset-0 h-full w-full" aria-hidden="true">
        <path d="M6 6h7M6 6v7M42 6h-7M42 6v7M6 58h7M6 58v-7M42 58h-7M42 58v-7" stroke="#fde68a" strokeWidth="1.2" opacity="0.55" fill="none" />
      </svg>
      <div className="absolute inset-0 flex items-center justify-center">
        <svg viewBox="0 0 24 24" className={`${size === 'xs' ? 'h-4 w-4' : 'h-8 w-8'} drop-shadow-[0_0_8px_rgba(251,191,36,0.35)]`} aria-hidden="true">
          <polygon points="12,2 19,9 12,22 5,9" fill="#fbbf24" opacity="0.85" />
          <polyline points="5,9 12,9 19,9" stroke="#fff7d6" strokeWidth="0.9" fill="none" opacity="0.9" />
          <polyline points="12,2 9.5,9 12,22" stroke="#fff7d6" strokeWidth="0.7" fill="none" opacity="0.75" />
          <polyline points="12,2 14.5,9 12,22" stroke="#92400e" strokeWidth="0.7" fill="none" opacity="0.6" />
        </svg>
      </div>
      {tier !== undefined && (
        <span
          className={`absolute end-[3px] top-[3px] flex items-center justify-center rounded-full font-black text-amber-950 shadow ${
            size === 'xs' ? 'h-3 w-3 text-[7px]' : 'h-[18px] w-[18px] text-[10px]'
          }`}
          style={{ background: 'radial-gradient(circle at 34% 28%, #fde68a, #f59e0b 70%)' }}
        >
          {tier}
        </span>
      )}
    </div>
  )
}
