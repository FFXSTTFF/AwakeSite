import { useState, useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { itemsApi } from '@/api/items'
import type { LoadoutSlot } from '@/types/api'
import { cn } from '@/lib/utils'
import { X, Search } from 'lucide-react'

const RANK_DOT: Record<string, string> = {
  RANK_VETERAN: 'bg-purple-500',
  RANK_MASTER: 'bg-red-500',
  RANK_LEGEND: 'bg-yellow-400',
}

interface ItemComboboxProps {
  categoryPrefix: string
  excludeCategory?: string
  placeholder: string
  value: LoadoutSlot | null
  onChange: (item: LoadoutSlot | null) => void
  required?: boolean
}

export function ItemCombobox({
  categoryPrefix,
  excludeCategory,
  placeholder,
  value,
  onChange,
  required,
}: ItemComboboxProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [] } = useQuery({
    queryKey: ['items', categoryPrefix, excludeCategory, query],
    queryFn: () => itemsApi.search(query, categoryPrefix, excludeCategory),
    enabled: query.length >= 2,
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
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-secondary">
        <img
          src={value.itemIcon}
          alt=""
          className="w-6 h-6 object-contain shrink-0"
          onError={(e) => (e.currentTarget.style.display = 'none')}
        />
        <span className="text-sm text-foreground flex-1">{value.itemName}</span>
        <button
          type="button"
          onClick={() => onChange(null)}
          className="text-muted-foreground hover:text-destructive transition-colors shrink-0"
        >
          <X size={14} />
        </button>
      </div>
    )
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-background focus-within:border-accent/50 transition-colors">
        <Search size={14} className="text-muted-foreground shrink-0" />
        <input
          type="text"
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          placeholder={placeholder}
          value={query}
          required={required && !value}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
        />
      </div>

      {open && results.length > 0 && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border border-border bg-card shadow-lg overflow-hidden">
          {results.map((item) => (
            <button
              key={item.id}
              type="button"
              className="flex items-center gap-3 w-full px-3 py-2.5 text-left hover:bg-secondary transition-colors"
              onMouseDown={(e) => {
                e.preventDefault()
                onChange({ itemId: item.id, itemName: item.nameRu, itemIcon: item.icon, upgrade: 0 })
                setQuery('')
                setOpen(false)
              }}
            >
              <img
                src={item.icon}
                alt=""
                className="w-7 h-7 object-contain shrink-0"
                onError={(e) => (e.currentTarget.style.display = 'none')}
              />
              <span className="text-sm text-foreground flex-1">{item.nameRu}</span>
              <span className={cn('w-2 h-2 rounded-full shrink-0', RANK_DOT[item.color] ?? 'bg-muted')} />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
