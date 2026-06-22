import { apiClient } from './client'
import type { LoginResponse, RegisterResponse } from '@/types/api'

export const authApi = {
  login: (data: { username: string; password: string }) =>
    apiClient.post<LoginResponse>('/auth/login', data),
  register: (data: { username: string; password: string; email?: string }) =>
    apiClient.post<RegisterResponse>('/auth/register', data),
  refresh: () => apiClient.post<LoginResponse>('/auth/refresh'),
}
