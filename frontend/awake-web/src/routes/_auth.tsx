import { createFileRoute, redirect, Outlet } from '@tanstack/react-router'
import { Sidebar } from '@/components/layout/Sidebar'
import { useAuthStore } from '@/store/authStore'

export const Route = createFileRoute('/_auth')({
  // getState() вместо context: контекст роутера обновляется только с рендером
  // RouterProvider, поэтому login() + мгновенный navigate() видел старое состояние
  beforeLoad: () => {
    if (!useAuthStore.getState().isAuthenticated) {
      throw redirect({ to: '/login' })
    }
  },
  component: AuthLayout,
})

function AuthLayout() {
  return (
    <div className="flex min-h-screen bg-bg-page">
      <Sidebar />
      <main className="flex-1 min-h-screen overflow-auto">
        <div className="max-w-5xl mx-auto px-6 py-8 md:pt-8 pt-16">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
