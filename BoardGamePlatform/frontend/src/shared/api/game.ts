export interface GameSession {
  id: string
  roomId: string
  gameType: string
  status: 'Active' | 'Paused' | 'Finished'
  currentState: GameState
  createdAt: string
  startedAt?: string
  finishedAt?: string
  players: GamePlayer[]
}

export interface GameMeta {
  gameType: string
  minPlayers: number
  maxPlayers: number
}

export interface GamePlayer {
  id: string
  userId: string
  displayName: string
  position: number
  isConnected: boolean
}

export interface GameState {
  sessionId: string
  gameType: string
  players: PlayerId[]
  currentPlayerIndex?: number
  isOver: boolean
  winner?: PlayerId
  version: number
  nextActionDeadlineUtc?: string
  data: Record<string, unknown>
}

export interface PlayerId {
  userId: string
}

export interface GameAction {
  playerId: PlayerId
  actionType: string
  payload: string
  timestamp: string
  sequenceNumber: number
}

export interface GameEvent {
  type: string
  playerId?: PlayerId
  payload: string
}

export interface CreateGameSessionRequest {
  roomId: string
}

export interface ProcessGameActionRequest {
  sessionId: string
  actionType: string
  payload: Record<string, unknown>
}

export interface ReconnectPlayerRequest {
  sessionId: string
  connectionId: string
}