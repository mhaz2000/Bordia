import type { CSSProperties } from 'react'
import { useI18n } from '@/i18n/I18nProvider'
import { GEM_ORDER, NOBLE_IMAGE, NOBLE_POINTS, type SplendorNobleDef } from '@/features/game/splendor'
import { GemIcon } from './SplendorCardVisual'

/** Small gilded crown used as the "noble owned" marker in seat panels. */
export function CrownGlyph({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="currentColor" aria-hidden="true">
      <path d="M4 16l-1.2-8 5 3.4L12 4l4.2 7.4 5-3.4L20 16zM4.6 18h14.8v2.2a.8.8 0 0 1-.8.8H5.4a.8.8 0 0 1-.8-.8z" />
    </svg>
  )
}


/** A noble tile: gilded obsidian frame, portrait, 3-VP badge, gem demands. */
export function SplendorNobleVisual({
  noble,
  eligible = false,
  size = 'md',
  className = '',
  style,
}: {
  noble: SplendorNobleDef
  /** Gold glow: the viewer's bonuses already satisfy this tile. */
  eligible?: boolean
  size?: 'sm' | 'md'
  className?: string
  style?: CSSProperties
}) {
  const { t } = useI18n()
  const dims = size === 'sm' ? 'w-[7.25rem] h-[6.5rem]' : 'w-[9.5rem] h-[7.4rem]'
  const requirement = noble.requirement
    .map((r, i) => (r > 0 ? `${r} ${t(`splendor.gems.${GEM_ORDER[i]}`)}` : ''))
    .filter(Boolean)
    .join(' + ')

  return (
    <div
      className={`${dims} relative select-none overflow-hidden rounded-xl border border-amber-200/90 bg-gradient-to-b from-zinc-700 via-zinc-900 to-black ${
        eligible ? 'ring-2 ring-amber-400' : ''
      } ${className}`}
      style={{
        boxShadow: 'inset 0 1px 0 rgba(253,230,138,0.3), inset 0 0 0 1px rgba(0,0,0,0.6), 0 4px 0 #27272a, 0 9px 16px rgba(0,0,0,0.55)',
        ...style,
      }}
      role="img"
      aria-label={`${t('splendor.noble')} - ${requirement}`}
      title={`${t('splendor.noble')} · ${requirement}`}
    >
      {/* portrait */}
      <div className={`absolute inset-x-[5px] top-[14px] overflow-hidden rounded-t-full rounded-b-[4px] ${size === 'sm' ? 'bottom-[19px]' : 'bottom-[23px]'}`}>
        <img src={NOBLE_IMAGE[noble.id]} alt="" draggable={false} className="h-full w-full object-cover object-top" />
        <div className="pointer-events-none absolute -inset-y-4 -left-1/3 w-1/3 rotate-12 bg-gradient-to-r from-transparent via-white/10 to-transparent" />
      </div>
      <div
        className={`pointer-events-none absolute inset-x-[5px] top-[14px] overflow-hidden rounded-t-full rounded-b-[4px] ring-1 ring-amber-200/50 ${
          size === 'sm' ? 'bottom-[19px]' : 'bottom-[23px]'
        }`}
      />

      {/* crown finial + VP badge */}
      <CrownGlyph className={`absolute start-1/2 -translate-x-1/2 text-amber-300 drop-shadow ${size === 'sm' ? 'top-0 h-3 w-3' : 'top-0.5 h-3.5 w-3.5'}`} />
      <span
        className={`absolute start-1 top-1 z-10 flex items-center justify-center rounded-full font-black text-amber-950 ring-1 ring-amber-200 ${
          size === 'sm' ? 'h-4 w-4 text-[9px]' : 'h-5 w-5 text-[11px]'
        }`}
        style={{
          background: 'radial-gradient(circle at 34% 28%, #fde68a, #f59e0b 75%)',
          boxShadow: 'inset 0 1px 1px rgba(255,255,255,0.8), 0 2px 3px rgba(0,0,0,0.5)',
        }}
      >
        {NOBLE_POINTS}
      </span>

      {/* requirement strip */}
      <span
        className={`absolute inset-x-[5px] bottom-[3px] flex items-end justify-center gap-1.5 rounded-[4px] bg-black/55 font-bold tabular-nums text-white shadow-[inset_0_1px_2px_rgba(0,0,0,0.5)] backdrop-blur-[1px] ${
          size === 'sm' ? 'py-0.5 text-[9px]' : 'py-1 text-[11px]'
        }`}
      >
        {noble.requirement.map((r, i) =>
          r > 0 ? (
            <span key={i} className="flex items-center gap-0.5">
              <GemIcon color={GEM_ORDER[i]} className={size === 'sm' ? 'h-3 w-3' : 'h-4 w-4'} />
              {r}
            </span>
          ) : null,
        )}
      </span>
    </div>
  )
}
