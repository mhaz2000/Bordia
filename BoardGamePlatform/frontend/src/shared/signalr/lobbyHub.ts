import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/shared/state/authStore'

export interface LobbyHubEvents {
  onPlayerJoined: (roomId: string, playerId: string, displayName: string, playerCount: number) => void
  onPlayerLeft: (roomId: string, playerId: string, playerCount: number) => void
  onPlayerReadyChanged: (roomId: string, playerId: string, isReady: boolean) => void
  onHostChanged: (roomId: string, newHostId: string) => void
  onRoomClosed: (roomId: string) => void
  onGameStarted: (roomId: string, gameSessionId: string) => void
}

type EventHandlers = Partial<LobbyHubEvents>

class LobbyHubClient {
  private connection: signalR.HubConnection | null = null
  private handlers: EventHandlers = {}
  private currentRoomId: string | null = null
  private connectPromise: Promise<void> | null = null

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    if (this.connectPromise) {
      await this.connectPromise
      return
    }

    this.connectPromise = (async () => {
      if (this.connection?.state === signalR.HubConnectionState.Connected) {
        return
      }
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/lobby', {
          accessTokenFactory: () => useAuthStore.getState().accessToken || '',
        })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build()

      this.setupHandlers()

      try {
        await this.connection.start()
        console.log('[LobbyHub] Connected')
      } catch (err) {
        console.error('[LobbyHub] Connection failed:', err)
        this.connection = null
        throw err
      }
    })()

    try {
      await this.connectPromise
    } finally {
      this.connectPromise = null
    }
  }

  private setupHandlers(): void {
    if (!this.connection) return

    this.connection.on('PlayerJoined', (roomId: string, playerId: string, displayName: string, playerCount: number) => {
      this.handlers.onPlayerJoined?.(roomId, playerId, displayName, playerCount)
    })

    this.connection.on('PlayerLeft', (roomId: string, playerId: string, playerCount: number) => {
      this.handlers.onPlayerLeft?.(roomId, playerId, playerCount)
    })

    this.connection.on('PlayerReadyChanged', (roomId: string, playerId: string, isReady: boolean) => {
      this.handlers.onPlayerReadyChanged?.(roomId, playerId, isReady)
    })

    this.connection.on('HostChanged', (roomId: string, newHostId: string) => {
      this.handlers.onHostChanged?.(roomId, newHostId)
    })

    this.connection.on('RoomClosed', (roomId: string) => {
      this.handlers.onRoomClosed?.(roomId)
    })

    this.connection.on('GameStarted', (roomId: string, gameSessionId: string) => {
      this.handlers.onGameStarted?.(roomId, gameSessionId)
    })

    this.connection.onclose((error) => {
      console.log('[LobbyHub] Disconnected', error)
    })
  }

  on<K extends keyof LobbyHubEvents>(event: K, handler: LobbyHubEvents[K]): () => void {
    this.handlers[event] = handler
    return () => {
      delete this.handlers[event]
    }
  }

  async joinRoom(roomId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      await this.connect()
    }
    await this.connection!.invoke('JoinRoom', roomId)
    this.currentRoomId = roomId
  }

  async leaveRoom(roomId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return
    }
    await this.connection.invoke('LeaveRoom', roomId)
    if (this.currentRoomId === roomId) {
      this.currentRoomId = null
    }
  }

  async disconnect(): Promise<void> {
    if (this.currentRoomId) {
      await this.leaveRoom(this.currentRoomId)
    }
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }
}

export const lobbyHub = new LobbyHubClient()