import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search, X } from 'lucide-react'
import { itemsApi } from '@/api/items'
import type { BoostItem, BoostType, ItemSearchResult } from '@/types/api'

export function BoostSlotPicker({
  boostType,
  value,
  onSelect,
  onClear,
  disabled = false,
}: {
  boostType: BoostType
  value: BoostItem | null
  onSelect: (item: ItemSearchResult) => void
  onClear: () => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [] } = useQuery({
    queryKey: ['items', 'boosts', boostType, query],
    queryFn: () => itemsApi.searchBoosts(query, boostType),
    enabled: open, // вариантов 3–32 — показываем список сразу по фокусу
    staleTime: 60_000,
  })

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  if (value) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-border bg-secondary px-3 py-2">
        {value.icon && (
          <img
            src={value.icon}
            alt=""
            className="h-6 w-6 shrink-0 object-contain"
            onError={(e) => (e.currentTarget.style.display = 'none')}
          />
        )}
        <span className="flex-1 text-sm text-foreground">{value.name}</span>
        <button
          type="button"
          onClick={onClear}
          disabled={disabled}
          className="shrink-0 text-muted-foreground transition-colors hover:text-destructive disabled:pointer-events-none disabled:opacity-60"
        >
          <X size={14} />
        </button>
      </div>
    )
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="flex items-center gap-2 rounded-lg border border-border bg-background px-3 py-2 transition-colors focus-within:border-accent/50">
        <Search size={14} className="shrink-0 text-muted-foreground" />
        <input
          type="text"
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          placeholder={t('boosts.searchPlaceholder')}
          value={query}
          disabled={disabled}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
        />
      </div>

      {open && results.length > 0 && (
        <div className="absolute z-50 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-border bg-card shadow-lg">
          {results.map((item) => (
            <button
              key={item.id}
              type="button"
              className="flex w-full items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-secondary"
              onMouseDown={(e) => {
                e.preventDefault()
                onSelect(item)
                setQuery('')
                setOpen(false)
              }}
            >
              <img
                src={item.icon}
                alt=""
                className="h-7 w-7 shrink-0 object-contain"
                onError={(e) => (e.currentTarget.style.display = 'none')}
              />
              <span className="flex-1 text-sm text-foreground">{item.nameRu}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
