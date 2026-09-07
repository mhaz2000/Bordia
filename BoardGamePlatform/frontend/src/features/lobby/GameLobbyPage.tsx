import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  PlayIcon,
  TrophyIcon,
  BoltIcon,
  ShieldCheckIcon,
  ArrowPathIcon,
  ClockIcon,
  FlagIcon,
  UserGroupIcon,
  PlusIcon,
  LockClosedIcon,
  ArrowLeftIcon,
} from '@heroicons/react/24/outline'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { gameApi, lobbyApi } from '@/shared/api/client'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Select } from '@/shared/components/Select'
import { Modal } from '@/shared/components/Modal'
import { Card, CardContent } from '@/shared/components/Card'
import { UnoCardVisual, UnoCardBackVisual } from '@/shared/components/UnoCardVisual'
import { getGameInfo, type RuleIconName } from './gameMeta'
import { RoomCard } from './RoomCard'
import { ActiveGameCard } from './ActiveGameCard'
import type { LobbyRoom } from '@/shared/api/lobby'

const RULE_ICONS: Record<RuleIconName, typeof PlayIcon> = {
  cards: PlayIcon,
  trophy: TrophyIcon,
  bolt: BoltIcon,
  shield: ShieldCheckIcon,
  refresh: ArrowPathIcon,
  clock: ClockIcon,
  flag: FlagIcon,
}

const ACTION_CARDS: { color: number; value: number; name: string; blurb: string }[] = [
  { color: 1, value: 10, name: 'Skip', blurb: 'The next player loses their turn' },
  { color: 3, value: 11, name: 'Reverse', blurb: 'Flips direction - acts as a Skip in 2p' },
  { color: 0, value: 12, name: 'Draw Two', blurb: 'Next player draws 2 and forfeits' },
  { color: 4, value: 13, name: 'Wild', blurb: 'Play anytime - choose the color' },
  { color: 4, value: 14, name: 'Wild Draw Four', blurb: 'Draw 4 + color - can be challenged' },
]

