import { apiClient } from './client'
import type { LeaderboardEntryDto } from '@/types/api'

export const publicApi = {
  getLeaderboard: () => apiClient.get<LeaderboardEntryDto[]>('/public/leaderboard'),
}
