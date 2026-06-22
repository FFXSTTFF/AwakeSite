import type { UserDto } from '@/types/api'
import { apiClient } from './client'

export const usersApi = {
  getAll: () => apiClient.get<UserDto[]>('/users'),
  updateRank: (userId: string, newRank: number) =>
    apiClient.put<void>(`/users/${userId}/rank`, { newRank }),
}
