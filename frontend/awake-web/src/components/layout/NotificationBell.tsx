import { useState, useRef, useEffect, useLayoutEffect } from 'react'
import { Bell } from 'lucide-react'
import { useNotificationStore } from '@/store/notificationStore'
import { useNotifications } from '@/hooks/useNotifications'
import { notificationsApi } from '@/api/notifications'
import { cn } from '@/lib/utils'

const PANEL_WIDTH = 320
const VIEWPORT_MARGIN = 16
const CLOSE_DURATION = 150

// direction="up" — для мест у нижнего края экрана (лист «Ещё» мобильной панели):
// попап раскрывается вверх-влево и не уходит под таб-бар / за вьюпорт
export function NotificationBell({ direction = 'down' }: { direction?: 'down' | 'up' }) {
  useNotifications()

  // render — панель есть в DOM; visible — включена анимация появления.
  // Разделены, чтобы доиграть анимацию закрытия перед размонтированием.
  const [render, setRender] = useState(false)
  const [visible, setVisible] = useState(false)
  const [coords, setCoords] = useState<{ left: number; width: number; top?: number; bottom?: number }>()
  const containerRef = useRef<HTMLDivElement>(null)
  const btnRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const closeTimer = useRef<ReturnType<typeof setTimeout>>(undefined)
  const notifications = useNotificationStore((s) => s.notifications)
  const unreadCount = useNotificationStore((s) => s.unreadCount)()
  const markAllRead = useNotificationStore((s) => s.markAllRead)

  function closePanel() {
    setVisible(false)
    clearTimeout(closeTimer.current)
    closeTimer.current = setTimeout(() => setRender(false), CLOSE_DURATION)
  }

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) closePanel()
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  useEffect(() => () => clearTimeout(closeTimer.current), [])

  // Плавное появление на следующем кадре после монтирования
  useEffect(() => {
    if (!render) return
    const id = requestAnimationFrame(() => setVisible(true))
    return () => cancelAnimationFrame(id)
  }, [render])

  // Позиция считается от реального места кнопки на экране (position: fixed),
  // а не от узкого родителя (сайдбар/лист «Ещё») — иначе панель шириной 320px
  // вылезает за пределы вьюпорта и раздувает страницу по горизонтали.
  useLayoutEffect(() => {
    if (!render || !btnRef.current || !panelRef.current) return
    const panel = panelRef.current

    function place() {
      const rect = btnRef.current!.getBoundingClientRect()
      const width = Math.min(PANEL_WIDTH, window.innerWidth - VIEWPORT_MARGIN * 2)
      const maxLeft = window.innerWidth - width - VIEWPORT_MARGIN
      const left = Math.min(Math.max(rect.right - width, VIEWPORT_MARGIN), Math.max(maxLeft, VIEWPORT_MARGIN))

      // Ширину выставляем до замера высоты — иначе высота посчитается для чужой ширины
      panel.style.width = `${width}px`
      const height = panel.offsetHeight

      if (direction === 'up') {
        const idealBottom = window.innerHeight - rect.top + 8
        const bottom = Math.max(Math.min(idealBottom, window.innerHeight - VIEWPORT_MARGIN - height), VIEWPORT_MARGIN)
        setCoords({ left, width, bottom })
      } else {
        let top = rect.bottom + 8
        // Не помещается снизу — раскрываем вверх от кнопки
        if (top + height > window.innerHeight - VIEWPORT_MARGIN) {
          top = Math.max(rect.top - height - 8, VIEWPORT_MARGIN)
        }
        setCoords({ left, width, top })
      }
    }

    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [render, direction])

  async function handleToggle() {
    if (render) {
      closePanel()
      return
    }
    clearTimeout(closeTimer.current)
    setRender(true)
    if (unreadCount > 0) {
      markAllRead()
      await notificationsApi.markAllRead().catch(() => null)
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        ref={btnRef}
        onClick={handleToggle}
        className="relative flex items-center justify-center w-8 h-8 rounded-md text-muted-foreground hover:text-foreground hover:bg-secondary transition-colors"
        aria-label="Уведомления"
      >
        <Bell size={16} />
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 px-0.5 rounded-full bg-destructive text-destructive-foreground text-[10px] font-bold flex items-center justify-center leading-none">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {render && (
        <div
          ref={panelRef}
          style={{
            left: coords?.left,
            width: coords?.width,
            top: coords?.top,
            bottom: coords?.bottom,
            visibility: coords ? 'visible' : 'hidden',
          }}
          className={cn(
            'fixed max-w-[calc(100vw-2rem)] bg-popover border border-border rounded-xl shadow-xl z-50 overflow-hidden',
            'origin-top transition-[opacity,transform] duration-150 ease-out',
            visible ? 'opacity-100 scale-100' : 'pointer-events-none opacity-0 scale-95',
          )}
        >
          <div className="px-4 py-2.5 border-b border-border flex items-center justify-between">
            <span className="text-xs font-semibold text-foreground">Уведомления</span>
            {notifications.length > 0 && (
              <span className="text-xs text-muted-foreground">{notifications.length} всего</span>
            )}
          </div>

          <div className="max-h-80 overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">
                Нет уведомлений
              </div>
            ) : (
              notifications.map((n) => (
                <div
                  key={n.id}
                  className={cn(
                    'px-4 py-3 border-b border-border last:border-0 text-sm',
                    !n.isRead && 'bg-accent/5',
                  )}
                >
                  <div className="flex items-start gap-2">
                    {!n.isRead && (
                      <div className="w-1.5 h-1.5 rounded-full bg-accent shrink-0 mt-1.5" />
                    )}
                    <div className={cn(!n.isRead ? '' : 'pl-3.5')}>
                      <p className="font-medium text-foreground leading-snug">{n.title}</p>
                      <p className="text-xs text-muted-foreground mt-0.5 leading-snug">{n.body}</p>
                      <p className="text-[10px] text-muted-foreground mt-1">
                        {new Date(n.createdAt).toLocaleString('ru-RU')}
                      </p>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}
