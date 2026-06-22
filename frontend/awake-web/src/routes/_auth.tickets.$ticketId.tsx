import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketStatus, UserRank } from '@/types/api'
import { useAuthStore } from '@/store/authStore'

export const Route = createFileRoute('/_auth/tickets/$ticketId')({
  component: TicketDetailPage,
})

const STATUS_OPTIONS = [
  TicketStatus.InReview,
  TicketStatus.Approved,
  TicketStatus.Rejected,
] as const

const STATUS_COLORS: Record<number, string> = {
  [TicketStatus.Pending]: 'text-text-muted bg-bg-hover',
  [TicketStatus.InReview]: 'text-accent bg-accent/10',
  [TicketStatus.Approved]: 'text-green-400 bg-green-400/10',
  [TicketStatus.Rejected]: 'text-red-400 bg-red-400/10',
}

function TicketDetailPage() {
  const { t } = useTranslation()
  const { ticketId } = Route.useParams()
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)
  const isOfficerPlus = (currentUser?.rank ?? 0) >= UserRank.Officer

  const [commentText, setCommentText] = useState('')
  const [selectedStatus, setSelectedStatus] = useState<TicketStatus | null>(null)

  const { data: ticket, isLoading } = useQuery({
    queryKey: ['tickets', ticketId],
    queryFn: () => ticketsApi.getById(ticketId),
  })

  const updateStatus = useMutation({
    mutationFn: (newStatus: TicketStatus) => ticketsApi.updateStatus(ticketId, newStatus),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tickets', ticketId] })
      void queryClient.invalidateQueries({ queryKey: ['tickets'] })
      setSelectedStatus(null)
    },
  })

  const addComment = useMutation({
    mutationFn: (content: string) => ticketsApi.addComment(ticketId, content),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tickets', ticketId] })
      setCommentText('')
    },
  })

  if (isLoading) {
    return <div className="text-text-muted">{t('common.loading')}</div>
  }

  if (!ticket) {
    return <div className="text-text-muted">{t('common.notFound')}</div>
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Header */}
      <div className="bg-bg-card border border-border rounded-lg p-6">
        <div className="flex items-start justify-between mb-4">
          <div>
            <h1 className="text-xl font-bold text-text-primary">{ticket.gameNickname}</h1>
            <div className="text-sm text-text-muted mt-1">
              {t(`tickets.types.${ticket.type}`)} · {t('tickets.author')}: {ticket.authorUsername}
            </div>
          </div>
          <span className={`text-xs px-3 py-1 rounded-full font-medium ${STATUS_COLORS[ticket.status]}`}>
            {t(`tickets.statuses.${ticket.status}`)}
          </span>
        </div>
        <p className="text-text-primary text-sm whitespace-pre-wrap">{ticket.description}</p>
        <div className="text-xs text-text-muted mt-4">
          {new Date(ticket.createdAt).toLocaleString('ru-RU')}
        </div>
      </div>

      {/* Officer controls */}
      {isOfficerPlus && (
        <div className="bg-bg-card border border-border rounded-lg p-4">
          <div className="text-sm text-text-muted mb-3">{t('tickets.changeStatus')}</div>
          <div className="flex items-center gap-3">
            <select
              value={selectedStatus ?? ticket.status}
              onChange={(e) => setSelectedStatus(Number(e.target.value) as TicketStatus)}
              className="bg-bg-page border border-border rounded px-3 py-1.5 text-text-primary text-sm focus:outline-none focus:border-accent"
            >
              {STATUS_OPTIONS.map((s) => (
                <option key={s} value={s}>{t(`tickets.statuses.${s}`)}</option>
              ))}
            </select>
            <button
              onClick={() => selectedStatus !== null && updateStatus.mutate(selectedStatus)}
              disabled={updateStatus.isPending || selectedStatus === null}
              className="px-4 py-1.5 bg-accent text-bg-page rounded text-sm font-medium disabled:opacity-40 hover:bg-accent/90 transition-colors"
            >
              {t('common.save')}
            </button>
          </div>
        </div>
      )}

      {/* Player data (officer+) */}
      {isOfficerPlus && (
        <div className="bg-bg-card border border-border rounded-lg p-4">
          <div className="text-sm font-medium text-text-primary mb-2">{t('tickets.playerData')}</div>
          {ticket.playerData ? (
            <pre className="text-xs text-text-muted whitespace-pre-wrap">
              {JSON.stringify(ticket.playerData, null, 2)}
            </pre>
          ) : (
            <div className="text-sm text-text-muted">{t('tickets.noPlayerData')}</div>
          )}
        </div>
      )}

      {/* Comments */}
      <div className="bg-bg-card border border-border rounded-lg p-4 space-y-4">
        <div className="text-sm font-medium text-text-primary">{t('tickets.comments')}</div>
        {ticket.comments.length === 0 && (
          <div className="text-sm text-text-muted">—</div>
        )}
        {ticket.comments.map((comment) => (
          <div key={comment.id} className="border-t border-border pt-3">
            <div className="flex items-center gap-2 mb-1">
              <span className="text-xs font-medium text-accent">{comment.authorUsername}</span>
              <span className="text-xs text-text-muted">
                {new Date(comment.createdAt).toLocaleString('ru-RU')}
              </span>
            </div>
            <p className="text-sm text-text-primary">{comment.content}</p>
          </div>
        ))}

        {/* Add comment form (officer+) */}
        {isOfficerPlus && (
          <div className="border-t border-border pt-3 space-y-2">
            <textarea
              value={commentText}
              onChange={(e) => setCommentText(e.target.value)}
              placeholder={t('tickets.commentContent')}
              rows={3}
              className="w-full bg-bg-page border border-border rounded px-3 py-2 text-text-primary text-sm focus:outline-none focus:border-accent resize-none"
              maxLength={1000}
            />
            <button
              onClick={() => commentText.trim() && addComment.mutate(commentText.trim())}
              disabled={addComment.isPending || !commentText.trim()}
              className="px-4 py-1.5 bg-accent text-bg-page rounded text-sm font-medium disabled:opacity-40 hover:bg-accent/90 transition-colors"
            >
              {t('tickets.addComment')}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
