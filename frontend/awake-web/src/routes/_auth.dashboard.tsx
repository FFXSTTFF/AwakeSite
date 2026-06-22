import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/store/authStore'
import { UserRank, TicketStatus } from '@/types/api'
import { squadsApi } from '@/api/squads'
import { ticketsApi } from '@/api/tickets'
import { Shield, FileText, Users, ChevronRight, Clock } from 'lucide-react'

export const Route = createFileRoute('/_auth/dashboard')({
  component: DashboardPage,
})

const RANK_LABELS: Record<number, string> = {
  [UserRank.Guest]: 'Гость',
  [UserRank.Member]: 'Участник',
  [UserRank.Officer]: 'Офицер',
  [UserRank.Colonel]: 'Полковник',
  [UserRank.Leader]: 'Лидер',
}

const RANK_COLORS: Record<number, string> = {
  [UserRank.Guest]: 'text-text-muted bg-bg-hover',
  [UserRank.Member]: 'text-blue-400 bg-blue-400/10',
  [UserRank.Officer]: 'text-accent bg-accent/10',
  [UserRank.Colonel]: 'text-yellow-400 bg-yellow-400/10',
  [UserRank.Leader]: 'text-red-400 bg-red-400/10',
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
    <div className="space-y-8">
      {/* Welcome */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-text-primary">
            Привет, <span className="text-accent">{user?.username}</span> 👋
          </h1>
          <p className="text-text-muted text-sm mt-1">Клан Awake [LOVE] · STALCRAFT</p>
        </div>
        <span className={`text-xs font-semibold px-3 py-1.5 rounded-full ${RANK_COLORS[user?.rank ?? 0]}`}>
          {RANK_LABELS[user?.rank ?? 0]}
        </span>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard
          icon={<Users size={18} className="text-blue-400" />}
          label="Бойцов в кланe"
          value={totalMembers}
          bg="bg-blue-400/5 border-blue-400/20"
        />
        <StatCard
          icon={<Shield size={18} className="text-accent" />}
          label="Отрядов"
          value={squads?.length ?? 0}
          bg="bg-accent/5 border-accent/20"
        />
        <StatCard
          icon={<Clock size={18} className="text-yellow-400" />}
          label="Ожидают рассмотрения"
          value={pendingTickets}
          bg="bg-yellow-400/5 border-yellow-400/20"
        />
        <StatCard
          icon={<FileText size={18} className="text-purple-400" />}
          label="На рассмотрении"
          value={activeTickets}
          bg="bg-purple-400/5 border-purple-400/20"
        />
      </div>

      <div className="grid md:grid-cols-2 gap-6">
        {/* Squads preview */}
        <div className="bg-bg-card border border-border rounded-xl p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-text-primary flex items-center gap-2">
              <Shield size={16} className="text-accent" /> {t('nav.squads')}
            </h2>
            <Link to="/squads" className="text-xs text-accent hover:text-accent/80 flex items-center gap-1">
              Все <ChevronRight size={12} />
            </Link>
          </div>
          <div className="space-y-3">
            {squads?.slice(0, 3).map((squad) => (
              <Link
                key={squad.id}
                to="/squads/$squadId"
                params={{ squadId: squad.id }}
                className="flex items-center justify-between group"
              >
                <span className="text-sm text-text-primary group-hover:text-accent transition-colors">
                  {squad.name}
                </span>
                <div className="flex items-center gap-2">
                  <div className="w-20 h-1.5 bg-bg-hover rounded-full overflow-hidden">
                    <div
                      className="h-full bg-accent/70 rounded-full"
                      style={{ width: `${(squad.memberCount / 5) * 100}%` }}
                    />
                  </div>
                  <span className="text-xs text-text-muted w-8 text-right">{squad.memberCount}/5</span>
                </div>
              </Link>
            ))}
            {!squads?.length && <p className="text-sm text-text-muted">—</p>}
          </div>
        </div>

        {/* Recent tickets */}
        <div className="bg-bg-card border border-border rounded-xl p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-text-primary flex items-center gap-2">
              <FileText size={16} className="text-accent" /> {t('nav.tickets')}
            </h2>
            <Link to="/tickets" className="text-xs text-accent hover:text-accent/80 flex items-center gap-1">
              Все <ChevronRight size={12} />
            </Link>
          </div>
          <div className="space-y-3">
            {tickets?.slice(0, 3).map((ticket) => (
              <Link
                key={ticket.id}
                to="/tickets/$ticketId"
                params={{ ticketId: ticket.id }}
                className="flex items-center justify-between group"
              >
                <span className="text-sm text-text-primary group-hover:text-accent transition-colors truncate max-w-[140px]">
                  {ticket.gameNickname}
                </span>
                <StatusPill status={ticket.status} t={t} />
              </Link>
            ))}
            {!tickets?.length && <p className="text-sm text-text-muted">—</p>}
          </div>
        </div>
      </div>
    </div>
  )
}

function StatCard({ icon, label, value, bg }: {
  icon: React.ReactNode
  label: string
  value: number
  bg: string
}) {
  return (
    <div className={`bg-bg-card border rounded-xl p-4 ${bg}`}>
      <div className="flex items-center gap-2 mb-2">{icon}</div>
      <div className="text-2xl font-bold text-text-primary">{value}</div>
      <div className="text-xs text-text-muted mt-0.5">{label}</div>
    </div>
  )
}

const STATUS_COLORS: Record<number, string> = {
  [TicketStatus.Pending]: 'text-text-muted bg-bg-hover',
  [TicketStatus.InReview]: 'text-accent bg-accent/10',
  [TicketStatus.Approved]: 'text-green-400 bg-green-400/10',
  [TicketStatus.Rejected]: 'text-red-400 bg-red-400/10',
}

function StatusPill({ status, t }: { status: number; t: (key: string) => string }) {
  return (
    <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-full shrink-0 ${STATUS_COLORS[status]}`}>
      {t(`tickets.statuses.${status}`)}
    </span>
  )
}
