import { apiClient } from './client'
import type { BuildType, PlayerInventory, UpdateLoadoutRequest } from '@/types/api'

export const inventoryApi = {
  getMy: (): Promise<PlayerInventory> => apiClient.get('/profile/inventory'),
  getFor: (userId: string): Promise<PlayerInventory> =>
    apiClient.get(`/players/${userId}/inventory`),
  addItem: (itemId: string): Promise<void> =>
    apiClient.post('/profile/inventory/items', { itemId }),
  removeItem: (itemId: string): Promise<void> =>
    apiClient.delete(`/profile/inventory/items/${itemId}`),
  uploadProof: (type: BuildType, file: File): Promise<void> => {
    const form = new FormData()
    form.set('type', String(type))
    form.set('file', file)
    return apiClient.postForm('/profile/build-proof', form)
  },
  deleteMyProof: (type: BuildType): Promise<void> =>
    apiClient.delete(`/profile/build-proof/${type}`),
  deleteProofFor: (userId: string, type: BuildType): Promise<void> =>
    apiClient.delete(`/players/${userId}/build-proof/${type}`),
  proofImageBlob: (userId: string, type: BuildType): Promise<Blob> =>
    apiClient.getBlob(`/players/${userId}/build-proof/${type}/image`),
  updateLoadout: (data: UpdateLoadoutRequest): Promise<void> =>
    apiClient.put('/profile/loadout', data),
}
