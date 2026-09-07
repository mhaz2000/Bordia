import { Routes, Route, Navigate, Outlet } from 'react-router-dom'
import { useEffect } from 'react'
import { useAuth } from '@/shared/hooks/useAuth'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { LobbyPage } from '@/features/lobby/LobbyPage'
import { GameLobbyPage } from '@/features/lobby/GameLobbyPage'
import { RoomPage } from '@/features/lobby/RoomPage'
import { GamePage } from '@/features/game/GamePage'

function PublicLayout() {
  return <Outlet />
}

function PrivateLayout() {
  const { isAuthenticated, isLoading, initializeAuth } = useAuth()

  useEffect(() => {
    initializeAuth()
  }, [initializeAuth])

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}

export default function App() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>
      <Route element={<PrivateLayout />}>
        <Route path="/lobby" element={<LobbyPage />} />
        <Route path="/lobby/games/:gameType" element={<GameLobbyPage />} />
        <Route path="/lobby/:id" element={<RoomPage />} />
        <Route path="/game/:id" element={<GamePage />} />
      </Route>
      <Route path="/" element={<Navigate to="/lobby" replace />} />
      <Route path="*" element={<Navigate to="/lobby" replace />} />
    </Routes>
  )
}