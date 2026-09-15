import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { useI18n } from '@/i18n/I18nProvider'
import { gameApi, lobbyApi } from '@/shared/api/client'
import { Button } from '@/shared/components/Button'
import { Card, CardContent } from '@/shared/components/Card'
import { BrandLogo, BrandMark } from '@/shared/components/BrandLogo'
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher'
import { UserGroupIcon, ArrowRightIcon } from '@heroicons/react/24/outline'
import { useGameInfo } from './gameMeta'
import { ActiveGameCard } from './ActiveGameCard'
import type { GameMeta } from '@/shared/api/game'

export function LobbyPage() {
  const navigate = useNavigate()
  const { setupSignalR } = useLobby()
  const { isAuthenticated, user, initializeAuth } = useAuth()
  const { t } = useI18n()

  useEffect(() => {
    initializeAuth()
    const cleanup = setupSignalR()
    return cleanup
  }, [initializeAuth, setupSignalR])

  const { data: games = [], isLoading: gamesLoading } = useQuery({
    queryKey: ['games'],
    queryFn: gameApi.listGames,
    staleTime: 1000 * 60 * 5,
  })

  const { data: rooms = [] } = useQuery({
    queryKey: ['lobby', 'rooms'],
    queryFn: lobbyApi.listRooms,
    refetchInterval: 10000,
    refetchIntervalInBackground: false,
  })

  const { data: mySessions = [] } = useQuery({
    queryKey: ['game', 'mySessions'],
    queryFn: gameApi.mySessions,
    refetchInterval: 15000,
    refetchIntervalInBackground: false,
    staleTime: 5000,
  })
  const activeGames = mySessions.filter((s) => s.status === 'Active')
  const openRooms = rooms.length

  if (!isAuthenticated && !gamesLoading) {
    return (
      <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-950 px-4">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(16,185,129,0.16),transparent_55%)]" />
        <Card className="relative w-full max-w-md border-0 bg-white/95 p-6 text-center shadow-2xl" padding="lg">
          <div className="mb-4 flex justify-center">
            <BrandMark className="h-14 w-14" />
          </div>
          <div className="mb-4 flex justify-center">
            <LanguageSwitcher />
          </div>
          <h2 className="text-lg font-semibold text-gray-900">{t('lobby.gateTitle')}</h2>
          <p className="mt-4 text-gray-600">{t('lobby.gateDetail')}</p>
          <div className="mt-6 flex justify-center gap-3">
            <Button variant="primary" onClick={() => navigate('/login')}>
              {t('common.signIn')}
            </Button>
            <Button variant="secondary" onClick={() => navigate('/register')}>
              {t('common.register')}
            </Button>
          </div>
        </Card>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* ======================= GLASS HEADER ======================= */}
      <header className="sticky top-0 z-20 border-b border-white/10 bg-slate-950/85 shadow-lg shadow-slate-950/20 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          <BrandLogo dark />
          <div className="flex items-center gap-3">
            {user && (
              <span className="hidden items-center gap-2 rounded-full bg-white/10 py-1 pe-3 ps-1 ring-1 ring-white/15 sm:flex">
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-emerald-400 to-teal-600 text-xs font-black text-white">
                  {user.displayName.charAt(0).toUpperCase()}
                </span>
                <span className="max-w-32 truncate text-sm font-medium text-white/90">{user.displayName}</span>
              </span>
            )}
            <LanguageSwitcher dark />
          </div>
        </div>
      </header>

      {/* ======================= HERO ======================= */}
      <section className="relative overflow-hidden bg-gradient-to-b from-slate-950 via-slate-900 to-slate-800 px-4 pb-20 pt-12 text-white sm:px-6 lg:px-8">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(16,185,129,0.18),transparent_55%)]" />
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(56,189,248,0.12),transparent_50%)]" />
        <span className="pointer-events-none absolute end-[8%] top-8 hidden h-16 w-16 rotate-[14deg] rounded-xl border border-white/10 lg:block" />
        <span className="pointer-events-none absolute start-[6%] bottom-10 hidden h-12 w-12 -rotate-12 rounded-xl border border-emerald-300/15 lg:block" />

        <div className="relative mx-auto max-w-7xl">
          <h2 className="text-3xl font-black tracking-tight sm:text-4xl">
            {t('lobby.welcome')}{' '}
            <span className="bg-gradient-to-r from-emerald-300 to-cyan-300 bg-clip-text text-transparent">
              {user?.displayName}
            </span>
          </h2>
          <p className="mt-2 max-w-xl text-sm leading-relaxed text-slate-300 sm:text-base">{t('site.heroSubtitle')}</p>

          <div className="mt-7 grid max-w-lg grid-cols-3 gap-3">
            {[
              { n: games.length, label: t('site.statGames') },
              { n: openRooms, label: t('site.statOpenRooms') },
              { n: activeGames.length, label: t('site.statYourGames') },
            ].map((s) => (
              <div key={s.label} className="rounded-2xl border border-white/10 bg-white/5 px-3 py-3 text-center backdrop-blur-sm">
                <p className="text-2xl font-black tabular-nums text-emerald-300">{s.n}</p>
                <p className="mt-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-400 sm:text-xs">{s.label}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* fade the dark hero into the light content area */}
      <div className="relative z-10 mx-auto -mt-10 max-w-7xl rounded-t-3xl bg-gray-50 px-4 pb-10 pt-8 shadow-[0_-12px_30px_rgba(2,6,23,0.25)] sm:px-6 lg:px-8">
        {activeGames.length > 0 && (
          <div className="mb-8 space-y-3">
            <SectionTitle label={t('lobby.yourActiveGames')} />
            {activeGames.map((session) => (
              <ActiveGameCard
                key={session.id}
                session={session}
                currentUserId={user?.id}
                onRejoin={() => navigate(`/game/${session.id}`)}
              />
            ))}
          </div>
        )}

        <div className="space-y-4">
          <SectionTitle label={t('lobby.chooseGame')} />
          <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
            {games.map((meta) => (
              <GameCatalogCard
                key={meta.gameType}
                meta={meta}
                openRooms={rooms.filter((r) => r.gameType === meta.gameType).length}
                onSelect={() => navigate(`/lobby/games/${meta.gameType}`)}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

function SectionTitle({ label }: { label: string }) {
  return (
    <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
      <span className="h-4 w-1 rounded-full bg-gradient-to-b from-emerald-500 to-teal-600" />
      {label}
    </h2>
  )
}

function GameCatalogCard({
  meta,
  openRooms,
  onSelect,
}: {
  meta: GameMeta
  openRooms: number
  onSelect: () => void
}) {
  const { t, lang } = useI18n()
  const info = useGameInfo(meta.gameType)
  const title = lang === 'fa' && info.gameType === 'UNO' ? 'UNO' : info.title

  return (
    <Card
      className={`overflow-hidden transition-all duration-200 ${
        info.comingSoon ? 'opacity-80' : 'cursor-pointer shadow-sm hover:-translate-y-1 hover:shadow-xl'
      }`}
    >
      <button type="button" onClick={onSelect} disabled={info.comingSoon} className="block w-full text-start">
        <div className={`relative flex h-32 items-center justify-center overflow-hidden bg-gradient-to-br ${info.gradient}`}>
          <span className="pointer-events-none absolute -bottom-8 -end-6 h-24 w-24 rounded-full bg-white/10" />
          <span className="pointer-events-none absolute -top-10 start-4 h-20 w-20 rounded-full bg-black/10" />
          <span
            className="pointer-events-none absolute inset-y-0 w-1/3 -rotate-12 bg-white/15 blur-sm"
            style={{ animation: 'a-sheen 3.2s ease-in-out infinite', left: '-60%' }}
          />
          <span className="text-4xl font-black tracking-widest text-white drop-shadow-lg -rotate-6">
            {title.toUpperCase()}
          </span>
          {info.comingSoon && (
            <span className="absolute top-3 end-3 rounded-full bg-black/50 px-3 py-1 text-xs font-bold text-white backdrop-blur-sm">
              {t('lobby.comingSoon')}
            </span>
          )}
          {!info.comingSoon && openRooms > 0 && (
            <span className="absolute top-3 end-3 flex items-center gap-1.5 rounded-full bg-black/45 px-2.5 py-1 text-[11px] font-bold text-white backdrop-blur-sm">
              <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400" />
              {t(openRooms === 1 ? 'lobby.openRoomOne' : 'lobby.openRoomMany', { n: openRooms })}
            </span>
          )}
        </div>
        <CardContent className="space-y-3">
          <div>
            <h3 className="font-semibold text-gray-900">{title}</h3>
            <p className="text-sm text-gray-500">{info.tagline}</p>
          </div>
          <div className="flex items-center justify-between text-xs text-gray-500">
            <span>
              <UserGroupIcon className="w-4 h-4 inline me-1" />
              {t('lobby.playersRange', { min: meta.minPlayers, max: meta.maxPlayers })}
            </span>
          </div>
          <div className="flex items-center gap-1 text-sm font-semibold text-emerald-700">
            {info.comingSoon ? t('lobby.stayTuned') : t('lobby.roomsAndRules')}
            {!info.comingSoon && <ArrowRightIcon className="w-4 h-4 rtl:-scale-x-100" />}
          </div>
        </CardContent>
      </button>
    </Card>
  )
}
