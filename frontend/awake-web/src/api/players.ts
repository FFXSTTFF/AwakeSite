import { apiClient } from './client'
import type { PlayerProfileDto } from '@/types/api'

export const playersApi = {
  getMyProfile: () => apiClient.get<PlayerProfileDto>('/players/me'),
  getProfile: (userId: string) => apiClient.get<PlayerProfileDto>(`/players/${userId}`),
  refreshMyStats: () => apiClient.post<void>('/players/me/stats/refresh'),
}
