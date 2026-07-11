import { RouterProvider, createRouter } from '@tanstack/react-router'
import { useEffect, useRef, useState } from 'react'
import { routeTree } from './routeTree.gen'
import { useAuthStore } from '@/store/authStore'
import { authApi } from '@/api/auth'

const router = createRouter({
  routeTree,
  context: {
    auth: undefined!,
  },
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

export default function App() {
  const auth = useAuthStore()
  const [restoring, setRestoring] = useState(true)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

    // Восстанавливаем сессию по refresh-куке (живёт 7 дней): стор в памяти,
    // без этого каждая полная загрузка страницы разлогинивала пользователя
    authApi
      .refresh()
      .then(({ accessToken, username, rank, userId }) => {
        useAuthStore.getState().login({ userId, username, rank }, accessToken)
      })
      .catch(() => {
        // куки нет или она истекла — остаёмся неавторизованными
      })
      .finally(() => setRestoring(false))
  }, [])

  // Роутер нельзя рендерить до восстановления: охранник _auth успеет
  // средиректить на /login раньше, чем придёт ответ refresh
  if (restoring) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <p className="text-muted-foreground">Загрузка…</p>
      </div>
    )
  }

  return <RouterProvider router={router} context={{ auth }} />
}
