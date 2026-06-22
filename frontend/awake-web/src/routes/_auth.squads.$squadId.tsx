import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
import { RankGuard } from '@/components/layout/RankGuard'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/squads/$squadId')({
  component: SquadDetailPage,
})

function SquadDetailPage() {
  const { t } = useTranslation()
  const { squadId } = Route.useParams()
  const queryClient = useQueryClient()

  const { data: squad, isLoading } = useQuery({
    queryKey: ['squads', squadId],
    queryFn: () => squadsApi.getById(squadId),
  })

  const removeMember = useMutation({
    mutationFn: (userId: string) => squadsApi.removeMember(squadId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['squads'] }),
  })

  const setLeader = useMutation({
    mutationFn: (userId: string) => squadsApi.setLeader(squadId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['squads'] }),
  })

  if (isLoading) {
    return <div className="text-text-muted">{t('common.loading')}</div>
  }

  if (!squad) {
    return <div className="text-text-muted">{t('common.notFound')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-text-primary mb-2">{squad.name}</h1>
      <p className="text-text-muted mb-6">{t('squads.memberCount', { count: squad.memberCount })}</p>

      <div className="bg-bg-card border border-border rounded-lg overflow-hidden">
        {squad.members.length === 0 ? (
          <div className="p-6 text-text-muted text-center">{t('squads.noMembers')}</div>
        ) : (
          <table className="w-full">
            <thead>
              <tr className="border-b border-border">
                <th className="text-left px-4 py-3 text-sm text-text-muted font-medium">
                  {t('users.username')}
                </th>
                <th className="text-left px-4 py-3 text-sm text-text-muted font-medium">
                  {t('squads.joinedAt')}
                </th>
                <RankGuard min={UserRank.Colonel}>
                  <th className="px-4 py-3" />
                </RankGuard>
              </tr>
            </thead>
            <tbody>
              {squad.members.map((member) => (
                <tr key={member.userId} className="border-b border-border last:border-0 hover:bg-bg-hover">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      {member.isLeader && <span className="text-accent text-sm">★</span>}
                      <span className="text-text-primary">{member.username}</span>
                      {member.gameNickname && (
                        <span className="text-text-muted text-sm">({member.gameNickname})</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-text-muted text-sm">
                    {new Date(member.joinedAt).toLocaleDateString('ru-RU')}
                  </td>
                  <RankGuard min={UserRank.Colonel}>
                    <td className="px-4 py-3">
                      <div className="flex gap-2 justify-end">
                        {!member.isLeader && (
                          <button
                            onClick={() => setLeader.mutate(member.userId)}
                            disabled={setLeader.isPending}
                            className="text-xs text-accent hover:text-text-primary px-2 py-1 rounded border border-accent/30 hover:border-accent transition-colors disabled:opacity-50"
                          >
                            {t('squads.setLeader')}
                          </button>
                        )}
                        <button
                          onClick={() => removeMember.mutate(member.userId)}
                          disabled={removeMember.isPending}
                          className="text-xs text-red-400 hover:text-red-300 px-2 py-1 rounded border border-red-400/30 hover:border-red-300 transition-colors disabled:opacity-50"
                        >
                          {t('squads.removeMember')}
                        </button>
                      </div>
                    </td>
                  </RankGuard>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
