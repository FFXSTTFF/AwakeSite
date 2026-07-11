import { createFileRoute, redirect } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { usersApi } from '@/api/users'
import { UserRank } from '@/types/api'
import { useAuthStore } from '@/store/authStore'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/_auth/manage/users')({
  beforeLoad: () => {
    if ((useAuthStore.getState().user?.rank ?? 0) < UserRank.Colonel) {
      throw redirect({ to: '/dashboard' })
    }
  },
  component: ManageUsersPage,
})

const ALL_RANKS = [UserRank.Guest, UserRank.Member, UserRank.Officer, UserRank.Colonel, UserRank.Leader]

const RANK_CLASSES: Record<number, string> = {
  [UserRank.Guest]: 'bg-secondary text-muted-foreground border-border',
  [UserRank.Member]: 'bg-blue-400/10 text-blue-400 border-blue-400/30',
  [UserRank.Officer]: 'bg-accent/10 text-accent border-accent/30',
  [UserRank.Colonel]: 'bg-yellow-400/10 text-yellow-400 border-yellow-400/30',
  [UserRank.Leader]: 'bg-destructive/10 text-destructive border-destructive/30',
}

function ManageUsersPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)
  const [editingId, setEditingId] = useState<string | null>(null)

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.getAll(),
  })

  const updateRank = useMutation({
    mutationFn: ({ userId, rank }: { userId: string; rank: number }) =>
      usersApi.updateRank(userId, rank),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      setEditingId(null)
    },
  })

  if (isLoading) {
    return <div className="text-muted-foreground">{t('common.loading')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-foreground mb-6">{t('users.title')}</h1>
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('users.username')}</TableHead>
                <TableHead>{t('users.rank')}</TableHead>
                <TableHead>{t('users.email')}</TableHead>
                <TableHead className="text-right" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {users?.map((user) => (
                <TableRow key={user.id}>
                  <TableCell className="font-medium text-foreground">{user.username}</TableCell>
                  <TableCell>
                    {editingId === user.id ? (
                      <Select
                        defaultValue={user.rank.toString()}
                        onValueChange={(v) => updateRank.mutate({ userId: user.id, rank: Number(v) })}
                      >
                        <SelectTrigger className="w-36 h-7 text-sm">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {ALL_RANKS
                            .filter((r) => r !== UserRank.Leader || currentUser?.rank === UserRank.Leader)
                            .map((r) => (
                              <SelectItem key={r} value={r.toString()}>
                                {t(`users.ranks.${r}`)}
                              </SelectItem>
                            ))}
                        </SelectContent>
                      </Select>
                    ) : (
                      <Badge className={cn('text-xs border', RANK_CLASSES[user.rank])}>
                        {t(`users.ranks.${user.rank}`)}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">{user.email ?? '—'}</TableCell>
                  <TableCell className="text-right">
                    {user.id !== currentUser?.userId && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setEditingId(editingId === user.id ? null : user.id)}
                        className="h-7 text-xs text-accent hover:text-accent hover:bg-accent/10"
                      >
                        {editingId === user.id ? t('common.cancel') : t('users.changeRank')}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
