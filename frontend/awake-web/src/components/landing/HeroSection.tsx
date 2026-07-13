import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Reveal } from '@/components/Reveal'
import { useAuthStore } from '@/store/authStore'

const API_URL = import.meta.env.VITE_API_URL ?? ''

export function HeroSection() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <section className="relative overflow-hidden pb-24 pt-32">
      {/* приглушённое зелёное свечение за заголовком */}
      <div
        aria-hidden
        className="absolute -top-40 left-1/4 h-[600px] w-[600px] rounded-full bg-accent/10 blur-[120px]"
      />
      <div className="relative mx-auto grid max-w-6xl items-center gap-12 px-4 md:grid-cols-2">
        <Reveal>
          <div>
            <span className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <span className="h-1.5 w-1.5 rounded-full bg-accent" />
              Клановая платформа STALCRAFT
            </span>
            <h1 className="mt-6 text-4xl font-black leading-[1.05] tracking-tight md:text-6xl">
              Играем вместе.
              <br />
              Побеждаем <span className="text-accent">вместе</span>.
            </h1>
            <p className="mt-6 max-w-md text-lg text-muted-foreground">
              Awake [LOVE] — клан STALCRAFT со своей платформой: статистика
              игроков, отряды и рекрутинг в одном месте.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              {isAuthenticated ? (
                <Button asChild size="lg">
                  <Link to="/dashboard">Открыть дашборд</Link>
                </Button>
              ) : (
                <Button asChild size="lg">
                  <a href={`${API_URL}/api/auth/discord/login`}>Войти через Discord</a>
                </Button>
              )}
              <Button asChild variant="outline" size="lg">
                <a href="#join">Как вступить</a>
              </Button>
            </div>
          </div>
        </Reveal>

        {/* Кольцо-декор: зарезервированное место под будущий арт клана */}
        <Reveal delayMs={150} className="hidden justify-center md:flex">
          <div className="relative h-80 w-80">
            <div className="absolute inset-0 rounded-full bg-accent/10 blur-3xl" />
            <div className="absolute inset-0 rounded-full border-[14px] border-accent/80" />
            <div className="absolute inset-6 rounded-full border border-accent/20" />
            <div className="absolute inset-0 flex items-center justify-center">
              <span className="text-3xl font-black tracking-widest text-accent">[LOVE]</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  )
}
