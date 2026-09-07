import { Button } from '@/shared/components/Button'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { LockClosedIcon, UserGroupIcon, PlayIcon } from '@heroicons/react/24/outline'
import type { LobbyRoom } from '@/shared/api/lobby'

export function RoomCard({
  room,
  currentUserId,
  onSelect,
}: {
  room: LobbyRoom
  currentUserId?: string
  onSelect: (room: LobbyRoom) => void
}) {
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
          <span
            className={`px-2 py-0.5 rounded-full text-xs font-medium ${
              room.status === 'Waiting'
                ? 'bg-green-100 text-green-800'
                : room.status === 'Started'
                  ? 'bg-blue-100 text-blue-800'
                  : 'bg-gray-100 text-gray-800'
            }`}
          >
            {room.status}
          </span>
        </div>
        <div className="text-xs text-gray-500">Host: {room.hostDisplayName}</div>
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
          {room.status === 'Waiting' && !isMember && <PlayIcon className="w-4 h-4 mr-1" />}
          {isMember ? 'Open' : room.status === 'Waiting' ? 'Join Room' : room.status === 'Started' ? 'Game Started' : 'Room Closed'}
        </Button>
      </CardContent>
    </Card>
  )
}
