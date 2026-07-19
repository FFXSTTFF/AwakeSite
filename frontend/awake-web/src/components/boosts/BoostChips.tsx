import { useTranslation } from 'react-i18next'
import type { BoostItem } from '@/types/api'

// Read-only чипы предметов-бустов: иконка + название, тултип — тип буста.
export function BoostChips({ items, short = false }: { items: BoostItem[]; short?: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-wrap gap-1.5">
      {items.map((b) => (
        <span
          key={b.boostType}
          title={t(`boosts.types.${b.boostType}`)}
          className="flex items-center gap-1.5 rounded-md border border-accent/30 bg-accent/10 px-2 py-1 text-xs font-medium text-accent"
        >
          {b.icon && (
            <img
              src={b.icon}
              alt=""
              className="h-4 w-4 shrink-0 object-contain"
              onError={(e) => (e.currentTarget.style.display = 'none')}
            />
          )}
          <span className={short ? 'max-w-24 truncate' : undefined}>{b.name}</span>
        </span>
      ))}
    </div>
  )
}
