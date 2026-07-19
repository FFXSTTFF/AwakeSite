import { apiClient } from './client'
import type { BoostType, ItemSearchResult } from '@/types/api'

export const itemsApi = {
  search: (q: string, category?: string, exclude?: string): Promise<ItemSearchResult[]> => {
    const params = new URLSearchParams({ q })
    if (category) params.set('category', category)
    if (exclude) params.set('exclude', exclude)
    return apiClient.get<ItemSearchResult[]>(`/items/search?${params.toString()}`)
  },
  searchBoosts: (q: string, boostType: BoostType): Promise<ItemSearchResult[]> => {
    const params = new URLSearchParams({ q, boostType: String(boostType) })
    return apiClient.get<ItemSearchResult[]>(`/items/search?${params.toString()}`)
  },
}
