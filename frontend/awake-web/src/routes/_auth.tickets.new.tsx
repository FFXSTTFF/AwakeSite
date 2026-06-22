import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketType } from '@/types/api'
import { UserPlus, RotateCcw, ArrowLeft, Send } from 'lucide-react'

export const Route = createFileRoute('/_auth/tickets/new')({
  component: NewTicketPage,
})

const TICKET_TYPES = [
  { value: TicketType.Recruitment, icon: UserPlus, labelKey: 'tickets.types.0', desc: 'Хочу вступить в клан Awake [LOVE]' },
  { value: TicketType.Appeal, icon: RotateCcw, labelKey: 'tickets.types.1', desc: 'Апелляция на кик или бан' },
] as const

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
    onError: () => setError(t('tickets.createError')),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    createTicket.mutate()
  }

  return (
    <div className="max-w-xl mx-auto">
      <Link
        to="/tickets"
        className="inline-flex items-center gap-2 text-sm text-text-muted hover:text-text-primary mb-6 transition-colors"
      >
        <ArrowLeft size={14} /> {t('common.cancel')}
      </Link>

      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('tickets.new')}</h1>

      <form onSubmit={handleSubmit} className="space-y-5">
        {/* Type selector */}
        <div>
          <label className="block text-sm font-medium text-text-muted mb-2">{t('tickets.type')}</label>
          <div className="grid grid-cols-2 gap-3">
            {TICKET_TYPES.map(({ value, icon: Icon, labelKey, desc }) => (
              <button
                key={value}
                type="button"
                onClick={() => setType(value)}
                className={`flex flex-col items-start gap-1 p-4 rounded-xl border text-left transition-all ${
                  type === value
                    ? 'border-accent bg-accent/8 shadow-[0_0_12px_rgba(61,220,132,0.1)]'
                    : 'border-border bg-bg-card hover:border-border hover:bg-bg-hover'
                }`}
              >
                <Icon size={18} className={type === value ? 'text-accent' : 'text-text-muted'} />
                <span className={`text-sm font-semibold ${type === value ? 'text-accent' : 'text-text-primary'}`}>
                  {t(labelKey)}
                </span>
                <span className="text-xs text-text-muted leading-snug">{desc}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Nickname */}
        <div>
          <label className="block text-sm font-medium text-text-muted mb-1.5">
            {t('tickets.gameNickname')}
          </label>
          <input
            value={gameNickname}
            onChange={(e) => setGameNickname(e.target.value)}
            className="w-full bg-bg-hover border border-border rounded-lg px-3 py-2.5 text-text-primary text-sm focus:outline-none focus:border-accent focus:ring-1 focus:ring-accent/30 transition-all"
            required
            maxLength={100}
            placeholder="Твой никнейм в игре"
          />
        </div>

        {/* Description */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <label className="text-sm font-medium text-text-muted">{t('tickets.description')}</label>
            <span className="text-xs text-text-muted">{description.length}/2000</span>
          </div>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={6}
            className="w-full bg-bg-hover border border-border rounded-lg px-3 py-2.5 text-text-primary text-sm focus:outline-none focus:border-accent focus:ring-1 focus:ring-accent/30 transition-all resize-none"
            required
            maxLength={2000}
            placeholder="Расскажи о себе — опыт, достижения, почему хочешь в клан..."
          />
        </div>

        {error && (
          <div className="bg-red-400/10 border border-red-400/30 text-red-400 text-sm rounded-lg px-4 py-3">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={createTicket.isPending}
          className="w-full flex items-center justify-center gap-2 py-2.5 bg-accent text-bg-page rounded-lg font-semibold text-sm disabled:opacity-50 hover:bg-accent-hover transition-colors shadow-[0_0_16px_rgba(61,220,132,0.2)]"
        >
          <Send size={15} />
          {createTicket.isPending ? t('common.loading') : t('tickets.submit')}
        </button>
      </form>
    </div>
  )
}
