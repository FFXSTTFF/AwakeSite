import { create } from 'zustand'
import type { CurrentUser } from '@/types/api'

export interface AuthState {
  accessToken: string | null
  user: CurrentUser | null
  isAuthenticated: boolean
  login: (user: CurrentUser, token: string) => void
  logout: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  isAuthenticated: false,
  login: (user, token) =>
    set({ accessToken: token, user, isAuthenticated: true }),
  logout: () =>
    set({ accessToken: null, user: null, isAuthenticated: false }),
}))
