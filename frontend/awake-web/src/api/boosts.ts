import { apiClient } from './client'
import type { BoostItem, BoostSelection, BoostSummaryEntry } from '@/types/api'

export const boostsApi = {
  getMy: (): Promise<BoostItem[]> => apiClient.get('/profile/boosts'),
  setMy: (selections: BoostSelection[]): Promise<void> =>
    apiClient.put('/profile/boosts', { selections }),
  summary: (): Promise<BoostSummaryEntry[]> => apiClient.get('/boosts/summary'),
}
