import { useI18n } from '@/i18n/I18nProvider'
import type { Dict, RuleIconName } from '@/i18n/locales/en'

export type { RuleIconName }
export type GameRule = Dict['games']['UNO']['rules'][number]

export interface GameInfo {
  gameType: string
  title: string
  tagline: string
  description: string
  rules: Dict['games']['UNO']['rules']
  actionCards: Dict['games']['UNO']['actionCards']
  gradient: string
  comingSoon?: boolean
}

type GameContent = Dict['games']['UNO']

/** Static per-game theming. Copy comes from the i18n dictionaries. */
const GAME_THEME: Record<string, { gradient: string; comingSoon?: boolean }> = {
  UNO: { gradient: 'from-red-500 via-orange-500 to-amber-400' },
  Silver: { gradient: 'from-indigo-500 via-slate-500 to-violet-500' },
  Splendor: { gradient: 'from-emerald-500 via-teal-500 to-cyan-500', comingSoon: true },
}

const DEFAULT_THEME = { gradient: 'from-slate-500 via-gray-600 to-gray-700', comingSoon: true }

/**
 * Returns the localized game info. Must be called from a component (it reads
 * the i18n context); the game type keys match the backend catalog, with a
 * dictionary-defined default for uncurated games.
 */
export function useGameInfo(gameType: string): GameInfo {
  const { d } = useI18n()
  const theme = GAME_THEME[gameType] ?? DEFAULT_THEME
  const content: GameContent = (d.games as unknown as Record<string, GameContent>)[gameType] ?? d.games._default

  return {
    gameType,
    title: content.title || gameType,
    tagline: content.tagline,
    description: content.description,
    rules: content.rules,
    actionCards: content.actionCards,
    ...theme,
  }
}
