import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { playersApi } from '@/api/players'
import { PlayerProfileSkeleton, PlayerProfileView } from '@/components/PlayerProfileView'
import { InventorySection } from '@/components/InventorySection'

export const Route = createFileRoute('/_auth/profile')({
  component: ProfilePage,
})

function ProfilePage() {
  const queryClient = useQueryClient()
  const [refreshing, setRefreshing] = useState(false)

  const { data: profile, isLoading, error } = useQuery({
    queryKey: ['players', 'me'],
    queryFn: playersApi.getMyProfile,
  })

  async function handleRefresh() {
    setRefreshing(true)
    try {
      await playersApi.refreshMyStats()
    } catch {
      // 429 (кулдаун) / 400 — кнопку всё равно держим заблокированной,
      // иначе её можно кликать без остановки
    }
    // Обновление идёт в фоне 15–30 c — перезапрашиваем профиль с задержкой
    setTimeout(() => {
      void queryClient.invalidateQueries({ queryKey: ['players', 'me'] })
      setRefreshing(false)
    }, 30_000)
  }

  if (isLoading) return <PlayerProfileSkeleton />
  if (error || !profile) return <p className="text-destructive">Не удалось загрузить профиль.</p>

  return (
    <>
      <PlayerProfileView profile={profile} onRefresh={handleRefresh} refreshing={refreshing} />
      <InventorySection />
    </>
  )
}
