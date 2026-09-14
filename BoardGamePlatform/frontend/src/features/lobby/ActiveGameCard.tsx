import { Button } from '@/shared/components/Button'
import { useI18n } from '@/i18n/I18nProvider'
import { useGameInfo } from '@/features/lobby/gameMeta'
import { ArrowRightIcon } from '@heroicons/react/24/outline'
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
    <div className="relative overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg">
      <div className={`absolute inset-y-0 start-0 w-1.5 bg-gradient-to-b ${gameInfo.gradient}`} />
      <div className="flex items-center justify-between gap-4 p-4 ps-6">
        <div className="flex min-w-0 items-center gap-4">
          <div className={`flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-xl bg-gradient-to-br ${gameInfo.gradient} text-lg font-black text-white shadow-md ring-1 ring-white/40`}>
            {gameInfo.title.charAt(0)}
          </div>
          <div className="min-w-0">
            <p className="flex items-center gap-2 font-semibold text-gray-900">
              {t('lobby.inProgress', { gameType: gameInfo.title })}
              <span className="relative flex h-2 w-2">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
              </span>
            </p>
            <p className="truncate text-sm text-gray-500">
              {t('lobby.vsStarted', {
                name: opponent ? opponent.displayName : t('common.players'),
                time: session.startedAt ? new Date(session.startedAt).toLocaleTimeString('en') : '...',
              })}
            </p>
          </div>
        </div>
        <Button
          variant="primary"
          onClick={onRejoin}
          className="flex-shrink-0 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700"
        >
          {t('lobby.rejoinGame')}
          <ArrowRightIcon className="w-4 h-4 ms-1 rtl:-scale-x-100" />
        </Button>
      </div>
    </div>
  )
}
