import { createFileRoute, redirect } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { DndContext, DragOverlay, useDroppable, type DragEndEvent, type DragStartEvent } from '@dnd-kit/core'
import { UserMinus, Users } from 'lucide-react'
import { squadBuilderApi } from '@/api/squadBuilder'
import { FighterCard } from '@/components/builder/FighterCard'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import type { BuilderFighter, BuilderSquad } from '@/types/api'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/_auth/squads/builder')({
  beforeLoad: () => {
    if ((useAuthStore.getState().user?.rank ?? 0) < UserRank.Officer) {
      throw redirect({ to: '/squads' })
    }
  },
  component: SquadBuilderPage,
})

const POOL_ID = 'pool'

function SquadBuilderPage() {
  const queryClient = useQueryClient()
  const [active, setActive] = useState<BuilderFighter | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['squads', 'builder'],
    queryFn: squadBuilderApi.get,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['squads', 'builder'] })

  const move = useMutation({
    mutationFn: ({ squadId, userId }: { squadId: string; userId: string }) =>
      squadBuilderApi.moveMember(squadId, userId),
    onSuccess: () => { setError(null); void invalidate() },
    onError: (e: Error) => { setError(e.message); void invalidate() },
  })
  const remove = useMutation({
    mutationFn: ({ squadId, userId }: { squadId: string; userId: string }) =>
      squadBuilderApi.removeMember(squadId, userId),
    onSuccess: () => { setError(null); void invalidate() },
    onError: (e: Error) => { setError(e.message); void invalidate() },
  })

  function findFighter(id: string): BuilderFighter | null {
    if (!data) return null
    return (
      data.pool.find((f) => f.userId === id) ??
      data.squads.flatMap((s) => s.members).find((f) => f.userId === id) ??
      null
    )
  }

  function squadOf(userId: string): BuilderSquad | null {
    return data?.squads.find((s) => s.members.some((m) => m.userId === userId)) ?? null
  }

  function onDragStart(e: DragStartEvent) {
    setActive(findFighter(String(e.active.id)))
  }

  function onDragEnd(e: DragEndEvent) {
    setActive(null)
    const userId = String(e.active.id)
    const target = e.over?.id != null ? String(e.over.id) : null
    if (!target) return

    const from = squadOf(userId)
    if (target === POOL_ID) {
      if (from) remove.mutate({ squadId: from.id, userId })
      return
    }
    if (from?.id === target) return
    move.mutate({ squadId: target, userId })
  }

  if (isLoading || !data) {
    return (
      <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
        <Skeleton className="h-96 rounded-xl" />
        <div className="grid gap-4 sm:grid-cols-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black tracking-tight text-foreground">Билдер отрядов</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Перетащи бойца из пула в отряд. Наведи, чтобы увидеть экипировку и КД.
          </p>
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
      </div>

      <DndContext onDragStart={onDragStart} onDragEnd={onDragEnd}>
        <div className="grid items-start gap-4 lg:grid-cols-[320px_1fr]">
          <PoolColumn fighters={data.pool} squads={data.squads} onMove={(squadId, userId) => move.mutate({ squadId, userId })} />
          <div className="grid gap-4 sm:grid-cols-2">
            {data.squads.map((squad) => (
              <SquadCard
                key={squad.id}
                squad={squad}
                onRemove={(userId) => remove.mutate({ squadId: squad.id, userId })}
              />
            ))}
            {data.squads.length === 0 && (
              <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
            )}
          </div>
        </div>
        <DragOverlay>{active ? <FighterCard fighter={active} /> : null}</DragOverlay>
      </DndContext>
    </div>
  )
}

function PoolColumn({
  fighters,
  squads,
  onMove,
}: {
  fighters: BuilderFighter[]
  squads: BuilderSquad[]
  onMove: (squadId: string, userId: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: POOL_ID })
  const [search, setSearch] = useState('')

  const filtered = fighters.filter((f) =>
    (f.gameNickname ?? f.username).toLowerCase().includes(search.toLowerCase()),
  )

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'rounded-xl border border-border bg-card p-3 transition-colors',
        isOver && 'border-accent/50 bg-accent/5',
      )}
    >
      <p className="mb-2 text-sm font-semibold">Пул ({fighters.length})</p>
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Поиск по нику…"
        className="mb-3 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm outline-none focus:border-accent/50"
      />
      <div className="space-y-1.5">
        {filtered.length === 0 ? (
          <p className="py-4 text-center text-xs text-muted-foreground">
            {fighters.length === 0 ? 'Все бойцы распределены.' : 'Никого не нашлось.'}
          </p>
        ) : (
          filtered.map((f) => (
            <div key={f.userId} className="space-y-1">
              <FighterCard fighter={f} />
              {/* мобильный fallback без перетаскивания */}
              <MobileAssign fighter={f} squads={squads} onMove={onMove} />
            </div>
          ))
        )}
      </div>
    </div>
  )
}

function MobileAssign({
  fighter,
  squads,
  onMove,
}: {
  fighter: BuilderFighter
  squads: BuilderSquad[]
  onMove: (squadId: string, userId: string) => void
}) {
  return (
    <select
      aria-label={`Назначить ${fighter.gameNickname ?? fighter.username} в отряд`}
      className="w-full rounded-md border border-border bg-background px-2 py-1 text-xs text-muted-foreground md:hidden"
      value=""
      onChange={(e) => e.target.value && onMove(e.target.value, fighter.userId)}
    >
      <option value="">→ в отряд…</option>
      {squads.map((s) => (
        <option key={s.id} value={s.id} disabled={s.members.length >= 5}>
          {s.name} ({s.members.length}/5)
        </option>
      ))}
    </select>
  )
}

function SquadCard({
  squad,
  onRemove,
}: {
  squad: BuilderSquad
  onRemove: (userId: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: squad.id })
  const full = squad.members.length >= 5

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'rounded-xl border border-border bg-card p-3 transition-colors',
        isOver && !full && 'border-accent/50 bg-accent/5',
        isOver && full && 'border-destructive/50 bg-destructive/5',
      )}
    >
      <div className="mb-2 flex items-center justify-between">
        <p className="flex items-center gap-2 text-sm font-semibold">
          <Users size={14} className="text-accent" />
          {squad.name}
        </p>
        <span className={cn('text-xs font-bold', full ? 'text-accent' : 'text-muted-foreground')}>
          {squad.members.length}/5
        </span>
      </div>
      <div className="space-y-1.5">
        {squad.members.map((m) => (
          <div key={m.userId} className="flex items-center gap-1.5">
            <div className="min-w-0 flex-1">
              <FighterCard fighter={m} />
            </div>
            <button
              type="button"
              aria-label={`Убрать ${m.gameNickname ?? m.username} из отряда`}
              onClick={() => onRemove(m.userId)}
              className="shrink-0 rounded p-1.5 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
            >
              <UserMinus size={14} />
            </button>
          </div>
        ))}
        {squad.members.length === 0 && (
          <p className="py-3 text-center text-xs text-muted-foreground">Перетащи бойцов сюда</p>
        )}
      </div>
    </div>
  )
}
