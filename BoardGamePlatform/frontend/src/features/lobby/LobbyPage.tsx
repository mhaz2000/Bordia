import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { gameApi, lobbyApi } from '@/shared/api/client'
import { Button } from '@/shared/components/Button'
import { Card, CardContent } from '@/shared/components/Card'
import { UserGroupIcon, ArrowRightIcon } from '@heroicons/react/24/outline'
import { getGameInfo } from './gameMeta'
import { ActiveGameCard } from './ActiveGameCard'
import type { GameMeta } from '@/shared/api/game'

export function LobbyPage() {
  const navigate = useNavigate()
  const { setupSignalR } = useLobby()
  const { isAuthenticated, user, initializeAuth } = useAuth()

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
  })

  const { data: mySessions = [] } = useQuery({
    queryKey: ['game', 'mySessions'],
    queryFn: gameApi.mySessions,
    refetchInterval: 15000,
    staleTime: 5000,
  })
  const activeGames = mySessions.filter((s) => s.status === 'Active')

  if (!isAuthenticated && !gamesLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <Card className="w-full max-w-md text-center" padding="lg">
          <h2 className="text-lg font-semibold text-gray-900">Please sign in to access the lobby</h2>
          <p className="text-gray-600 mt-4">You need to be logged in to create or join rooms.</p>
          <div className="mt-6 flex gap-3 justify-center">
            <Button variant="primary" onClick={() => navigate('/login')}>
              Sign In
            </Button>
            <Button variant="secondary" onClick={() => navigate('/register')}>
              Register
            </Button>
          </div>
        </Card>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <h1 className="text-xl font-bold text-gray-900">Board Game Lobby</h1>
            {user && (
              <span className="text-sm text-gray-500">
                Welcome, <strong className="text-gray-700">{user.displayName}</strong>
              </span>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
        {activeGames.length > 0 && (
          <div className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">Your active games</h2>
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

        <div className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">Choose a game</h2>
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
      </main>
    </div>
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
  const info = getGameInfo(meta.gameType)

  return (
    <Card
      className={`overflow-hidden transition-shadow ${info.comingSoon ? 'opacity-80' : 'hover:shadow-lg cursor-pointer'}`}
    >
      <button
        type="button"
        onClick={onSelect}
        disabled={info.comingSoon}
        className="block w-full text-left"
      >
        <div className={`h-28 bg-gradient-to-br ${info.gradient} flex items-center justify-center relative`}>
          <span className="text-4xl font-black tracking-widest text-white drop-shadow-lg -rotate-6">
            {info.title.toUpperCase()}
          </span>
          {info.comingSoon && (
            <span className="absolute top-3 right-3 rounded-full bg-black/50 px-3 py-1 text-xs font-bold text-white backdrop-blur-sm">
              Coming soon
            </span>
          )}
        </div>
        <CardContent className="space-y-3">
          <div>
            <h3 className="font-semibold text-gray-900">{info.title}</h3>
            <p className="text-sm text-gray-500">{info.tagline}</p>
          </div>
          <div className="flex items-center justify-between text-xs text-gray-500">
            <span>
              <UserGroupIcon className="w-4 h-4 inline mr-1" />
              {meta.minPlayers}-{meta.maxPlayers} players
            </span>
            {!info.comingSoon && (
              <span>
                {openRooms} open room{openRooms === 1 ? '' : 's'}
              </span>
            )}
          </div>
          <div className="flex items-center gap-1 text-sm font-semibold text-blue-600">
            {info.comingSoon ? 'Stay tuned' : 'Rooms & rules'}
            {!info.comingSoon && <ArrowRightIcon className="w-4 h-4" />}
          </div>
        </CardContent>
      </button>
    </Card>
  )
}
