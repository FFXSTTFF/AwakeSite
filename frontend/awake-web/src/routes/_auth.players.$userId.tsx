import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { playersApi } from '@/api/players'
import { ApiError } from '@/api/client'
import { PlayerProfileView } from '@/components/PlayerProfileView'
import { useAuth } from '@/hooks/useAuth'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/players/$userId')({
  component: PlayerPage,
})

function PlayerPage() {
  const { userId } = Route.useParams()
  const { rank, user } = useAuth()

  const { data: profile, isLoading, error } = useQuery({
    queryKey: ['players', userId],
    queryFn: () => playersApi.getProfile(userId),
    retry: false,
  })

  // Гость может смотреть только свой профиль
  if (rank < UserRank.Member && user?.userId !== userId) {
    return <Navigate to="/profile" />
  }
  if (user?.userId === userId) {
    return <Navigate to="/profile" />
  }

  if (isLoading) return <p className="text-muted-foreground">Загрузка…</p>
  if (error instanceof ApiError && error.status === 403) return <Navigate to="/profile" />
  if (error || !profile) return <p className="text-destructive">Не удалось загрузить профиль.</p>

  return <PlayerProfileView profile={profile} />
}
