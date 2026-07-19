import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { inventoryApi } from '@/api/inventory'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { InventoryItem, Loadout } from '@/types/api'

type SlotKey = 'sniper' | 'weapon' | 'armor'

interface SlotDraft {
  itemId: string // '' — слот пуст
  upgrade: number
}

const SLOTS: { key: SlotKey; label: string; optional: boolean }[] = [
  { key: 'sniper', label: 'Снайперка', optional: true },
  { key: 'weapon', label: 'Основное оружие', optional: false },
  { key: 'armor', label: 'Броня', optional: false },
]

function itemsForSlot(items: InventoryItem[], key: SlotKey): InventoryItem[] {
  const usable = items.filter((i) => !i.unknown && i.category)
  if (key === 'armor') return usable.filter((i) => i.category!.startsWith('armor/'))
  if (key === 'sniper') return usable.filter((i) => i.category === 'weapon/sniper_rifle')
  return usable.filter(
    (i) => i.category!.startsWith('weapon') && i.category !== 'weapon/sniper_rifle',
  )
}

export function LoadoutCard({ loadout, editable }: { loadout: Loadout | null; editable?: boolean }) {
  const [editing, setEditing] = useState(false)

  if (!loadout && !editable) return null

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Экипировка</CardTitle>
        {editable && !editing && (
          <Button size="sm" variant="outline" className="gap-2" onClick={() => setEditing(true)}>
            <Pencil size={13} />
            Изменить
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {editing ? (
          <LoadoutEditor loadout={loadout} onClose={() => setEditing(false)} />
        ) : loadout ? (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {loadout.sniper && <LoadoutTile label="Снайперка" slot={loadout.sniper} />}
            <LoadoutTile label="Основное оружие" slot={loadout.weapon} />
            <LoadoutTile label="Броня" slot={loadout.armor} />
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            Экипировка не указана — нажми «Изменить» и выбери предметы из инвентаря.
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function LoadoutEditor({ loadout, onClose }: { loadout: Loadout | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [draft, setDraft] = useState<Record<SlotKey, SlotDraft>>({
    sniper: { itemId: loadout?.sniper?.itemId ?? '', upgrade: loadout?.sniper?.upgrade ?? 0 },
    weapon: { itemId: loadout?.weapon.itemId ?? '', upgrade: loadout?.weapon.upgrade ?? 0 },
    armor: { itemId: loadout?.armor.itemId ?? '', upgrade: loadout?.armor.upgrade ?? 0 },
  })

  const { data: inventory, isLoading } = useQuery({
    queryKey: ['inventory', 'my'],
    queryFn: inventoryApi.getMy,
  })

  // Инвентарь мог измениться с момента сохранения лоадаута — если сохранённый
  // предмет исчез из инвентаря, сбрасываем слот, чтобы select и заточка не
  // расходились с тем, что реально можно сохранить. Санитизация делается один
  // раз на каждую загрузку инвентаря — прямо во время рендера (без эффекта),
  // как рекомендует React для "подстройки состояния под новые данные".
  const [sanitizedFor, setSanitizedFor] = useState<InventoryItem[] | null>(null)
  if (inventory?.items && inventory.items !== sanitizedFor) {
    setSanitizedFor(inventory.items)
    const items = inventory.items
    const cleaned: Partial<Record<SlotKey, SlotDraft>> = {}
    for (const key of Object.keys(draft) as SlotKey[]) {
      const slot = draft[key]
      if (slot.itemId && !itemsForSlot(items, key).some((i) => i.itemId === slot.itemId)) {
        cleaned[key] = { itemId: '', upgrade: 0 }
      }
    }
    if (Object.keys(cleaned).length > 0) {
      setDraft((d) => ({ ...d, ...cleaned }))
    }
  }

  const save = useMutation({
    mutationFn: inventoryApi.updateLoadout,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['players', 'me'] })
      onClose()
    },
    onError: (e: Error) => setError(e.message),
  })

  const items = inventory?.items ?? []
  const canSave = draft.weapon.itemId !== '' && draft.armor.itemId !== ''

  function setSlot(key: SlotKey, patch: Partial<SlotDraft>) {
    setDraft((d) => ({ ...d, [key]: { ...d[key], ...patch } }))
  }

  function handleSave() {
    setError(null)
    save.mutate({
      sniper: draft.sniper.itemId
        ? { itemId: draft.sniper.itemId, upgrade: draft.sniper.upgrade }
        : null,
      weapon: { itemId: draft.weapon.itemId, upgrade: draft.weapon.upgrade },
      armor: { itemId: draft.armor.itemId, upgrade: draft.armor.upgrade },
    })
  }

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Загружаем инвентарь…</p>
  }

  return (
    <div className="space-y-3">
      {SLOTS.map(({ key, label, optional }) => {
        const options = itemsForSlot(items, key)
        return (
          <div key={key} className="grid grid-cols-[1fr_auto] items-end gap-3">
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">
                {label}
                {!optional && ' *'}
              </p>
              {options.length === 0 ? (
                <p className="rounded-lg border border-border px-3 py-2 text-sm text-muted-foreground">
                  Нет подходящих предметов — добавь их в инвентарь ниже.
                </p>
              ) : (
                <select
                  aria-label={label}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-accent/50"
                  value={draft[key].itemId}
                  onChange={(e) => setSlot(key, { itemId: e.target.value })}
                >
                  <option value="">{optional ? '— без снайперки —' : '— выбери предмет —'}</option>
                  {options.map((item) => (
                    <option key={item.itemId} value={item.itemId}>
                      {item.name}
                    </option>
                  ))}
                </select>
              )}
            </div>
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">Заточка</p>
              <input
                type="number"
                aria-label={`Заточка: ${label}`}
                min={0}
                max={15}
                step={1}
                className="w-20 rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-accent/50 disabled:opacity-50"
                value={draft[key].upgrade}
                disabled={draft[key].itemId === ''}
                onChange={(e) =>
                  setSlot(key, {
                    upgrade: Math.max(0, Math.min(15, Math.round(Number(e.target.value) || 0))),
                  })
                }
              />
            </div>
          </div>
        )
      })}

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex gap-2 pt-1">
        <Button size="sm" onClick={handleSave} disabled={!canSave || save.isPending}>
          {save.isPending ? 'Сохраняем…' : 'Сохранить'}
        </Button>
        <Button size="sm" variant="outline" onClick={onClose} disabled={save.isPending}>
          Отмена
        </Button>
      </div>
    </div>
  )
}

function LoadoutTile({ label, slot }: { label: string; slot: { itemName: string; upgrade: number } }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-sm font-medium text-foreground">
        {slot.itemName}
        {slot.upgrade > 0 && <span className="text-accent"> +{slot.upgrade}</span>}
      </p>
    </div>
  )
}
