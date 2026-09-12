export interface LobbyRoom {
  id: string
  name: string
  roomCode: string
  gameType: string
  maxPlayers: number
  isPrivate: boolean
  status: 'Waiting' | 'Started' | 'Closed'
  hostId: string
  hostDisplayName: string
  createdAt: string
  startedAt?: string
  gameSessionId?: string
  players: LobbyRoomPlayer[]
  settings?: Record<string, unknown>
}

export interface LobbyRoomPlayer {
  userId: string
  displayName: string
  isReady: boolean
  joinedAt: string
  /** Live SignalR presence from the server (not the raw connection id). */
  isConnected: boolean
}

export interface CreateRoomRequest {
  name: string
  gameType: string
  maxPlayers: number
  isPrivate?: boolean
  settingsJson?: string
}

export interface JoinRoomRequest {
  roomId: string
}

export interface SetReadyRequest {
  roomId: string
  isReady: boolean
}

export interface KickPlayerRequest {
  roomId: string
  playerId: string
}

export interface TransferHostRequest {
  roomId: string
  playerId: string
}