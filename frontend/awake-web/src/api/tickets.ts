import { apiClient } from './client'
import type { TicketListItemDto, TicketDetailDto, TicketCommentDto, TicketType } from '@/types/api'

export const ticketsApi = {
  getAll: () => apiClient.get<TicketListItemDto[]>('/tickets'),
  getById: (id: string) => apiClient.get<TicketDetailDto>(`/tickets/${id}`),
  create: (data: { gameNickname: string; type: TicketType; description: string }) =>
    apiClient.post<TicketListItemDto>('/tickets', data),
  updateStatus: (id: string, newStatus: number) =>
    apiClient.put<void>(`/tickets/${id}/status`, { newStatus }),
  addComment: (id: string, content: string) =>
    apiClient.post<TicketCommentDto>(`/tickets/${id}/comments`, { content }),
}
