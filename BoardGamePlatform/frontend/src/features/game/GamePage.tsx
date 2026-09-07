import { useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useGame } from '@/shared/hooks/useGame'
import { useAuth } from '@/shared/hooks/useAuth'
import { useGameStore } from '@/shared/state/gameStore'
import { gameApi } from '@/shared/api/client'
import { UnoGameView } from '@/features/game/UnoGameView'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { ArrowRightOnRectangleIcon, PauseIcon, PlayIcon, ArrowPathIcon } from '@heroicons/react/24/outline'

export function GamePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const sessionId = id!
  const { 
    currentSession, 
    currentState, 
    sendAction, 
    pause, 
    resume,
    connectAndJoinSession,
    leaveSessionSignalR,
    setupSignalR,
    disconnectGameHub,
    takenOver,
    isLoading,
    isSendingAction,
  } = useGame()
  const { user } = useAuth()
  const setSession = useGameStore((s) => s.setSession)
  const setTakenOver = useGameStore((s) => s.setTakenOver)
  const [showLeaveModal, setShowLeaveModal] = useState(false)
  const [gameError, setGameError] = useState('')
  const [sessionFetchFailed, setSessionFetchFailed] = useState(false)

  // Reset the take-over flag whenever we visit a session page.
  useEffect(() => {
    setTakenOver(false)
  }, [sessionId, setTakenOver])

  useEffect(() => {
    let active = true
    setSessionFetchFailed(false)
    gameApi.getSession(sessionId)
      .then((session) => {
        if (active) setSession(session)
      })
      .catch((err: Error) => {
        console.error('[Game] Failed to load session:', err)
        if (active) {
          setSession(null)
          setSessionFetchFailed(true)
        }
      })
    return () => {
      active = false
    }
  }, [sessionId, setSession])

  // A newer tab/connection took over this session (single-tab enforcement):
  // stop listening, leave the hub group, and return to the lobby.
  useEffect(() => {
    if (!takenOver) return
    leaveSessionSignalR(sessionId).catch(() => {})
    disconnectGameHub().catch(() => {})
    navigate('/lobby')
  }, [takenOver, sessionId, leaveSessionSignalR, disconnectGameHub, navigate])

  useEffect(() => {
    const cleanup = setupSignalR()
    connectAndJoinSession(sessionId).catch((err: Error) => {
      console.error('[Game] Failed to join session:', err)
      setGameError(err.message || 'Failed to connect to the game session.')
    })
    return () => {
      cleanup()
      leaveSessionSignalR(sessionId)
    }
  }, [sessionId, setupSignalR, connectAndJoinSession, leaveSessionSignalR])

  const handleLeave = async () => {
    await leaveSessionSignalR(sessionId)
    navigate('/lobby')
  }

  const handlePause = async () => {
    if (currentSession) await pause(currentSession.id)
  }

  const handleResume = async () => {
    if (currentSession) await resume(currentSession.id)
  }

  const actionInFlight = useRef(false)

  const handleAction = async (actionType: string, payload: Record<string, unknown>) => {
    if (actionInFlight.current) return
    actionInFlight.current = true
    setGameError('')
    try {
      await sendAction(actionType, payload)
    } catch (err) {
      setGameError(err instanceof Error ? err.message : 'Action failed')
    } finally {
      actionInFlight.current = false
    }
  }

  if (isLoading && !currentSession && !sessionFetchFailed) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!currentSession) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <Card className="w-full max-w-md text-center" padding="lg">
          <CardTitle>Game session not found</CardTitle>
          <p className="text-gray-600 mt-4">This game session may have ended or doesn't exist.</p>
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
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-4">
              <Button variant="ghost" onClick={() => setShowLeaveModal(true)}>
                <ArrowRightOnRectangleIcon className="w-5 h-5" />
              </Button>
              <div>
                <h1 className="text-xl font-bold text-gray-900">{currentSession.gameType}</h1>
                <p className="text-sm text-gray-500">Session: {currentSession.id.slice(0, 8)}...</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {currentSession.status === 'Active' && (
                <Button variant="secondary" onClick={handlePause} size="sm">
                  <PauseIcon className="w-4 h-4 mr-1" />
                  Pause
                </Button>
              )}
              {currentSession.status === 'Paused' && (
                <Button variant="primary" onClick={handleResume} size="sm">
                  <PlayIcon className="w-4 h-4 mr-1" />
                  Resume
                </Button>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {gameError && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg flex items-center justify-between" role="alert">
            <span>{gameError}</span>
            <Button variant="ghost" size="sm" onClick={() => setGameError('')}>
              <ArrowPathIcon className="w-4 h-4" />
            </Button>
          </div>
        )}

        {currentState?.gameType === 'UNO' ? (
          <UnoGameView
            state={currentState}
            session={currentSession}
            userId={user?.id}
            onAction={handleAction}
            isSending={isSendingAction}
          />
        ) : (
          <div className="grid gap-6 lg:grid-cols-4">
            <div className="lg:col-span-3 space-y-6">
              <GameBoard
                state={currentState}
              />
            </div>

            <div className="space-y-6">
              <GamePlayersPanel session={currentSession} currentUserId={user?.id} />
              <GameActionsPanel
                state={currentState}
                userId={user?.id}
                onAction={handleAction}
                isLoading={isSendingAction}
              />
              <GameLogPanel events={[]} />
            </div>
          </div>
        )}
      </main>

      <Modal isOpen={showLeaveModal} onClose={() => setShowLeaveModal(false)} title="Leave Game">
        <p className="text-gray-600">
          Are you sure you want to leave this game? You can rejoin later if the game is still in progress.
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowLeaveModal(false)} className="flex-1">
            Stay
          </Button>
          <Button variant="danger" onClick={handleLeave} className="flex-1">
            Leave Game
          </Button>
        </div>
      </Modal>
    </div>
  )
}

