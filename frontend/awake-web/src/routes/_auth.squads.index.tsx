import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { SquadCard } from '@/components/squads/SquadCard'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import { Shield, Wrench } from 'lucide-react'

export const Route = createFileRoute('/_auth/squads/')({
  component: SquadsPage,
})

function SquadsPage() {
  const { t } = useTranslation()
  const rank = useAuthStore((s) => s.user?.rank ?? 0)
  const { data: squads, isLoading } = useQuery({
    queryKey: ['squads'],
    queryFn: () => squadsApi.getAll(),
  })

  if (isLoading) {
    return (
      <div>
        <h1 className="mb-6 text-2xl font-black tracking-tight text-foreground">{t('squads.title')}</h1>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-black tracking-tight text-foreground">{t('squads.title')}</h1>
        {rank >= UserRank.Officer && (
          <Button asChild variant="outline" className="gap-2">
            <Link to="/squads/builder">
              <Wrench size={15} />
              Собрать отряды
            </Link>
          </Button>
        )}
      </div>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        {squads?.map((squad) => (
          <SquadCard key={squad.id} squad={squad} canRename={rank >= UserRank.Officer} />
        ))}
      </div>
      {!squads?.length && (
        <div className="rounded-xl border border-border bg-card py-16 text-center">
          <div className="mx-auto mb-3 flex h-11 w-11 items-center justify-center rounded-lg bg-accent/10">
            <Shield size={20} className="text-accent" />
          </div>
          <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
        </div>
      )}
    </div>
  )
}
