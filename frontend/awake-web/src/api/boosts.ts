import { apiClient } from './client'
import type { BoostSummaryEntry, BoostType } from '@/types/api'

export const boostsApi = {
  getMy: (): Promise<BoostType[]> => apiClient.get('/profile/boosts'),
  setMy: (boostTypes: BoostType[]): Promise<void> =>
    apiClient.put('/profile/boosts', { boostTypes }),
  summary: (): Promise<BoostSummaryEntry[]> => apiClient.get('/boosts/summary'),
}
