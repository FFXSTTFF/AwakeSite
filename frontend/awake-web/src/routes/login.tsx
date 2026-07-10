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
    <div className="min-h-screen bg-background flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        {/* Brand */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-2">
            <div className="w-2 h-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
            <span className="font-bold text-foreground text-lg">
              Awake <span className="text-accent">[LOVE]</span>
            </span>
          </div>
          <p className="text-xs text-muted-foreground">STALCRAFT · Clan Platform</p>
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
              <p className="text-sm text-destructive text-center">
                Не удалось войти через Discord. Попробуй ещё раз.
              </p>
            )}
            <Button asChild className="w-full">
              <a href={`${API_URL}/api/auth/discord/login`}>
                Войти через Discord
              </a>
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
