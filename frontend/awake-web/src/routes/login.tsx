import { createFileRoute } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { BrandMark } from '@/components/BrandMark'
import { discordLoginUrl } from '@/lib/discord'

export const Route = createFileRoute('/login')({
  component: LoginPage,
  validateSearch: (search: Record<string, unknown>): { error?: string } => ({
    error: typeof search.error === 'string' ? search.error : undefined,
  }),
})

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
          <BrandMark size="lg" className="mb-2 justify-center" />
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
              <a href={discordLoginUrl}>Войти через Discord</a>
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
