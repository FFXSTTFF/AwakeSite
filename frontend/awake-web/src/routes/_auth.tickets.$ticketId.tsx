import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketStatus, UserRank } from '@/types/api'
import type { LoadoutSlot } from '@/types/api'
import { useAuthStore } from '@/store/authStore'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/_auth/tickets/$ticketId')({
  component: TicketDetailPage,
})

const STATUS_OPTIONS = [
  TicketStatus.InReview,
  TicketStatus.Approved,
  TicketStatus.Rejected,
] as const

const STATUS_VARIANT: Record<number, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  [TicketStatus.Pending]: 'secondary',
  [TicketStatus.InReview]: 'default',
  [TicketStatus.Approved]: 'default',
  [TicketStatus.Rejected]: 'destructive',
  [TicketStatus.Closed]: 'secondary',
}

const STATUS_CLASS: Record<number, string> = {
  [TicketStatus.Pending]: '',
  [TicketStatus.InReview]: 'bg-accent/10 text-accent border-accent/30 hover:bg-accent/20',
  [TicketStatus.Approved]: 'bg-green-400/10 text-green-400 border-green-400/30 hover:bg-green-400/20',
  [TicketStatus.Rejected]: '',
  [TicketStatus.Closed]: 'bg-muted text-muted-foreground border-border',
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

  if (isLoading) return <div className="text-muted-foreground">{t('common.loading')}</div>
  if (!ticket) return <div className="text-muted-foreground">{t('common.notFound')}</div>

  const statusClass = STATUS_CLASS[ticket.status] || ''

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      {/* Main info */}
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle className="text-lg">{ticket.gameNickname}</CardTitle>
              <p className="text-sm text-muted-foreground mt-1">
                {t(`tickets.types.${ticket.type}`)} · {t('tickets.author')}: {ticket.authorUsername}
              </p>
            </div>
            <Badge
              variant={STATUS_VARIANT[ticket.status]}
              className={statusClass}
            >
              {t(`tickets.statuses.${ticket.status}`)}
            </Badge>
          </div>
        </CardHeader>
        <Separator />
        <CardContent className="pt-4">
          <p className="text-sm text-foreground whitespace-pre-wrap">{ticket.description}</p>
          <p className="text-xs text-muted-foreground mt-4">
            {new Date(ticket.createdAt).toLocaleString('ru-RU')}
            {ticket.reviewedByUsername && (
              <> · Рассмотрел: <span className="text-accent">{ticket.reviewedByUsername}</span></>
            )}
          </p>
        </CardContent>
      </Card>

      {/* Officer controls */}
      {isOfficerPlus && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-muted-foreground">{t('tickets.changeStatus')}</CardTitle>
          </CardHeader>
          <CardContent className="flex items-center gap-3">
            <Select
              value={selectedStatus?.toString() ?? ticket.status.toString()}
              onValueChange={(v) => setSelectedStatus(Number(v) as TicketStatus)}
            >
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map((s) => (
                  <SelectItem key={s} value={s.toString()}>
                    {t(`tickets.statuses.${s}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              onClick={() => selectedStatus !== null && updateStatus.mutate(selectedStatus)}
              disabled={updateStatus.isPending || selectedStatus === null}
              size="sm"
            >
              {t('common.save')}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Player data */}
      {isOfficerPlus && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium">{t('tickets.playerData')}</CardTitle>
          </CardHeader>
          <CardContent>
            {ticket.playerData ? (
              <pre className="text-xs text-muted-foreground whitespace-pre-wrap bg-secondary rounded-md p-3">
                {JSON.stringify(ticket.playerData, null, 2)}
              </pre>
            ) : (
              <p className="text-sm text-muted-foreground">{t('tickets.noPlayerData')}</p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Loadout */}
      {ticket.loadout && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium">{t('tickets.loadout.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2.5">
            <LoadoutRow label={t('tickets.loadout.sniper')} slot={ticket.loadout.sniper} emptyText={t('tickets.loadout.noSniper')} />
            <LoadoutRow label={t('tickets.loadout.weapon')} slot={ticket.loadout.weapon} />
            <LoadoutRow label={t('tickets.loadout.armor')} slot={ticket.loadout.armor} />
          </CardContent>
        </Card>
      )}

      {/* Comments */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">{t('tickets.comments')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {ticket.comments.length === 0 && (
            <p className="text-sm text-muted-foreground">—</p>
          )}
          {ticket.comments.map((comment, i) => (
            <div key={comment.id}>
              {i > 0 && <Separator className="mb-4" />}
              <div className="flex items-center gap-2 mb-1.5">
                <span className="text-xs font-semibold text-accent">{comment.authorUsername}</span>
                <span className="text-xs text-muted-foreground">
                  {new Date(comment.createdAt).toLocaleString('ru-RU')}
                </span>
              </div>
              <p className="text-sm text-foreground">{comment.content}</p>
            </div>
          ))}

          {isOfficerPlus && ticket.status !== TicketStatus.Closed && (
            <>
              {ticket.comments.length > 0 && <Separator />}
              <div className="space-y-3">
                <Textarea
                  value={commentText}
                  onChange={(e) => setCommentText(e.target.value)}
                  placeholder={t('tickets.commentContent')}
                  rows={3}
                  maxLength={1000}
                  className="resize-none"
                />
                <Button
                  size="sm"
                  onClick={() => commentText.trim() && addComment.mutate(commentText.trim())}
                  disabled={addComment.isPending || !commentText.trim()}
                >
                  {t('tickets.addComment')}
                </Button>
              </div>
            </>
          )}
          {ticket.status === TicketStatus.Closed && (
            <p className="text-xs text-muted-foreground">{t('tickets.closed')}</p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function LoadoutRow({ label, slot, emptyText }: { label: string; slot: LoadoutSlot | null; emptyText?: string }) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs text-muted-foreground w-32 shrink-0">{label}</span>
      {slot ? (
        <div className="flex items-center gap-2 flex-1">
          <img
            src={slot.itemIcon}
            alt=""
            className="w-6 h-6 object-contain shrink-0"
            onError={(e) => (e.currentTarget.style.display = 'none')}
          />
          <span className="text-sm text-foreground">{slot.itemName}</span>
        </div>
      ) : (
        <span className="text-sm text-muted-foreground">{emptyText ?? '—'}</span>
      )}
    </div>
  )
}
