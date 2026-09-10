/**
 * Maps backend error messages (engine failures + application handler
 * exceptions) to dictionary keys so the UI shows them in the active language.
 * The backend message set is finite and developer-controlled; unknown
 * messages pass through as-is.
 */
export function backendErrorKey(message: string): string | null {
  const drawn = /^You must draw (\d+) cards first$/.exec(message)
  if (drawn) return `unoErrors.mustDraw:${drawn[1]}`

  const table: Record<string, string> = {
    'Game is already over': 'unoErrors.gameOver',
    'Invalid player': 'unoErrors.invalidPlayer',
    'Not your turn': 'unoErrors.notYourTurn',
    'Invalid action type': 'unoErrors.invalidActionType',
    'Card is required': 'unoErrors.cardRequired',
    'Card not in hand': 'unoErrors.cardNotInHand',
    'Card cannot be played on current top card': 'unoErrors.cannotPlayOnTop',
    'Must choose a color for wild card': 'unoErrors.chooseWildColor',
    'You must accept the draw or challenge the Wild Draw Four': 'unoErrors.acceptOrChallenge',
    'Use AcceptDraw to take the pending cards': 'unoErrors.useAcceptDraw',
    'You may only draw once per turn': 'unoErrors.oneDrawPerTurn',
    'You can only pass after drawing a card': 'unoErrors.passAfterDraw',
    'No cards left to draw': 'unoErrors.noCardsToDraw',
    'No pending draw to accept': 'unoErrors.noPendingDraw',
    'The draw penalty is not yours to accept': 'unoErrors.notYourPenalty',
    'No Wild Draw Four to challenge': 'unoErrors.noWdfToChallenge',
    'This draw penalty cannot be challenged': 'unoErrors.notChallengeable',
    'The Wild Draw Four challenge has already been resolved': 'unoErrors.challengeResolved',
    'This draw penalty is not yours to challenge': 'unoErrors.notYourChallenge',
    'Last card is not Wild Draw Four': 'unoErrors.lastNotWdf',
    'No card to challenge': 'unoErrors.noCardToChallenge',
    'Turn timer has not expired': 'unoErrors.timerNotExpired',
    'Game time limit has not been reached': 'unoErrors.timeNotUp',
    "You don't have exactly one card": 'unoErrors.notOneCard',
    'UNO already called': 'unoErrors.unoAlreadyCalled',
    'Not your turn to call UNO': 'unoErrors.notYourUno',
    'This room is full.': 'serverErrors.roomFull',
    'This room is no longer accepting players.': 'serverErrors.roomNotAccepting',
    'You are not a member of this room.': 'serverErrors.notMember',
    'The player is not a member of this room.': 'serverErrors.playerNotMember',
    'The host cannot kick themselves.': 'serverErrors.cannotKickSelf',
    'Only the host can kick players.': 'serverErrors.onlyHostKick',
    'Only the host can start the game.': 'serverErrors.onlyHostStart',
    'Only the host can close the room.': 'serverErrors.onlyHostClose',
    'Only the host can transfer host.': 'serverErrors.onlyHostTransfer',
    'All players must be ready before starting.': 'serverErrors.allMustBeReady',
    'At least two players are required to start.': 'serverErrors.twoPlayersRequired',
    'This room is not in a startable state.': 'serverErrors.roomNotStartable',
    'The game service could not start a session. Please try again.': 'serverErrors.gameServiceFailed',
    'This game is not accepting actions right now.': 'serverErrors.gameNotAccepting',
    'You are not a player in this game.': 'serverErrors.notAPlayer',
    'User is not authenticated.': 'serverErrors.notAuthenticated',
  }
  return table[message] ?? null
}

/**
 * Resolves a backend message through the dictionary. Parameterized messages
 * (e.g. draw counts) are matched by regex first.
 */
export function translateBackendMessage(
  message: string,
  t: (key: string, params?: Record<string, string | number>) => string,
): string {
  const drawn = /^You must draw (\d+) cards first$/.exec(message)
  if (drawn) return t('unoErrors.mustDraw', { n: Number(drawn[1]) })
  const key = backendErrorKey(message)
  return key ? t(key) : message
}
