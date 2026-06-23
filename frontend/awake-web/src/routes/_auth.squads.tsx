import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { Crown } from 'lucide-react'

export const Route = createFileRoute('/_auth/squads')({
  component: SquadsPage,
})

function SquadsPage() {
  const { t } = useTranslation()
  const { data: squads, isLoading } = useQuery({
    queryKey: ['squads'],
    queryFn: () => squadsApi.getAll(),
  })

  if (isLoading) {
    return <div className="text-muted-foreground">{t('common.loading')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-foreground mb-6">{t('squads.title')}</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {squads?.map((squad) => {
          const leader = squad.members.find((m) => m.isLeader)
          const others = squad.members.filter((m) => !m.isLeader)
          const pct = (squad.memberCount / 5) * 100
          const isFull = squad.memberCount >= 5

          return (
            <Link
              key={squad.id}
              to="/squads/$squadId"
              params={{ squadId: squad.id }}
              className="group block"
            >
              <Card className="h-full transition-all duration-200 group-hover:border-accent/30 group-hover:shadow-[0_0_20px_rgba(61,220,132,0.06)]">
                <CardContent className="pt-5 pb-5">
                  {/* Header */}
                  <div className="flex items-start justify-between mb-4">
                    <h2 className="text-base font-semibold text-foreground group-hover:text-accent transition-colors">
                      {squad.name}
                    </h2>
                    <Badge
                      className={cn(
                        'text-xs font-medium border shrink-0',
                        isFull
                          ? 'bg-destructive/10 text-destructive border-destructive/30'
                          : 'bg-accent/10 text-accent border-accent/30',
                      )}
                    >
                      {t('squads.memberCount', { count: squad.memberCount })}
                    </Badge>
                  </div>

                  {/* Capacity bar */}
                  <div className="mb-4">
                    <div className="h-1 bg-secondary rounded-full overflow-hidden">
                      <div
                        className={cn('h-full rounded-full transition-all', isFull ? 'bg-destructive/60' : 'bg-accent/70')}
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                  </div>

                  {/* Members */}
                  <div className="space-y-2">
                    {leader && (
                      <div className="flex items-center gap-2">
                        <Crown size={12} className="text-yellow-400 shrink-0" />
                        <span className="text-sm text-foreground font-medium">{leader.username}</span>
                        {leader.gameNickname && (
                          <span className="text-xs text-muted-foreground truncate">· {leader.gameNickname}</span>
                        )}
                      </div>
                    )}
                    {others.slice(0, leader ? 2 : 3).map((m) => (
                      <div key={m.userId} className="flex items-center gap-2 pl-5">
                        <span className="text-sm text-muted-foreground">{m.username}</span>
                        {m.gameNickname && (
                          <span className="text-xs text-muted-foreground truncate">· {m.gameNickname}</span>
                        )}
                      </div>
                    ))}
                    {squad.memberCount > (leader ? 3 : 3) && (
                      <div className="text-xs text-muted-foreground pl-5">
                        {t('squads.more', { count: squad.memberCount - (leader ? 3 : 3) })}
                      </div>
                    )}
                    {squad.memberCount === 0 && (
                      <div className="text-sm text-muted-foreground">{t('squads.noMembers')}</div>
                    )}
                  </div>
                </CardContent>
              </Card>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
