import { Biohazard, Crosshair, Footprints, Heart, Shield } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { PlayerFlags } from '@/types/api'

const FLAG_DEFS = [
  { key: 'bio', icon: Biohazard, label: 'Био (комбинированная броня)' },
  { key: 'combat', icon: Shield, label: 'Боевая броня' },
  { key: 'sniper', icon: Crosshair, label: 'Снайперка' },
  { key: 'speed', icon: Footprints, label: 'Сборка на скорость' },
  { key: 'vitality', icon: Heart, label: 'Сборка на живучесть' },
] as const

export function InventoryFlags({
  flags,
  size = 'md',
}: {
  flags: PlayerFlags
  size?: 'sm' | 'md'
}) {
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {FLAG_DEFS.map(({ key, icon: Icon, label }) => {
        const active = flags[key]
        return (
          <span
            key={key}
            title={label + (active ? '' : ' — нет')}
            className={cn(
              'inline-flex items-center justify-center rounded-md border',
              size === 'md' ? 'h-8 w-8' : 'h-6 w-6',
              active
                ? 'border-accent/30 bg-accent/10 text-accent'
                : 'border-border bg-secondary/50 text-muted-foreground/40',
            )}
          >
            <Icon size={size === 'md' ? 16 : 12} />
          </span>
        )
      })}
    </div>
  )
}
