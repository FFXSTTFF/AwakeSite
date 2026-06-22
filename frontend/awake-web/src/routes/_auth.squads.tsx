import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'

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
    return (
      <div className="flex justify-center py-12">
        <div className="text-text-muted">{t('common.loading')}</div>
      </div>
    )
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('squads.title')}</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {squads?.map((squad) => (
          <Link
            key={squad.id}
            to="/squads/$squadId"
            params={{ squadId: squad.id }}
            className="block bg-bg-card border border-border rounded-lg p-4 hover:bg-bg-hover transition-colors"
          >
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-lg font-semibold text-text-primary">{squad.name}</h2>
              <span className="text-xs text-text-muted bg-bg-page px-2 py-1 rounded">
                {t('squads.memberCount', { count: squad.memberCount })}
              </span>
            </div>
            <div className="space-y-1">
              {squad.members.slice(0, 3).map((member) => (
                <div key={member.userId} className="flex items-center gap-2">
                  {member.isLeader && (
                    <span className="text-accent text-xs font-medium">★</span>
                  )}
                  <span className="text-sm text-text-primary">{member.username}</span>
                  {member.gameNickname && (
                    <span className="text-xs text-text-muted">({member.gameNickname})</span>
                  )}
                </div>
              ))}
              {squad.memberCount > 3 && (
                <div className="text-xs text-text-muted">+{squad.memberCount - 3} ещё</div>
              )}
              {squad.memberCount === 0 && (
                <div className="text-sm text-text-muted">{t('squads.noMembers')}</div>
              )}
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
