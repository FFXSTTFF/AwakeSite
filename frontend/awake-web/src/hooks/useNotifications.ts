import { useEffect } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { useAuthStore } from '@/store/authStore'
import { useNotificationStore } from '@/store/notificationStore'
import { notificationsApi, type NotificationDto } from '@/api/notifications'

const HUB_URL = `${import.meta.env.VITE_API_URL ?? ''}/hubs/notifications`

export function useNotifications() {
  const accessToken = useAuthStore((s) => s.accessToken)
  const { setNotifications, addNotification } = useNotificationStore()

  // Load existing notifications from API
  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationsApi.getAll(),
    enabled: !!accessToken,
  })

  useEffect(() => {
    if (data) setNotifications(data)
  }, [data, setNotifications])

  // Connect to SignalR hub for real-time
  useEffect(() => {
    if (!accessToken) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => accessToken })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('Notification', (notification: NotificationDto) => {
      addNotification(notification)
    })

    connection.start().catch(() => {
      // Silently fail if hub is unreachable (dev mode, no backend)
    })

    return () => {
      void connection.stop()
    }
  }, [accessToken, addNotification])
}
