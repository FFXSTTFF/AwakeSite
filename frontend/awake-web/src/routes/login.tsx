import { createFileRoute } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'

export const Route = createFileRoute('/login')({
  component: LoginPage,
  validateSearch: (search: Record<string, unknown>): { error?: string } => ({
    error: typeof search.error === 'string' ? search.error : undefined,
  }),
})

const API_URL = import.meta.env.VITE_API_URL ?? ''

function LoginPage() {
  const { error } = Route.useSearch()

  return (
    <div className="relative min-h-screen overflow-hidden bg-background flex items-center justify-center px-4">
      {/* приглушённое свечение за карточкой */}
      <div
        aria-hidden
        className="absolute left-1/2 top-1/3 h-[500px] w-[500px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent/10 blur-[120px]"
      />
      <div className="relative w-full max-w-sm">
        {/* Brand */}
        <div className="mb-8 text-center">
          <div className="mb-2 inline-flex items-center gap-2">
            <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
            <span className="text-2xl font-black tracking-tight text-foreground">
              Awake <span className="text-accent">[LOVE]</span>
            </span>
          </div>
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            STALCRAFT · Clan Platform
          </p>
        </div>

        <Card>
          <CardHeader className="pb-4">
            <CardTitle className="text-center">Вход</CardTitle>
            <CardDescription className="text-center">
              Используй свой Discord-аккаунт
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {error === 'discord' && (
              <p className="text-center text-sm text-destructive">
                Не удалось войти через Discord. Попробуй ещё раз.
              </p>
            )}
            <Button asChild className="w-full">
              <a href={`${API_URL}/api/auth/discord/login`}>Войти через Discord</a>
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
