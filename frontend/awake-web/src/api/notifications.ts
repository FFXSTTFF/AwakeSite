import { apiClient } from './client'

export interface NotificationDto {
  id: string
  title: string
  body: string
  isRead: boolean
  createdAt: string
}

export const notificationsApi = {
  getAll: () => apiClient.get<NotificationDto[]>('/notifications'),
  markAllRead: () => apiClient.put<void>('/notifications/read'),
}
