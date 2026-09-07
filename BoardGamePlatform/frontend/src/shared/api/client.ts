export * from './auth'
export * from './lobby'
export * from './game'

import { useAuthStore } from '@/shared/state/authStore'
import type { 
  RegisterRequest, 
  LoginRequest, 
  RefreshTokenRequest, 
  LogoutRequest, 
  User, 
  UpdateProfileRequest, 
  ChangePasswordRequest,
  AuthResponse 
} from './auth'
import type { 
  LobbyRoom, 
  CreateRoomRequest 
} from './lobby'
import type { 
  GameSession, 
  GameState, 
  ProcessGameActionRequest, 
  ReconnectPlayerRequest,
  GameMeta
} from './game'

const API_BASE = (import.meta as any).env?.VITE_API_BASE || '/api'

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = useAuthStore.getState().accessToken
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
    ...options.headers,
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  })

  if (!response.ok) {
    const error = await response.json().catch(() => ({ detail: 'Request failed' }))
    if (response.status === 401 && !path.includes('/identity/login') && !path.includes('/identity/logout')) {
      useAuthStore.getState().logout()
    }
    throw new ApiError(response.status, error.detail || error.title || 'Request failed', error.errors)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json()
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors?: Record<string, string[]>
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export const authApi = {
  register: (data: RegisterRequest) => request<AuthResponse>('/identity/register', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  login: (data: LoginRequest) => request<AuthResponse>('/identity/login', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  refresh: (data: RefreshTokenRequest) => request<AuthResponse>('/identity/refresh', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  logout: (data: LogoutRequest) => request<void>('/identity/logout', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  getMe: () => request<User>('/identity/me'),
  updateProfile: (data: UpdateProfileRequest) => request<User>('/identity/me', {
    method: 'PUT',
    body: JSON.stringify(data),
  }),
  changePassword: (data: ChangePasswordRequest) => request<void>('/identity/change-password', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
}

export const lobbyApi = {
  listRooms: () => request<LobbyRoom[]>('/lobby/rooms'),
  getRoom: (id: string) => request<LobbyRoom>(`/lobby/rooms/${id}`),
  createRoom: (data: CreateRoomRequest) => request<LobbyRoom>('/lobby/rooms', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  joinRoom: (id: string) => request<LobbyRoom>(`/lobby/rooms/${id}/join`, {
    method: 'POST',
  }),
  leaveRoom: (id: string) => request<LobbyRoom>(`/lobby/rooms/${id}/leave`, {
    method: 'POST',
  }),
  setReady: (id: string, isReady: boolean) => request<LobbyRoom>(`/lobby/rooms/${id}/${isReady ? 'ready' : 'unready'}`, {
    method: 'POST',
  }),
  kickPlayer: (roomId: string, playerId: string) => request<LobbyRoom>(`/lobby/rooms/${roomId}/kick/${playerId}`, {
    method: 'POST',
  }),
  transferHost: (roomId: string, playerId: string) => request<LobbyRoom>(`/lobby/rooms/${roomId}/transfer-host/${playerId}`, {
    method: 'POST',
  }),
  startGame: (id: string) => request<LobbyRoom>(`/lobby/rooms/${id}/start`, {
    method: 'POST',
  }),
  closeRoom: (id: string) => request<LobbyRoom>(`/lobby/rooms/${id}/close`, {
    method: 'POST',
  }),
}

export const gameApi = {
  listGames: () => request<GameMeta[]>('/game/games'),
  mySessions: () => request<GameSession[]>('/game/sessions/mine'),
  createSession: (data: { roomId: string }) => request<GameSession>('/game/sessions', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  getSession: (id: string) => request<GameSession>(`/game/sessions/${id}`),
  getState: (id: string) => request<GameState>(`/game/sessions/${id}/state`),
  processAction: (data: ProcessGameActionRequest) => request<GameState>(`/game/sessions/${data.sessionId}/actions`, {
    method: 'POST',
    body: JSON.stringify({ actionType: data.actionType, payload: JSON.stringify(data.payload) }),
  }),
  reconnect: (data: ReconnectPlayerRequest) => request<void>(`/game/sessions/${data.sessionId}/reconnect`, {
    method: 'POST',
    body: JSON.stringify({ connectionId: data.connectionId }),
  }),
  pause: (id: string) => request<void>(`/game/sessions/${id}/pause`, {
    method: 'POST',
  }),
  resume: (id: string) => request<void>(`/game/sessions/${id}/resume`, {
    method: 'POST',
  }),
}