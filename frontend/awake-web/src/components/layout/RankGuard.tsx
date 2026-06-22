import { useAuthStore } from '@/store/authStore'
import type { UserRank } from '@/types/api'

interface RankGuardProps {
  min: UserRank
  children: React.ReactNode
}

export function RankGuard({ min, children }: RankGuardProps) {
  const rank = useAuthStore((s) => s.user?.rank ?? 0)
  return rank >= min ? <>{children}</> : null
}
