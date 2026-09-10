import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/hooks/useAuth'
import { useI18n } from '@/i18n/I18nProvider'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { LanguageSwitcher } from '@/shared/components/LanguageSwitcher'

export function RegisterPage() {
  const navigate = useNavigate()
  const { register, isRegistering, isLoading } = useAuth()
  const { t } = useI18n()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!email.trim() || !displayName.trim() || !password) {
      setError(t('auth.fillAllFields'))
      return
    }

    if (password !== confirmPassword) {
      setError(t('auth.passwordsNoMatch'))
      return
    }

    if (password.length < 8) {
      setError(t('auth.passwordShort'))
      return
    }

    try {
      await register({ email, password, displayName })
      navigate('/lobby')
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.registerFailed'))
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
          <CardTitle className="text-2xl text-center">{t('auth.createAccount')}</CardTitle>
          <p className="text-center text-gray-600 mt-2">{t('auth.createAccountSubtitle')}</p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm" role="alert">
                {error}
              </div>
            )}
            <Input
              label={t('auth.displayName')}
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              autoComplete="name"
              placeholder={t('auth.displayNamePlaceholder')}
            />
            <Input
              label={t('auth.email')}
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
              placeholder={t('auth.emailPlaceholder')}
            />
            <Input
              label={t('auth.password')}
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="new-password"
              placeholder="••••••••"
              helperText={t('auth.atLeast8')}
            />
            <Input
              label={t('auth.confirmPassword')}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              autoComplete="new-password"
              placeholder="••••••••"
            />
            <Button type="submit" className="w-full" isLoading={isRegistering} size="lg">
              {t('auth.createAccount')}
            </Button>
          </form>
          <div className="mt-6 text-center">
            <p className="text-gray-600">
              {t('auth.haveAccount')}{' '}
              <Link to="/login" className="text-blue-600 hover:underline font-medium">
                {t('common.signIn')}
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
