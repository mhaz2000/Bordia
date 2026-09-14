import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/hooks/useAuth'
import { useI18n } from '@/i18n/I18nProvider'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { AuthShell } from '@/shared/components/AuthShell'
import { BrandMark } from '@/shared/components/BrandLogo'

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
      <div className="flex min-h-screen items-center justify-center bg-slate-950">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-emerald-400 border-t-transparent" />
      </div>
    )
  }

  return (
    <AuthShell>
      <Card variant="elevated" className="rounded-2xl border border-white/40 bg-white/95 p-2 shadow-2xl backdrop-blur">
        <CardHeader className="pt-4 text-center">
          <div className="mb-3 flex justify-center lg:hidden">
            <BrandMark className="h-12 w-12" />
          </div>
          <CardTitle className="text-2xl">{t('auth.welcomeBack')}</CardTitle>
          <p className="mt-1.5 text-sm text-gray-500">{t('auth.signInSubtitle')}</p>
        </CardHeader>
        <CardContent className="px-4 pb-4">
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700" role="alert">
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
              placeholder="********"
            />
            <Button
              type="submit"
              className="w-full bg-gradient-to-r from-emerald-600 to-teal-600 text-lg hover:from-emerald-700 hover:to-teal-700"
              isLoading={isLoggingIn}
              size="lg"
            >
              {t('common.signIn')}
            </Button>
          </form>
          <div className="mt-6 text-center">
            <p className="text-gray-600">
              {t('auth.noAccount')}{' '}
              <Link to="/register" className="font-semibold text-emerald-700 hover:underline">
                {t('common.register')}
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </AuthShell>
  )
}
