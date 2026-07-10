import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect } from 'react'
import { useAuthStore } from '@/store/authStore'
import type { UserRank } from '@/types/api'

export const Route = createFileRoute('/auth/callback')({
  component: AuthCallbackPage,
})

function AuthCallbackPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  useEffect(() => {
    const params = new URLSearchParams(window.location.hash.slice(1))
    const accessToken = params.get('accessToken')
    const username = params.get('username')
    const rank = params.get('rank')
    const userId = params.get('userId')

    // Убираем токен из адресной строки сразу после чтения
    window.history.replaceState(null, '', window.location.pathname)

    if (!accessToken || !username || rank === null || !userId) {
      void navigate({ to: '/login', search: { error: 'discord' } })
      return
    }

    login(
      { userId, username, rank: Number(rank) as UserRank },
      accessToken,
    )
    void navigate({ to: '/dashboard' })
  }, [login, navigate])

  return (
    <div className="min-h-screen bg-background flex items-center justify-center">
      <p className="text-muted-foreground">Входим…</p>
    </div>
  )
}
