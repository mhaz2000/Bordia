import { Button } from '@/shared/components/Button'
import type { GameSession } from '@/shared/api/game'

export function ActiveGameCard({
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
          <p className="font-semibold text-gray-900">{session.gameType} game in progress</p>
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
