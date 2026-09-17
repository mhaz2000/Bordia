import type { CSSProperties } from 'react'
import type { AzulColor } from '@/features/game/azul'
import { useI18n } from '@/i18n/I18nProvider'

/**
 * Original Azul visuals: glossy ceramic tiles modeled on traditional Iranian
 * haft-rangi tiles (khatam star / pomegranate / yalduz / girih / gonbad
 * rosette motifs), each color with its OWN printed motif so color is never the
 * only signal, factory tray discs holding four tiles, the first-player marker,
 * and the two-part player board (wall grid + staircase pattern lines + floor).
 * Inline SVG/CSS only, RTL-neutral. Sources of truth: docs/games/azul.md
 * §27-§30.
 */

/** Per color: Persian-glaze face + dark clay edge (3D depth) + motif + accent ink. */
const TILE_STYLE: Record<AzulColor, { bg: string; edge: string; motif: 'khatam' | 'anar' | 'yalduz' | 'girih' | 'gonbad'; stroke: string }> = {
  0: { bg: 'radial-gradient(circle at 30% 24%, #7dd3fc 0%, #2563eb 45%, #172554 100%)', edge: '#0a1633', motif: 'khatam', stroke: 'rgba(165,243,252,0.65)' },
  1: { bg: 'radial-gradient(circle at 30% 24%, #fca5a5 0%, #dc2626 48%, #4c0519 100%)', edge: '#31030f', motif: 'anar', stroke: 'rgba(255,214,220,0.6)' },
  2: { bg: 'radial-gradient(circle at 30% 24%, #fef08a 0%, #d97706 50%, #713f12 100%)', edge: '#3d2006', motif: 'yalduz', stroke: 'rgba(254,243,199,0.65)' },
  3: { bg: 'radial-gradient(circle at 30% 24%, #71717a 0%, #18181b 55%, #000000 100%)', edge: '#000000', motif: 'girih', stroke: 'rgba(250,204,21,0.5)' },
  4: { bg: 'radial-gradient(circle at 30% 24%, #fffbeb 0%, #e2e8f0 55%, #64748b 100%)', edge: '#3f4c63', motif: 'gonbad', stroke: 'rgba(13,148,136,0.75)' },
}

/**
 * Inner SVG markup per color motif - traditional Persian tile geometry:
 * 8-pointed khatam star (blue), pomegranate (red), six-pointed yalduz star
 * (yellow), girih diamond cross (black), dotted gonbad rosette (white).
 * Single source for both the React tiles and the WAAPI flight clones.
 */
const MOTIF_SVG: Record<string, string> = {
  khatam:
    '<g stroke="currentColor" stroke-width="1.3" fill="none"><rect x="6.8" y="6.8" width="10.4" height="10.4"/><rect x="6.8" y="6.8" width="10.4" height="10.4" transform="rotate(45 12 12)"/><circle cx="12" cy="12" r="2"/></g>',
  anar:
    '<g stroke="currentColor" stroke-width="1.25" fill="none"><circle cx="12" cy="14" r="4.4"/><path d="M9.8 10.1 8.6 6.6l2.2 1.6L12 5.6l1.2 2.6 2.2-1.6-1.2 3.5"/></g>',
  yalduz:
    '<g stroke="currentColor" stroke-width="1.25" fill="none"><path d="M12 4.5l7 12.5H5z"/><path d="M12 19.5 5 7.5h14z"/></g>',
  girih:
    '<g stroke="currentColor" stroke-width="1.3" fill="none"><path d="M12 4.2 14.3 6.5 12 8.8 9.7 6.5zM12 15.2l2.3 2.3-2.3 2.3-2.3-2.3zM4.2 12l2.3-2.3L8.8 12l-2.3 2.3zM15.2 12l2.3-2.3L19.8 12l-2.3 2.3z"/><circle cx="12" cy="12" r="1.3"/></g>',
  gonbad:
    '<g stroke="currentColor" stroke-width="1.25" fill="none"><circle cx="12" cy="12" r="6.4" stroke-dasharray="2.4 1.5"/><path d="M12 7l1.35 3.65L17 12l-3.65 1.35L12 17l-1.35-3.65L7 12l3.65-1.35z"/></g>',
}

