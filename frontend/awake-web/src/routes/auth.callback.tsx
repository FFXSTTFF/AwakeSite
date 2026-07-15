import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect, useRef } from 'react'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/auth/callback')({
  component: AuthCallbackPage,
})

function AuthCallbackPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)
  const handled = useRef(false)

  useEffect(() => {
    // StrictMode в dev вызывает эффект дважды — второй прогон видит уже пустой hash
    if (handled.current) return
    handled.current = true

    const params = new URLSearchParams(window.location.hash.slice(1))
    const accessToken = params.get('accessToken')
    const username = params.get('username')
    const rank = params.get('rank')
    const userId = params.get('userId')

    // Убираем токен из адресной строки сразу после чтения
    window.history.replaceState(null, '', window.location.pathname)

    const rankNum = rank === null ? NaN : Number(rank)
    const rankValid = Number.isInteger(rankNum) && rankNum >= 0 && rankNum <= 4

    if (!accessToken || !username || !rankValid || !userId) {
      void navigate({ to: '/login', search: { error: 'discord' } })
      return
    }

    login(
      { userId, username, rank: rankNum as UserRank },
      accessToken,
    )
    void navigate({ to: rankNum >= UserRank.Member ? '/dashboard' : '/tickets' })
  }, [login, navigate])

  return (
    <div className="min-h-screen bg-background flex items-center justify-center">
      <p className="text-muted-foreground">Входим…</p>
    </div>
  )
}