export function GameLobbyPage() {
  const { gameType } = useParams<{ gameType: string }>()
  const gameTypeParam = gameType ?? ''
  const info = getGameInfo(gameTypeParam)
  const isUno = gameTypeParam === 'UNO'
  const navigate = useNavigate()
  const { createRoom, joinRoom, isCreating } = useLobby()
  const { user } = useAuth()

  const { data: games = [] } = useQuery({
    queryKey: ['games'],
    queryFn: gameApi.listGames,
    staleTime: 1000 * 60 * 5,
  })
  const catalogGame = games.find((g) => g.gameType === gameTypeParam)

  const { data: rooms = [], isLoading: roomsLoading } = useQuery({
    queryKey: ['lobby', 'rooms'],
    queryFn: lobbyApi.listRooms,
    refetchInterval: 5000,
  })
  const gameRooms = rooms.filter((r) => r.gameType === gameTypeParam)
  const seatedPlayers = gameRooms.reduce((sum, r) => sum + r.players.length, 0)

  const { data: mySessions = [] } = useQuery({
    queryKey: ['game', 'mySessions'],
    queryFn: gameApi.mySessions,
    refetchInterval: 15000,
    staleTime: 5000,
  })
  const activeGames = mySessions.filter((s) => s.status === 'Active' && s.gameType === gameTypeParam)

  const [showCreateModal, setShowCreateModal] = useState(false)
  const [name, setName] = useState('')
  const [maxPlayers, setMaxPlayers] = useState(2)
  const [isPrivate, setIsPrivate] = useState(false)
  const [createError, setCreateError] = useState('')
  const [joinError, setJoinError] = useState('')

  const openCreateModal = () => {
    setName('')
    setMaxPlayers(catalogGame?.minPlayers ?? 2)
    setIsPrivate(false)
    setCreateError('')
    setShowCreateModal(true)
  }

  const handleCreateRoom = async (e: React.FormEvent) => {
    e.preventDefault()
    setCreateError('')
    if (!name.trim()) {
      setCreateError('Please enter a room name.')
      return
    }
    if (!catalogGame) {
      setCreateError('Game catalog is still loading. Please try again.')
      return
    }
    try {
      const room = await createRoom({ name, gameType: gameTypeParam, maxPlayers, isPrivate })
      setShowCreateModal(false)
      setName('')
      navigate(`/lobby/${room.id}`)
    } catch (err) {
      setCreateError(err instanceof Error ? err.message : 'Failed to create room')
    }
  }

  const handleJoinRoom = async (room: LobbyRoom) => {
    setJoinError('')
    const isMember = room.players.some((p) => p.userId === user?.id)
    try {
      if (!isMember) {
        await joinRoom(room.id)
      }
      navigate(`/lobby/${room.id}`)
    } catch (err) {
      setJoinError(err instanceof Error ? err.message : 'Failed to join room')
    }
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3">
              <Button variant="ghost" asChild>
                <Link to="/lobby">
                  <ArrowLeftIcon className="w-5 h-5" />
                </Link>
              </Button>
              <h1 className="text-xl font-bold text-gray-900">{info.title}</h1>
            </div>
            {!info.comingSoon && (
              <Button variant="primary" onClick={openCreateModal} isLoading={isCreating}>
                <PlusIcon className="w-5 h-5 mr-2" />
                Create {info.title} Room
              </Button>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-10">
        {/* ============================ HERO ============================ */}
        <section className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-emerald-900 via-emerald-950 to-black shadow-2xl">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(16,185,129,0.35),transparent_55%)]" />
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(239,68,68,0.18),transparent_50%)]" />

          {/* Decorative cards */}
          {isUno && (
            <>
              <div className="pointer-events-none absolute -left-6 top-8 hidden sm:block">
                <div className="relative">
                  <UnoCardBackVisual className="absolute -left-10 top-2 rotate-[-24deg] opacity-70" />
                  <div className="animate-floaty" style={{ '--rot': '-14deg' } as React.CSSProperties}>
                    <UnoCardVisual card={{ Color: 0, Value: 7 }} size="lg" className="rotate-[-14deg]" />
                  </div>
                </div>
              </div>
              <div className="pointer-events-none absolute right-6 top-6 hidden md:flex gap-3">
                <div className="animate-floaty-delayed" style={{ '--rot': '10deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 4, Value: 13 }} size="lg" className="rotate-[10deg]" />
                </div>
                <div className="animate-floaty mt-8" style={{ '--rot': '18deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 3, Value: 12 }} size="lg" className="rotate-[18deg]" />
                </div>
                <div className="animate-floaty-delayed mt-16" style={{ '--rot': '26deg' } as React.CSSProperties}>
                  <UnoCardVisual card={{ Color: 1, Value: 5 }} size="lg" className="rotate-[26deg]" />
                </div>
              </div>
            </>
          )}
          {!isUno && (
            <div className="pointer-events-none absolute right-8 top-8 hidden md:block opacity-20">
              <span className="text-[10rem] font-black text-white leading-none">{info.title.charAt(0)}</span>
            </div>
          )}

          <div className="relative px-6 py-12 sm:px-12 sm:py-16 max-w-2xl">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full bg-white/10 px-3 py-1 text-[11px] font-bold uppercase tracking-widest text-emerald-200 backdrop-blur-sm">
                Card game
              </span>
              {catalogGame && (
                <span className="inline-flex items-center gap-1 rounded-full bg-white/10 px-3 py-1 text-[11px] font-bold uppercase tracking-widest text-white/80 backdrop-blur-sm">
                  <UserGroupIcon className="w-3.5 h-3.5" />
                  {catalogGame.minPlayers}-{catalogGame.maxPlayers} players
                </span>
              )}
            </div>
            <h2 className="mt-4 text-5xl sm:text-6xl font-black italic tracking-tighter text-white drop-shadow-[0_4px_12px_rgba(0,0,0,0.5)]">
              {info.title.toUpperCase()}
            </h2>
            <p className="mt-2 text-lg font-semibold text-emerald-200">{info.tagline}</p>
            <p className="mt-3 text-sm sm:text-base leading-relaxed text-white/75 max-w-xl">{info.description}</p>
            {!info.comingSoon && (
              <div className="mt-6 flex flex-wrap gap-3">
                <Button onClick={openCreateModal} isLoading={isCreating} className="!bg-white !text-emerald-900 hover:!bg-emerald-50 border-none shadow-lg">
                  <PlusIcon className="w-5 h-5 mr-1.5" />
                  Create a table
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => document.getElementById('open-rooms')?.scrollIntoView({ behavior: 'smooth' })}
                  className="!text-white border !border-white/30 hover:!bg-white/10"
                >
                  Browse open rooms
                </Button>
              </div>
            )}
          </div>
        </section>

        {/* ============================ STATS ============================ */}
        {!info.comingSoon && (
          <section className="grid grid-cols-3 gap-4">
            <StatTile label="Open rooms" value={gameRooms.length} />
            <StatTile label="Players seated" value={seatedPlayers} />
            <StatTile label="Your live games" value={activeGames.length} />
          </section>
        )}

        {joinError && (
          <div className="p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg" role="alert">
            {joinError}
          </div>
        )}

        {/* ============================ RULES ============================ */}
        <section className="space-y-4">
          <SectionTitle title="How to play" />
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {info.rules.map((rule, i) => {
              const Icon = RULE_ICONS[rule.icon] ?? PlayIcon
              return (
                <Card key={i} className="hover:shadow-md transition-shadow">
                  <CardContent className="flex gap-4">
                    <div className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white shadow">
                      <Icon className="h-5 w-5" />
                    </div>
                    <div>
                      <h3 className="font-semibold text-gray-900 text-sm">{rule.title}</h3>
                      <p className="mt-1 text-sm text-gray-500 leading-relaxed">{rule.detail}</p>
                    </div>
                  </CardContent>
                </Card>
              )
            })}
          </div>
        </section>

        {/* ============================ ACTION CARDS (UNO) ============================ */}
        {isUno && (
          <section className="space-y-4">
            <SectionTitle title="Meet the action cards" />
            <div className="rounded-3xl bg-gradient-to-br from-slate-800 via-slate-900 to-black p-6 sm:p-8 shadow-inner">
              <div className="grid grid-cols-2 gap-6 sm:grid-cols-3 lg:grid-cols-5">
                {ACTION_CARDS.map((c) => (
                  <div key={c.value} className="flex flex-col items-center gap-3 text-center">
                    <div className="transition-transform duration-200 hover:-translate-y-2 hover:scale-105">
                      <UnoCardVisual card={{ Color: c.color, Value: c.value }} size="lg" />
                    </div>
                    <div>
                      <p className="text-sm font-bold text-white">{c.name}</p>
                      <p className="mt-1 text-xs text-white/50 leading-snug">{c.blurb}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </section>
        )}

        {info.comingSoon ? (
          <Card>
            <CardContent className="py-12 text-center">
              <LockClosedIcon className="mx-auto h-10 w-10 text-gray-400" />
              <h3 className="mt-2 text-lg font-medium text-gray-900">{info.title} is coming soon</h3>
              <p className="mt-1 text-gray-500">Rooms will open once the game is released.</p>
              <Button variant="primary" className="mt-4" asChild>
                <Link to="/lobby">Back to game catalog</Link>
              </Button>
            </CardContent>
          </Card>
        ) : (
          <>
            {/* Active games of this type */}
            {activeGames.length > 0 && (
              <section className="space-y-3">
                <SectionTitle title={`Your live ${info.title} games`} pulse />
                {activeGames.map((session) => (
                  <ActiveGameCard
                    key={session.id}
                    session={session}
                    currentUserId={user?.id}
                    onRejoin={() => navigate(`/game/${session.id}`)}
                  />
                ))}
              </section>
            )}

            {/* Open rooms */}
            <section id="open-rooms" className="space-y-4 scroll-mt-24">
              <div className="flex items-center justify-between">
                <SectionTitle title="Open tables" pulse />
                <Button variant="secondary" size="sm" onClick={openCreateModal} isLoading={isCreating}>
                  <PlusIcon className="w-4 h-4 mr-1" />
                  New room
                </Button>
              </div>

              {roomsLoading && gameRooms.length === 0 ? (
                <div className="flex justify-center py-12">
                  <div className="animate-spin rounded-full h-10 w-10 border-4 border-blue-600 border-t-transparent" />
                </div>
              ) : gameRooms.length === 0 ? (
                <Card>
                  <CardContent className="py-14 text-center">
                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-50">
                      <UserGroupIcon className="h-7 w-7 text-blue-500" />
                    </div>
                    <h3 className="mt-4 text-lg font-semibold text-gray-900">No open {info.title} tables yet</h3>
                    <p className="mt-1 text-gray-500">Be the host - create a room and deal the first hand!</p>
                    <Button variant="primary" className="mt-5" onClick={openCreateModal} isLoading={isCreating}>
                      <PlusIcon className="w-5 h-5 mr-2" />
                      Create {info.title} Room
                    </Button>
                  </CardContent>
                </Card>
              ) : (
                <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                  {gameRooms.map((room) => (
                    <RoomCard key={room.id} room={room} currentUserId={user?.id} onSelect={handleJoinRoom} />
                  ))}
                </div>
              )}
            </section>
          </>
        )}
      </main>

      {/* Create room modal (game type locked to this page) */}
      <Modal isOpen={showCreateModal} onClose={() => setShowCreateModal(false)} title={`Create ${info.title} Room`}>
        <form onSubmit={handleCreateRoom} noValidate className="space-y-4">
          {createError && (
            <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm" role="alert">
              {createError}
            </div>
          )}
          <Input
            label="Room Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder={`My ${info.title} table`}
            maxLength={50}
          />
          <Select
            label="Max Players"
            value={maxPlayers}
            onChange={(e) => setMaxPlayers(Number(e.target.value))}
            disabled={!catalogGame}
          >
            {catalogGame &&
              Array.from(
                { length: catalogGame.maxPlayers - catalogGame.minPlayers + 1 },
                (_, index) => catalogGame.minPlayers + index
              ).map((n) => (
                <option key={n} value={n}>
                  {n} players
                </option>
              ))}
          </Select>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isPrivateGame"
              checked={isPrivate}
              onChange={(e) => setIsPrivate(e.target.checked)}
              className="h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
            />
            <label htmlFor="isPrivateGame" className="text-sm text-gray-700">
              Private room (invite only)
            </label>
          </div>
          <div className="flex gap-3 pt-2">
            <Button variant="secondary" type="button" onClick={() => setShowCreateModal(false)} className="flex-1">
              Cancel
            </Button>
            <Button type="submit" isLoading={isCreating} disabled={!catalogGame} className="flex-1">
              Create Room
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}

function StatTile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl bg-white border border-gray-200 px-5 py-4 shadow-sm">
      <p className="text-3xl font-black text-gray-900 tabular-nums">{value}</p>
      <p className="mt-0.5 text-xs font-semibold uppercase tracking-wide text-gray-400">{label}</p>
    </div>
  )
}

function SectionTitle({ title, pulse }: { title: string; pulse?: boolean }) {
  return (
    <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
      {pulse && <span className="relative flex h-2 w-2">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
        <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
      </span>}
      {title}
    </h2>
  )
}
