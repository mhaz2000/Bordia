import { anchorRect, flyBetween } from './tokenFlight'
import { createAzulMarkerClone, createAzulTileClone } from '@/shared/components/AzulBoardVisuals'
import type { AzulColor, AzulState } from './azul'

/**
 * Azul's §29 animation layer: after each accepted broadcast the view diffs the
 * previous and current projected state and replays what physically moved as
 * flying tiles (draft source -> pattern line, overflow -> floor, factory
 * leftovers -> center, line -> wall cell, marker hand-offs), score floats, a
 * round veil and a red floor flash on penalties. Purely cosmetic - the engine
 * already applied everything; anchors come from tokenFlight's registry.
 */

type AzulFlight =
  | { kind: 'tile'; color: AzulColor; from: string; to: string; delay: number }
  | { kind: 'marker'; from: string; to: string; delay: number }

export interface AzulEffectPlan {
  flights: AzulFlight[]
  /** Round advanced: veil + bag-refill transition instead of draft flights. */
  roundAdvanced: boolean
  newRound: number
  /** Score-pill floats: anchor key + delta. */
  popups: { key: string; delta: number }[]
  /** Seats whose floor line should flash red (round-end penalty). */
  penaltySeats: number[]
  /** Seat that just took the first-player marker (marker fly already planned). */
  markerTakenSeat: number
}

/** Multiset difference: tiles in `from` not matched by tiles in `to`. */
function removedTiles(from: number[], to: number[]): number[] {
  const pool = [...to]
  const out: number[] = []
  for (const t of from) {
    const i = pool.indexOf(t)
    if (i >= 0) pool.splice(i, 1)
    else out.push(t)
  }
  return out
}

