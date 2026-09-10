import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/shared/state/authStore'
import { getActiveT } from '@/i18n/I18nProvider'

export interface GameHubEvents {
  onGameStateUpdated: (gameSessionId: string, state: unknown) => void
  onActionProcessed: (gameSessionId: string, playerId: string, actionType: string) => void
  onPlayerConnectionChanged: (gameSessionId: string, playerId: string, isConnected: boolean) => void
  onGameFinished: (gameSessionId: string, winnerId?: string) => void
  onSessionTakenOver: (gameSessionId: string, playerId: string) => void
}

type EventHandlers = Partial<GameHubEvents>

class GameHubClient {
  private connection: signalR.HubConnection | null = null
  private handlers: EventHandlers = {}
  private currentSessionId: string | null = null
  private joinedConnectionId: string | null = null
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
        .withUrl('/hubs/game', {
          accessTokenFactory: () => useAuthStore.getState().accessToken || '',
        })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build()

      this.setupHandlers()

      // After an automatic reconnect the connection (and its group memberships)
      // is new: re-join the current session so broadcasts keep flowing. A new
      // connection id also re-registers the seat connection server-side.
      this.connection.onreconnected(async () => {
        const sessionId = this.currentSessionId
        if (sessionId) {
          try {
            this.joinedConnectionId = null
            await this.joinSession(sessionId)
          } catch (err) {
            console.error('[GameHub] Rejoin after reconnect failed:', err)
          }
        }
      })

      try {
        await this.connection.start()
        console.log('[GameHub] Connected')
      } catch (err) {
        console.error('[GameHub] Connection failed:', err)
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

    this.connection.on('GameStateUpdated', (gameSessionId: string, state: unknown) => {
      this.handlers.onGameStateUpdated?.(gameSessionId, state)
    })

    this.connection.on('ActionProcessed', (gameSessionId: string, playerId: string, actionType: string) => {
      this.handlers.onActionProcessed?.(gameSessionId, playerId, actionType)
    })

    this.connection.on('PlayerConnectionChanged', (gameSessionId: string, playerId: string, isConnected: boolean) => {
      this.handlers.onPlayerConnectionChanged?.(gameSessionId, playerId, isConnected)
    })

    this.connection.on('GameFinished', (gameSessionId: string, winnerId?: string) => {
      this.handlers.onGameFinished?.(gameSessionId, winnerId)
    })

    this.connection.on('SessionTakenOver', (gameSessionId: string, playerId: string) => {
      this.handlers.onSessionTakenOver?.(gameSessionId, playerId)
    })

    this.connection.onclose((error) => {
      console.log('[GameHub] Disconnected', error)
    })
  }

  on<K extends keyof GameHubEvents>(event: K, handler: GameHubEvents[K]): () => void {
    this.handlers[event] = handler
    return () => {
      delete this.handlers[event]
    }
  }

  async joinSession(sessionId: string): Promise<void> {
    await this.connect()
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error(getActiveT()('game.connectionNotReady'))
    }
    // Idempotent join: the same connection re-joining the same session (React
    // StrictMode double-mount, effect re-runs) must not invoke JoinSession twice,
    // or the seat takeover logic would kick this very connection.
    if (this.currentSessionId === sessionId && this.joinedConnectionId === this.connection.connectionId) {
      return
    }
    await this.connection.invoke('JoinSession', sessionId)
    this.currentSessionId = sessionId
    this.joinedConnectionId = this.connection.connectionId
  }

  async leaveSession(sessionId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return
    }
    await this.connection.invoke('LeaveSession', sessionId)
    if (this.currentSessionId === sessionId) {
      this.currentSessionId = null
      this.joinedConnectionId = null
    }
  }

  async disconnect(): Promise<void> {
    if (this.currentSessionId) {
      await this.leaveSession(this.currentSessionId)
    }
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
    this.joinedConnectionId = null
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }
}

export const gameHub = new GameHubClient()