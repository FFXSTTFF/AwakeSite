import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/store/authStore'

const API_URL = import.meta.env.VITE_API_URL ?? ''

export function LandingNav() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-border/60 bg-background/70 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <div className="flex items-center gap-2.5">
          <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
          <span className="font-bold">
            Awake <span className="text-accent">[LOVE]</span>
          </span>
        </div>
        {isAuthenticated ? (
          <Button asChild size="sm">
            <Link to="/dashboard">В дашборд</Link>
          </Button>
        ) : (
          <Button asChild size="sm">
            <a href={`${API_URL}/api/auth/discord/login`}>Войти</a>
          </Button>
        )}
      </div>
    </header>
  )
}
