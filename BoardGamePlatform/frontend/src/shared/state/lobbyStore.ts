import { create } from 'zustand'
import type { LobbyRoom, LobbyRoomPlayer } from '@/shared/api/lobby'

interface LobbyState {
  currentRoom: LobbyRoom | null
  rooms: LobbyRoom[]
  isLoading: boolean
  error: string | null
  currentGameSessionId: string | null

  setCurrentRoom: (room: LobbyRoom | null) => void
  setGameSessionId: (sessionId: string | null) => void
  setRooms: (rooms: LobbyRoom[]) => void
  addRoom: (room: LobbyRoom) => void
  removeRoom: (roomId: string) => void
  updateRoom: (room: LobbyRoom) => void
  setLoading: (loading: boolean) => void
  setError: (error: string | null) => void
  
  // Player actions
  addPlayer: (player: LobbyRoomPlayer) => void
  removePlayer: (userId: string) => void
  updatePlayerReady: (userId: string, isReady: boolean) => void
  updatePlayerPresence: (userId: string, isConnected: boolean) => void
  setHost: (hostId: string) => void
  setRoomStatus: (status: LobbyRoom['status'], startedAt?: string) => void
  closeRoom: () => void
}

export const useLobbyStore = create<LobbyState>((set) => ({
  currentRoom: null,
  rooms: [],
  isLoading: false,
  error: null,
  currentGameSessionId: null,

  setCurrentRoom: (room) => set({ currentRoom: room }),

  setGameSessionId: (sessionId) => set({ currentGameSessionId: sessionId }),

  setRooms: (rooms) => set({ rooms }),

  addRoom: (room) => set((state) => ({ rooms: [...state.rooms, room] })),

  removeRoom: (roomId) => set((state) => ({
    rooms: state.rooms.filter((r) => r.id !== roomId),
    currentRoom: state.currentRoom?.id === roomId ? null : state.currentRoom,
  })),

  updateRoom: (room) => set((state) => ({
    rooms: state.rooms.map((r) => (r.id === room.id ? room : r)),
    currentRoom: state.currentRoom?.id === room.id ? room : state.currentRoom,
  })),

  setLoading: (isLoading) => set({ isLoading }),

  setError: (error) => set({ error }),

  // Player actions
  addPlayer: (player) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        players: [...state.currentRoom.players, player],
      },
    }
  }),

  removePlayer: (userId) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        players: state.currentRoom.players.filter((p) => p.userId !== userId),
      },
    }
  }),

  updatePlayerReady: (userId, isReady) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        players: state.currentRoom.players.map((p) =>
          p.userId === userId ? { ...p, isReady } : p
        ),
      },
    }
  }),

  updatePlayerPresence: (userId, isConnected) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        players: state.currentRoom.players.map((p) =>
          p.userId === userId ? { ...p, isConnected } : p
        ),
      },
    }
  }),

  setHost: (hostId) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        hostId,
      },
    }
  }),

  setRoomStatus: (status, startedAt) => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        status,
        ...(startedAt && { startedAt }),
      },
    }
  }),

  closeRoom: () => set((state) => {
    if (!state.currentRoom) return state
    return {
      currentRoom: {
        ...state.currentRoom,
        status: 'Closed',
      },
    }
  }),
}))