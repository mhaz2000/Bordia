import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/hooks/useAuth'
import { useI18n } from '@/i18n/I18nProvider'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher'

export function LoginPage() {
  const navigate = useNavigate()
  const { login, isLoggingIn, isLoading } = useAuth()
  const { t } = useI18n()
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    if (!identifier.trim() || !password) {
      setError(t('auth.enterCredentials'))
      return
    }
    try {
      await login({ identifier, password })
      navigate('/lobby')
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.loginFailed'))
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
          <div className="flex justify-end mb-2">
            <LanguageSwitcher />
          </div>
          <CardTitle className="text-2xl text-center">{t('auth.welcomeBack')}</CardTitle>
          <p className="text-center text-gray-600 mt-2">{t('auth.signInSubtitle')}</p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm" role="alert">
                {error}
              </div>
            )}
            <Input
              label={t('auth.emailOrUsername')}
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              required
              autoComplete="username"
              placeholder={t('auth.emailOrUsernamePlaceholder')}
            />
            <Input
              label={t('auth.password')}
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="current-password"
              placeholder="••••••••"
            />
            <Button type="submit" className="w-full" isLoading={isLoggingIn} size="lg">
              {t('common.signIn')}
            </Button>
          </form>
          <div className="mt-6 text-center">
            <p className="text-gray-600">
              {t('auth.noAccount')}{' '}
              <Link to="/register" className="text-blue-600 hover:underline font-medium">
                {t('common.register')}
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
