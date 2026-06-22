import { createFileRoute } from '@tanstack/react-router'
import { useTheme } from '@/hooks/useTheme'

export const Route = createFileRoute('/_auth/settings')({
  component: SettingsPage,
})

function SettingsPage() {
  const { theme, toggle } = useTheme()

  return (
    <div className="bg-bg-card p-6 rounded-lg">
      <h1 className="text-2xl font-bold text-text-primary mb-6">Настройки</h1>
      <div className="flex items-center gap-4">
        <span className="text-text-muted">Тема:</span>
        <button
          onClick={toggle}
          className="px-4 py-2 rounded-lg border border-border text-text-primary bg-bg-hover hover:bg-bg-card transition-colors"
        >
          {theme === 'dark' ? 'Светлая' : 'Тёмная'}
        </button>
      </div>
    </div>
  )
}
