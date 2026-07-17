import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { ALL_BOOST_TYPES, type BoostType } from '@/types/api'

// Без onToggle — read-only: показываются только активные чипы (пустые — шум).
// С onToggle — все 4 типа как тумблеры.
export function BoostChips({
  selected,
  onToggle,
  short = false,
  disabled = false,
}: {
  selected: BoostType[]
  onToggle?: (type: BoostType) => void
  short?: boolean
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const readonly = !onToggle
  const visible = readonly ? ALL_BOOST_TYPES.filter((b) => selected.includes(b)) : ALL_BOOST_TYPES

  return (
    <div className="flex flex-wrap gap-1.5">
      {visible.map((type) => {
        const active = selected.includes(type)
        const label = t(`boosts.${short ? 'typesShort' : 'types'}.${type}`)
        const cls = cn(
          'rounded-md border px-2 py-1 text-xs font-medium transition-colors',
          active
            ? 'border-accent/30 bg-accent/10 text-accent'
            : 'border-border bg-secondary/50 text-muted-foreground',
        )
        return readonly ? (
          <span key={type} className={cls}>
            {label}
          </span>
        ) : (
          <button
            key={type}
            type="button"
            onClick={() => onToggle(type)}
            disabled={disabled}
            className={cn(
              cls,
              'cursor-pointer hover:border-accent/50 disabled:pointer-events-none disabled:opacity-60',
            )}
          >
            {label}
          </button>
        )
      })}
    </div>
  )
}
