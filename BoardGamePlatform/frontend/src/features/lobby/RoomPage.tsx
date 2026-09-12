import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useLobby } from '@/shared/hooks/useLobby'
import { useAuth } from '@/shared/hooks/useAuth'
import { useLobbyStore } from '@/shared/state/lobbyStore'
import { Button } from '@/shared/components/Button'
import { Modal } from '@/shared/components/Modal'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import {
  ArrowRightOnRectangleIcon,
  CheckCircleIcon,
  ClipboardDocumentIcon,
  LockClosedIcon,
  PlayIcon,
  SignalIcon,
  TrophyIcon,
  UserPlusIcon,
  UsersIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline'
import { useI18n } from '@/i18n/I18nProvider'
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher'
import { useGameInfo } from '@/features/lobby/gameMeta'
import type { LobbyRoom, LobbyRoomPlayer } from '@/shared/api/lobby'

export function RoomPage() {
  const { t } = useI18n()
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
  const theme = useGameInfo(currentRoom?.gameType ?? '')
  const [showKickModal, setShowKickModal] = useState<{ playerId: string; displayName: string } | null>(null)
  const [showTransferModal, setShowTransferModal] = useState<{ playerId: string; displayName: string } | null>(null)
  const [showCloseModal, setShowCloseModal] = useState(false)
  const [codeCopied, setCodeCopied] = useState(false)

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

  const copyCode = async () => {
    if (!currentRoom) return
    try {
      await navigator.clipboard.writeText(currentRoom.roomCode)
      setCodeCopied(true)
      window.setTimeout(() => setCodeCopied(false), 1500)
    } catch {
      // Clipboard unavailable (permissions/http): the code stays visible to copy manually.
    }
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <p>{t('room.loginRequired')}</p>
      </div>
    )
  }

  if ((isLoading && !currentRoom) || (!currentRoom && roomQuery.isLoading)) {
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
          <CardTitle>{t('room.notFound')}</CardTitle>
          <p className="text-gray-600 mt-4">{t('room.notFoundDetail')}</p>
          <Button variant="primary" className="mt-4" asChild>
            <a href="/lobby">{t('common.backToLobby')}</a>
          </Button>
        </Card>
      </div>
    )
  }

  const guestPlayers = currentRoom.players.filter((p) => p.userId !== currentRoom.hostId)
  const readyCount = guestPlayers.filter((p) => p.isReady).length
  const enoughPlayers = currentRoom.players.length >= 2
  const allReady = guestPlayers.length > 0 && readyCount === guestPlayers.length
  const canStart = isHost && enoughPlayers && allReady && currentRoom.status === 'Waiting'

  return (
    <div className="min-h-screen bg-gray-100 pb-16">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3 min-w-0">
              <Button variant="ghost" onClick={handleLeave} isLoading={isLeaving} title={t('room.leave')}>
                <ArrowRightOnRectangleIcon className="w-5 h-5" />
              </Button>
              <div className="min-w-0">
                <h1 className="text-lg font-bold text-gray-900 truncate">{currentRoom.name}</h1>
                <p className="text-xs text-gray-500 truncate">
                  {theme.title} · {t('room.playersCount', { n: currentRoom.players.length })}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <LanguageSwitcher />
              {isHost && currentRoom.status === 'Waiting' && (
                <Button variant="primary" onClick={handleStart} isLoading={isStarting} disabled={!canStart}>
                  <PlayIcon className="w-4 h-4 me-2" />
                  {t('room.startGame')}
                </Button>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 pt-6 space-y-6">
        {/* ===== Hero banner ===== */}
        <div className={`relative overflow-hidden rounded-3xl bg-gradient-to-br ${theme.gradient} px-6 py-8 text-white shadow-xl`}>
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(255,255,255,0.28),transparent_55%)]" />
          <div className="pointer-events-none absolute -bottom-10 -end-10 h-40 w-40 rounded-full bg-white/10 blur-2xl" />
          <div className="pointer-events-none absolute -top-14 start-1/3 h-40 w-40 rounded-full bg-black/10 blur-3xl" />
          <div className="relative flex flex-wrap items-end justify-between gap-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-widest text-white/75">{theme.title}</p>
              <h2 className="mt-1 text-2xl sm:text-3xl font-black drop-shadow-sm">{currentRoom.name}</h2>
              <p className="mt-1 text-sm text-white/85">{theme.tagline}</p>
            </div>
            <div className="flex flex-col items-start sm:items-end gap-2">
              <button
                type="button"
                onClick={copyCode}
                className="group flex items-center gap-2 rounded-2xl bg-white/15 px-4 py-2 backdrop-blur-sm ring-1 ring-white/30 transition hover:bg-white/25"
                title={t('room.copyCode')}
              >
                <span className="text-[10px] font-semibold uppercase tracking-widest text-white/70">{t('room.roomCode')}</span>
                <span className="font-mono text-xl font-black tracking-widest">{currentRoom.roomCode}</span>
                {codeCopied
                  ? <CheckCircleIcon className="h-5 w-5 text-emerald-200" />
                  : <ClipboardDocumentIcon className="h-5 w-5 text-white/80 transition group-hover:text-white" />}
              </button>
              <div className="flex items-center gap-2 text-xs text-white/85">
                <UsersIcon className="h-4 w-4" />
                {t('room.joinedCount', { n: currentRoom.players.length })} · {t('room.max', { n: currentRoom.maxPlayers })}
                {currentRoom.isPrivate && (
                  <span className="flex items-center gap-1 rounded-full bg-white/15 px-2 py-0.5 ring-1 ring-white/25">
                    <LockClosedIcon className="h-3 w-3" /> {t('room.privateTag')}
                  </span>
                )}
              </div>
            </div>
          </div>
          {codeCopied && (
            <p className="relative mt-2 text-xs font-semibold text-emerald-100">{t('room.codeCopied')}</p>
          )}
        </div>

        <div className="grid gap-6 lg:grid-cols-3">
          {/* ===== Seats ===== */}
          <div className="lg:col-span-2">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>{t('room.playersTitle')}</CardTitle>
                  <span className="text-sm text-gray-500 tabular-nums">{currentRoom.players.length}/{currentRoom.maxPlayers}</span>
                </div>
              </CardHeader>
              <CardContent>
                <div className="grid gap-3 sm:grid-cols-2">
                  {currentRoom.players.map((player) => (
                    <PlayerSeat
                      key={player.userId}
                      player={player}
                      room={currentRoom}
                      isHost={isHost}
                      isCurrentUser={player.userId === user?.id}
                      onKick={() => setShowKickModal({ playerId: player.userId, displayName: player.displayName })}
                      onTransfer={() => setShowTransferModal({ playerId: player.userId, displayName: player.displayName })}
                    />
                  ))}
                  {Array.from({ length: Math.max(0, currentRoom.maxPlayers - currentRoom.players.length) }, (_, i) => (
                    <div
                      key={`empty-${i}`}
                      className="flex min-h-[104px] flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50/60 p-4 text-center"
                    >
                      <div className="flex h-11 w-11 items-center justify-center rounded-full bg-gray-100 text-gray-300">
                        <UserPlusIcon className="h-6 w-6" />
                      </div>
                      <p className="text-xs font-medium text-gray-400">{t('room.emptySeat')}</p>
                    </div>
                  ))}
                </div>
                {currentRoom.players.length < currentRoom.maxPlayers && (
                  <p className="mt-4 flex items-center gap-2 text-xs text-gray-400">
                    <SignalIcon className="h-4 w-4" />
                    {t('room.waitingPlayers')}
                  </p>
                )}
              </CardContent>
            </Card>

            {currentRoom.settings && Object.keys(currentRoom.settings).length > 0 && (
              <Card className="mt-6">
                <CardHeader>
                  <CardTitle>{t('room.settingsTitle')}</CardTitle>
                </CardHeader>
                <CardContent>
                  <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
                    {Object.entries(currentRoom.settings)
                      .filter(([, v]) => typeof v !== 'object')
                      .map(([k, v]) => (
                        <div key={k} className="flex items-center justify-between gap-2 rounded-lg bg-gray-50 px-3 py-1.5">
                          <dt className="text-gray-500">{k}</dt>
                          <dd className="font-semibold tabular-nums text-gray-800">{String(v)}</dd>
                        </div>
                      ))}
                  </dl>
                </CardContent>
              </Card>
            )}
          </div>

          {/* ===== Side column ===== */}
          <div className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>{t('room.statusTitle')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center gap-3">
                  <span className={`w-3 h-3 rounded-full ${currentRoom.status === 'Waiting' ? 'bg-green-500 animate-pulse' : currentRoom.status === 'Started' ? 'bg-blue-500' : 'bg-gray-500'}`} />
                  <div>
                    <p className="font-medium text-gray-900">{t('room.status' + currentRoom.status)}</p>
                    {currentRoom.status === 'Started' && currentRoom.startedAt && (
                      <p className="text-sm text-gray-500">{t('room.startedAt', { time: new Date(currentRoom.startedAt).toLocaleTimeString('en') })}</p>
                    )}
                  </div>
                </div>

                {/* Ready progress (all guests must be ready to start) */}
                <div>
                  <div className="mb-1.5 flex items-center justify-between text-xs font-medium text-gray-500">
                    <span>{t('room.readyProgress', { ready: readyCount, total: guestPlayers.length })}</span>
                    {allReady
                      ? <span className="flex items-center gap-1 text-emerald-600"><CheckCircleIcon className="h-4 w-4" />{t('room.allReady')}</span>
                      : <span>{!enoughPlayers ? t('room.waitingPlayers') : t('room.waitingForReady')}</span>}
                  </div>
                  <div className="h-2 overflow-hidden rounded-full bg-gray-100">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${allReady ? 'bg-emerald-500' : 'bg-blue-500'}`}
                      style={{ width: `${guestPlayers.length === 0 ? 0 : Math.round((readyCount / guestPlayers.length) * 100)}%` }}
                    />
                  </div>
                </div>

                {currentRoom.status === 'Started' && currentGameSessionId && (
                  <Button variant="primary" className="w-full" asChild>
                    <a href={`/game/${currentGameSessionId}`}>{t('room.goToGame')}</a>
                  </Button>
                )}
                {isHost && currentRoom.status === 'Waiting' && (
                  <Button variant="secondary" className="w-full" onClick={() => setShowCloseModal(true)}>
                    {t('room.closeRoom')}
                  </Button>
                )}
              </CardContent>
            </Card>

            {!isHost && currentRoom.status === 'Waiting' && (
              <Card>
                <CardHeader>
                  <CardTitle>{t('room.yourStatus')}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{t('room.readyToPlay')}</span>
                    <button
                      type="button"
                      role="switch"
                      aria-checked={isReady}
                      onClick={handleReady}
                      disabled={isSettingReady}
                      className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${isReady ? 'bg-emerald-500' : 'bg-gray-200'}`}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${isReady ? 'translate-x-6' : 'translate-x-1'}`}
                      />
                    </button>
                  </div>
                  <p className={`text-xs font-medium ${isReady ? 'text-emerald-600' : 'text-gray-400'}`}>
                    {isReady ? t('room.allReady') : t('room.waitingForReady')}
                  </p>
                </CardContent>
              </Card>
            )}

            {isHost && currentRoom.status === 'Waiting' && (
              <Card variant="outlined">
                <CardContent className="space-y-2 text-center">
                  <p className="text-sm text-gray-600">{t('room.waitingReady')}</p>
                  <p className="text-xs text-gray-400">{t('room.hostNoReady')}</p>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      </main>

      <Modal isOpen={!!showKickModal} onClose={() => setShowKickModal(null)} title={t('room.kickTitle')}>
        <p className="text-gray-600">
          {t('room.kickConfirm', { name: showKickModal?.displayName ?? '' })}
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowKickModal(null)} className="flex-1">
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={handleKick} className="flex-1">
            {t('room.kickBtn')}
          </Button>
        </div>
      </Modal>

      <Modal isOpen={!!showTransferModal} onClose={() => setShowTransferModal(null)} title={t('room.transferTitle')}>
        <p className="text-gray-600">
          {t('room.transferConfirm', { name: showTransferModal?.displayName ?? '' })}
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowTransferModal(null)} className="flex-1">
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={handleTransfer} className="flex-1">
            {t('room.transfer')}
          </Button>
        </div>
      </Modal>

      <Modal isOpen={showCloseModal} onClose={() => setShowCloseModal(false)} title={t('room.closeRoom')}>
        <p className="text-gray-600">
          {t('room.closeConfirm')}
        </p>
        <div className="flex gap-3 pt-4">
          <Button variant="secondary" onClick={() => setShowCloseModal(false)} className="flex-1">
            {t('common.cancel')}
          </Button>
          <Button variant="danger" onClick={handleClose} className="flex-1">
            {t('room.closeRoom')}
          </Button>
        </div>
      </Modal>
    </div>
  )
}

function PlayerSeat({
  player,
  room,
  isHost,
  isCurrentUser,
  onKick,
  onTransfer,
}: {
  player: LobbyRoomPlayer
  room: LobbyRoom
  isHost: boolean
  isCurrentUser: boolean
  onKick: () => void
  onTransfer: () => void
}) {
  const { t } = useI18n()
  const isPlayerHost = player.userId === room.hostId
  // By the lobby rules only guests must ready up; the host starts the game.
  const showReady = !isPlayerHost

  return (
    <div
      className={`relative flex items-start gap-3 rounded-2xl border p-4 transition-all ${
        player.isReady && !isPlayerHost
          ? 'border-emerald-300 bg-emerald-50/70 ring-1 ring-emerald-200'
          : 'border-gray-200 bg-white'
      } ${!player.isConnected ? 'opacity-75' : ''}`}
    >
      <div className="relative shrink-0">
        <div
          className={`flex h-11 w-11 items-center justify-center rounded-full text-base font-bold text-white shadow ${
            isPlayerHost ? 'bg-gradient-to-br from-amber-400 to-orange-500' : 'bg-gradient-to-br from-indigo-500 to-violet-600'
          }`}
        >
          {player.displayName.charAt(0).toUpperCase()}
        </div>
        <span
          className={`absolute -bottom-0.5 -end-0.5 h-3.5 w-3.5 rounded-full border-2 border-white ${
            player.isConnected ? 'bg-emerald-500' : 'bg-gray-300'
          }`}
          title={player.isConnected ? t('common.online') : t('common.offline')}
        />
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <p className="truncate font-semibold text-gray-900">{player.displayName}</p>
          {isPlayerHost && (
            <span className="flex items-center gap-1 rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-700">
              <TrophyIcon className="h-3 w-3" /> {t('common.host')}
            </span>
          )}
          {isCurrentUser && (
            <span className="rounded-full bg-blue-100 px-2 py-0.5 text-[10px] font-bold text-blue-700">{t('common.you')}</span>
          )}
        </div>
        <div className="mt-1 flex items-center gap-2 text-xs">
          {player.isConnected ? (
            <span className="flex items-center gap-1 font-medium text-emerald-600">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" /> {t('common.online')}
            </span>
          ) : (
            <span className="flex items-center gap-1 font-medium text-gray-400">
              <span className="h-1.5 w-1.5 rounded-full bg-gray-300" /> {t('common.offline')}
            </span>
          )}
          {showReady && (
            <span
              className={`ms-auto flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold ${
                player.isReady ? 'bg-emerald-100 text-emerald-700' : 'bg-gray-100 text-gray-500'
              }`}
            >
              {player.isReady ? <CheckCircleIcon className="h-3.5 w-3.5" /> : <XMarkIcon className="h-3.5 w-3.5" />}
              {player.isReady ? t('common.ready') : t('common.notReady')}
            </span>
          )}
        </div>
      </div>

      {isHost && !isCurrentUser && !isPlayerHost && (
        <div className="absolute top-2 end-2 flex items-center gap-0.5">
          <button
            type="button"
            onClick={onTransfer}
            className="rounded-lg p-1.5 text-gray-400 transition hover:bg-amber-50 hover:text-amber-600"
            title={t('room.transfer')}
          >
            <TrophyIcon className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={onKick}
            className="rounded-lg p-1.5 text-gray-400 transition hover:bg-red-50 hover:text-red-600"
            title={t('room.kickBtn')}
          >
            <XMarkIcon className="h-4 w-4" />
          </button>
        </div>
      )}
    </div>
  )
}
