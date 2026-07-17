import { useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { InventoryFlags } from '@/components/InventoryFlags'
import { BoostChips } from '@/components/boosts/BoostChips'
import type { BoostType, PlayerFlags } from '@/types/api'

const PANEL_WIDTH = 224
const VIEWPORT_MARGIN = 12

// Позиция считается от реального места строки на экране (position: fixed) и
// клэмпится в границы вьюпорта — карточки отрядов стоят в сетке на всю ширину
// страницы, и крайние карточки иначе выталкивали бы попап за экран.
export function MemberHoverInfo({
  nickname,
  flags,
  kd,
  boosts = [],
  children,
}: {
  nickname: string
  flags: PlayerFlags
  kd: number | null
  boosts?: BoostType[]
  children: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [coords, setCoords] = useState<{ left: number; top: number }>()
  const anchorRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    if (!open || !anchorRef.current) return

    function place() {
      const rect = anchorRef.current!.getBoundingClientRect()
      const panelHeight = panelRef.current?.offsetHeight ?? 0
      const left = Math.min(
        Math.max(rect.left, VIEWPORT_MARGIN),
        window.innerWidth - PANEL_WIDTH - VIEWPORT_MARGIN,
      )
      let top = rect.bottom + 6
      if (top + panelHeight > window.innerHeight - VIEWPORT_MARGIN) {
        top = Math.max(rect.top - panelHeight - 6, VIEWPORT_MARGIN)
      }
      setCoords({ left, top })
    }

    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  return (
    <div
      ref={anchorRef}
      className="relative"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      {children}
      {open && (
        <div
          ref={panelRef}
          style={{
            left: coords?.left,
            top: coords?.top,
            width: PANEL_WIDTH,
            visibility: coords ? 'visible' : 'hidden',
          }}
          className="fixed z-40 rounded-lg border border-border bg-popover p-3 text-xs shadow-xl"
        >
          <p className="font-semibold text-foreground">{nickname}</p>
          <p className="mt-1 text-muted-foreground">
            КД:{' '}
            <span className="font-bold text-foreground">
              {kd != null ? kd.toLocaleString('ru-RU', { maximumFractionDigits: 2 }) : '—'}
            </span>
          </p>
          <div className="mt-2">
            <InventoryFlags flags={flags} size="sm" />
          </div>
          {boosts.length > 0 && (
            <div className="mt-2">
              <BoostChips selected={boosts} short />
            </div>
          )}
        </div>
      )}
    </div>
  )
}
