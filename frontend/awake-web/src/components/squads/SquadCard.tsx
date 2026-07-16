import { useRef, useState, type MouseEvent } from 'react'
import { Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Crown, Pencil } from 'lucide-react'
import { squadsApi } from '@/api/squads'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { MemberHoverInfo } from '@/components/squads/MemberHoverInfo'
import { cn } from '@/lib/utils'
import type { SquadDto } from '@/types/api'

export function SquadCard({ squad, canRename }: { squad: SquadDto; canRename: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(squad.name)
  const suppressCommitRef = useRef(false)

  const rename = useMutation({
    mutationFn: (name: string) => squadsApi.rename(squad.id, name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['squads'] }),
  })

  const leader = squad.members.find((m) => m.isLeader)
  const others = squad.members.filter((m) => !m.isLeader)
  const pct = (squad.memberCount / 5) * 100
  const isFull = squad.memberCount >= 5

  function startEdit(e: MouseEvent) {
    e.preventDefault()
    e.stopPropagation()
    suppressCommitRef.current = false
    setDraft(squad.name)
    setEditing(true)
  }

  function commit() {
    if (suppressCommitRef.current) {
      suppressCommitRef.current = false
      return
    }
    // unmounting the focused input re-fires blur -> commit; suppress that re-entry
    suppressCommitRef.current = true
    const trimmed = draft.trim()
    setEditing(false)
    if (trimmed && trimmed !== squad.name) {
      rename.mutate(trimmed)
    }
  }

  function cancel() {
    suppressCommitRef.current = true
    setDraft(squad.name)
    setEditing(false)
  }

  return (
    <Link to="/squads/$squadId" params={{ squadId: squad.id }} className="group block">
      <Card className="h-full transition-all duration-200 group-hover:border-accent/30 group-hover:shadow-[0_0_20px_rgba(61,220,132,0.06)]">
        <CardContent className="pt-5 pb-5">
          {/* Header */}
          <div className="mb-4 flex items-start justify-between gap-2">
            {editing ? (
              <input
                autoFocus
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onClick={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                }}
                onMouseDown={(e) => e.stopPropagation()}
                onKeyDown={(e) => {
                  e.stopPropagation()
                  if (e.key === 'Enter') commit()
                  if (e.key === 'Escape') cancel()
                }}
                onBlur={commit}
                disabled={rename.isPending}
                maxLength={100}
                className="min-w-0 flex-1 rounded-md border border-accent/40 bg-secondary px-2 py-1 text-base font-semibold text-foreground outline-none"
              />
            ) : (
              <div className="flex min-w-0 items-center gap-1.5">
                <h2 className="truncate text-base font-semibold text-foreground transition-colors group-hover:text-accent">
                  {squad.name}
                </h2>
                {canRename && (
                  <button
                    type="button"
                    onClick={startEdit}
                    aria-label="Переименовать отряд"
                    className="shrink-0 rounded p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                  >
                    <Pencil size={13} />
                  </button>
                )}
              </div>
            )}
            <Badge
              className={cn(
                'shrink-0 border text-xs font-medium',
                isFull
                  ? 'border-destructive/30 bg-destructive/10 text-destructive'
                  : 'border-accent/30 bg-accent/10 text-accent',
              )}
            >
              {t('squads.memberCount', { count: squad.memberCount })}
            </Badge>
          </div>

          {/* Capacity bar */}
          <div className="mb-4">
            <div className="h-1 overflow-hidden rounded-full bg-secondary">
              <div
                className={cn('h-full rounded-full transition-all', isFull ? 'bg-destructive/60' : 'bg-accent/70')}
                style={{ width: `${pct}%` }}
              />
            </div>
          </div>

          {/* Members */}
          <div className="space-y-2">
            {leader && (
              <MemberHoverInfo nickname={leader.gameNickname ?? leader.username} flags={leader.flags} kd={leader.kd}>
                <div className="flex items-center gap-2">
                  <Crown size={12} className="shrink-0 text-yellow-400" />
                  <span className="truncate text-sm font-medium text-foreground">
                    {leader.gameNickname ?? leader.username}
                  </span>
                </div>
              </MemberHoverInfo>
            )}
            {others.slice(0, leader ? 2 : 3).map((m) => (
              <MemberHoverInfo key={m.userId} nickname={m.gameNickname ?? m.username} flags={m.flags} kd={m.kd}>
                <div className="flex items-center gap-2 pl-5">
                  <span className="truncate text-sm text-muted-foreground">
                    {m.gameNickname ?? m.username}
                  </span>
                </div>
              </MemberHoverInfo>
            ))}
            {squad.memberCount > 3 && (
              <div className="pl-5 text-xs text-muted-foreground">
                {t('squads.more', { count: squad.memberCount - 3 })}
              </div>
            )}
            {squad.memberCount === 0 && (
              <div className="text-sm text-muted-foreground">{t('squads.noMembers')}</div>
            )}
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
