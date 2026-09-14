/**
 * Bordia brand: a stacked trio of game pieces (card, die, gem) over a
 * gradient mark, with the platform wordmark. Used in the site headers and
 * auth screens; wrap in a Link where navigation is wanted.
 */
export function BrandMark({ className = 'h-9 w-9' }: { className?: string }) {
  return (
    <svg viewBox="0 0 40 40" className={className} aria-hidden="true">
      <defs>
        <linearGradient id="bdg" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#34d399" />
          <stop offset="0.5" stopColor="#14b8a6" />
          <stop offset="1" stopColor="#0ea5e9" />
        </linearGradient>
      </defs>
      <rect x="1" y="1" width="38" height="38" rx="10" fill="url(#bdg)" />
      <rect x="1" y="1" width="38" height="38" rx="10" fill="none" stroke="rgba(255,255,255,0.45)" strokeWidth="1.4" />
      {/* card */}
      <g transform="rotate(-14 14 20)">
        <rect x="7.5" y="10" width="12" height="18" rx="2.4" fill="#0f172a" opacity="0.88" />
        <rect x="9.2" y="11.8" width="8.6" height="14.4" rx="1.2" fill="none" stroke="rgba(255,255,255,0.4)" strokeWidth="0.9" />
        <path d="M13.5 16.5l2 3-2 3-2-3z" fill="#f8fafc" />
      </g>
      {/* die */}
      <g transform="rotate(9 27 16)">
        <rect x="20.5" y="8.5" width="13" height="13" rx="3" fill="#f8fafc" />
        <circle cx="24" cy="12" r="1.35" fill="#0f172a" />
        <circle cx="30" cy="12" r="1.35" fill="#0f172a" />
        <circle cx="27" cy="15" r="1.35" fill="#0f172a" />
        <circle cx="24" cy="18" r="1.35" fill="#0f172a" />
        <circle cx="30" cy="18" r="1.35" fill="#0f172a" />
      </g>
      {/* gem */}
      <g transform="translate(0 1)">
        <polygon points="28,21 35,25.5 28,34.5 21,25.5" fill="#fbbf24" />
        <polyline points="21,25.5 28,25.5 35,25.5" stroke="rgba(255,255,255,0.8)" strokeWidth="1" fill="none" />
        <polyline points="28,21 25.2,25.5 28,34.5" stroke="rgba(120,53,15,0.5)" strokeWidth="0.9" fill="none" />
      </g>
    </svg>
  )
}

export function BrandLogo({ dark = true, compact = false }: { dark?: boolean; compact?: boolean }) {
  return (
    <span className="flex items-center gap-2.5">
      <BrandMark className={compact ? 'h-7 w-7' : 'h-9 w-9'} />
      <span className={`text-lg font-black tracking-tight ${dark ? 'text-white' : 'text-gray-900'}`}>
        Bordia
        <span className="text-emerald-400">.</span>
      </span>
    </span>
  )
}
