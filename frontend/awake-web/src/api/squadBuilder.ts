import { apiClient } from './client'
import type { SquadBuilderData } from '@/types/api'

export const squadBuilderApi = {
  get: (): Promise<SquadBuilderData> => apiClient.get('/squads/builder'),
  moveMember: (squadId: string, userId: string): Promise<void> =>
    apiClient.post(`/squads/${squadId}/move-member`, { userId }),
  removeMember: (squadId: string, userId: string): Promise<void> =>
    apiClient.delete(`/squads/${squadId}/builder-members/${userId}`),
}
