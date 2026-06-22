import { createFileRoute } from '@tanstack/react-router'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/dashboard')({
  component: DashboardPage,
})

const rankNames: Record<UserRank, string> = {
  [UserRank.Guest]: 'Guest',
  [UserRank.Member]: 'Member',
  [UserRank.Officer]: 'Officer',
  [UserRank.Colonel]: 'Colonel',
  [UserRank.Leader]: 'Leader',
}

function DashboardPage() {
  const user = useAuthStore((s) => s.user)

  return (
    <div className="bg-bg-card p-6 rounded-lg">
      <h1 className="text-2xl font-semibold text-text-primary mb-4">
        Добро пожаловать, {user?.username ?? ''}!
      </h1>
      {user && (
        <span className="inline-block px-3 py-1 rounded-full text-sm font-medium text-accent bg-accent-tint">
          {rankNames[user.rank]}
        </span>
      )}
    </div>
  )
}
