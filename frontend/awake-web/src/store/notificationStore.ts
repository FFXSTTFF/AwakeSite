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

  addNotification: (item) =>
    set((s) => ({ notifications: [item, ...s.notifications] })),

  markAllRead: () =>
    set((s) => ({
      notifications: s.notifications.map((n) => ({ ...n, isRead: true })),
    })),

  unreadCount: () => get().notifications.filter((n) => !n.isRead).length,
}))
