import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/store/authStore'
import { UserRank, TicketStatus } from '@/types/api'
import { squadsApi } from '@/api/squads'
import { ticketsApi } from '@/api/tickets'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { Shield, FileText, Users, ChevronRight, Clock } from 'lucide-react'

export const Route = createFileRoute('/_auth/dashboard')({
  component: DashboardPage,
})

const RANK_CLASSES: Record<number, string> = {
  [UserRank.Guest]: 'bg-secondary text-muted-foreground border-border',
  [UserRank.Member]: 'bg-blue-400/10 text-blue-400 border-blue-400/30',
  [UserRank.Officer]: 'bg-accent/10 text-accent border-accent/30',
  [UserRank.Colonel]: 'bg-yellow-400/10 text-yellow-400 border-yellow-400/30',
  [UserRank.Leader]: 'bg-destructive/10 text-destructive border-destructive/30',
}

const RANK_LABELS: Record<number, string> = {
  [UserRank.Guest]: 'Гость',
  [UserRank.Member]: 'Участник',
  [UserRank.Officer]: 'Офицер',
  [UserRank.Colonel]: 'Полковник',
  [UserRank.Leader]: 'Лидер',
}

function DashboardPage() {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)

  const { data: squads, isLoading: squadsLoading } = useQuery({ queryKey: ['squads'], queryFn: () => squadsApi.getAll() })
  const { data: tickets, isLoading: ticketsLoading } = useQuery({ queryKey: ['tickets'], queryFn: () => ticketsApi.getAll() })

  const totalMembers = squads?.reduce((sum, s) => sum + s.memberCount, 0) ?? 0
  const pendingTickets = tickets?.filter((t) => t.status === TicketStatus.Pending).length ?? 0
  const activeTickets = tickets?.filter((t) => t.status === TicketStatus.InReview).length ?? 0

  return (
    <div className="space-y-6">
      {/* Welcome */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-black tracking-tight text-foreground">
            Привет, <span className="text-accent">{user?.username}</span>
          </h1>
          <p className="text-muted-foreground text-sm mt-0.5">Клан Awake [LOVE] · STALCRAFT</p>
        </div>
        <Badge className={cn('text-xs font-semibold border', RANK_CLASSES[user?.rank ?? 0])}>
          {RANK_LABELS[user?.rank ?? 0]}
        </Badge>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {squadsLoading || ticketsLoading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-[118px] rounded-xl" />
          ))
        ) : (
          <>
            <StatCard icon={Users} label="Бойцов в клане" value={totalMembers} tone="blue" />
            <StatCard icon={Shield} label="Отрядов" value={squads?.length ?? 0} tone="green" />
            <StatCard icon={Clock} label="Ожидают рассмотрения" value={pendingTickets} tone="yellow" />
            <StatCard icon={FileText} label="На рассмотрении" value={activeTickets} tone="purple" />
          </>
        )}
      </div>

      <div className="grid md:grid-cols-2 gap-4">
        {/* Squads preview */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Shield size={14} className="text-accent" /> {t('nav.squads')}
              </CardTitle>
              <Link to="/squads" className="text-xs text-accent hover:text-accent/80 flex items-center gap-0.5 transition-colors">
                Все <ChevronRight size={12} />
              </Link>
            </div>
          </CardHeader>
          <Separator />
          <CardContent className="pt-4 space-y-3">
            {squadsLoading ? (
              <div className="space-y-3">
                {Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-5 w-full" />
                ))}
              </div>
            ) : (
              squads?.slice(0, 4).map((squad) => (
                <Link
                  key={squad.id}
                  to="/squads/$squadId"
                  params={{ squadId: squad.id }}
                  className="flex items-center justify-between group"
                >
                  <span className="text-sm text-foreground group-hover:text-accent transition-colors">
                    {squad.name}
                  </span>
                  <div className="flex items-center gap-2">
                    <div className="w-20 h-1.5 bg-secondary rounded-full overflow-hidden">
                      <div
                        className="h-full bg-accent/70 rounded-full"
                        style={{ width: `${(squad.memberCount / 5) * 100}%` }}
                      />
                    </div>
                    <span className="text-xs text-muted-foreground w-8 text-right">{squad.memberCount}/5</span>
                  </div>
                </Link>
              ))
            )}
            {!squadsLoading && !squads?.length && (
              <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
            )}
          </CardContent>
        </Card>

        {/* Recent tickets */}
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <FileText size={14} className="text-accent" /> {t('nav.tickets')}
              </CardTitle>
              <Link to="/tickets" className="text-xs text-accent hover:text-accent/80 flex items-center gap-0.5 transition-colors">
                Все <ChevronRight size={12} />
              </Link>
            </div>
          </CardHeader>
          <Separator />
          <CardContent className="pt-4 space-y-3">
            {ticketsLoading ? (
              <div className="space-y-3">
                {Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-5 w-full" />
                ))}
              </div>
            ) : (
              tickets?.slice(0, 4).map((ticket) => (
                <Link
                  key={ticket.id}
                  to="/tickets/$ticketId"
                  params={{ ticketId: ticket.id }}
                  className="flex items-center justify-between group"
                >
                  <span className="text-sm text-foreground group-hover:text-accent transition-colors truncate max-w-[160px]">
                    {ticket.gameNickname}
                  </span>
                  <StatusPill status={ticket.status} t={t} />
                </Link>
              ))
            )}
            {!ticketsLoading && !tickets?.length && (
              <p className="text-sm text-muted-foreground">Тикетов пока нет.</p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function StatCard({ icon: Icon, label, value, tone }: {
  icon: React.ComponentType<{ size?: number; className?: string }>
  label: string
  value: number
  tone: 'blue' | 'green' | 'yellow' | 'purple'
}) {
  const styles = {
    blue:   { border: 'border-blue-400/20',   tile: 'bg-blue-400/10 text-blue-400' },
    green:  { border: 'border-accent/20',     tile: 'bg-accent/10 text-accent' },
    yellow: { border: 'border-yellow-400/20', tile: 'bg-yellow-400/10 text-yellow-400' },
    purple: { border: 'border-purple-400/20', tile: 'bg-purple-400/10 text-purple-400' },
  }[tone]

  return (
    <Card className={cn('border transition-transform hover:-translate-y-0.5', styles.border)}>
      <CardContent className="pb-4 pt-4">
        <div className={cn('mb-3 flex h-9 w-9 items-center justify-center rounded-lg', styles.tile)}>
          <Icon size={17} />
        </div>
        <div className="text-3xl font-black tracking-tight text-foreground">{value}</div>
        <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
      </CardContent>
    </Card>
  )
}

const STATUS_CLASSES: Record<number, string> = {
  [TicketStatus.Pending]: 'bg-secondary text-muted-foreground',
  [TicketStatus.InReview]: 'bg-accent/10 text-accent',
  [TicketStatus.Approved]: 'bg-green-400/10 text-green-400',
  [TicketStatus.Rejected]: 'bg-destructive/10 text-destructive',
  [TicketStatus.Closed]: 'bg-muted text-muted-foreground',
}

function StatusPill({ status, t }: { status: number; t: (key: string) => string }) {
  return (
    <span className={cn('text-[10px] font-semibold px-2 py-0.5 rounded-full shrink-0', STATUS_CLASSES[status])}>
      {t(`tickets.statuses.${status}`)}
    </span>
  )
}
