import type { CSSProperties } from 'react'
import { EyeIcon, MoonIcon, ShieldCheckIcon, SparklesIcon } from '@heroicons/react/24/solid'
import { useI18n } from '@/i18n/I18nProvider'
import { cardNameKey, tierOf, TIER_STYLES, type SilverViewCard } from '@/features/game/silver'
import { SilverCardArt } from './SilverCardArt'

export type SilverCardVisualSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl'

const DIMS: Record<SilverCardVisualSize, string> = {
  xs: 'w-8 h-12',
  sm: 'w-12 h-[4.5rem]',
  md: 'w-16 h-24',
  lg: 'w-20 h-[7.5rem] sm:w-24 sm:h-32',
  xl: 'w-24 h-36 sm:w-28 sm:h-40',
}

const CENTER_VALUE: Record<SilverCardVisualSize, string> = {
  xs: 'text-sm',
  sm: 'text-2xl',
  md: 'text-3xl',
  lg: 'text-4xl sm:text-5xl',
  xl: 'text-5xl sm:text-6xl',
}

/** Character art scale per size (xs stays a plain number - too small for art). */
const ART_SIZE: Record<SilverCardVisualSize, string> = {
  xs: 'hidden',
  sm: 'h-7 w-7',
  md: 'h-9 w-9',
  lg: 'h-12 w-12',
  xl: 'h-14 w-14',
}

/** Value type when shown beneath the character art. */
const ART_VALUE: Record<SilverCardVisualSize, string> = {
  xs: 'text-sm',
  sm: 'text-[11px]',
  md: 'text-base',
  lg: 'text-xl',
  xl: 'text-2xl',
}

const NAME_TEXT: Record<SilverCardVisualSize, string> = {
  xs: 'hidden',
  sm: 'text-[6px]',
  md: 'text-[8px]',
  lg: 'text-[9px] sm:text-[11px]',
  xl: 'text-[10px] sm:text-xs',
}

const CORNER_TEXT: Record<SilverCardVisualSize, string> = {
  xs: 'text-[7px]',
  sm: 'text-[9px]',
  md: 'text-[10px]',
  lg: 'text-[11px] sm:text-xs',
  xl: 'text-xs sm:text-sm',
}

/**
 * A Silver card. The projection drives the rendering: a visible value renders
 * the character face (full when face up, dimmed with a memory badge when the
 * viewer merely knows the face-down card), a null value renders the card back.
 * Protection draws the Silver Amulet on top of any state.
 */
export function SilverCardVisual({
  card,
  size = 'md',
  className = '',
  style,
  actionable = false,
}: {
  card: SilverViewCard
  size?: SilverCardVisualSize
  className?: string
  style?: CSSProperties
  /** Amber sparkles marker: this resident's ability can be used right now. */
  actionable?: boolean
}) {
  const { t } = useI18n()
  const badge = card.Protected ? <ProtectionBadge card={card} size={size} /> : null

  if (card.Value === null) {
    return (
      <div className={`relative ${className}`} style={style}>
        <SilverCardBack size={size} />
        {badge}
      </div>
    )
  }

  const tier = TIER_STYLES[tierOf(card.Value)]
  const name = t(`games.Silver.cardNames.${cardNameKey(card.Value)}`)
  const knownFaceDown = !card.FaceUp

  return (
    <div
      className={`${DIMS[size]} relative flex-shrink-0 select-none overflow-hidden rounded-xl border-2 border-slate-200/80 bg-gradient-to-br shadow-lg ${tier.gradient} ${className}`}
      style={style}
    >
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-white/10 to-black/25" />
      <div className="pointer-events-none absolute inset-[3px] rounded-lg border border-white/20" />

      {knownFaceDown && <div className="pointer-events-none absolute inset-0 bg-indigo-950/45" />}

      {/* top-start corner value */}
      <span className={`${CORNER_TEXT[size]} absolute start-1.5 top-0.5 font-black text-white/90 drop-shadow`}>
        {card.Value}
      </span>

      {/* character art + werewolf count */}
      {size === 'xs' ? (
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className={`${CENTER_VALUE[size]} ${tier.accent} font-black leading-none tracking-tight drop-shadow-[0_2px_3px_rgba(0,0,0,0.6)]`}>
            {card.Value}
          </span>
        </div>
      ) : (
        <div className="absolute inset-x-0 top-3 bottom-4 flex flex-col items-center justify-center gap-1">
          <SilverCardArt value={card.Value} className={`${ART_SIZE[size]} ${tier.accent}`} />
          <span className={`${ART_VALUE[size]} ${tier.accent} font-black leading-none tracking-tight drop-shadow-[0_2px_3px_rgba(0,0,0,0.6)]`}>
            {card.Value}
          </span>
          {size === 'xl' && (
            <span className="flex items-center gap-1 text-[8px] font-semibold uppercase tracking-widest text-white/60">
              <MoonIcon className="h-2.5 w-2.5" />
              {t('silver.werewolves')}
            </span>
          )}
        </div>
      )}

      {/* character name plate */}
      <span className={`${NAME_TEXT[size]} absolute inset-x-0 bottom-0.5 truncate px-0.5 text-center font-bold text-white/95 drop-shadow`}>
        {name}
      </span>

      {/* memory badge: the viewer knows this face-down card */}
      {knownFaceDown && (
        <span
          className="absolute end-1 top-1 rounded-full bg-indigo-900/85 p-0.5 text-indigo-100 shadow ring-1 ring-indigo-300/50"
          title={t('silver.knownCard')}
        >
          <EyeIcon className="h-2.5 w-2.5 sm:h-3 sm:w-3" />
        </span>
      )}

      {/* public badge: a face-up card is visible to every player */}
      {card.FaceUp && (
        <span
          className="absolute end-1 top-1 rounded-full bg-emerald-900/85 p-0.5 text-emerald-200 shadow ring-1 ring-emerald-300/50"
          title={t('silver.faceUpBadge')}
        >
          <EyeIcon className="h-2.5 w-2.5 sm:h-3 sm:w-3" />
        </span>
      )}

      {/* ready badge: this resident's ability can be played right now */}
      {actionable && (
        <span
          className="absolute bottom-3 start-1 flex animate-pulse items-center justify-center rounded-full bg-amber-400 p-0.5 text-amber-950 shadow ring-1 ring-amber-200"
          title={t('silver.abilityReady')}
        >
          <SparklesIcon className="h-2.5 w-2.5 sm:h-3 sm:w-3" />
        </span>
      )}

      {/* protection badge: Silver Amulet or a Guard */}
      {badge}
    </div>
  )
}

