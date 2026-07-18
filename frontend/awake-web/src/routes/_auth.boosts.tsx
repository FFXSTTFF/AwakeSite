import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Check, Minus } from 'lucide-react'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { ALL_BOOST_TYPES, UserRank } from '@/types/api'

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

  const counts = new Map(
    ALL_BOOST_TYPES.map((type) => [
      type,
      entries.filter((e) => e.boostTypes.includes(type)).length,
    ]),
  )

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
        <>
          {/* Десктоп: таблица игроки × типы */}
          <Card className="hidden md:block">
            <CardContent className="pt-5 pb-5">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border text-left">
                      <th className="py-2 pr-4 text-xs font-medium text-muted-foreground">
                        {t('boosts.player')}
                      </th>
                      {ALL_BOOST_TYPES.map((type) => (
                        <th key={type} className="px-3 py-2 text-center text-xs font-medium text-muted-foreground">
                          <div>{t(`boosts.typesShort.${type}`)}</div>
                          <div className="mt-0.5 font-semibold text-accent">{counts.get(type)}</div>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {entries.map((entry) => (
                      <tr key={entry.userId} className="border-b border-border/50 last:border-0">
                        <td className="py-2.5 pr-4">
                          <Link
                            to="/players/$userId"
                            params={{ userId: entry.userId }}
                            className="font-medium text-foreground transition-colors hover:text-accent"
                          >
                            {entry.gameNickname ?? entry.username}
                          </Link>
                        </td>
                        {ALL_BOOST_TYPES.map((type) => (
                          <td key={type} className="px-3 py-2.5 text-center">
                            {entry.boostTypes.includes(type) ? (
                              <Check size={15} className="inline text-accent" />
                            ) : (
                              <Minus size={15} className="inline text-muted-foreground/40" />
                            )}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>

          {/* Мобила: карточки */}
          <div className="space-y-2 md:hidden">
            {entries.map((entry) => (
              <Card key={entry.userId}>
                <CardContent className="pt-4 pb-4">
                  <Link
                    to="/players/$userId"
                    params={{ userId: entry.userId }}
                    className="text-sm font-medium text-foreground"
                  >
                    {entry.gameNickname ?? entry.username}
                  </Link>
                  <div className="mt-2">
                    <BoostChips selected={entry.boostTypes} short />
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
