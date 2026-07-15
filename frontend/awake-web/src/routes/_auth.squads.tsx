import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/squads')({
  beforeLoad: () => {
    if ((useAuthStore.getState().user?.rank ?? 0) < UserRank.Member) {
      throw redirect({ to: '/tickets' })
    }
  },
  component: () => <Outlet />,
})
