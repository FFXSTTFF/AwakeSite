import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
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
    return <div className="text-text-muted">{t('common.loading')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('squads.title')}</h1>
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
              className="group block bg-bg-card border border-border rounded-xl p-5 hover:border-accent/30 hover:shadow-[0_0_20px_rgba(61,220,132,0.06)] transition-all duration-200"
            >
              {/* Header */}
              <div className="flex items-start justify-between mb-4">
                <h2 className="text-base font-semibold text-text-primary group-hover:text-accent transition-colors">
                  {squad.name}
                </h2>
                <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${
                  isFull
                    ? 'text-red-400 border-red-400/30 bg-red-400/5'
                    : 'text-accent border-accent/30 bg-accent/5'
                }`}>
                  {t('squads.memberCount', { count: squad.memberCount })}
                </span>
              </div>

              {/* Capacity bar */}
              <div className="mb-4">
                <div className="h-1 bg-bg-hover rounded-full overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all ${isFull ? 'bg-red-400/70' : 'bg-accent/70'}`}
                    style={{ width: `${pct}%` }}
                  />
                </div>
              </div>

              {/* Members */}
              <div className="space-y-2">
                {leader && (
                  <div className="flex items-center gap-2">
                    <Crown size={12} className="text-yellow-400 shrink-0" />
                    <span className="text-sm text-text-primary font-medium">{leader.username}</span>
                    {leader.gameNickname && (
                      <span className="text-xs text-text-muted truncate">· {leader.gameNickname}</span>
                    )}
                  </div>
                )}
                {others.slice(0, leader ? 2 : 3).map((m) => (
                  <div key={m.userId} className="flex items-center gap-2 pl-[20px]">
                    <span className="text-sm text-text-muted">{m.username}</span>
                    {m.gameNickname && (
                      <span className="text-xs text-text-muted truncate">· {m.gameNickname}</span>
                    )}
                  </div>
                ))}
                {squad.memberCount > (leader ? 3 : 3) && (
                  <div className="text-xs text-text-muted pl-[20px]">
                    {t('squads.more', { count: squad.memberCount - (leader ? 3 : 3) })}
                  </div>
                )}
                {squad.memberCount === 0 && (
                  <div className="text-sm text-text-muted">{t('squads.noMembers')}</div>
                )}
              </div>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