const MARKER_SVG_PATH =
  'M7 21c-2.2 0-4-1.7-4-3.9 0-1 .4-2 1.1-2.7L8 10.5c.6-.6 1.6-.6 2.2 0 .3.3.4.6.4 1V9c0-.8.6-1.4 1.4-1.4S13.4 8.2 13.4 9v.6c0-.8.6-1.4 1.4-1.4s1.4.6 1.4 1.4v.8c0-.7.6-1.3 1.3-1.3 1.3 0 2 1.4 1.5 2.7l-1.7 4.6c-.6 2.4-2.5 4.1-4.8 4.6L7 21z'

/**
 * Raw-DOM clones matching AzulTile / AzulMarker, for the Web Animations API
 * flights in AzulGameView (spec §29). The caller positions them via flyBetween.
 */
export function createAzulTileClone(color: AzulColor): HTMLElement {
  const s = TILE_STYLE[color]
  const el = document.createElement('span')
  el.style.width = '30px'
  el.style.height = '30px'
  el.style.display = 'inline-flex'
  el.style.alignItems = 'center'
  el.style.justifyContent = 'center'
  el.style.borderRadius = '22%'
  el.style.background = s.bg
  el.style.boxShadow = `0 2px 0 1px ${s.edge}, 0 6px 14px rgba(0,0,0,0.55), inset 0 1px 1px rgba(255,255,255,0.45)`
  const svg = document.createElement('span')
  svg.style.width = '17px'
  svg.style.height = '17px'
  svg.style.color = s.stroke
  svg.style.opacity = '0.85'
  svg.innerHTML = `<svg viewBox="0 0 24 24" width="17" height="17">${MOTIF_SVG[s.motif]}</svg>`
  el.appendChild(svg)
  return el
}

export function createAzulMarkerClone(): HTMLElement {
  const el = document.createElement('span')
  el.style.width = '30px'
  el.style.height = '30px'
  el.style.display = 'inline-flex'
  el.style.alignItems = 'center'
  el.style.justifyContent = 'center'
  el.style.borderRadius = '9999px'
  el.style.background = 'radial-gradient(circle at 34% 28%, #fef3c7 0%, #fbbf24 45%, #b45309 100%)'
  el.style.boxShadow = '0 6px 14px rgba(0,0,0,0.55), inset 0 1px 2px rgba(255,255,255,0.6)'
  el.innerHTML = `<svg viewBox="0 0 24 24" width="18" height="18" fill="#78350f"><path d="${MARKER_SVG_PATH}"/></svg>`
  return el
}

export type AzulTileSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl'

const TILE_DIMS: Record<AzulTileSize, string> = {
  xs: 'h-4 w-4',
  sm: 'h-5 w-5',
  md: 'h-7 w-7',
  lg: 'h-10 w-10',
  xl: 'h-11 w-11 sm:h-12 sm:w-12',
}

const MOTIF_SIZE: Record<AzulTileSize, string> = {
  xs: 'h-2.5 w-2.5',
  sm: 'h-3 w-3',
  md: 'h-4 w-4',
  lg: 'h-6 w-6',
  xl: 'h-6 w-6 sm:h-7 sm:w-7',
}

/** A single 3D ceramic azulejo tile with its color's printed motif. */
export function AzulTile({
  color,
  size = 'md',
  className = '',
  style,
}: {
  color: AzulColor
  size?: AzulTileSize
  className?: string
  style?: CSSProperties
}) {
  const s = TILE_STYLE[color]
  return (
    <span
      className={`relative inline-flex ${TILE_DIMS[size]} select-none items-center justify-center overflow-hidden rounded-[22%] ${className}`}
      style={{
        background: s.bg,
        boxShadow: `0 2px 0 1px ${s.edge}, 0 ${size === 'lg' ? 6 : size === 'md' ? 4 : 2}px ${size === 'lg' ? 12 : 6}px rgba(0,0,0,0.55), inset 0 1px 1px rgba(255,255,255,0.45)`,
        ...style,
      }}
      aria-hidden="true"
    >
      {/* glazed top-edge highlight + bottom inner shadow for volume */}
      <span className="pointer-events-none absolute inset-x-[10%] top-[4%] h-[26%] rounded-[50%] bg-white/40 blur-[1px]" />
      <span className="pointer-events-none absolute inset-x-[12%] bottom-[4%] h-[18%] rounded-[50%] bg-black/25 blur-[1px]" />
      <span className="pointer-events-none absolute inset-[12%] rounded-[18%]" style={{ border: `1px solid ${s.stroke}` }} />
      {/* haft-rangi corner spandrels on readable sizes */}
      {(size === 'md' || size === 'lg' || size === 'xl') && (
        <span className="pointer-events-none absolute inset-0">
          <span className="absolute left-[5px] top-[5px] h-1 w-1 rotate-45" style={{ background: s.stroke }} />
          <span className="absolute right-[5px] top-[5px] h-1 w-1 rotate-45" style={{ background: s.stroke }} />
          <span className="absolute left-[5px] bottom-[5px] h-1 w-1 rotate-45" style={{ background: s.stroke }} />
          <span className="absolute right-[5px] bottom-[5px] h-1 w-1 rotate-45" style={{ background: s.stroke }} />
        </span>
      )}
      <svg
        viewBox="0 0 24 24"
        className={`${MOTIF_SIZE[size]} relative opacity-90`}
        style={{ color: s.stroke, filter: 'drop-shadow(0 1px 0 rgba(0,0,0,0.35))' }}
        dangerouslySetInnerHTML={{ __html: MOTIF_SVG[s.motif] }}
      />
    </span>
  )
}

