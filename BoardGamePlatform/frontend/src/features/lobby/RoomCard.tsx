import { Button } from '@/shared/components/Button'
import { LockClosedIcon, UserGroupIcon, PlayIcon } from '@heroicons/react/24/outline'
import { useI18n } from '@/i18n/I18nProvider'
import { useGameInfo } from '@/features/lobby/gameMeta'
import type { LobbyRoom } from '@/shared/api/lobby'

/** Room status pill keys for the backend enum values (raw text fallback for unknowns). */
const STATUS_KEYS: Record<string, string> = {
  Waiting: 'room.statusWaiting',
  Started: 'room.statusStarted',
  Closed: 'room.statusClosed',
}

export function RoomCard({
  room,
  currentUserId,
  onSelect,
}: {
  room: LobbyRoom
  currentUserId?: string
  onSelect: (room: LobbyRoom) => void
}) {
  const { t } = useI18n()
  const gameInfo = useGameInfo(room.gameType)
  const isMember = room.players.some((p) => p.userId === currentUserId)
  const canJoin = room.status === 'Waiting' && (isMember || room.players.length < room.maxPlayers)
  const readyPct = room.maxPlayers > 0 ? (room.players.filter((p) => p.isReady).length / room.maxPlayers) * 100 : 0

  return (
    <div className="group overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg">
      {/* game identity strip */}
      <div className={`relative h-9 overflow-hidden bg-gradient-to-r ${gameInfo.gradient}`}>
        <span className="absolute -bottom-4 -end-3 h-10 w-10 rounded-full bg-white/15" />
        <span className="absolute -top-4 start-6 h-8 w-8 rounded-full bg-black/10" />
        <div className="relative flex h-full items-center justify-between px-3">
          <span className="text-[11px] font-black uppercase tracking-widest text-white drop-shadow">{gameInfo.title}</span>
          {room.isPrivate && (
            <span className="flex items-center gap-1 rounded-full bg-black/35 px-2 py-0.5 text-[10px] font-bold text-white backdrop-blur-sm">
              <LockClosedIcon className="w-3 h-3" />
              {t('room.privateTag')}
            </span>
          )}
        </div>
      </div>

      <div className="space-y-3 p-4">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <h3 className="truncate text-base font-semibold text-gray-900">{room.name}</h3>
            <p className="mt-0.5 text-xs text-gray-500">
              {t('room.host')}: {room.hostDisplayName}
            </p>
          </div>
          <span
            className={`shrink-0 rounded-full px-2.5 py-1 text-[11px] font-bold ${
              room.status === 'Waiting'
                ? 'bg-emerald-100 text-emerald-700'
                : room.status === 'Started'
                  ? 'bg-sky-100 text-sky-700'
                  : 'bg-gray-100 text-gray-500'
            }`}
          >
            {STATUS_KEYS[room.status] ? t(STATUS_KEYS[room.status]) : room.status}
          </span>
        </div>

        <div className="flex items-center justify-between">
          <span className="font-mono text-sm font-bold tracking-[0.2em] text-gray-700 ring-1 ring-gray-200 rounded-lg px-2.5 py-1 bg-gray-50">
            {room.roomCode}
          </span>
          <div className="flex items-center">
            {room.players.slice(0, 4).map((player) => (
              <div
                key={player.userId}
                className={`-me-2 last:me-0 h-9 w-9 rounded-full flex items-center justify-center text-xs font-bold text-white ring-2 ring-white shadow-sm ${
                  player.isReady ? 'bg-gradient-to-br from-emerald-500 to-teal-600' : 'bg-gradient-to-br from-slate-400 to-slate-600'
                }`}
                title={`${player.displayName}${player.isReady ? ` · ${t('common.ready')}` : ''}`}
              >
                {player.displayName.charAt(0).toUpperCase()}
              </div>
            ))}
            {room.players.length > 4 && (
              <div className="h-9 w-9 rounded-full bg-gray-200 flex items-center justify-center text-[10px] font-bold text-gray-600 ring-2 ring-white">
                +{room.players.length - 4}
              </div>
            )}
            <span className="ms-4 flex items-center gap-1 text-xs font-medium text-gray-500 tabular-nums">
              <UserGroupIcon className="w-4 h-4" />
              {room.players.length}/{room.maxPlayers}
            </span>
          </div>
        </div>

        {/* ready progress hairline */}
        <div className="h-1 w-full overflow-hidden rounded-full bg-gray-100">
          <div className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-teal-500 transition-all" style={{ width: `${readyPct}%` }} />
        </div>

        <Button
          className={`w-full ${
            room.status === 'Waiting' && canJoin
              ? 'bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700'
              : ''
          }`}
          onClick={() => onSelect(room)}
          disabled={!canJoin || room.status !== 'Waiting'}
          variant={room.status === 'Waiting' ? 'primary' : 'secondary'}
        >
          {room.status === 'Waiting' && !isMember && <PlayIcon className="w-4 h-4 me-1" />}
          {isMember ? t('roomCard.open') : room.status === 'Waiting' ? t('roomCard.joinRoom') : room.status === 'Started' ? t('roomCard.gameStarted') : t('roomCard.roomClosed')}
        </Button>
      </div>
    </div>
  )
}

