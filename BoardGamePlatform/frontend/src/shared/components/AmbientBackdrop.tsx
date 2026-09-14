import type { ReactNode } from 'react'

/**
 * Fixed decorative layer for dark site surfaces: felt gradient washes and a
 * scatter of slowly floating board-game glyphs (die, cards, gem, meeple,
 * crown). Purely ornamental - pointer-events-none, aria-hidden, RTL-neutral.
 */
const GLYPHS: { cls: string; rot: string; children: ReactNode }[] = [
  {
    cls: 'start-[6%] top-[16%] h-14 w-14',
    rot: '-12deg',
    children: (
      <g>
        <rect x="6" y="6" width="32" height="32" rx="7" stroke="currentColor" strokeWidth="2.4" />
        <circle cx="15" cy="15" r="2.6" fill="currentColor" />
        <circle cx="29" cy="15" r="2.6" fill="currentColor" />
        <circle cx="22" cy="22" r="2.6" fill="currentColor" />
        <circle cx="15" cy="29" r="2.6" fill="currentColor" />
        <circle cx="29" cy="29" r="2.6" fill="currentColor" />
      </g>
    ),
  },
  {
    cls: 'end-[10%] top-[22%] h-16 w-16',
    rot: '10deg',
    children: (
      <g>
        <rect x="10" y="6" width="24" height="34" rx="4" stroke="currentColor" strokeWidth="2.2" />
        <path d="M22 16l6 7-6 8-6-8z" fill="currentColor" fillOpacity="0.65" />
      </g>
    ),
  },
  {
    cls: 'start-[16%] bottom-[18%] h-12 w-12',
    rot: '8deg',
    children: <polygon points="22,6 36,16 29,38 15,38 8,16" stroke="currentColor" strokeWidth="2.2" fill="none" />,
  },
  {
    cls: 'end-[18%] bottom-[12%] h-14 w-14',
    rot: '-7deg',
    children: (
      <path
        d="M22 6c3.5 0 5.5 2.4 5.5 5.4 0 2-.8 3.4-2 4.8 3.4 2.6 6.5 6.4 6.5 11.4v3H12v-3c0-5 3.1-8.8 6.5-11.4-1.2-1.4-2-2.8-2-4.8C16.5 8.4 18.5 6 22 6z"
        stroke="currentColor"
        strokeWidth="2.2"
        fill="none"
      />
    ),
  },
  {
    cls: 'start-[44%] top-[9%] h-10 w-10',
    rot: '4deg',
    children: <path d="M5 30l-2-16 8 5 7-11 7 11 8-5-2 16z" stroke="currentColor" strokeWidth="2.2" fill="none" />,
  },
  {
    cls: 'end-[38%] top-[58%] h-9 w-9',
    rot: '-16deg',
    children: (
      <g>
        <rect x="9" y="9" width="22" height="22" rx="5" stroke="currentColor" strokeWidth="2.2" />
        <circle cx="20" cy="20" r="3.4" fill="currentColor" />
      </g>
    ),
  },
]

export function AmbientBackdrop({ dense = false }: { dense?: boolean }) {
  return (
    <div className="pointer-events-none fixed inset-0 overflow-hidden" aria-hidden="true">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(16,185,129,0.14),transparent_55%)]" />
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_right,rgba(56,189,248,0.10),transparent_50%)]" />
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(251,191,36,0.07),transparent_45%)]" />
      {/* woven felt grain */}
      <div
        className="absolute inset-0 opacity-[0.05]"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, rgba(255,255,255,0.6) 0 1px, transparent 1px 9px), repeating-linear-gradient(-45deg, rgba(255,255,255,0.6) 0 1px, transparent 1px 9px)',
        }}
      />
      {(dense ? GLYPHS : GLYPHS.slice(0, 5)).map((g, i) => (
        <svg
          key={i}
          viewBox="0 0 44 44"
          className={`absolute ${g.cls} a-glyph${(i % 4) + 1} text-emerald-200/10`}
          style={{ ['--g-rot' as string]: g.rot }}
          fill="none"
          stroke="none"
        >
          {g.children}
        </svg>
      ))}
    </div>
  )
}
