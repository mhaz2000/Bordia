/** Static per-game copy shown on game pages. Gameplay ranges come from the backend catalog. */
export type RuleIconName = 'cards' | 'trophy' | 'bolt' | 'shield' | 'refresh' | 'clock' | 'flag'

export interface GameRule {
  icon: RuleIconName
  title: string
  detail: string
}

export interface GameInfo {
  gameType: string
  title: string
  tagline: string
  description: string
  rules: GameRule[]
  gradient: string
  comingSoon?: boolean
}

export const GAME_INFO: Record<string, GameInfo> = {
  UNO: {
    gameType: 'UNO',
    title: 'UNO',
    tagline: 'The classic shedding card game',
    description:
      'Race to empty your hand before everyone else. Match the top card by color or value, unleash action cards on your rivals, and do not forget to call UNO when you are down to one card.',
    rules: [
      {
        icon: 'cards',
        title: 'Match & shed',
        detail: 'Play a card matching the top discard by color or number - Wilds go on anything. First to empty their hand wins instantly.',
      },
      {
        icon: 'bolt',
        title: '+2 and +4 hit hard',
        detail: 'Draw Two and Wild Draw Four force the next player to draw and forfeit their turn. Penalties cannot be stacked or passed on.',
      },
      {
        icon: 'shield',
        title: 'Challenge the +4',
        detail: 'Suspect an illegal Wild Draw Four? Challenge it: if they had a matching color they draw 4, otherwise you draw 6.',
      },
      {
        icon: 'refresh',
        title: 'Draw, then play or pass',
        detail: 'One voluntary draw per turn. If the drawn card fits, play it right away - otherwise the turn passes automatically.',
      },
      {
        icon: 'clock',
        title: 'Timed turns with a bank',
        detail: '30 seconds per turn; unused time banks up to 120s. Overtime shortens your next turn - 3 skipped turns in a row removes you as AFK.',
      },
      {
        icon: 'flag',
        title: '60-minute finish',
        detail: 'When the game clock runs out, the player with the fewest cards wins. A shared minimum is a draw.',
      },
    ],
    gradient: 'from-red-500 via-orange-500 to-amber-400',
  },
  Splendor: {
    gameType: 'Splendor',
    title: 'Splendor',
    tagline: 'Gem merchants of the Renaissance',
    description:
      'Collect gem tokens, reserve development cards, and build an engine of mines and merchants to attract nobles. Splendor is coming to the platform soon.',
    rules: [
      {
        icon: 'flag',
        title: 'Coming soon',
        detail: 'Rules will be published together with the game release.',
      },
    ],
    gradient: 'from-emerald-500 via-teal-500 to-cyan-500',
    comingSoon: true,
  },
}

/** Fallback copy for catalog games without curated content yet. */
export function getGameInfo(gameType: string): GameInfo {
  return (
    GAME_INFO[gameType] ?? {
      gameType,
      title: gameType,
      tagline: 'A board game on the platform',
      description: 'Details for this game are coming soon.',
      rules: [
        {
          icon: 'flag',
          title: 'Coming soon',
          detail: 'Rules will be published together with the game release.',
        },
      ],
      gradient: 'from-slate-500 via-gray-600 to-gray-700',
      comingSoon: true,
    }
  )
}
