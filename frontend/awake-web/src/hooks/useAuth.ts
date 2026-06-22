import { useAuthStore } from '@/store/authStore'

export function useAuth() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const user = useAuthStore((s) => s.user)
  const rank = useAuthStore((s) => s.user?.rank ?? 0)
  const login = useAuthStore((s) => s.login)
  const logout = useAuthStore((s) => s.logout)

  return { isAuthenticated, user, rank, login, logout }
}
