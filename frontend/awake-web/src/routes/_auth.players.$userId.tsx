import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { playersApi } from '@/api/players'
import { ApiError } from '@/api/client'
import { inventoryApi } from '@/api/inventory'
import { PlayerProfileSkeleton, PlayerProfileView } from '@/components/PlayerProfileView'
import { InventoryFlags } from '@/components/InventoryFlags'
import { ProofModeration } from '@/components/ProofModeration'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/players/$userId')({
  component: PlayerPage,
})

function PlayerPage() {
  const { userId } = Route.useParams()
  const { rank, user } = useAuth()
  const { t } = useTranslation()

  const { data: profile, isLoading, error } = useQuery({
    queryKey: ['players', userId],
    queryFn: () => playersApi.getProfile(userId),
    retry: false,
  })

  const { data: inventory } = useQuery({
    queryKey: ['inventory', userId],
    queryFn: () => inventoryApi.getFor(userId),
    retry: false,
  })

  // Гость может смотреть только свой профиль
  if (rank < UserRank.Member && user?.userId !== userId) {
    return <Navigate to="/profile" />
  }
  if (user?.userId === userId) {
    return <Navigate to="/profile" />
  }

  if (isLoading) return <PlayerProfileSkeleton />
  if (error instanceof ApiError && error.status === 403) return <Navigate to="/profile" />
  if (error || !profile) return <p className="text-destructive">Не удалось загрузить профиль.</p>

  return (
    <>
      <PlayerProfileView
        profile={profile}
        flagsSlot={
          inventory ? (
            <div className="flex flex-col items-end gap-2">
              <InventoryFlags flags={inventory.flags} size="sm" />
              {rank >= UserRank.Officer && <ProofModeration userId={userId} flags={inventory.flags} />}
            </div>
          ) : null
        }
      />
      {profile.boosts.length > 0 && (
        <Card className="mt-6">
          <CardContent className="pt-5 pb-5">
            <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
            <div className="mt-4">
              <BoostChips items={profile.boosts} />
            </div>
          </CardContent>
        </Card>
      )}
    </>
  )
}
