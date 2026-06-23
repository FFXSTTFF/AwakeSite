import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
import { RankGuard } from '@/components/layout/RankGuard'
import { UserRank } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Crown } from 'lucide-react'

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
    return <div className="text-muted-foreground">{t('common.loading')}</div>
  }

  if (!squad) {
    return <div className="text-muted-foreground">{t('common.notFound')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-foreground mb-1">{squad.name}</h1>
      <p className="text-muted-foreground text-sm mb-6">{t('squads.memberCount', { count: squad.memberCount })}</p>

      <Card>
        <CardContent className="p-0">
          {squad.members.length === 0 ? (
            <div className="p-6 text-muted-foreground text-center">{t('squads.noMembers')}</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('users.username')}</TableHead>
                  <TableHead>{t('squads.joinedAt')}</TableHead>
                  <RankGuard min={UserRank.Colonel}>
                    <TableHead className="text-right" />
                  </RankGuard>
                </TableRow>
              </TableHeader>
              <TableBody>
                {squad.members.map((member) => (
                  <TableRow key={member.userId}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {member.isLeader && <Crown size={13} className="text-yellow-400 shrink-0" />}
                        <span className={member.isLeader ? 'text-foreground font-medium' : 'text-foreground'}>
                          {member.username}
                        </span>
                        {member.gameNickname && (
                          <span className="text-muted-foreground text-sm">({member.gameNickname})</span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground text-sm">
                      {new Date(member.joinedAt).toLocaleDateString('ru-RU')}
                    </TableCell>
                    <RankGuard min={UserRank.Colonel}>
                      <TableCell className="text-right">
                        <div className="flex gap-2 justify-end">
                          {!member.isLeader && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => setLeader.mutate(member.userId)}
                              disabled={setLeader.isPending}
                              className="h-7 text-xs border-accent/30 text-accent hover:text-accent hover:bg-accent/10"
                            >
                              {t('squads.setLeader')}
                            </Button>
                          )}
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => removeMember.mutate(member.userId)}
                            disabled={removeMember.isPending}
                            className="h-7 text-xs border-destructive/30 text-destructive hover:text-destructive hover:bg-destructive/10"
                          >
                            {t('squads.removeMember')}
                          </Button>
                        </div>
                      </TableCell>
                    </RankGuard>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
