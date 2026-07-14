import { useDraggable } from '@dnd-kit/core'
import { CSS } from '@dnd-kit/utilities'
import { InventoryFlags } from '@/components/InventoryFlags'
import { cn } from '@/lib/utils'
import type { BuilderFighter } from '@/types/api'

export function FighterCard({ fighter }: { fighter: BuilderFighter }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: fighter.userId,
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={transform ? { transform: CSS.Translate.toString(transform) } : undefined}
      className={cn(
        'group relative flex cursor-grab items-center gap-2 rounded-lg border border-border bg-card px-2.5 py-2 text-sm transition-colors hover:border-accent/30',
        isDragging && 'z-50 opacity-70 shadow-lg',
      )}
    >
      {fighter.avatarUrl ? (
        <img src={fighter.avatarUrl} alt="" className="h-6 w-6 shrink-0 rounded-full" />
      ) : (
        <span className="h-6 w-6 shrink-0 rounded-full bg-secondary" />
      )}
      <span className="min-w-0 flex-1 truncate font-medium">
        {fighter.gameNickname ?? fighter.username}
      </span>
      <InventoryFlags flags={fighter.flags} size="sm" />

      {/* Ховер-попап: расшифровка + КД */}
      <div className="pointer-events-none absolute left-1/2 top-full z-40 mt-1 hidden w-56 -translate-x-1/2 rounded-lg border border-border bg-popover p-3 text-xs shadow-xl group-hover:block">
        <p className="font-semibold">{fighter.gameNickname ?? fighter.username}</p>
        <p className="mt-1 text-muted-foreground">
          КД: <span className="font-bold text-foreground">{fighter.kd != null ? fighter.kd.toLocaleString('ru-RU', { maximumFractionDigits: 2 }) : '—'}</span>
        </p>
        <ul className="mt-2 space-y-0.5 text-muted-foreground">
          <li>{fighter.flags.bio ? '✓' : '✗'} Био-броня</li>
          <li>{fighter.flags.combat ? '✓' : '✗'} Боевая броня</li>
          <li>{fighter.flags.sniper ? '✓' : '✗'} Снайперка</li>
          <li>{fighter.flags.speed ? '✓' : '✗'} Сборка на скорость</li>
          <li>{fighter.flags.vitality ? '✓' : '✗'} Сборка на живучесть</li>
        </ul>
      </div>
    </div>
  )
}