/** A factory tray: terracotta disc holding its tiles around the rim. */
export function AzulFactoryDisc({
  tiles,
  index,
  selected,
  dimmed,
  disabled,
  onSelect,
  anchor,
}: {
  tiles: number[]
  index: number
  selected?: boolean
  dimmed?: boolean
  disabled?: boolean
  onSelect?: () => void
  /** Flight-anchor ref callback (see tokenFlight). */
  anchor?: (el: HTMLElement | null) => void
}) {
  const { t } = useI18n()
  // Up to 4 tiles laid at the disc's corners, like the physical tray.
  const positions = [
    'left-1/2 top-1.5 -translate-x-1/2',
    'left-1.5 top-1/2 -translate-y-1/2',
    'left-1/2 bottom-1.5 -translate-x-1/2',
    'right-1.5 top-1/2 -translate-y-1/2',
  ]
  return (
    <button
      ref={anchor}
      type="button"
      disabled={disabled}
      onClick={onSelect}
      title={t('azul.factoryAria', { n: index + 1 })}
      aria-label={t('azul.factoryAria', { n: index + 1 })}
      className={`relative h-24 w-24 rounded-full transition-all duration-150 sm:h-28 sm:w-28 ${
        selected
          ? 'ring-3 ring-emerald-300 shadow-[0_0_22px_rgba(52,211,153,0.8)]'
          : 'ring-1 ring-black/40'
      } ${dimmed ? 'opacity-45 grayscale' : disabled ? 'opacity-80' : 'hover:-translate-y-1.5 hover:shadow-2xl'} ${
        !disabled && !dimmed ? 'cursor-pointer' : 'cursor-default'
      }`}
      style={{
        background:
          'radial-gradient(circle at 50% 42%, #c2670b 0%, #a3510c 38%, #7c3a0d 64%, #4a2106 86%, #331604 100%)',
        boxShadow: 'inset 0 4px 8px rgba(255,214,170,0.22), inset 0 -8px 14px rgba(0,0,0,0.6), 0 6px 12px rgba(0,0,0,0.55)',
      }}
    >
      {/* ghost number baked into the clay */}
      <span className="pointer-events-none absolute inset-0 flex items-center justify-center text-5xl font-black text-amber-100/10">
        {index + 1}
      </span>
      <span className="pointer-events-none absolute inset-[16%] rounded-full border border-amber-100/25" />
      <span className="pointer-events-none absolute inset-[26%] rounded-full border border-black/25" />
      {tiles.length === 0 ? (
        <span className="absolute inset-0 flex items-center justify-center text-[10px] font-bold uppercase tracking-wide text-amber-100/40">
          {t('azul.empty')}
        </span>
      ) : (
        tiles.slice(0, 4).map((c, i) => (
          <span key={i} className={`absolute ${positions[i]}`}>
            <AzulTile color={c as AzulColor} size="md" />
          </span>
        ))
      )}
    </button>
  )
}

