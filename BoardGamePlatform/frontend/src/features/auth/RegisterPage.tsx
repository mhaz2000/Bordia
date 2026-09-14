import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/hooks/useAuth'
import { useI18n } from '@/i18n/I18nProvider'
import { Button } from '@/shared/components/Button'
import { Input } from '@/shared/components/Input'
import { Card, CardHeader, CardTitle, CardContent } from '@/shared/components/Card'
import { AuthShell } from '@/shared/components/AuthShell'
import { BrandMark } from '@/shared/components/BrandLogo'

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
          <CardTitle className="text-2xl">{t('auth.createAccount')}</CardTitle>
          <p className="mt-1.5 text-sm text-gray-500">{t('auth.createAccountSubtitle')}</p>
        </CardHeader>
        <CardContent className="px-4 pb-4">
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700" role="alert">
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
              placeholder="********"
              helperText={t('auth.atLeast8')}
            />
            <Input
              label={t('auth.confirmPassword')}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              autoComplete="new-password"
              placeholder="********"
            />
            <Button
              type="submit"
              className="w-full bg-gradient-to-r from-emerald-600 to-teal-600 text-lg hover:from-emerald-700 hover:to-teal-700"
              isLoading={isRegistering}
              size="lg"
            >
              {t('common.register')}
            </Button>
          </form>
          <div className="mt-6 text-center">
            <p className="text-gray-600">
              {t('auth.haveAccount')}{' '}
              <Link to="/login" className="font-semibold text-emerald-700 hover:underline">
                {t('common.signIn')}
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </AuthShell>
  )
}