/** The card back: midnight village with a silver crescent moon. */
export function SilverCardBack({
  size = 'md',
  className = '',
  style,
}: {
  size?: SilverCardVisualSize
  className?: string
  style?: CSSProperties
}) {
  return (
    <div
      className={`${DIMS[size]} relative flex-shrink-0 select-none overflow-hidden rounded-xl border-2 border-indigo-300/40 bg-gradient-to-br from-indigo-950 via-slate-900 to-indigo-950 shadow-lg ${className}`}
      style={style}
    >
      <div className="pointer-events-none absolute inset-[3px] rounded-lg border border-indigo-400/25" />
      {/* stars */}
      <span className="absolute start-[18%] top-[16%] h-0.5 w-0.5 rounded-full bg-slate-300/90" />
      <span className="absolute end-[22%] top-[30%] h-0.5 w-0.5 rounded-full bg-slate-300/70" />
      <span className="absolute start-[30%] bottom-[18%] h-0.5 w-0.5 rounded-full bg-slate-400/70" />
      <span className="absolute end-[16%] bottom-[26%] h-0.5 w-0.5 rounded-full bg-slate-300/80" />
      <div className="absolute inset-0 flex items-center justify-center">
        <MoonIcon className="h-1/3 w-1/3 text-slate-300/90 drop-shadow-[0_0_8px_rgba(199,210,254,0.55)]" />
      </div>
    </div>
  )
}

/** Protection marker: the Silver Amulet pendant, or a Guard shield. */
export function ProtectionBadge({ card, size = 'md' }: { card: SilverViewCard; size?: SilverCardVisualSize }) {
  const { t } = useI18n()
  const big = size === 'lg' || size === 'xl'
  const amulet = card.AmuletProtected
  return (
    <span
      className={`absolute ${big ? '-end-1.5 -top-1.5 h-6 w-6' : '-end-1 -top-1 h-[18px] w-[18px]'} flex items-center justify-center rounded-full shadow-md ring-2 ${
        amulet
          ? 'bg-gradient-to-br from-slate-100 to-slate-400 shadow-slate-400/50 ring-white/80'
          : 'bg-gradient-to-br from-emerald-200 to-emerald-500 shadow-emerald-500/50 ring-white/70'
      }`}
      title={amulet ? t('silver.protectedAmulet') : t('silver.protectedGuard')}
    >
      {amulet ? (
        <MoonIcon className={`${big ? 'h-3.5 w-3.5' : 'h-2.5 w-2.5'} text-slate-700`} />
      ) : (
        <ShieldCheckIcon className={`${big ? 'h-3.5 w-3.5' : 'h-2.5 w-2.5'} text-emerald-900`} />
      )}
    </span>
  )
}
