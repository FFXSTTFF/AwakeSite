import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostSlotPicker } from '@/components/boosts/BoostSlotPicker'
import { Card, CardContent } from '@/components/ui/card'
import { ALL_BOOST_TYPES, type BoostItem, type BoostType, type ItemSearchResult } from '@/types/api'

export function BoostsSection() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: selected = [] } = useQuery({
    queryKey: ['boosts', 'my'],
    queryFn: boostsApi.getMy,
  })

  const mutation = useMutation({
    mutationFn: (next: BoostItem[]) =>
      boostsApi.setMy(next.map((b) => ({ boostType: b.boostType, itemId: b.itemId }))),
    onMutate: async (next: BoostItem[]) => {
      await queryClient.cancelQueries({ queryKey: ['boosts', 'my'] })
      const prev = queryClient.getQueryData<BoostItem[]>(['boosts', 'my'])
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

  function setSlot(type: BoostType, item: ItemSearchResult | null) {
    const rest = selected.filter((b) => b.boostType !== type)
    const next = item
      ? [...rest, { boostType: type, itemId: item.id, name: item.nameRu, icon: item.icon }]
      : rest
    mutation.mutate(next)
  }

  return (
    <Card className="mt-6">
      <CardContent className="pt-5 pb-5">
        <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{t('boosts.myHint')}</p>
        <div className="mt-4 space-y-3">
          {ALL_BOOST_TYPES.map((type) => (
            <div key={type} className="grid grid-cols-[10rem_1fr] items-center gap-3">
              <span className="text-sm text-muted-foreground">{t(`boosts.types.${type}`)}</span>
              <BoostSlotPicker
                boostType={type}
                value={selected.find((b) => b.boostType === type) ?? null}
                onSelect={(item) => setSlot(type, item)}
                onClear={() => setSlot(type, null)}
                disabled={mutation.isPending}
              />
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
