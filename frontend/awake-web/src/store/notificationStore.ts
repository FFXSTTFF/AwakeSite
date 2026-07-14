import { create } from 'zustand'
import type { NotificationDto } from '@/api/notifications'

interface NotificationState {
  notifications: NotificationDto[]
  setNotifications: (items: NotificationDto[]) => void
  addNotification: (item: NotificationDto) => void
  markAllRead: () => void
  unreadCount: () => number
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  notifications: [],

  setNotifications: (items) => set({ notifications: items }),

  // дедуп по id: Sidebar и мобильный лист монтируют по своему NotificationBell,
  // каждый с собственным SignalR-подключением — событие может прийти дважды
  addNotification: (item) =>
    set((s) =>
      s.notifications.some((n) => n.id === item.id)
        ? s
        : { notifications: [item, ...s.notifications] },
    ),

  markAllRead: () =>
    set((s) => ({
      notifications: s.notifications.map((n) => ({ ...n, isRead: true })),
    })),

  unreadCount: () => get().notifications.filter((n) => !n.isRead).length,
}))