/** The first-player marker token (hand of the king). */
export function AzulMarker({ size = 'md', faded = false, className = '' }: { size?: 'sm' | 'md' | 'lg'; faded?: boolean; className?: string }) {
  const px = size === 'sm' ? 'h-6 w-6' : size === 'lg' ? 'h-12 w-12' : 'h-8 w-8'
  return (
    <span
      className={`${px} relative inline-flex select-none items-center justify-center rounded-full shadow-[0_3px_6px_rgba(0,0,0,0.5),inset_0_1px_2px_rgba(255,255,255,0.6)] ${faded ? 'opacity-50 grayscale' : ''} ${className}`}
      style={{ background: 'radial-gradient(circle at 34% 28%, #fef3c7 0%, #fbbf24 45%, #b45309 100%)' }}
      aria-hidden="true"
    >
      <svg viewBox="0 0 24 24" className={size === 'lg' ? 'h-7 w-7' : size === 'sm' ? 'h-3.5 w-3.5' : 'h-[18px] w-[18px]'} fill="#78350f" dangerouslySetInnerHTML={{ __html: `<path d="${MARKER_SVG_PATH}"/>` }} />
    </span>
  )
}

/** Printed connector symbol per row/line (same shapes the physical board uses). */
const ROW_GLYPHS = ['khatam', 'anar', 'yalduz', 'girih', 'gonbad'] as const

/**
 * The physical board's printed wall mosaic: a cyclic Latin square so every
 * row and every column shows each color exactly once (row 1: Blue Yellow Red
 * Black White, each row shifted one step). PURELY DECORATIVE - completed
 * lines always tile the leftmost open cell of their row (§10/OD-6), and the
 * real rules only care about placed tiles, never this under-glaze design.
 * Rendered as ink washes so empty holes never read as placed tiles.
 */
const WALL_BASE_SEQUENCE = [0, 2, 1, 3, 4] as const
const printedWallColor = (row: number, col: number): AzulColor =>
  WALL_BASE_SEQUENCE[(((col - row) % 5) + 5) % 5] as AzulColor

/** Flat per-color tint for tiny printed hints (compact boards / holes). */
const PLAIN_TINTS = ['#1d4ed8', '#b91c1c', '#d97706', '#09090b', '#f1f5f9']

function RowGlyph({ row, className }: { row: number; className: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={className}
      fill="none"
      stroke="currentColor"
      dangerouslySetInnerHTML={{ __html: MOTIF_SVG[ROW_GLYPHS[row % 5]] }}
      aria-hidden="true"
    />
  )
}

/**
 * A full player board laid out like the physical Azul board: a bright blue
 * majolica panel in two side-by-side halves - the LEFT half holds the
 * staircase of pattern lines with the floor line and printed marker space
 * underneath, the RIGHT half holds the 5x5 palace wall. Both halves share a
 * row height, and each wall row carries the printed symbol of the pattern
 * line that feeds it at the seam, exactly like the board's connector cues.
 * Glazed Persian tiles pop in and catch the light, completed wall rows gild.
 * When a color is being picked, legal lines (or the forced floor) glow;
 * picking calls back with the line index.
 */
