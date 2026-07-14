import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { BrandMark } from '@/components/BrandMark'
import { discordLoginUrl } from '@/lib/discord'
import { useAuthStore } from '@/store/authStore'

export function LandingNav() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-border/60 bg-background/70 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <BrandMark />
        {isAuthenticated ? (
          <Button asChild size="sm">
            <Link to="/dashboard">В дашборд</Link>
          </Button>
        ) : (
          <Button asChild size="sm">
            <a href={discordLoginUrl}>Войти</a>
          </Button>
        )}
      </div>
    </header>
  )
}
