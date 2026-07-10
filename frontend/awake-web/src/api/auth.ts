import { apiClient } from './client'
import type { LoginResponse } from '@/types/api'

export const authApi = {
  refresh: () => apiClient.post<LoginResponse>('/auth/refresh'),
}