export function AzulPlayerBoard({
  wall,
  patternLines,
  floor,
  score,
  markerHere,
  picking,
  onPickLine,
  onPickFloor,
  compact = false,
  anchorId,
  registerAnchor,
  flashFloor = false,
}: {
  wall: number[][]
  patternLines: number[][]
  floor: number[]
  score: number
  markerHere: boolean
  /** The staged color and its legal lines, or null when not picking. */
  picking: { color: AzulColor; lines: number[]; forced: boolean } | null
  onPickLine?: (line: number) => void
  onPickFloor?: () => void
  compact?: boolean
  /** Prefix for flight anchors, e.g. `seat:2` (pairs with `registerAnchor`). */
  anchorId?: string
  registerAnchor?: (key: string) => (el: HTMLElement | null) => void
  /** Round-end floor penalty: pulse the floor line red (spec §29). */
  flashFloor?: boolean
}) {
  const { t } = useI18n()
  const anchorFor = (suffix: string) => (anchorId && registerAnchor ? registerAnchor(`${anchorId}:${suffix}`) : undefined)

  // One size ladder; pattern rows and wall rows share heights so the two
  // halves of the board stay row-aligned like the physical one. Tiles sit
  // slightly inside their cells, like glazed stones resting in printed holes.
  const cell = compact ? 'h-5 w-5' : 'h-12 w-12 sm:h-14 sm:w-14'
  const tile: AzulTileSize = compact ? 'xs' : 'xl'
  const gap = compact ? 'gap-0.5' : 'gap-1.5 sm:gap-2.5'
  const floorCell = compact ? 'h-4 w-4' : 'h-8 w-8 sm:h-9 sm:w-9'
  const markerSize = compact ? 'h-6 w-6' : 'h-11 w-11 sm:h-12 sm:w-12'
  const FLOOR_PENALTIES = [-1, -1, -2, -2, -2, -3, -3]
  const forcedFloor = !!picking && picking.forced
  const legalLine = (i: number) => !!picking && picking.lines.includes(i)

  return (
    <div
      className={`relative w-full select-none overflow-hidden ${compact ? 'rounded-lg p-1.5' : 'rounded-2xl p-3 sm:p-4'}`}
      style={{
        background: `
          linear-gradient(168deg, #1a2a4a 0%, #142038 35%, #0d1828 65%, #07101c 100%),
          url("data:image/svg+xml,%3Csvg width='80' height='80' viewBox='0 0 80 80' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%231e3a5f' fill-opacity='0.06'%3E%3Cpath d='M40 0c22.09 0 40 17.91 40 40s-17.91 40-40 40S0 62.09 0 40 17.91 0 40 0zm0 5c19.29 0 35 15.71 35 35s-15.71 35-35 35S5 59.29 5 40 20.71 5 40 5zm0 6c16.02 0 29 12.98 29 29s-12.98 29-29 29-29-12.98-29-29 12.98-29 29-29z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")
        `,
        boxShadow:
          'inset 0 1px 0 rgba(199,210,254,0.28), inset 0 -6px 16px rgba(0,0,0,0.65), inset 0 0 0 1px rgba(129,140,248,0.2), 0 16px 40px rgba(0,0,0,0.7)',
      }}
    >
      {/* ornate border frame - carved wood/stone look with inner bevel */}
      <span className={`pointer-events-none absolute ${compact ? 'inset-0.5' : 'inset-1'} rounded-xl border border-indigo-100/35`} />
      <span className={`pointer-events-none absolute ${compact ? 'inset-1.5' : 'inset-2'} rounded-lg border border-slate-800/60`} />
      <span className={`pointer-events-none absolute ${compact ? 'inset-2' : 'inset-3'} rounded-md border border-indigo-200/10`} />

      {/* header: which half is which + the gold score medal */}
      <div className={`relative flex items-center justify-between ${compact ? 'px-0.5 pb-1' : 'px-1.5 pb-2'}`}>
        <div className="flex items-center gap-1">
          <svg className="h-4 w-4 text-indigo-200/60" viewBox="0 0 24 24" fill="currentColor"><path d="M4 6h16v2H4zm0 5h16v2H4zm0 5h16v2H4z"/></svg>
          <p className={`${compact ? 'text-[6px]' : 'text-[9px]'} font-black uppercase tracking-[0.25em] text-indigo-200/60`}>{t('azul.linesLabel')}</p>
        </div>
        <div className="flex items-center gap-3">
          <p className={`${compact ? 'hidden' : 'text-[9px]'} font-black uppercase tracking-[0.25em] text-indigo-200/60`}>{t('azul.wallLabel')}</p>
          <span
            ref={anchorFor('score')}
            className={`flex items-baseline gap-1.5 rounded-full font-black tabular-nums text-amber-950 ring-2 ring-amber-100/70 ${
              compact ? 'px-1.5 py-px text-xs' : 'px-5 py-1.5 text-2xl shadow-[0_6px_20px_rgba(0,0,0,0.65)]'
            }`}
            style={{ background: 'linear-gradient(180deg, #fef3c7 0%, #fbbf24 40%, #f59e0b 60%, #d97706 100%)' }}
            title={t('azul.vpN', { n: score })}
          >
            <span className="flex items-center gap-1.5">
              <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2l2.4 7.4 8 1.2-5.8 5.6 1.4 8.1L12 18.8l-6.4 3.4 1.4-8.1L0 10.6l8-1.2z" /></svg>
              <span>{score}</span>
            </span>
            <span className={`${compact ? 'text-[6px]' : 'text-[9px]'} font-bold uppercase opacity-70`}>{t('azul.vp')}</span>
          </span>
        </div>
      </div>

      {/* ==================== TWO-PART BOARD: LINES | WALL ==================== */}
      <div className={`flex items-start justify-center ${compact ? 'gap-0.5' : 'gap-1 sm:gap-2'}`}>
        {/* ---- LEFT half: staircase pattern lines + floor + marker ---- */}
        <div className={`flex flex-col items-stretch ${compact ? 'gap-0.5' : 'gap-1.5 sm:gap-2'}`}>
          {patternLines.map((row, i) => {
            const legal = legalLine(i)
            const cap = i + 1
            return (
              <div
                key={i}
                ref={anchorFor(`line:${i}`)}
                onClick={() => legal && onPickLine?.(i)}
                role={legal ? 'button' : undefined}
                className={`flex items-center justify-end ${gap} ${legal ? 'cursor-pointer' : ''}`}
              >
                {Array.from({ length: cap }).map((_, s) => {
                  const idx = row.length - cap + s
                  const c = idx >= 0 ? (row[idx] as number) : -1
                  const hasTile = c >= 0
                  return (
                    <span
                      key={s}
                      className={`${cell} relative flex items-center justify-center rounded-[18%] ${
                        hasTile ? '' : 'border border-white/10 bg-white/[0.03]'
                      } ${
                        legal
                          ? 'ring-1 ring-emerald-300/70 shadow-[0_0_10px_rgba(52,211,153,0.35)]'
                          : hasTile
                            ? ''
                            : ''
                      }`}
                    >
                      {hasTile ? (
                        <AzulTile color={c as AzulColor} size={tile} />
                      ) : (
                        <span className={`${compact ? 'text-[7px]' : 'text-[11px]'} font-bold text-sky-200/25`}>{i + 1}</span>
                      )}
                    </span>
                  )
                })}
              </div>
            )
          })}

          {/* floor line + marker space */}
          <div
            ref={anchorFor('floor')}
            onClick={() => forcedFloor && onPickFloor?.()}
            className={`flex items-center ${gap} ${
              flashFloor ? 'animate-pulse rounded-xl ring-2 ring-rose-400/70' : ''
            } ${forcedFloor ? 'cursor-pointer rounded-xl ring-2 ring-rose-300/70' : ''}`}
          >
            {FLOOR_PENALTIES.map((p, i) => {
              const hasTile = floor.length > i
              return (
                <span
                  key={i}
                  className={`${floorCell} relative flex items-center justify-center rounded-full ${
                    hasTile ? '' : 'border border-white/10 bg-white/[0.03]'
                  }`}
                >
                  {hasTile ? (
                    <AzulTile color={floor[i] as AzulColor} size="sm" />
                  ) : (
                    <span className={`${compact ? 'text-[7px]' : 'text-[10px]'} font-bold text-rose-200/50`}>{p}</span>
                  )}
                </span>
              )
            })}
            <span
              ref={anchorFor('marker')}
              className={`${markerSize} relative flex items-center justify-center rounded-full ${
                markerHere ? '' : 'border border-dashed border-amber-200/30 bg-white/[0.03]'
              }`}
              title={t('azul.markerSpace')}
            >
              {markerHere && <AzulMarker size="md" />}
            </span>
          </div>
        </div>

        {/* ---- seam: connector glyphs aligning pattern lines to wall rows ---- */}
        <div className={`flex flex-col items-center ${compact ? 'gap-0.5' : 'gap-1.5 sm:gap-2'}`}>
          {Array.from({ length: 5 }).map((_, i) => (
            <span key={i} className={`${cell} flex items-center justify-center`} aria-hidden="true">
              <RowGlyph row={i} className={compact ? 'h-3 w-3 text-amber-200/30' : 'h-5 w-5 text-amber-200/40'} />
            </span>
          ))}
        </div>

        {/* ---- RIGHT half: the 5x5 palace wall ---- */}
        <div className={`flex flex-col ${compact ? 'gap-0.5' : 'gap-1.5 sm:gap-2'}`}>
          {wall.map((row, r) => {
            const complete = row.every((c) => c >= 0)
            return (
              <div key={r} className={`flex ${gap} ${complete ? 'rounded-xl ring-1 ring-amber-200/40' : ''}`}>
                {row.map((c, col) => {
                  const placed = c >= 0
                  return (
                    <span
                      key={col}
                      ref={anchorFor(`wall:${r}:${col}`)}
                      className={`${cell} relative flex items-center justify-center overflow-hidden rounded-[14%] ${
                        placed ? '' : 'border border-white/10 bg-white/[0.03]'
                      }`}
                      title={placed ? '' : t('azul.wallSlot')}
                    >
                      {placed ? (
                        <AzulTile color={c as AzulColor} size={tile} />
                      ) : (
                        <span
                          className={`${cell} block rounded-[14%]`}
                          style={{
                            background: `radial-gradient(circle at 50% 45%, ${PLAIN_TINTS[printedWallColor(r, col)]}55, transparent 70%)`,
                          }}
                          aria-hidden="true"
                        />
                      )}
                    </span>
                  )
                })}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