export function diffAzul(prev: AzulState, cur: AzulState, prevOver: boolean, curOver: boolean): AzulEffectPlan {
  const plan: AzulEffectPlan = {
    flights: [],
    roundAdvanced: cur.RoundNumber > prev.RoundNumber,
    newRound: cur.RoundNumber,
    popups: [],
    penaltySeats: [],
    markerTakenSeat: -1,
  }
  const gameOverJust = curOver && !prevOver
  const tableReset = plan.roundAdvanced || gameOverJust

  for (let i = 0; i < Math.min(prev.Seats.length, cur.Seats.length); i++) {
    const delta = cur.Seats[i].Score - prev.Seats[i].Score
    if (delta !== 0) plan.popups.push({ key: `seat:${i}:score`, delta })
    if (tableReset && prev.Seats[i].Floor.length > 0) plan.penaltySeats.push(i)
  }

  // Completed lines glide one tile into the fresh wall cell (always safe:
  // fires on round transitions and the final tiling pass alike).
  let glide = 0
  for (let s = 0; s < Math.min(prev.Seats.length, cur.Seats.length); s++) {
    for (let r = 0; r < 5; r++) {
      for (let c = 0; c < 5; c++) {
        const before = prev.Seats[s].Wall[r]?.[c] ?? -1
        const after = cur.Seats[s].Wall[r]?.[c] ?? -1
        if (before === -1 && after >= 0) {
          plan.flights.push({
            kind: 'tile',
            color: after as AzulColor,
            from: `seat:${s}:line:${r}`,
            to: `seat:${s}:wall:${r}:${c}`,
            delay: 320 + glide++ * 60,
          })
        }
      }
    }
  }

  // Marker hand-offs: taken into a seat mid-round, back to the center at round end.
  if (prev.MarkerSeat === -1 && cur.MarkerSeat >= 0) {
    plan.flights.push({ kind: 'marker', from: 'center', to: `seat:${cur.MarkerSeat}:marker`, delay: 140 })
    plan.markerTakenSeat = cur.MarkerSeat
  } else if (tableReset && prev.MarkerSeat >= 0 && cur.MarkerSeat === -1) {
    plan.flights.push({ kind: 'marker', from: `seat:${prev.MarkerSeat}:marker`, to: 'center', delay: 120 })
  }

  if (tableReset) return plan

  // ----- normal turn: locate the single atomic draft the engine applied -----
  const actor = prev.CurrentPlayerIndex
  let fromKey = ''
  let draftedColor: AzulColor | null = null
  let draftedCount = 0
  let leftovers: number[] = []
  for (let f = 0; f < prev.Factories.length; f++) {
    const lost = removedTiles(prev.Factories[f], cur.Factories[f] ?? [])
    if (lost.length > 0) {
      // A drafted factory loses ALL its tiles: the color group to the player,
      // the mixed remainder to the center.
      fromKey = `factory:${f}`
      draftedColor = lost[0] as AzulColor
      draftedCount = lost.filter((t) => t === draftedColor).length
      leftovers = lost.filter((t) => t !== draftedColor)
      break
    }
  }
  if (!fromKey) {
    const lostCenter = removedTiles(prev.Center, cur.Center)
    if (lostCenter.length > 0) {
      fromKey = 'center'
      draftedColor = lostCenter[0] as AzulColor
      draftedCount = lostCenter.length
    }
  }
  if (!fromKey || draftedColor === null) return plan

  const prevSeat = prev.Seats[actor]
  const curSeat = cur.Seats[actor]
  if (!prevSeat || !curSeat) return plan

  let delay = 60
  let landed = 0
  for (let l = 0; l < 5; l++) {
    const grow = (curSeat.PatternLines[l]?.length ?? 0) - (prevSeat.PatternLines[l]?.length ?? 0)
    for (let j = 0; j < grow; j++) {
      plan.flights.push({ kind: 'tile', color: draftedColor, from: fromKey, to: `seat:${actor}:line:${l}`, delay: delay + j * 40 })
    }
    landed += Math.max(0, grow)
    delay += Math.max(0, grow) * 40
  }
  const floorGrow = Math.max(0, curSeat.Floor.length - prevSeat.Floor.length)
  for (let j = 0; j < floorGrow; j++) {
    plan.flights.push({ kind: 'tile', color: draftedColor, from: fromKey, to: `seat:${actor}:floor`, delay: delay + j * 40 })
  }
  landed += floorGrow
  if (landed === 0) {
    // Everything was discarded (no line slot AND no floor space): still show the take-off.
    for (let j = 0; j < draftedCount; j++) {
      plan.flights.push({ kind: 'tile', color: draftedColor, from: fromKey, to: `seat:${actor}:score`, delay: 60 + j * 40 })
    }
  }
  // Factory leftovers slide into the center after the draft takes off.
  if (fromKey !== 'center' && leftovers.length > 0) {
    leftovers.forEach((t, i) => {
      plan.flights.push({ kind: 'tile', color: t as AzulColor, from: fromKey, to: 'center', delay: 200 + i * 50 })
    })
  }
  return plan
}

/** Plays a plan: clones fly between anchors, score floats rise over the pills. */
export function runAzulPlan(plan: AzulEffectPlan) {
  for (const f of plan.flights) {
    const clone = f.kind === 'tile' ? createAzulTileClone(f.color) : createAzulMarkerClone()
    flyBetween(clone, f.from, f.to, { toFallback: f.to, delay: f.delay, duration: 420, arc: 36, endScale: 0.6 })
  }
  for (const p of plan.popups) {
    const rect = anchorRect(p.key)
    if (!rect) continue
    const el = document.createElement('span')
    el.textContent = p.delta > 0 ? `+${p.delta}` : `${p.delta}`
    el.style.position = 'fixed'
    el.style.left = `${rect.left + rect.width / 2 - 16}px`
    el.style.top = `${rect.top - 6}px`
    el.style.zIndex = '61'
    el.style.pointerEvents = 'none'
    el.style.fontWeight = '900'
    el.style.fontSize = '15px'
    el.style.fontVariantNumeric = 'tabular-nums'
    el.style.color = p.delta > 0 ? '#fbbf24' : '#fb7185'
    el.style.textShadow = '0 1px 3px rgba(0,0,0,0.8)'
    document.body.appendChild(el)
    const anim = el.animate(
      [
        { transform: 'translateY(0)', opacity: 0 },
        { transform: 'translateY(-10px)', opacity: 1, offset: 0.25 },
        { transform: 'translateY(-30px)', opacity: 0 },
      ],
      { duration: 1100, delay: 500, easing: 'ease-out', fill: 'both' },
    )
    anim.onfinish = () => el.remove()
    anim.oncancel = () => el.remove()
  }
}
