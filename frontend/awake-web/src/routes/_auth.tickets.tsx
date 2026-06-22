import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ticketsApi } from '@/api/tickets'
import { TicketStatus } from '@/types/api'

export const Route = createFileRoute('/_auth/tickets')({
  component: TicketsPage,
})

const STATUS_COLORS: Record<number, string> = {
  [TicketStatus.Pending]: 'text-text-muted border-border',
  [TicketStatus.InReview]: 'text-accent border-accent/40',
  [TicketStatus.Approved]: 'text-green-400 border-green-400/40',
  [TicketStatus.Rejected]: 'text-red-400 border-red-400/40',
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
          className="px-4 py-2 bg-accent text-bg-page rounded-lg text-sm font-medium hover:bg-accent/90 transition-colors"
        >
          {t('tickets.new')}
        </Link>
      </div>

      {!tickets?.length ? (
        <div className="text-text-muted text-center py-12">{t('tickets.noTickets')}</div>
      ) : (
        <div className="space-y-3">
          {tickets.map((ticket) => (
            <Link
              key={ticket.id}
              to="/tickets/$ticketId"
              params={{ ticketId: ticket.id }}
              className="block bg-bg-card border border-border rounded-lg p-4 hover:bg-bg-hover transition-colors"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <span className="text-text-primary font-medium">{ticket.gameNickname}</span>
                  <span className="text-xs text-text-muted">
                    {t(`tickets.types.${ticket.type}`)}
                  </span>
                </div>
                <span className={`text-xs border rounded px-2 py-0.5 ${STATUS_COLORS[ticket.status]}`}>
                  {t(`tickets.statuses.${ticket.status}`)}
                </span>
              </div>
              <div className="mt-1 flex items-center gap-3 text-xs text-text-muted">
                <span>{t('tickets.author')}: {ticket.authorUsername}</span>
                <span>{new Date(ticket.createdAt).toLocaleDateString('ru-RU')}</span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
