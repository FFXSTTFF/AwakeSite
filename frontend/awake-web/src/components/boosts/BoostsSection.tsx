import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import type { BoostType } from '@/types/api'

export function BoostsSection() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: selected = [] } = useQuery({
    queryKey: ['boosts', 'my'],
    queryFn: boostsApi.getMy,
  })

  const mutation = useMutation({
    mutationFn: boostsApi.setMy,
    onMutate: async (next: BoostType[]) => {
      await queryClient.cancelQueries({ queryKey: ['boosts', 'my'] })
      const prev = queryClient.getQueryData<BoostType[]>(['boosts', 'my'])
      queryClient.setQueryData(['boosts', 'my'], next)
      return { prev }
    },
    onError: (_err, _next, ctx) => {
      queryClient.setQueryData(['boosts', 'my'], ctx?.prev ?? [])
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['boosts'] })
      void queryClient.invalidateQueries({ queryKey: ['squads'] })
      void queryClient.invalidateQueries({ queryKey: ['players'] })
    },
  })

  function toggle(type: BoostType) {
    const next = selected.includes(type)
      ? selected.filter((x) => x !== type)
      : [...selected, type]
    mutation.mutate(next)
  }

  return (
    <Card className="mt-6">
      <CardContent className="pt-5 pb-5">
        <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{t('boosts.myHint')}</p>
        <div className="mt-4">
          <BoostChips selected={selected} onToggle={toggle} disabled={mutation.isPending} />
        </div>
      </CardContent>
    </Card>
  )
}
