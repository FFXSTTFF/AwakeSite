import { createFileRoute, redirect, Outlet } from '@tanstack/react-router'
import { Sidebar } from '@/components/layout/Sidebar'
import { MobileTabBar } from '@/components/layout/MobileTabBar'
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
      <main className="min-h-screen flex-1 overflow-auto">
        <div className="mx-auto max-w-5xl px-6 pb-24 pt-8 md:pb-8">
          <Outlet />
        </div>
      </main>
      <MobileTabBar />
    </div>
  )
}
