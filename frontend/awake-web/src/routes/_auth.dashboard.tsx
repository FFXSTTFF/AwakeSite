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

  const { data: squads } = useQuery({ queryKey: ['squads'], queryFn: () => squadsApi.getAll() })
  const { data: tickets } = useQuery({ queryKey: ['tickets'], queryFn: () => ticketsApi.getAll() })

  const totalMembers = squads?.reduce((sum, s) => sum + s.memberCount, 0) ?? 0
  const pendingTickets = tickets?.filter((t) => t.status === TicketStatus.Pending).length ?? 0
  const activeTickets = tickets?.filter((t) => t.status === TicketStatus.InReview).length ?? 0

  return (
    <div className="space-y-6">
      {/* Welcome */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">
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
        <StatCard
          icon={<Users size={17} className="text-blue-400" />}
          label="Бойцов в клане"
          value={totalMembers}
          accent="blue"
        />
        <StatCard
          icon={<Shield size={17} className="text-accent" />}
          label="Отрядов"
          value={squads?.length ?? 0}
          accent="green"
        />
        <StatCard
          icon={<Clock size={17} className="text-yellow-400" />}
          label="Ожидают рассмотрения"
          value={pendingTickets}
          accent="yellow"
        />
        <StatCard
          icon={<FileText size={17} className="text-purple-400" />}
          label="На рассмотрении"
          value={activeTickets}
          accent="purple"
        />
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
            {squads?.slice(0, 4).map((squad) => (
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
            ))}
            {!squads?.length && <p className="text-sm text-muted-foreground">—</p>}
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
            {tickets?.slice(0, 4).map((ticket) => (
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
            ))}
            {!tickets?.length && <p className="text-sm text-muted-foreground">—</p>}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function StatCard({ icon, label, value, accent }: {
  icon: React.ReactNode
  label: string
  value: number
  accent: 'blue' | 'green' | 'yellow' | 'purple'
}) {
  const border = {
    blue: 'border-blue-400/20',
    green: 'border-accent/20',
    yellow: 'border-yellow-400/20',
    purple: 'border-purple-400/20',
  }[accent]

  return (
    <Card className={cn('border', border)}>
      <CardContent className="pt-4 pb-4">
        <div className="mb-2">{icon}</div>
        <div className="text-2xl font-bold text-foreground">{value}</div>
        <div className="text-xs text-muted-foreground mt-0.5">{label}</div>
      </CardContent>
    </Card>
  )
}

const STATUS_CLASSES: Record<number, string> = {
  [TicketStatus.Pending]: 'bg-secondary text-muted-foreground',
  [TicketStatus.InReview]: 'bg-accent/10 text-accent',
  [TicketStatus.Approved]: 'bg-green-400/10 text-green-400',
  [TicketStatus.Rejected]: 'bg-destructive/10 text-destructive',
}

function StatusPill({ status, t }: { status: number; t: (key: string) => string }) {
  return (
    <span className={cn('text-[10px] font-semibold px-2 py-0.5 rounded-full shrink-0', STATUS_CLASSES[status])}>
      {t(`tickets.statuses.${status}`)}
    </span>
  )
}
