import { useCallback } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useLobbyStore } from '@/shared/state/lobbyStore'
import { lobbyApi } from '@/shared/api/client'
import { lobbyHub } from '@/shared/signalr/lobbyHub'

export function useLobby() {
  const { 
    currentRoom, 
    rooms, 
    isLoading, 
    error,
    currentGameSessionId,
    setCurrentRoom,
    addRoom,
    updateRoom,
  } = useLobbyStore()
  const queryClient = useQueryClient()

  const { data: roomsData, isLoading: roomsLoading, refetch: refetchRooms } = useQuery({
    queryKey: ['lobby', 'rooms'],
    queryFn: lobbyApi.listRooms,
    staleTime: 5000,
  })

  const createRoomMutation = useMutation({
    mutationFn: lobbyApi.createRoom,
    onSuccess: (room) => {
      addRoom(room)
      setCurrentRoom(room)
      queryClient.invalidateQueries({ queryKey: ['lobby', 'rooms'] })
    },
  })

  const joinRoomMutation = useMutation({
    mutationFn: (roomId: string) => lobbyApi.joinRoom(roomId),
    onSuccess: (room) => {
      setCurrentRoom(room)
      queryClient.invalidateQueries({ queryKey: ['lobby', 'rooms'] })
    },
  })

  const leaveRoomMutation = useMutation({
    mutationFn: (roomId: string) => lobbyApi.leaveRoom(roomId),
    onSuccess: () => {
      setCurrentRoom(null)
      queryClient.invalidateQueries({ queryKey: ['lobby', 'rooms'] })
    },
  })

  const setReadyMutation = useMutation({
    mutationFn: ({ roomId, isReady }: { roomId: string; isReady: boolean }) => lobbyApi.setReady(roomId, isReady),
    onSuccess: (room) => {
      updateRoom(room)
    },
  })

  const kickPlayerMutation = useMutation({
    mutationFn: ({ roomId, playerId }: { roomId: string; playerId: string }) => lobbyApi.kickPlayer(roomId, playerId),
    onSuccess: (room) => {
      updateRoom(room)
    },
  })

  const transferHostMutation = useMutation({
    mutationFn: ({ roomId, playerId }: { roomId: string; playerId: string }) => lobbyApi.transferHost(roomId, playerId),
    onSuccess: (room) => {
      updateRoom(room)
    },
  })

  const startGameMutation = useMutation({
    mutationFn: (roomId: string) => lobbyApi.startGame(roomId),
    onSuccess: (room) => {
      updateRoom(room)
      // The REST response carries the new game session id, so the host can
      // navigate even if the GameStarted SignalR event is missed.
      if (room.gameSessionId) {
        useLobbyStore.getState().setGameSessionId(room.gameSessionId)
      }
    },
  })

  const closeRoomMutation = useMutation({
    mutationFn: (roomId: string) => lobbyApi.closeRoom(roomId),
    onSuccess: (room) => {
      updateRoom(room)
    },
  })

  // SignalR event handlers
  const setupSignalR = useCallback(() => {
    const unsubscribers = [
      lobbyHub.on('onPlayerJoined', (roomId: string, playerId: string, displayName: string, _playerCount: number) => {
        const currentRoom = useLobbyStore.getState().currentRoom
        if (currentRoom?.id === roomId) {
          const newPlayer = {
            id: playerId,
            userId: playerId,
            displayName,
            isReady: false,
            joinedAt: new Date().toISOString(),
          }
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          ;(currentRoom as any).players.push(newPlayer)
        }
      }),
      lobbyHub.on('onPlayerLeft', (roomId: string, playerId: string, _playerCount: number) => {
        if (useLobbyStore.getState().currentRoom?.id === roomId) {
          useLobbyStore.getState().removePlayer(playerId)
        }
      }),
      lobbyHub.on('onPlayerReadyChanged', (roomId: string, playerId: string, isReady: boolean) => {
        if (useLobbyStore.getState().currentRoom?.id === roomId) {
          useLobbyStore.getState().updatePlayerReady(playerId, isReady)
        }
      }),
      lobbyHub.on('onHostChanged', (roomId: string, newHostId: string) => {
        if (useLobbyStore.getState().currentRoom?.id === roomId) {
          useLobbyStore.getState().setHost(newHostId)
        }
      }),
      lobbyHub.on('onRoomClosed', (roomId: string) => {
        if (useLobbyStore.getState().currentRoom?.id === roomId) {
          useLobbyStore.getState().closeRoom()
        }
      }),
      lobbyHub.on('onGameStarted', (roomId: string, gameSessionId: string) => {
        if (useLobbyStore.getState().currentRoom?.id === roomId) {
          useLobbyStore.getState().setRoomStatus('Started', new Date().toISOString())
          useLobbyStore.getState().setGameSessionId(gameSessionId)
        }
      }),
    ]

    return () => {
      unsubscribers.forEach((unsub) => unsub())
    }
  }, [])

  const connectAndJoinRoom = useCallback(async (roomId: string) => {
    await lobbyHub.connect()
    await lobbyHub.joinRoom(roomId)
  }, [])

  const leaveRoomSignalR = useCallback(async (roomId: string) => {
    await lobbyHub.leaveRoom(roomId)
  }, [])

  return {
    currentRoom,
    currentGameSessionId,
    rooms: roomsData || rooms,
    isLoading: isLoading || roomsLoading,
    error,
    createRoom: createRoomMutation.mutateAsync,
    joinRoom: joinRoomMutation.mutateAsync,
    leaveRoom: leaveRoomMutation.mutateAsync,
    getRoom: (id: string) => lobbyApi.getRoom(id),
    setCurrentRoom,
    setReady: setReadyMutation.mutateAsync,
    kickPlayer: kickPlayerMutation.mutateAsync,
    transferHost: transferHostMutation.mutateAsync,
    startGame: startGameMutation.mutateAsync,
    closeRoom: closeRoomMutation.mutateAsync,
    refetchRooms,
    isCreating: createRoomMutation.isPending,
    isJoining: joinRoomMutation.isPending,
    isLeaving: leaveRoomMutation.isPending,
    isSettingReady: setReadyMutation.isPending,
    isStarting: startGameMutation.isPending,
    setupSignalR,
    connectAndJoinRoom,
    leaveRoomSignalR,
  }
}