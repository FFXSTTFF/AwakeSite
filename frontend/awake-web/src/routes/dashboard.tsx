import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard')({
  component: DashboardPage,
})

function DashboardPage() {
  return (
    <div className="min-h-screen bg-bg-page flex items-center justify-center">
      <h1 className="text-3xl font-semibold text-text-primary">Dashboard</h1>
    </div>
  )
}
