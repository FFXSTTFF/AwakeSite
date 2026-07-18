import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { UserRank, type BoostItem } from '@/types/api'

export const Route = createFileRoute('/_auth/boosts')({
  component: BoostsPage,
})

function BoostsPage() {
  const { t } = useTranslation()
  const { rank } = useAuth()

  const { data: entries = [], isLoading, isError } = useQuery({
    queryKey: ['boosts', 'summary'],
    queryFn: boostsApi.summary,
    enabled: rank >= UserRank.Member,
  })

  if (rank < UserRank.Member) return <Navigate to="/profile" />
  if (isLoading) return <p className="text-muted-foreground">{t('common.loading')}</p>
  if (isError) return <p className="text-destructive">{t('boosts.title')}: {t('auth.errors.networkError')}</p>

  // Итого: сколько игроков отметили каждый предмет
  const totals = new Map<string, { item: BoostItem; count: number }>()
  for (const entry of entries) {
    for (const b of entry.boosts) {
      const existing = totals.get(b.itemId)
      if (existing) existing.count += 1
      else totals.set(b.itemId, { item: b, count: 1 })
    }
  }
  const totalRows = [...totals.values()].sort((a, b) => b.count - a.count)

  return (
    <div>
      <h1 className="mb-6 text-xl font-semibold text-foreground">{t('boosts.title')}</h1>

      {entries.length === 0 ? (
        <Card>
          <CardContent className="pt-5 pb-5 text-center">
            <p className="text-sm text-muted-foreground">{t('boosts.empty')}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t('boosts.emptyHint')}</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          <Card>
            <CardContent className="pt-5 pb-5">
              <h2 className="mb-3 text-sm font-semibold text-foreground">{t('boosts.totals')}</h2>
              <div className="space-y-2">
                {totalRows.map(({ item, count }) => (
                  <div key={item.itemId} className="flex items-center gap-3">
                    {item.icon && (
                      <img
                        src={item.icon}
                        alt=""
                        className="h-6 w-6 shrink-0 object-contain"
                        onError={(e) => (e.currentTarget.style.display = 'none')}
                      />
                    )}
                    <span className="flex-1 text-sm text-foreground">{item.name}</span>
                    <span className="text-xs text-muted-foreground">{t(`boosts.types.${item.boostType}`)}</span>
                    <span className="w-10 text-right text-sm font-semibold text-accent">× {count}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-5 pb-5">
              <h2 className="mb-3 text-sm font-semibold text-foreground">{t('boosts.player')}</h2>
              <div className="space-y-3">
                {entries.map((entry) => (
                  <div key={entry.userId} className="flex flex-col gap-1.5 sm:flex-row sm:items-center sm:gap-4">
                    <Link
                      to="/players/$userId"
                      params={{ userId: entry.userId }}
                      className="w-40 shrink-0 text-sm font-medium text-foreground transition-colors hover:text-accent"
                    >
                      {entry.gameNickname ?? entry.username}
                    </Link>
                    <BoostChips items={entry.boosts} short />
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}
