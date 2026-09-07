import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { useLobbyStore } from '@/shared/state/lobbyStore'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { UserGroupIcon, TrophyIcon, ArrowRightOnRectangleIcon, PlayIcon, XMarkIcon } from '@heroicons/react/24/outline'

export function RoomPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const roomId = id!
  const { 
    currentRoom, 
    currentGameSessionId,
    getRoom,
    leaveRoom, 
    setReady, 
    kickPlayer, 
    transferHost, 
    startGame, 
    closeRoom,
    setCurrentRoom,
    connectAndJoinRoom,
    leaveRoomSignalR,
    setupSignalR,
    isLoading,
    isLeaving,
    isSettingReady,
    isStarting,
  } = useLobby()
  const { user, isAuthenticated } = useAuth()
  const [showKickModal, setShowKickModal] = useState<{ playerId: string; displayName: string } | null>(null)
  const [showTransferModal, setShowTransferModal] = useState<{ playerId: string; displayName: string } | null>(null)
  const [showCloseModal, setShowCloseModal] = useState(false)

  const roomQuery = useQuery({
    queryKey: ['lobby', 'room', roomId],
    queryFn: () => getRoom(roomId),
    enabled: !!roomId && !!isAuthenticated,
    retry: 1,
  })

  useEffect(() => {
    if (!roomQuery.data) return
    if (!currentRoom) {
      setCurrentRoom(roomQuery.data)
    }
    // Rejoin fallback: a refreshed page that missed the GameStarted broadcast
    // can still recover the session id from the room itself.
    if (
      roomQuery.data.status === 'Started' &&
      roomQuery.data.gameSessionId &&
      !useLobbyStore.getState().currentGameSessionId
    ) {
      useLobbyStore.getState().setGameSessionId(roomQuery.data.gameSessionId)
    }
  }, [roomQuery.data, currentRoom, setCurrentRoom])

  const isHost = currentRoom?.hostId === user?.id
  const currentUserPlayer = currentRoom?.players.find(p => p.userId === user?.id)
  const isReady = currentUserPlayer?.isReady || false

  useEffect(() => {
    const cleanup = setupSignalR()
    if (isAuthenticated) {
      connectAndJoinRoom(roomId)
    }
    return () => {
      cleanup()
      leaveRoomSignalR(roomId)
    }
  }, [roomId, isAuthenticated, setupSignalR, connectAndJoinRoom, leaveRoomSignalR])

  useEffect(() => {
    if (currentRoom?.status === 'Started' && currentGameSessionId) {
      navigate(`/game/${currentGameSessionId}`)
    }
  }, [currentRoom?.status, currentGameSessionId, navigate])

  // The room was closed (by the host or automatically as abandoned); leave the hub group and go back.
  useEffect(() => {
    if (currentRoom?.status !== 'Closed') return
    leaveRoomSignalR(roomId).catch(() => {})
    navigate('/lobby')
  }, [currentRoom?.status, roomId, leaveRoomSignalR, navigate])

  const handleLeave = async () => {
    await leaveRoom(roomId)
    navigate('/lobby')
  }

  const handleReady = async () => {
    await setReady({ roomId, isReady: !isReady })
  }

  const handleKick = async () => {
    if (showKickModal) {
      await kickPlayer({ roomId, playerId: showKickModal.playerId })
      setShowKickModal(null)
    }
  }

  const handleTransfer = async () => {
    if (showTransferModal) {
      await transferHost({ roomId, playerId: showTransferModal.playerId })
      setShowTransferModal(null)
    }
  }

  const handleStart = async () => {
    await startGame(roomId)
  }

  const handleClose = async () => {
    await closeRoom(roomId)
    navigate('/lobby')
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <p>Please log in to access this room.</p>
      </div>
    )
  }

  if (isLoading && !currentRoom) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!currentRoom && roomQuery.isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!currentRoom) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <Card className="w-full max-w-md text-center" padding="lg">
          <CardTitle>Room not found</CardTitle>
          <p className="text-gray-600 mt-4">This room may have been closed or doesn't exist.</p>
          <Button variant="primary" className="mt-4" asChild>
            <a href="/lobby">Back to Lobby</a>
          </Button>
        </Card>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-4">
              <Button variant="ghost" onClick={handleLeave} isLoading={isLeaving}>
                <ArrowRightOnRectangleIcon className="w-5 h-5" />
              </Button>
              <div>
                <h1 className="text-xl font-bold text-gray-900">
                  {currentRoom.name}
                </h1>
                <p className="text-sm text-gray-500">
                  <span className="font-mono font-semibold text-gray-700 mr-2">{currentRoom.roomCode}</span>
                  {currentRoom.gameType} - {currentRoom.players.length}/{currentRoom.maxPlayers} players
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {isHost && currentRoom.status === 'Waiting' && (
                <>
                  <Button variant="primary" onClick={handleStart} isLoading={isStarting} disabled={currentRoom.players.length < 2}>
                    <PlayIcon className="w-4 h-4 mr-2" />
                    Start Game
                  </Button>
                  <Button variant="secondary" onClick={() => setShowCloseModal(true)}>
                    Close Room
                  </Button>
                </>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-6">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>Players</CardTitle>
                  <span className="text-sm text-gray-500">{currentRoom.players.length}/{currentRoom.maxPlayers}</span>
                </div>
              </CardHeader>
              <CardContent>
                <ul className="divide-y divide-gray-200">
                  {currentRoom.players.map((player) => (
                    <PlayerRow
                      key={player.id}
                      player={player}
                      isHost={isHost}
                      isCurrentUser={player.userId === user?.id}
                      currentRoom={currentRoom}
                      onKick={setShowKickModal}
                      onTransfer={setShowTransferModal}
                    />
                  ))}
                </ul>
              </CardContent>
            </Card>

            {currentRoom.settings && (
              <Card>
                <CardHeader>
                  <CardTitle>Room Settings</CardTitle>
                </CardHeader>
                <CardContent>
                  <pre className="text-sm text-gray-600 bg-gray-50 p-4 rounded-lg overflow-auto">
                    {JSON.stringify(currentRoom.settings, null, 2)}
                  </pre>
                </CardContent>
              </Card>
            )}
          </div>

          <div className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Status</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center gap-3">
                  <UserGroupIcon className="w-6 h-6 text-gray-400" />
                  <div>
                    <p className="font-medium text-gray-900">{currentRoom.players.length} players joined</p>
                    <p className="text-sm text-gray-500">Max: {currentRoom.maxPlayers}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <span className={`w-3 h-3 rounded-full ${currentRoom.status === 'Waiting' ? 'bg-green-500' : currentRoom.status === 'Started' ? 'bg-blue-500' : 'bg-gray-500'}`} />
                  <div>
                    <p className="font-medium text-gray-900 capitalize">{currentRoom.status.toLowerCase()}</p>
                    {currentRoom.status === 'Started' && currentRoom.startedAt && (
                      <p className="text-sm text-gray-500">Started at {new Date(currentRoom.startedAt).toLocaleTimeString()}</p>
                    )}
                  </div>
                </div>
                {currentRoom.status === 'Started' && currentGameSessionId && (
                  <Button variant="primary" className="w-full" asChild>
                    <a href={`/game/${currentGameSessionId}`}>Go to Game</a>
                  </Button>
                )}
              </CardContent>
            </Card>

            {!isHost && (
              <Card>
                <CardHeader>
                  <CardTitle>Your Status</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="font-medium">Ready to play</span>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={isReady}
                    onClick={handleReady}
                    disabled={isSettingReady}
                    className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${isReady ? 'bg-blue-600' : 'bg-gray-200'}`}
                  >
                    <span
                      className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${isReady ? 'translate-x-6' : 'translate-x-1'}`}
                    />
                  </button>
                </div>
              </CardContent>
            </Card>
            )}

            {isHost && currentRoom.status === 'Waiting' && (
              <Card variant="outlined">
                <CardContent className="space-y-2">
                  <p className="text-sm text-gray-600 text-center">Waiting for all players to be ready...</p>
                  <div className="flex gap-2">
                    <Button variant="secondary" className="flex-1" onClick={() => setShowCloseModal(true)}>
                      Close Room
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      </main>

      <Modal isOpen={!!showKickModal} onClose={() => setShowKickModal(null)} title="Kick Player">
        <p className="text-gray-600">
          Are you sure you want to kick <strong>{showKickModal?.displayName}</strong> from the room?
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowKickModal(null)} className="flex-1">
            Cancel
          </Button>
          <Button variant="danger" onClick={handleKick} className="flex-1">
            Kick Player
          </Button>
        </div>
      </Modal>

      <Modal isOpen={!!showTransferModal} onClose={() => setShowTransferModal(null)} title="Transfer Host">
        <p className="text-gray-600">
          Transfer host to <strong>{showTransferModal?.displayName}</strong>? You will no longer be the host.
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowTransferModal(null)} className="flex-1">
            Cancel
          </Button>
          <Button variant="primary" onClick={handleTransfer} className="flex-1">
            Transfer
          </Button>
        </div>
      </Modal>

      <Modal isOpen={showCloseModal} onClose={() => setShowCloseModal(false)} title="Close Room">
        <p className="text-gray-600">
          This will close the room and remove all players. This action cannot be undone.
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowCloseModal(false)} className="flex-1">
            Cancel
          </Button>
          <Button variant="danger" onClick={handleClose} className="flex-1">
            Close Room
          </Button>
        </div>
      </Modal>
    </div>
  )
}

