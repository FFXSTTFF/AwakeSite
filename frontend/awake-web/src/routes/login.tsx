import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/store/authStore'
import { ApiError } from '@/api/client'

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
    <div className="min-h-screen bg-bg-page flex items-center justify-center px-4">
      <div className="w-full max-w-sm bg-bg-card border border-border rounded-2xl p-8 shadow-lg">
        <h1 className="text-2xl font-semibold text-text-primary mb-6 text-center">
          {t('auth.login')}
        </h1>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm text-text-muted" htmlFor="username">
              {t('auth.username')}
            </label>
            <input
              id="username"
              type="text"
              autoComplete="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="bg-bg-hover border border-border rounded-lg px-3 py-2 text-text-primary placeholder:text-text-muted focus:outline-none focus:border-accent transition-colors"
              placeholder={t('auth.username')}
            />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-sm text-text-muted" htmlFor="password">
              {t('auth.password')}
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="bg-bg-hover border border-border rounded-lg px-3 py-2 text-text-primary placeholder:text-text-muted focus:outline-none focus:border-accent transition-colors"
              placeholder={t('auth.password')}
            />
          </div>

          {error && (
            <p className="text-sm text-red-400 text-center">{error}</p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="mt-2 bg-accent hover:bg-accent-hover disabled:opacity-50 disabled:cursor-not-allowed text-bg-page font-semibold rounded-lg py-2 transition-colors"
          >
            {loading ? '...' : t('auth.loginButton')}
          </button>
        </form>

        <p className="mt-4 text-center text-sm text-text-muted">
          {t('auth.noAccount')}{' '}
          <Link to="/register" className="text-accent hover:underline">
            {t('auth.switchToRegister')}
          </Link>
        </p>
      </div>
    </div>
  )
}
