import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/hooks/useAuth'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'

export function LoginPage() {
  const navigate = useNavigate()
  const { login, isLoggingIn, isLoading } = useAuth()
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    if (!identifier.trim() || !password) {
      setError('Please enter your email or username and password.')
      return
    }
    try {
      await login({ identifier, password })
      navigate('/lobby')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    }
  }

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4 py-12">
      <Card className="w-full max-w-md" padding="lg">
        <CardHeader>
          <CardTitle className="text-2xl text-center">Welcome Back</CardTitle>
          <p className="text-center text-gray-600 mt-2">Sign in to your account</p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm" role="alert">
                {error}
              </div>
            )}
            <Input
              label="Email or Username"
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              required
              autoComplete="username"
              placeholder="you@example.com or your username"
            />
            <Input
              label="Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="current-password"
              placeholder="••••••••"
            />
            <Button type="submit" className="w-full" isLoading={isLoggingIn} size="lg">
              Sign In
            </Button>
          </form>
          <div className="mt-6 text-center">
            <p className="text-gray-600">
              Don't have an account?{' '}
              <Link to="/register" className="text-blue-600 hover:underline font-medium">
                Register
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}