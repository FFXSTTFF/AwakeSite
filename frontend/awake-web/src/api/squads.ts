import type { SquadDto } from '@/types/api'
import { apiClient } from './client'

export const squadsApi = {
  getAll: () => apiClient.get<SquadDto[]>('/squads'),
  getById: (id: string) => apiClient.get<SquadDto>(`/squads/${id}`),
  addMember: (squadId: string, userId: string) =>
    apiClient.post<void>(`/squads/${squadId}/members`, { userId }),
  removeMember: (squadId: string, userId: string) =>
    apiClient.delete<void>(`/squads/${squadId}/members/${userId}`),
  setLeader: (squadId: string, userId: string) =>
    apiClient.put<void>(`/squads/${squadId}/leader`, { userId }),
  rename: (squadId: string, name: string) =>
    apiClient.put<void>(`/squads/${squadId}/name`, { name }),
}
