import { useEffect, useState } from 'react'

function remainingMs(deadlineUtc?: string): number {
  if (!deadlineUtc) return 0
  const deadline = Date.parse(deadlineUtc)
  if (Number.isNaN(deadline)) return 0
  return deadline - Date.now()
}

/**
 * Ticks down to a UTC ISO deadline. Returns remaining milliseconds,
 * clamped to 0 (whether the deadline passed, expired, or was absent).
 */
export function useCountdown(deadlineUtc?: string, tickMs = 250): number {
  const [remaining, setRemaining] = useState(() => Math.max(0, remainingMs(deadlineUtc)))

  useEffect(() => {
    setRemaining(Math.max(0, remainingMs(deadlineUtc)))
    if (!deadlineUtc) return

    const timer = window.setInterval(() => {
      setRemaining(Math.max(0, remainingMs(deadlineUtc)))
    }, tickMs)

    return () => window.clearInterval(timer)
  }, [deadlineUtc, tickMs])

  return remaining
}

/**
 * Ticks the current wall-clock time. Combine with your own deadlines when the
 * countdown must be allowed to go negative (e.g. overtime grace windows).
 */
export function useNow(tickMs = 250): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    setNow(Date.now())
    const timer = window.setInterval(() => setNow(Date.now()), tickMs)
    return () => window.clearInterval(timer)
  }, [tickMs])

  return now
}