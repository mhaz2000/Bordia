import { useCallback } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/shared/state/authStore'
import { authApi } from '@/shared/api/client'

export function useAuth() {
  const { user, accessToken, refreshToken, isAuthenticated, isLoading, setAuth, setUser, logout, setLoading } = useAuthStore()
  const queryClient = useQueryClient()

  const { isLoading: meLoading } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: authApi.getMe,
    enabled: !!accessToken,
    retry: false,
    staleTime: 1000 * 60 * 10,
  })

  const registerMutation = useMutation({
    mutationFn: authApi.register,
    onSuccess: (data) => {
      setAuth(data)
      queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })

  const loginMutation = useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      setAuth(data)
      queryClient.invalidateQueries({ queryKey: ['auth', 'me'] })
    },
  })

  const logoutMutation = useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => {
      logout()
      queryClient.clear()
    },
  })

  const refreshMutation = useMutation({
    mutationFn: authApi.refresh,
    onSuccess: (data) => {
      setAuth(data)
    },
    onError: () => {
      logout()
      queryClient.clear()
    },
  })

  const updateProfileMutation = useMutation({
    mutationFn: authApi.updateProfile,
    onSuccess: (data) => {
      setUser(data)
    },
  })

  const changePasswordMutation = useMutation({
    mutationFn: authApi.changePassword,
  })

  const initializeAuth = useCallback(async () => {
    if (accessToken && !user) {
      setLoading(true)
      try {
        const userData = await authApi.getMe()
        setUser(userData)
      } catch {
        logout()
      } finally {
        setLoading(false)
      }
    } else {
      setLoading(false)
    }
  }, [accessToken, user, setLoading, setUser, logout])

  return {
    user,
    accessToken,
    refreshToken,
    isAuthenticated,
    isLoading: isLoading || meLoading,
    register: registerMutation.mutateAsync,
    login: loginMutation.mutateAsync,
    logout: logoutMutation.mutateAsync,
    refresh: refreshMutation.mutateAsync,
    updateProfile: updateProfileMutation.mutateAsync,
    changePassword: changePasswordMutation.mutateAsync,
    isRegistering: registerMutation.isPending,
    isLoggingIn: loginMutation.isPending,
    isLoggingOut: logoutMutation.isPending,
    initializeAuth,
  }
}