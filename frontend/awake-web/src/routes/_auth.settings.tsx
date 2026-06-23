import { createFileRoute } from '@tanstack/react-router'
import { useTheme } from '@/hooks/useTheme'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Sun, Moon } from 'lucide-react'

export const Route = createFileRoute('/_auth/settings')({
  component: SettingsPage,
})

function SettingsPage() {
  const { theme, toggle } = useTheme()

  return (
    <div className="max-w-xl">
      <h1 className="text-2xl font-bold text-foreground mb-6">Настройки</h1>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">Внешний вид</CardTitle>
          <CardDescription>Управление темой интерфейса</CardDescription>
        </CardHeader>
        <Separator />
        <CardContent className="pt-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-foreground">Тема</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                {theme === 'dark' ? 'Тёмная тема активна' : 'Светлая тема активна'}
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={toggle} className="gap-2">
              {theme === 'dark' ? <Sun size={14} /> : <Moon size={14} />}
              {theme === 'dark' ? 'Светлая' : 'Тёмная'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
