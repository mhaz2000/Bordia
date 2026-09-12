import type { ReactNode } from 'react'

/**
 * Original character art for the 14 Silver residents (0 Villager ... 13
 * Doppelgänger). Inline SVG line art in the card's accent color with subtle
 * idle animations (twinkles, blinks, a swaying lantern...). Decorative only:
 * aria-hidden, RTL-neutral, and safe to swap out for a licensed asset pack by
 * replacing the glyph for a value.
 */
export function SilverCardArt({ value, className = '' }: { value: number; className?: string }) {
  return (
    <svg
      viewBox="0 0 48 48"
      className={`${className} text-current`}
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {GLYPHS[value] ?? GLYPHS[0]}
    </svg>
  )
}

const GLYPHS: Record<number, ReactNode> = {
  // 0 Villager - a humble resident with a pitchfork, gently bobbing.
  0: (
    <g className="a-bob">
      <circle cx="20" cy="15" r="5" />
      <path d="M11 36c1.5-8 16-8 18 0" />
      <path d="M38 14v20" />
      <path d="M33 14v-5M38 14v-7M43 14v-5" />
      <path d="M33 14h10" />
    </g>
  ),

  // 1 Squire - a shield bearing the crescent, softly glowing.
  1: (
    <g>
      <path d="M24 7l13 4.5V23c0 8-6 13-13 15-7-2-13-7-13-15V11.5z" />
      <path className="a-flicker" d="M27 15a6 6 0 1 0 3.5 9A7 7 0 0 1 27 15z" fill="currentColor" opacity="0.7" stroke="none" />
    </g>
  ),

  // 2 Enchanter - a hooded head beneath three twinkling motes.
  2: (
    <g>
      <path d="M15 36c0-10 4-17 9-17s9 7 9 17" />
      <circle cx="24" cy="27" r="5.5" />
      <circle className="a-twinkle" cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
      <circle className="a-twinkle-2" cx="24" cy="7" r="1.6" fill="currentColor" stroke="none" />
      <circle className="a-twinkle-3" cx="36" cy="12" r="1.6" fill="currentColor" stroke="none" />
    </g>
  ),

  // 3 Guard - a shield with a watchful, blinking eye.
  3: (
    <g>
      <path d="M24 6l14 5v12c0 9-7 15-14 17-7-2-14-8-14-17V11z" />
      <path d="M17 21c3.5-4.5 10.5-4.5 14 0-3.5 4.5-10.5 4.5-14 0z" />
      <circle className="a-blink" cx="24" cy="21" r="2.6" fill="currentColor" stroke="none" />
    </g>
  ),

  // 4 Trickster - a rocking jester with bell tips.
  4: (
    <g className="a-rock">
      <path d="M13 27c0-9 5-15 11-15s11 6 11 15" />
      <path d="M13 27l-5-4M35 27l5-4" />
      <circle className="a-twinkle" cx="7" cy="22" r="2" fill="currentColor" stroke="none" />
      <circle className="a-twinkle-2" cx="41" cy="22" r="2" fill="currentColor" stroke="none" />
      <circle cx="24" cy="34" r="6" />
      <path d="M21.5 34.5h.01M26.5 34.5h.01M22 37c1.4 1.2 2.8 1.2 4.2 0" strokeWidth="1.6" />
    </g>
  ),

  // 5 Exposer - a wide blinking eye with light rays.
  5: (
    <g>
      <path className="a-twinkle" d="M24 6v4M9 24H5M43 24h-4M12 11l3 3M36 11l-3 3" strokeWidth="1.6" />
      <path d="M11 27c6-8 20-8 26 0-6 8-20 8-26 0z" />
      <circle className="a-blink" cx="24" cy="27" r="4" fill="currentColor" stroke="none" />
    </g>
  ),

  // 6 Revealer - a lantern with a flickering flame.
  6: (
    <g>
      <path d="M20 14c0-5 8-5 8 0" />
      <path d="M18 14h12l2 4v12a8 8 0 0 1-16 0V18z" />
      <circle className="a-flicker" cx="24" cy="24" r="4" fill="currentColor" stroke="none" opacity="0.9" />
      <path d="M16 40h16" />
    </g>
  ),

  // 7 Apprentice Seer - a crystal ball on a stand, swirling light inside.
  7: (
    <g>
      <circle cx="24" cy="21" r="11" />
      <path className="a-swap" d="M18 19c2-4 8-5 11-2" strokeWidth="1.6" />
      <circle className="a-twinkle" cx="28" cy="15" r="1.5" fill="currentColor" stroke="none" />
      <path d="M17 32l-2 6h18l-2-6" />
      <path d="M12 38h24" />
    </g>
  ),

  // 8 Seer - a crescent moon cradling a blinking eye.
  8: (
    <g>
      <path d="M31 7a15 15 0 1 0 9 26 13 13 0 0 1-9-26z" />
      <path d="M22 20c3.5-4 10-4 13 0-3.5 4-9.5 4-13 0z" />
      <circle className="a-blink" cx="28.5" cy="20" r="2.2" fill="currentColor" stroke="none" />
      <circle className="a-twinkle" cx="13" cy="12" r="1.3" fill="currentColor" stroke="none" />
    </g>
  ),

  // 9 Beholder - a telescope scanning the night, gently swaying.
  9: (
    <g>
      <path d="M16 34l-2 6h8l2-6" />
      <g className="a-sway">
        <path d="M14 28l20-12 4 7-20 12z" />
        <path d="M34 16l6-4-1 8" />
      </g>
      <circle className="a-twinkle" cx="10" cy="10" r="1.5" fill="currentColor" stroke="none" />
    </g>
  ),

  // 10 Master - a rising card selected from a steady stack.
  10: (
    <g>
      <rect x="14" y="16" width="12" height="17" rx="2" />
      <rect x="22" y="14" width="12" height="17" rx="2" />
      <rect className="a-slide" x="27" y="8" width="12" height="17" rx="2" fill="currentColor" fillOpacity="0.18" />
      <path d="M12 40c6-4 18-4 24 0" />
    </g>
  ),

  // 11 Witch - a bubbling potion flask.
  11: (
    <g>
      <path d="M21 7h6v9l6.5 13a10 10 0 1 1-19 0L21 16z" />
      <path d="M16.5 27h15" />
      <path d="M16.5 27a10 10 0 0 0 19 0" fill="currentColor" opacity="0.25" stroke="none" />
      <circle className="a-bubble" cx="21" cy="34" r="1.4" fill="currentColor" stroke="none" />
      <circle className="a-bubble" style={{ animationDelay: '0.9s' }} cx="26" cy="36" r="1.8" fill="currentColor" stroke="none" />
      <path d="M19 7h10" />
    </g>
  ),

  // 12 Robber - a hooded figure with a coin pouch and a glinting coin.
  12: (
    <g>
      <path d="M15 24c0-9 4-13 9-13s9 4 9 13l-4 3H19z" />
      <path d="M20 24h8l1.5 12a5.5 5.5 0 0 1-11 0z" />
      <path d="M21.5 24c0-2.5 5-2.5 5 0" />
      <circle className="a-twinkle" cx="37" cy="33" r="2.2" fill="currentColor" stroke="none" />
    </g>
  ),

  // 13 Doppelgänger - two masks alternately surfacing.
  13: (
    <g>
      <g className="a-swap">
        <path d="M10 13h14v10a7 7 0 0 1-14 0z" />
        <path d="M13.5 18h.01M20.5 18h.01M14 23c2.5 2 4.5 2 6.5 0" strokeWidth="1.6" />
      </g>
      <g className="a-swap-alt">
        <path d="M24 22h14v10a7 7 0 0 1-14 0z" fill="currentColor" fillOpacity="0.15" />
        <path d="M27.5 27h.01M34.5 27h.01M28 33c2-1.8 4.4-1.8 6.4 0" strokeWidth="1.6" />
      </g>
    </g>
  ),
}
