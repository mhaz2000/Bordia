import { Button } from '@/shared/components/Button'
import { useI18n } from '@/i18n/I18nProvider'
import { useGameInfo } from '@/features/lobby/gameMeta'
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
  const { t } = useI18n()
  const gameInfo = useGameInfo(session.gameType)
  const opponent = session.players.find((p) => p.userId !== currentUserId)

  return (
    <div className="flex items-center justify-between rounded-xl border-2 border-blue-200 bg-blue-50 px-5 py-4 shadow-sm">
      <div className="flex items-center gap-4">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-lg font-black text-white shadow">
          {gameInfo.title.charAt(0)}
        </div>
        <div>
          <p className="font-semibold text-gray-900">{t('lobby.inProgress', { gameType: gameInfo.title })}</p>
          <p className="text-sm text-gray-500">
            {t('lobby.vsStarted', {
              name: opponent ? opponent.displayName : t('common.players'),
              time: session.startedAt ? new Date(session.startedAt).toLocaleTimeString('en') : '...',
            })}
          </p>
        </div>
      </div>
      <Button variant="primary" onClick={onRejoin}>
        {t('lobby.rejoinGame')}
      </Button>
    </div>
  )
}