function GameBoard({ 
  state 
}: { 
  state: { 
    gameType: string
    players: { userId: string }[]
    currentPlayerIndex?: number
    isOver: boolean
    winner?: { userId: string }
    data: Record<string, unknown>
  } | null
}) {
  if (!state) {
    return (
      <Card className="h-96 flex items-center justify-center">
        <p className="text-gray-500">Loading game state...</p>
      </Card>
    )
  }

  if (state.gameType === 'Splendor') {
    return (
      <Card className="min-h-[500px]">
        <CardHeader>
          <CardTitle>Splendor - Game Board</CardTitle>
        </CardHeader>
        <CardContent className="min-h-[400px] flex items-center justify-center">
          <div className="text-center text-gray-500">
            <p className="text-lg font-medium mb-2">Splendor Game Board</p>
            <p>Game implementation pending (Phase 2)</p>
            <div className="mt-4 p-4 bg-gray-50 rounded-lg text-left max-w-md mx-auto">
              <p className="font-medium mb-2">Current State:</p>
              <pre className="text-sm text-gray-600 overflow-auto">
                {JSON.stringify(state.data, null, 2) || '{}'}
              </pre>
            </div>
            <div className="mt-4 space-y-2">
              <p className="text-sm">Players: {state.players.map(p => p.userId.slice(0, 8)).join(', ')}</p>
              <p className="text-sm">Current turn: {state.currentPlayerIndex !== undefined ? state.players[state.currentPlayerIndex]?.userId.slice(0, 8) : 'N/A'}</p>
              <p className="text-sm">Game over: {state.isOver ? 'Yes' : 'No'}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="min-h-[500px]">
      <CardHeader>
        <CardTitle>{state.gameType} - Game Board</CardTitle>
      </CardHeader>
      <CardContent className="min-h-[400px] flex items-center justify-center text-gray-500">
        Game board for {state.gameType} not implemented yet.
      </CardContent>
    </Card>
  )
}

function GamePlayersPanel({ session, currentUserId }: { session: { players: { id: string; userId: string; displayName: string; position: number; isConnected: boolean }[] }; currentUserId?: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Players</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="space-y-2">
          {session.players.map((player) => (
            <li key={player.id} className="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-50">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${player.userId === currentUserId ? 'bg-blue-100 text-blue-800' : 'bg-gray-100 text-gray-600'}`}>
                {player.displayName.charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-medium text-gray-900 truncate">{player.displayName}</p>
                <p className="text-xs text-gray-500">Position: {player.position + 1}</p>
              </div>
              <span className={`w-2 h-2 rounded-full ${player.isConnected ? 'bg-green-500' : 'bg-gray-400'}`} />
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function GameActionsPanel({ state, userId, onAction, isLoading }: { state: { currentPlayerIndex?: number; players: { userId: string }[] } | null; userId?: string; onAction: (actionType: string, payload: Record<string, unknown>) => void; isLoading: boolean }) {
  if (!state) return null

  const isCurrentPlayer = state.currentPlayerIndex !== undefined && state.players[state.currentPlayerIndex]?.userId === userId

  return (
    <Card>
      <CardHeader>
        <CardTitle>Actions</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {!isCurrentPlayer ? (
            <p className="text-sm text-gray-500 text-center py-4">Waiting for your turn...</p>
          ) : (
            <>
              <Button variant="primary" className="w-full" onClick={() => onAction('TakeTokens', {})} isLoading={isLoading}>
                Take Tokens
              </Button>
              <Button variant="secondary" className="w-full" onClick={() => onAction('BuyCard', {})} isLoading={isLoading}>
                Buy Card
              </Button>
              <Button variant="secondary" className="w-full" onClick={() => onAction('ReserveCard', {})} isLoading={isLoading}>
                Reserve Card
              </Button>
            </>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function GameLogPanel({ events }: { events: { type: string; playerId?: { userId: string }; payload: string }[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Game Log</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="max-h-64 overflow-y-auto space-y-2">
          {events.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">No events yet</p>
          ) : (
            events.slice().reverse().map((event, index) => (
              <div key={index} className="text-sm text-gray-600 p-2 bg-gray-50 rounded">
                <span className="font-medium text-gray-900">{event.type}</span>
                {event.playerId && <span className="mx-2 text-blue-600">Player {event.playerId.userId.slice(0, 8)}</span>}
                <span className="text-gray-500">{event.payload}</span>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  )
}