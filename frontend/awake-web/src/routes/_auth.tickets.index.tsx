import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ticketsApi } from '@/api/tickets'
import { TicketStatus, TicketType } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { UserPlus, RotateCcw, FileText, Plus } from 'lucide-react'

export const Route = createFileRoute('/_auth/tickets/')({
  component: TicketsPage,
})

const STATUS_CLASSES: Record<number, string> = {
  [TicketStatus.Pending]: 'bg-secondary text-muted-foreground border-border',
  [TicketStatus.InReview]: 'bg-accent/10 text-accent border-accent/30',
  [TicketStatus.Approved]: 'bg-green-400/10 text-green-400 border-green-400/30',
  [TicketStatus.Rejected]: 'bg-destructive/10 text-destructive border-destructive/30',
  [TicketStatus.Closed]: 'bg-muted text-muted-foreground border-border',
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
    return <div className="text-muted-foreground">{t('common.loading')}</div>
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-foreground">{t('tickets.title')}</h1>
        <Button asChild size="sm">
          <Link to="/tickets/new" className="gap-1.5">
            <Plus size={14} />
            {t('tickets.new')}
          </Link>
        </Button>
      </div>

      {!tickets?.length ? (
        <div className="text-muted-foreground text-center py-16 bg-card border border-border rounded-xl">
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
                className="flex items-center gap-4 bg-card border border-border rounded-xl px-5 py-4 hover:border-accent/25 hover:shadow-[0_0_16px_rgba(61,220,132,0.05)] transition-all group"
              >
                <div className="flex-shrink-0 w-8 h-8 rounded-lg bg-secondary flex items-center justify-center">
                  <TypeIcon size={15} className="text-muted-foreground group-hover:text-accent transition-colors" />
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-foreground group-hover:text-accent transition-colors">
                      {ticket.gameNickname}
                    </span>
                    <span className="text-xs text-muted-foreground hidden sm:block">
                      · {t(`tickets.types.${ticket.type}`)}
                    </span>
                  </div>
                  <div className="text-xs text-muted-foreground mt-0.5">
                    {t('tickets.author')}: {ticket.authorUsername} · {new Date(ticket.createdAt).toLocaleDateString('ru-RU')}
                  </div>
                </div>

                <Badge className={cn('text-xs font-semibold border shrink-0', STATUS_CLASSES[ticket.status])}>
                  {t(`tickets.statuses.${ticket.status}`)}
                </Badge>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}
