import { useEffect } from 'react'
import { useAuthStore } from '@/shared/state/authStore'
import { refreshSession } from '@/shared/api/client'

/** Decode a JWT's expiry; null when the token is unparseable. */
function tokenExpiryMs(jwt: string | null): number | null {
  if (!jwt) return null
  try {
    const payload = jwt.split('.')[1]
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number }
    return typeof json.exp === 'number' ? json.exp * 1000 : null
  } catch {
    return null
  }
}

/**
 * Keeps the session alive without user action: refreshes shortly before the
 * access token expires (server rotates one-time-use refresh tokens) and
 * re-checks whenever the tab becomes visible after a long idle. Only mounted
 * for authenticated areas.
 */
export function useSessionMaintenance() {
  const accessToken = useAuthStore((s) => s.accessToken)

  useEffect(() => {
    if (!accessToken) return

    let cancelled = false
    let timer = 0

    const refreshNow = () => {
      void refreshSession().then((ok) => {
        if (cancelled) return
        if (!ok) useAuthStore.getState().logout()
      })
    }

    const schedule = (token: string | null) => {
      const exp = tokenExpiryMs(token)
      if (!exp) return
      const lead = 60_000
      const delay = exp - Date.now() - lead
      if (delay <= 0) {
        refreshNow()
        return
      }
      timer = window.setTimeout(refreshNow, delay)
    }

    const onVisible = () => {
      if (document.visibilityState !== 'visible') return
      const window5m = 5 * 60_000
      const exp = tokenExpiryMs(useAuthStore.getState().accessToken)
      if (exp && Date.now() > exp - window5m) refreshNow()
    }

    document.addEventListener('visibilitychange', onVisible)
    schedule(accessToken)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [accessToken])
}
