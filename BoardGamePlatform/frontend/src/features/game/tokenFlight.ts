import { GEM_IMAGE, SCENE_IMAGE, type GemKind } from './splendor'
import { GEM_STYLE } from '@/shared/components/SplendorCardVisual'

/**
 * Tiny dependency-free flight animation used by the Splendor view: DOM anchors
 * (supply piles, market slots, deck stacks, nobles, seat panels) register
 * here; after each accepted action the view diffs the projected state and
 * spawns floating clones that travel from source to destination with the
 * Web Animations API. Purely cosmetic - the state is already authoritative.
 */

const anchors = new Map<string, HTMLElement>()
const lastRects = new Map<string, DOMRect>()

export function flightAnchor(key: string) {
  return (el: HTMLElement | null) => {
    if (el) {
      anchors.set(key, el)
      lastRects.set(key, el.getBoundingClientRect())
    } else {
      anchors.delete(key)
    }
  }
}

/** Drops cached geometry (e.g. when leaving a session, where keys repeat). */
export function resetFlights() {
  anchors.clear()
  lastRects.clear()
}

function rectOf(key: string, fallback: string): DOMRect | null {
  if (!key) {
    if (fallback) return rectOf(fallback, '')
    return null
  }
  const el = anchors.get(key)
  if (el && el.isConnected) {
    const r = el.getBoundingClientRect()
    lastRects.set(key, r)
    return r
  }
  const cached = lastRects.get(key)
  if (cached) return cached
  return fallback ? rectOf(fallback, '') : null
}

export interface Flight {
  kind: 'token' | 'card' | 'noble'
  gem?: GemKind
  tier?: 1 | 2 | 3
  noble?: string
  fromKey: string
  toKey: string
  /** Destination fallback (e.g. the seat panel) when `toKey` isn't mounted yet. */
  toFallback?: string
  delay?: number
}

function fly(clone: HTMLElement, from: DOMRect, to: DOMRect, delay: number) {
  const w = clone.offsetWidth
  const h = clone.offsetHeight
  const x0 = from.left + from.width / 2 - w / 2
  const y0 = from.top + from.height / 2 - h / 2
  const x1 = to.left + to.width / 2 - w / 2
  const y1 = to.top + to.height / 2 - h / 2
  const mid = Math.min(y0, y1) - 60
  clone.style.position = 'fixed'
  clone.style.left = '0'
  clone.style.top = '0'
  clone.style.zIndex = '60'
  clone.style.pointerEvents = 'none'
  document.body.appendChild(clone)
  const anim = clone.animate(
    [
      { transform: `translate(${x0}px, ${y0}px) scale(1.1)`, opacity: 1 },
      { transform: `translate(${(x0 + x1) / 2}px, ${mid}px) scale(1.25)`, opacity: 1, offset: 0.5 },
      { transform: `translate(${x1}px, ${y1}px) scale(0.45)`, opacity: 0.95 },
    ],
    { duration: 640, delay, easing: 'cubic-bezier(0.3, 0.65, 0.3, 1)', fill: 'both' },
  )
  anim.onfinish = () => clone.remove()
  anim.oncancel = () => clone.remove()
}

const CHIP = 44
const CARD_W = 62
const CARD_H = 84

export function runFlights(flights: Flight[]) {
  flights.forEach((f, i) => {
    const to = rectOf(f.toKey, f.toFallback ?? '')
    const from = rectOf(f.fromKey, f.toFallback ?? '')
    if (!to || !from) return
    const clone = document.createElement('div')
    const delay = f.delay ?? i * 90
    if (f.kind === 'token' && f.gem) {
      const s = GEM_STYLE[f.gem]
      clone.style.width = `${CHIP}px`
      clone.style.height = `${CHIP}px`
      clone.style.boxSizing = 'border-box'
      clone.style.borderRadius = '9999px'
      clone.style.border = `5px solid ${s.lo}`
      clone.style.backgroundImage = `url(${GEM_IMAGE[f.gem]})`
      clone.style.backgroundSize = 'cover'
      clone.style.backgroundPosition = 'center'
      clone.style.boxShadow = 'inset 0 2px 3px rgba(255,255,255,0.3), 0 10px 18px rgba(0,0,0,0.5)'
    } else if (f.kind === 'card' && f.tier) {
      clone.style.width = `${CARD_W}px`
      clone.style.height = `${CARD_H}px`
      clone.style.borderRadius = '10px'
      clone.style.border = '1.5px solid rgba(255,255,255,0.85)'
      clone.style.backgroundImage = `url(${SCENE_IMAGE[f.tier]})`
      clone.style.backgroundSize = 'cover'
      clone.style.boxShadow = '0 10px 22px rgba(0,0,0,0.55)'
    } else if (f.kind === 'noble') {
      clone.style.width = `${CARD_W}px`
      clone.style.height = '56px'
      clone.style.borderRadius = '10px'
      clone.style.border = '2px solid rgba(252,211,77,0.9)'
      clone.style.background = 'linear-gradient(160deg, #3f3f46, #09090b)'
      clone.style.boxShadow = '0 10px 22px rgba(0,0,0,0.55)'
    } else {
      return
    }
    fly(clone, from, to, delay)
  })
}
