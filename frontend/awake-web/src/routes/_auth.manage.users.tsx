import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { usersApi } from '@/api/users'
import { UserRank } from '@/types/api'
import { useAuthStore } from '@/store/authStore'

export const Route = createFileRoute('/_auth/manage/users')({
  component: ManageUsersPage,
})

const RANK_NAMES: Record<number, string> = {
  0: 'Гость',
  1: 'Участник',
  2: 'Офицер',
  3: 'Полковник',
  4: 'Лидер',
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
    return <div className="text-text-muted">{t('common.loading')}</div>
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('users.title')}</h1>
      <div className="bg-bg-card border border-border rounded-lg overflow-hidden">
        <table className="w-full">
          <thead>
            <tr className="border-b border-border">
              <th className="text-left px-4 py-3 text-sm text-text-muted font-medium">{t('users.username')}</th>
              <th className="text-left px-4 py-3 text-sm text-text-muted font-medium">{t('users.rank')}</th>
              <th className="text-left px-4 py-3 text-sm text-text-muted font-medium">{t('users.email')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {users?.map((user) => (
              <tr key={user.id} className="border-b border-border last:border-0 hover:bg-bg-hover">
                <td className="px-4 py-3 text-text-primary">{user.username}</td>
                <td className="px-4 py-3">
                  {editingId === user.id ? (
                    <select
                      defaultValue={user.rank}
                      className="bg-bg-page border border-border text-text-primary rounded px-2 py-1 text-sm"
                      onChange={(e) => {
                        const newRank = Number(e.target.value)
                        updateRank.mutate({ userId: user.id, rank: newRank })
                      }}
                    >
                      {Object.entries(RANK_NAMES)
                        .filter(([rank]) => {
                          const r = Number(rank)
                          if (r === UserRank.Leader && currentUser?.rank !== UserRank.Leader) return false
                          return true
                        })
                        .map(([rank, name]) => (
                          <option key={rank} value={rank}>{name}</option>
                        ))}
                    </select>
                  ) : (
                    <span className="text-text-primary text-sm">{RANK_NAMES[user.rank] ?? user.rank}</span>
                  )}
                </td>
                <td className="px-4 py-3 text-text-muted text-sm">{user.email ?? '—'}</td>
                <td className="px-4 py-3">
                  {user.id !== currentUser?.userId && (
                    <button
                      onClick={() => setEditingId(editingId === user.id ? null : user.id)}
                      className="text-xs text-accent hover:text-text-primary px-2 py-1 rounded border border-accent/30 hover:border-accent transition-colors"
                    >
                      {t('users.changeRank')}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
