import { create } from 'zustand'
import type { GameSession, GameState, GameEvent } from '@/shared/api/game'

interface GameStateState {
  currentSession: GameSession | null
  currentState: GameState | null
  isLoading: boolean
  error: string | null
  events: GameEvent[]
  takenOver: boolean

  setSession: (session: GameSession | null) => void
  setState: (state: GameState | null) => void
  setLoading: (loading: boolean) => void
  setError: (error: string | null) => void
  addEvent: (event: GameEvent) => void
  clearEvents: () => void
  updatePlayerConnection: (playerId: string, isConnected: boolean) => void
  setTakenOver: (takenOver: boolean) => void
}

export const useGameStore = create<GameStateState>((set) => ({
  currentSession: null,
  currentState: null,
  isLoading: false,
  error: null,
  events: [],
  takenOver: false,

  setSession: (session) => set({ currentSession: session }),

  setState: (state) => set({ currentState: state }),

  setLoading: (isLoading) => set({ isLoading }),

  setError: (error) => set({ error }),

  addEvent: (event) => set((prev) => ({
    events: [...prev.events, event].slice(-50),
  })),

  clearEvents: () => set({ events: [] }),

  updatePlayerConnection: (playerId, isConnected) => set((state) => {
    if (!state.currentSession) return state
    return {
      currentSession: {
        ...state.currentSession,
        players: state.currentSession.players.map((p) =>
          p.userId === playerId ? { ...p, isConnected } : p
        ),
      },
    }
  }),

  setTakenOver: (takenOver) => set({ takenOver }),
}))