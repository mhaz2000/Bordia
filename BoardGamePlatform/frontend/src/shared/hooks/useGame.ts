import { useCallback, useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useGameStore } from '@/shared/state/gameStore'
import { gameApi, type ProcessGameActionRequest } from '@/shared/api/client'
import { gameHub } from '@/shared/signalr/gameHub'
import { getActiveT } from '@/i18n/I18nProvider'
import type { GameState } from '@/shared/api/game'

export function useGame(sessionId: string) {
  const {
    currentSession,
    currentState,
    error,
    takenOver,
    setSession,
    setState,
  } = useGameStore()
  const queryClient = useQueryClient()

  const createSessionMutation = useMutation({
    mutationFn: (data: { roomId: string }) => gameApi.createSession(data),
    onSuccess: (session) => {
      setSession(session)
    },
  })

  const { data: sessionData, refetch: refetchSession } = useQuery({
    queryKey: ['game', 'session', sessionId],
    queryFn: () => gameApi.getSession(sessionId),
    enabled: !!sessionId,
    staleTime: 5000,
  })

  const { data: stateData, refetch: refetchState } = useQuery({
    queryKey: ['game', 'state', sessionId],
    queryFn: () => gameApi.getState(sessionId),
    enabled: !!sessionId,
    staleTime: 2000,
  })

  // Reset the live state whenever we switch sessions so the previous session's
  // state never leaks into the next one.
  useEffect(() => {
    setState(null)
  }, [sessionId, setState])

  // Prime the store from the state query once. After this the store is the
  // authoritative source for live updates (REST action responses + SignalR
  // broadcast); a later stale query result must never override it.
  useEffect(() => {
    if (stateData) setState(stateData)
  }, [stateData, setState])

  const processActionMutation = useMutation({
    mutationFn: (data: ProcessGameActionRequest) => gameApi.processAction(data),
    onSuccess: (state) => {
      setState(state)
      if (state.isOver) {
        queryClient.invalidateQueries({ queryKey: ['game', 'session', sessionId] })
      }
    },
  })

  const reconnectMutation = useMutation({
    mutationFn: (data: { sessionId: string; connectionId: string }) => gameApi.reconnect(data),
  })

  const pauseMutation = useMutation({
    mutationFn: () => gameApi.pause(sessionId),
  })

  const resumeMutation = useMutation({
    mutationFn: () => gameApi.resume(sessionId),
  })

  // SignalR event handlers
  const setupSignalR = useCallback(() => {
    const unsubscribers = [
      gameHub.on('onGameStateUpdated', (gameSessionId: string, state: unknown) => {
        if (useGameStore.getState().currentSession?.id === gameSessionId) {
          useGameStore.getState().setState(state as GameState)
        }
      }),
      gameHub.on('onActionProcessed', (gameSessionId: string, playerId: string, actionType: string) => {
        if (useGameStore.getState().currentSession?.id === gameSessionId) {
          useGameStore.getState().addEvent({
            type: 'ActionProcessed',
            playerId: { userId: playerId },
            payload: JSON.stringify({ actionType }),
          })
        }
      }),
      gameHub.on('onPlayerConnectionChanged', (gameSessionId: string, playerId: string, isConnected: boolean) => {
        if (useGameStore.getState().currentSession?.id === gameSessionId) {
          useGameStore.getState().updatePlayerConnection(playerId, isConnected)
        }
      }),
      gameHub.on('onGameFinished', (gameSessionId: string, winnerId?: string) => {
        if (useGameStore.getState().currentSession?.id === gameSessionId) {
          useGameStore.getState().addEvent({
            type: 'GameFinished',
            playerId: winnerId ? { userId: winnerId } : undefined,
            payload: JSON.stringify({ winnerId }),
          })
        }
      }),
      gameHub.on('onSessionTakenOver', (gameSessionId: string) => {
        if (useGameStore.getState().currentSession?.id === gameSessionId) {
          useGameStore.getState().setTakenOver(true)
        }
      }),
    ]

    return () => {
      unsubscribers.forEach((unsub) => unsub())
    }
  }, [])

  const connectAndJoinSession = useCallback(async () => {
    await gameHub.connect()
    await gameHub.joinSession(sessionId)
  }, [sessionId])

  const leaveSessionSignalR = useCallback(async () => {
    await gameHub.leaveSession(sessionId)
  }, [sessionId])

  const sendAction = async (actionType: string, payload: Record<string, unknown>) => {
    if (!sessionId) throw new Error(getActiveT()('game.noActiveSession'))
    return processActionMutation.mutateAsync({
      sessionId,
      actionType,
      payload,
    })
  }

  // Use sessionData from query if available, otherwise fall back to store
  const effectiveSession = sessionData || currentSession

  return {
    currentSession: effectiveSession,
    currentState,
    takenOver,
    isLoading: !effectiveSession,
    error,
    createSession: createSessionMutation.mutateAsync,
    sendAction,
    reconnect: reconnectMutation.mutateAsync,
    pause: pauseMutation.mutateAsync,
    resume: resumeMutation.mutateAsync,
    refetchSession,
    refetchState,
    isCreating: createSessionMutation.isPending,
    isSendingAction: processActionMutation.isPending,
    setupSignalR,
    connectAndJoinSession,
    leaveSessionSignalR,
    disconnectGameHub: () => gameHub.disconnect(),
  }
}