import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/store/authStore'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/card'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

const loginSchema = z.object({
  username: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
})

function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)

    const result = loginSchema.safeParse({ username, password })
    if (!result.success) {
      setError(result.error.issues[0].message)
      return
    }

    setLoading(true)
    try {
      const res = await authApi.login({ username, password })
      login(
        { userId: res.userId, username: res.username, rank: res.rank },
        res.accessToken,
      )
      await navigate({ to: '/dashboard' })
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError(t('auth.errors.networkError'))
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        {/* Brand */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-2">
            <div className="w-2 h-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
            <span className="font-bold text-foreground text-lg">Awake <span className="text-accent">[LOVE]</span></span>
          </div>
          <p className="text-xs text-muted-foreground">STALCRAFT · Clan Platform</p>
        </div>

        <Card>
          <CardHeader className="pb-4">
            <CardTitle className="text-center">{t('auth.login')}</CardTitle>
            <CardDescription className="text-center">Введи данные своего аккаунта</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label className="text-sm text-muted-foreground" htmlFor="username">
                  {t('auth.username')}
                </label>
                <Input
                  id="username"
                  type="text"
                  autoComplete="username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder={t('auth.username')}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm text-muted-foreground" htmlFor="password">
                  {t('auth.password')}
                </label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                />
              </div>

              {error && (
                <p className="text-sm text-destructive text-center">{error}</p>
              )}

              <Button type="submit" disabled={loading} className="mt-1 w-full">
                {loading ? '...' : t('auth.loginButton')}
              </Button>
            </form>
          </CardContent>
          <CardFooter className="justify-center">
            <p className="text-sm text-muted-foreground">
              {t('auth.noAccount')}{' '}
              <Link to="/register" className="text-accent hover:underline font-medium">
                {t('auth.switchToRegister')}
              </Link>
            </p>
          </CardFooter>
        </Card>
      </div>
    </div>
  )
}
