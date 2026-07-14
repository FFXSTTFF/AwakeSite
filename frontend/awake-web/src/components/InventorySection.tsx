import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, Upload } from 'lucide-react'
import { inventoryApi } from '@/api/inventory'
import { ItemCombobox } from '@/components/ItemCombobox'
import { InventoryFlags } from '@/components/InventoryFlags'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuthStore } from '@/store/authStore'
import { BuildType } from '@/types/api'
import type { LoadoutSlot } from '@/types/api'
import { cn } from '@/lib/utils'

const PROOF_SLOTS = [
  { type: BuildType.Speed, title: 'Сборка на скорость', flagKey: 'speed' as const },
  { type: BuildType.Vitality, title: 'Сборка на живучесть', flagKey: 'vitality' as const },
]

export function InventorySection() {
  const queryClient = useQueryClient()
  const userId = useAuthStore((s) => s.user?.userId)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['inventory', 'my'],
    queryFn: inventoryApi.getMy,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['inventory', 'my'] })
  const onError = (e: Error) => setError(e.message)

  const addItem = useMutation({
    mutationFn: (itemId: string) => inventoryApi.addItem(itemId),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const removeItem = useMutation({
    mutationFn: (itemId: string) => inventoryApi.removeItem(itemId),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const uploadProof = useMutation({
    mutationFn: ({ type, file }: { type: BuildType; file: File }) =>
      inventoryApi.uploadProof(type, file),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const deleteProof = useMutation({
    mutationFn: (type: BuildType) => inventoryApi.deleteMyProof(type),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })

  if (isLoading || !data) {
    return <Skeleton className="mt-6 h-64 w-full rounded-xl" />
  }

  return (
    <Card className="mt-6">
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="text-sm font-medium">Инвентарь и сборки</CardTitle>
            <CardDescription>
              Отметь, что у тебя есть — офицеры видят эти флаги при сборе отрядов
            </CardDescription>
          </div>
          <InventoryFlags flags={data.flags} />
        </div>
      </CardHeader>
      <CardContent className="space-y-5 pt-2">
        {error && <p className="text-sm text-destructive">{error}</p>}

        {/* Добавление предмета: броня или оружие */}
        <div className="grid gap-3 sm:grid-cols-2">
          <ItemCombobox
            categoryPrefix="armor/"
            placeholder="Добавить броню…"
            value={null}
            onChange={(item: LoadoutSlot | null) => item && addItem.mutate(item.itemId)}
          />
          <ItemCombobox
            categoryPrefix="weapon/"
            placeholder="Добавить оружие…"
            value={null}
            onChange={(item: LoadoutSlot | null) => item && addItem.mutate(item.itemId)}
          />
        </div>

        {/* Список предметов */}
        {data.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Инвентарь пуст — добавь свою броню и оружие.
          </p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {data.items.map((item) => (
              <li
                key={item.itemId}
                className={cn(
                  'inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/50 py-1 pl-2 pr-1 text-sm',
                  item.unknown && 'opacity-60',
                )}
              >
                {item.icon && (
                  <img src={item.icon} alt="" className="h-5 w-5 object-contain" />
                )}
                <span className="max-w-[180px] truncate">{item.name}</span>
                <button
                  type="button"
                  aria-label={`Убрать ${item.name}`}
                  onClick={() => removeItem.mutate(item.itemId)}
                  className="rounded p-1 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                >
                  <Trash2 size={13} />
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* Пруфы сборок */}
        <div className="grid gap-3 sm:grid-cols-2">
          {PROOF_SLOTS.map((slot) => (
            <ProofSlot
              key={slot.type}
              title={slot.title}
              uploaded={data.flags[slot.flagKey]}
              uploading={uploadProof.isPending}
              userId={userId}
              type={slot.type}
              onUpload={(file) => uploadProof.mutate({ type: slot.type, file })}
              onDelete={() => deleteProof.mutate(slot.type)}
            />
          ))}
        </div>
      </CardContent>
    </Card>
  )
}

function ProofSlot({
  title,
  uploaded,
  uploading,
  userId,
  type,
  onUpload,
  onDelete,
}: {
  title: string
  uploaded: boolean
  uploading: boolean
  userId: string | undefined
  type: BuildType
  onUpload: (file: File) => void
  onDelete: () => void
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [preview, setPreview] = useState<string | null>(null)

  async function showProof() {
    if (!userId) return
    const blob = await inventoryApi.proofImageBlob(userId, type)
    setPreview(URL.createObjectURL(blob))
  }

  return (
    <div className="rounded-lg border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm font-medium">{title}</p>
        {uploaded ? (
          <span className="text-xs text-accent">пруф загружен</span>
        ) : (
          <span className="text-xs text-muted-foreground">нет пруфа</span>
        )}
      </div>
      <div className="mt-2 flex flex-wrap gap-2">
        <input
          ref={inputRef}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) onUpload(file)
            e.target.value = ''
          }}
        />
        <Button
          variant="outline"
          size="sm"
          className="gap-2"
          disabled={uploading}
          onClick={() => inputRef.current?.click()}
        >
          <Upload size={14} />
          {uploaded ? 'Заменить скрин' : 'Загрузить скрин'}
        </Button>
        {uploaded && (
          <>
            <Button variant="outline" size="sm" onClick={() => void showProof()}>
              Посмотреть
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={onDelete}
            >
              Удалить
            </Button>
          </>
        )}
      </div>
      {preview && (
        <button
          type="button"
          className="mt-3 block w-full"
          onClick={() => setPreview(null)}
          aria-label="Закрыть превью"
        >
          <img src={preview} alt={title} className="max-h-64 w-full rounded-md object-contain" />
        </button>
      )}
    </div>
  )
}
