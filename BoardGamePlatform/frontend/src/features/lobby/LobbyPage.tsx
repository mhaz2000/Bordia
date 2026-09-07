import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { gameApi } from '@/shared/api/client'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Select } from '@/shared/components/Select'
import { Modal } from '@/shared/components/Modal'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { PlusIcon, LockClosedIcon, UserGroupIcon, PlayIcon } from '@heroicons/react/24/outline'
import type { LobbyRoom } from '@/shared/api/lobby'
import type { GameSession } from '@/shared/api/game'

export function LobbyPage() {
  const navigate = useNavigate()
  const { 
    rooms, 
    isLoading, 
    error,
    createRoom,
    joinRoom,
    refetchRooms,
    isCreating,
    setupSignalR,
  } = useLobby()
  const { isAuthenticated, user, initializeAuth } = useAuth()
  const { data: games = [] } = useQuery({
    queryKey: ['games'],
    queryFn: gameApi.listGames,
    staleTime: 1000 * 60 * 5,
  })
  // Active game sessions of the current user (for rejoining after a closed tab).
  const { data: mySessions = [] } = useQuery({
    queryKey: ['game', 'mySessions'],
    queryFn: gameApi.mySessions,
    refetchInterval: 15000,
    staleTime: 5000,
  })
  const activeGames = mySessions.filter((s) => s.status === 'Active')
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [name, setName] = useState('')
  const [gameType, setGameType] = useState('')
  const [maxPlayers, setMaxPlayers] = useState(2)
  const [isPrivate, setIsPrivate] = useState(false)
  const [createError, setCreateError] = useState('')
  const [joinError, setJoinError] = useState('')

  useEffect(() => {
    initializeAuth()
    const cleanup = setupSignalR()
    return cleanup
  }, [initializeAuth, setupSignalR])

  const selectedGame = games.find((game) => game.gameType === gameType)

  const openCreateModal = () => {
    const initial = games[0]
    setGameType(initial?.gameType ?? '')
    setMaxPlayers(initial?.minPlayers ?? 2)
    setName('')
    setCreateError('')
    setShowCreateModal(true)
  }

  const handleGameTypeChange = (nextGameType: string) => {
    setGameType(nextGameType)
    const next = games.find((game) => game.gameType === nextGameType)
    if (next) {
      setMaxPlayers(Math.min(next.maxPlayers, Math.max(maxPlayers, next.minPlayers)))
    }
  }

  const handleCreateRoom = async (e: React.FormEvent) => {
    e.preventDefault()
    setCreateError('')
    if (!name.trim()) {
      setCreateError('Please enter a room name.')
      return
    }
    if (!selectedGame) {
      setCreateError('No games available. Please try again.')
      return
    }
    try {
      const room = await createRoom({ name, gameType, maxPlayers, isPrivate })
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

  if (!isAuthenticated && !isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <Card className="w-full max-w-md text-center" padding="lg">
          <CardTitle>Please sign in to access the lobby</CardTitle>
          <p className="text-gray-600 mt-4">You need to be logged in to create or join rooms.</p>
          <div className="mt-6 flex gap-3 justify-center">
            <Button variant="primary" asChild>
              <Link to="/login">Sign In</Link>
            </Button>
            <Button variant="secondary" asChild>
              <Link to="/register">Register</Link>
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
            <div className="flex items-center gap-4">
              <Button variant="primary" onClick={openCreateModal} isLoading={isCreating}>
                <PlusIcon className="w-5 h-5 mr-2" />
                Create Room
              </Button>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg" role="alert">
            {error}
            <Button variant="ghost" size="sm" className="ml-4" onClick={() => refetchRooms()}>
              Retry
            </Button>
          </div>
        )}

        {joinError && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg" role="alert">
            {joinError}
          </div>
        )}

        {/* Rejoinable active games (e.g. after accidentally closing the game tab) */}
        {activeGames.length > 0 && (
          <div className="mb-8 space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">
              Your active games
            </h2>
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

        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {rooms.map((room) => (
            <RoomCard key={room.id} room={room} currentUserId={user?.id} onSelect={handleJoinRoom} />
          ))}

          {rooms.length === 0 && !isLoading && (
            <div className="col-span-full text-center py-12">
              <LockClosedIcon className="mx-auto h-12 w-12 text-gray-400" />
              <h3 className="mt-2 text-lg font-medium text-gray-900">No rooms available</h3>
              <p className="mt-1 text-gray-500">Create a new room to start playing!</p>
              <Button variant="primary" className="mt-4" onClick={openCreateModal}>
                <PlusIcon className="w-5 h-5 mr-2" />
                Create Room
              </Button>
            </div>
          )}
        </div>
      </main>

      <Modal isOpen={showCreateModal} onClose={() => setShowCreateModal(false)} title="Create New Room">
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
            placeholder="My Awesome Game"
            maxLength={50}
          />
          <div className="grid grid-cols-2 gap-4">
            <Select
              label="Game Type"
              value={gameType}
              onChange={(e) => handleGameTypeChange(e.target.value)}
            >
              {games.length === 0 && <option value="">Loading games...</option>}
              {games.map((game) => (
                <option key={game.gameType} value={game.gameType}>
                  {game.gameType}
                </option>
              ))}
            </Select>
            <Select
              label="Max Players"
              value={maxPlayers}
              onChange={(e) => setMaxPlayers(Number(e.target.value))}
              disabled={!selectedGame}
            >
              {selectedGame &&
                Array.from(
                  { length: selectedGame.maxPlayers - selectedGame.minPlayers + 1 },
                  (_, index) => selectedGame.minPlayers + index
                ).map((n) => (
                  <option key={n} value={n}>
                    {n} players
                  </option>
                ))}
            </Select>
          </div>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isPrivate"
              checked={isPrivate}
              onChange={(e) => setIsPrivate(e.target.checked)}
              className="h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
            />
            <label htmlFor="isPrivate" className="text-sm text-gray-700">
              Private room (invite only)
            </label>
          </div>
          <div className="flex gap-3 pt-2">
            <Button variant="secondary" type="button" onClick={() => setShowCreateModal(false)} className="flex-1">
              Cancel
            </Button>
            <Button type="submit" isLoading={isCreating} disabled={!selectedGame} className="flex-1">
              Create Room
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}

function ActiveGameCard({
  session,
  currentUserId,
  onRejoin,
}: {
  session: GameSession
  currentUserId?: string
  onRejoin: () => void
}) {
  const opponent = session.players.find((p) => p.userId !== currentUserId)

  return (
    <div className="flex items-center justify-between rounded-xl border-2 border-blue-200 bg-blue-50 px-5 py-4 shadow-sm">
      <div className="flex items-center gap-4">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-lg font-black text-white shadow">
          {session.gameType.charAt(0)}
        </div>
        <div>
          <p className="font-semibold text-gray-900">
            {session.gameType} game in progress
          </p>
          <p className="text-sm text-gray-500">
            vs {opponent ? opponent.displayName : 'other players'} - started{' '}
            {session.startedAt ? new Date(session.startedAt).toLocaleTimeString() : 'recently'}
          </p>
        </div>
      </div>
      <Button variant="primary" onClick={onRejoin}>
        Rejoin game
      </Button>
    </div>
  )
}

function RoomCard({ room, currentUserId, onSelect }: { room: LobbyRoom; currentUserId?: string; onSelect: (room: LobbyRoom) => void }) {
  const isMember = room.players.some((p) => p.userId === currentUserId)
  const canJoin = room.status === 'Waiting' && (isMember || room.players.length < room.maxPlayers)

  return (
    <Card variant="outlined" className="hover:shadow-md transition-shadow">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="text-base">{room.name}</CardTitle>
            <p className="text-sm text-gray-500 mt-1">
              <span className="font-mono font-semibold text-gray-700 mr-2">{room.roomCode}</span>
              {room.gameType}
            </p>
          </div>
          {room.isPrivate && <LockClosedIcon className="w-5 h-5 text-gray-400 mt-1" />}
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-600">
            <UserGroupIcon className="w-4 h-4 inline mr-1" />
            {room.players.length}/{room.maxPlayers}
          </span>
          <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
            room.status === 'Waiting' ? 'bg-green-100 text-green-800' :
            room.status === 'Started' ? 'bg-blue-100 text-blue-800' :
            'bg-gray-100 text-gray-800'
          }`}>
            {room.status}
          </span>
        </div>
        <div className="text-xs text-gray-500">
          Host: {room.hostDisplayName}
        </div>
        <div className="flex gap-1">
          {room.players.slice(0, 4).map((player) => (
            <div
              key={player.id}
              className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-medium ${
                player.isReady ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'
              }`}
              title={player.displayName}
            >
              {player.displayName.charAt(0).toUpperCase()}
            </div>
          ))}
          {room.players.length > 4 && (
            <div className="w-8 h-8 rounded-full bg-gray-200 flex items-center justify-center text-xs text-gray-600">
              +{room.players.length - 4}
            </div>
          )}
        </div>
        <Button 
          className="w-full mt-2" 
          onClick={() => onSelect(room)} 
          disabled={!canJoin || room.status !== 'Waiting'}
          variant={room.status === 'Waiting' ? 'primary' : 'secondary'}
        >
          {room.status === 'Waiting' && !isMember && (
            <PlayIcon className="w-4 h-4 mr-1" />
          )}
          {isMember ? 'Open' : room.status === 'Waiting' ? 'Join Room' : room.status === 'Started' ? 'Game Started' : 'Room Closed'}
        </Button>
      </CardContent>
    </Card>
  )
}