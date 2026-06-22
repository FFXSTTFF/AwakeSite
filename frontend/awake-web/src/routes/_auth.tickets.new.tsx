import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketType } from '@/types/api'

export const Route = createFileRoute('/_auth/tickets/new')({
  component: NewTicketPage,
})

function NewTicketPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [gameNickname, setGameNickname] = useState('')
  const [type, setType] = useState<TicketType>(TicketType.Recruitment)
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)

  const createTicket = useMutation({
    mutationFn: () => ticketsApi.create({ gameNickname, type, description }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tickets'] })
      void navigate({ to: '/tickets' })
    },
    onError: () => setError('Ошибка при создании тикета. Проверьте введённые данные.'),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    createTicket.mutate()
  }

  return (
    <div className="max-w-lg mx-auto">
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('tickets.new')}</h1>
      <form onSubmit={handleSubmit} className="bg-bg-card border border-border rounded-lg p-6 space-y-4">
        <div>
          <label className="block text-sm text-text-muted mb-1">{t('tickets.gameNickname')}</label>
          <input
            value={gameNickname}
            onChange={(e) => setGameNickname(e.target.value)}
            className="w-full bg-bg-page border border-border rounded px-3 py-2 text-text-primary text-sm focus:outline-none focus:border-accent"
            required
            maxLength={100}
          />
        </div>
        <div>
          <label className="block text-sm text-text-muted mb-1">{t('tickets.type')}</label>
          <select
            value={type}
            onChange={(e) => setType(Number(e.target.value) as TicketType)}
            className="w-full bg-bg-page border border-border rounded px-3 py-2 text-text-primary text-sm focus:outline-none focus:border-accent"
          >
            <option value={TicketType.Recruitment}>{t('tickets.types.0')}</option>
            <option value={TicketType.Appeal}>{t('tickets.types.1')}</option>
          </select>
        </div>
        <div>
          <label className="block text-sm text-text-muted mb-1">{t('tickets.description')}</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={5}
            className="w-full bg-bg-page border border-border rounded px-3 py-2 text-text-primary text-sm focus:outline-none focus:border-accent resize-none"
            required
            maxLength={2000}
          />
        </div>
        {error && <div className="text-red-400 text-sm">{error}</div>}
        <div className="flex gap-3 justify-end">
          <Link
            to="/tickets"
            className="px-4 py-2 text-text-muted hover:text-text-primary text-sm transition-colors"
          >
            {t('common.cancel')}
          </Link>
          <button
            type="submit"
            disabled={createTicket.isPending}
            className="px-4 py-2 bg-accent text-bg-page rounded text-sm font-medium disabled:opacity-50 hover:bg-accent/90 transition-colors"
          >
            {createTicket.isPending ? t('common.loading') : t('tickets.submit')}
          </button>
        </div>
      </form>
    </div>
  )
}