function PlayerRow({ 
  player, 
  isHost, 
  isCurrentUser, 
  currentRoom,
  onKick,
  onTransfer,
}: { 
  player: { id: string; userId: string; displayName: string; isReady: boolean; joinedAt: string; connectionId?: string }
  isHost: boolean
  isCurrentUser: boolean
  currentRoom: { hostId: string; players: { userId: string }[] }
  onKick: (data: { playerId: string; displayName: string }) => void
  onTransfer: (data: { playerId: string; displayName: string }) => void
}) {
  const isPlayerHost = player.userId === currentRoom?.hostId
  const isConnected = !!player.connectionId

  return (
    <li className="py-3 flex items-center gap-4">
      <div className={`w-10 h-10 rounded-full flex items-center justify-center text-sm font-medium ${player.isReady ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'}`}>
        {player.displayName.charAt(0).toUpperCase()}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="font-medium text-gray-900 truncate">{player.displayName}</p>
          {isPlayerHost && (
            <TrophyIcon className="w-4 h-4 text-yellow-500 flex-shrink-0" title="Host" />
          )}
          {isCurrentUser && (
            <span className="px-2 py-0.5 text-xs bg-blue-100 text-blue-800 rounded-full">You</span>
          )}
        </div>
        <div className="flex items-center gap-3 text-sm text-gray-500">
          <span className={`flex items-center gap-1 ${isConnected ? 'text-green-600' : 'text-gray-400'}`}>
            {isConnected ? (
              <>
                <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
                Online
              </>
            ) : 'Offline'}
          </span>
          <span>{player.isReady ? 'Ready' : 'Not ready'}</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        {isHost && !isCurrentUser && !isPlayerHost && (
          <>
            <Button variant="ghost" size="sm" onClick={() => onTransfer({ playerId: player.id, displayName: player.displayName })}>
              <TrophyIcon className="w-4 h-4" />
            </Button>
            <Button variant="ghost" size="sm" onClick={() => onKick({ playerId: player.id, displayName: player.displayName })} className="text-red-600 hover:text-red-700 hover:bg-red-50">
              <XMarkIcon className="w-4 h-4" />
            </Button>
          </>
        )}
      </div>
    </li>
  )
}