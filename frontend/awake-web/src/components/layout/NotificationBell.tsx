import { useState, useRef, useEffect } from 'react'
import { Bell } from 'lucide-react'
import { useNotificationStore } from '@/store/notificationStore'
import { useNotifications } from '@/hooks/useNotifications'
import { notificationsApi } from '@/api/notifications'
import { cn } from '@/lib/utils'

export function NotificationBell() {
  useNotifications()

  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const notifications = useNotificationStore((s) => s.notifications)
  const unreadCount = useNotificationStore((s) => s.unreadCount)()
  const markAllRead = useNotificationStore((s) => s.markAllRead)

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  async function handleOpen() {
    setOpen((v) => !v)
    if (!open && unreadCount > 0) {
      markAllRead()
      await notificationsApi.markAllRead().catch(() => null)
    }
  }

  return (
    <div ref={ref} className="relative">
      <button
        onClick={handleOpen}
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

      {open && (
        <div className="absolute left-0 top-9 w-80 bg-popover border border-border rounded-xl shadow-xl z-50 overflow-hidden">
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
