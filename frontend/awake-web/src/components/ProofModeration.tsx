import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { inventoryApi } from '@/api/inventory'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { BuildType } from '@/types/api'
import type { PlayerFlags } from '@/types/api'

const PROOF_SLOTS = [
  { type: BuildType.Speed, title: 'Пруф: скорость', flagKey: 'speed' as const },
  { type: BuildType.Vitality, title: 'Пруф: живучесть', flagKey: 'vitality' as const },
]

export function ProofModeration({ userId, flags }: { userId: string; flags: PlayerFlags }) {
  const active = PROOF_SLOTS.filter((slot) => flags[slot.flagKey])
  if (active.length === 0) return null

  return (
    <div className="flex flex-col items-end gap-1.5">
      {active.map((slot) => (
        <ProofModerationRow key={slot.type} userId={userId} type={slot.type} title={slot.title} />
      ))}
    </div>
  )
}

function ProofModerationRow({
  userId,
  type,
  title,
}: {
  userId: string
  type: (typeof PROOF_SLOTS)[number]['type']
  title: string
}) {
  const queryClient = useQueryClient()
  const [preview, setPreview] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!preview) return
    return () => URL.revokeObjectURL(preview)
  }, [preview])

  const deleteProof = useMutation({
    mutationFn: () => inventoryApi.deleteProofFor(userId, type),
    onSuccess: () => {
      setError(null)
      setPreview(null)
      void queryClient.invalidateQueries({ queryKey: ['inventory', userId] })
    },
    onError: (e: Error) => {
      // 404 значит пруф уже удалён — просто обновим состояние, не роняем в консоль
      if (e instanceof ApiError && e.status === 404) {
        void queryClient.invalidateQueries({ queryKey: ['inventory', userId] })
        return
      }
      setError('Не удалось удалить пруф')
    },
  })

  async function showProof() {
    setError(null)
    try {
      const blob = await inventoryApi.proofImageBlob(userId, type)
      setPreview(URL.createObjectURL(blob))
    } catch {
      setError('Не удалось загрузить скрин')
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex items-center gap-2 text-xs">
        <span className="text-muted-foreground">{title}</span>
        <Button variant="outline" size="sm" className="h-6 px-2 text-xs" onClick={() => void showProof()}>
          Посмотреть
        </Button>
        <Button
          variant="outline"
          size="sm"
          className="h-6 px-2 text-xs text-destructive hover:bg-destructive/10 hover:text-destructive"
          disabled={deleteProof.isPending}
          onClick={() => deleteProof.mutate()}
        >
          Удалить
        </Button>
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
      {preview && (
        <button
          type="button"
          className="block max-w-xs"
          onClick={() => setPreview(null)}
          aria-label="Закрыть превью"
        >
          <img src={preview} alt={title} className="max-h-48 w-full rounded-md border border-border object-contain" />
        </button>
      )}
    </div>
  )
}
