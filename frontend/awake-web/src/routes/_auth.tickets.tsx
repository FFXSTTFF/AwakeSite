import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ticketsApi } from '@/api/tickets'
import { TicketStatus, TicketType } from '@/types/api'
import { UserPlus, RotateCcw, Plus } from 'lucide-react'

export const Route = createFileRoute('/_auth/tickets')({
  component: TicketsPage,
})

const STATUS_STYLES: Record<number, string> = {
  [TicketStatus.Pending]: 'text-text-muted bg-bg-hover border-border',
  [TicketStatus.InReview]: 'text-accent bg-accent/10 border-accent/30',
  [TicketStatus.Approved]: 'text-green-400 bg-green-400/10 border-green-400/30',
  [TicketStatus.Rejected]: 'text-red-400 bg-red-400/10 border-red-400/30',
}

const TYPE_ICON: Record<number, React.ComponentType<{ size?: number; className?: string }>> = {
  [TicketType.Recruitment]: UserPlus,
  [TicketType.Appeal]: RotateCcw,
}

function TicketsPage() {
  const { t } = useTranslation()
  const { data: tickets, isLoading } = useQuery({
    queryKey: ['tickets'],
    queryFn: () => ticketsApi.getAll(),
  })

  if (isLoading) {
    return <div className="text-text-muted">{t('common.loading')}</div>
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-text-primary">{t('tickets.title')}</h1>
        <Link
          to="/tickets/new"
          className="flex items-center gap-2 px-4 py-2 bg-accent text-bg-page rounded-lg text-sm font-semibold hover:bg-accent-hover transition-colors shadow-[0_0_12px_rgba(61,220,132,0.25)]"
        >
          <Plus size={15} />
          {t('tickets.new')}
        </Link>
      </div>

      {!tickets?.length ? (
        <div className="text-text-muted text-center py-16 bg-bg-card border border-border rounded-xl">
          {t('tickets.noTickets')}
        </div>
      ) : (
        <div className="space-y-2">
          {tickets.map((ticket) => {
            const TypeIcon = TYPE_ICON[ticket.type] ?? FileText
            return (
              <Link
                key={ticket.id}
                to="/tickets/$ticketId"
                params={{ ticketId: ticket.id }}
                className="flex items-center gap-4 bg-bg-card border border-border rounded-xl px-5 py-4 hover:border-accent/25 hover:shadow-[0_0_16px_rgba(61,220,132,0.05)] transition-all group"
              >
                {/* Type icon */}
                <div className="flex-shrink-0 w-8 h-8 rounded-lg bg-bg-hover flex items-center justify-center">
                  <TypeIcon size={15} className="text-text-muted group-hover:text-accent transition-colors" />
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-text-primary group-hover:text-accent transition-colors">
                      {ticket.gameNickname}
                    </span>
                    <span className="text-xs text-text-muted hidden sm:block">
                      · {t(`tickets.types.${ticket.type}`)}
                    </span>
                  </div>
                  <div className="text-xs text-text-muted mt-0.5">
                    {t('tickets.author')}: {ticket.authorUsername} · {new Date(ticket.createdAt).toLocaleDateString('ru-RU')}
                  </div>
                </div>

                {/* Status */}
                <span className={`text-xs font-semibold border rounded-full px-2.5 py-0.5 shrink-0 ${STATUS_STYLES[ticket.status]}`}>
                  {t(`tickets.statuses.${ticket.status}`)}
                </span>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}

function FileText({ size, className }: { size?: number; className?: string }) {
  return (
    <svg width={size ?? 16} height={size ?? 16} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z"/>
      <polyline points="14 2 14 8 20 8"/>
      <line x1="16" y1="13" x2="8" y2="13"/>
      <line x1="16" y1="17" x2="8" y2="17"/>
      <line x1="10" y1="9" x2="8" y2="9"/>
    </svg>
  )
}
