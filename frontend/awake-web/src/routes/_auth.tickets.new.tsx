import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketType } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
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
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground mb-6 transition-colors"
      >
        <ArrowLeft size={14} /> {t('common.cancel')}
      </Link>

      <Card>
        <CardHeader>
          <CardTitle>{t('tickets.new')}</CardTitle>
          <CardDescription>Заполни форму — офицеры рассмотрят заявку</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Type selector */}
            <div>
              <label className="block text-sm font-medium text-muted-foreground mb-2">
                {t('tickets.type')}
              </label>
              <div className="grid grid-cols-2 gap-3">
                {TICKET_TYPES.map(({ value, icon: Icon, labelKey, desc }) => (
                  <button
                    key={value}
                    type="button"
                    onClick={() => setType(value)}
                    className={`flex flex-col items-start gap-1.5 p-4 rounded-lg border text-left transition-all ${
                      type === value
                        ? 'border-accent/50 bg-accent/8 ring-1 ring-accent/30'
                        : 'border-border bg-card hover:bg-secondary'
                    }`}
                  >
                    <Icon size={17} className={type === value ? 'text-accent' : 'text-muted-foreground'} />
                    <span className={`text-sm font-semibold ${type === value ? 'text-accent' : 'text-foreground'}`}>
                      {t(labelKey)}
                    </span>
                    <span className="text-xs text-muted-foreground leading-snug">{desc}</span>
                  </button>
                ))}
              </div>
            </div>

            {/* Nickname */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-muted-foreground">
                {t('tickets.gameNickname')}
              </label>
              <Input
                value={gameNickname}
                onChange={(e) => setGameNickname(e.target.value)}
                required
                maxLength={100}
                placeholder="Твой никнейм в игре"
              />
            </div>

            {/* Description */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium text-muted-foreground">
                  {t('tickets.description')}
                </label>
                <span className="text-xs text-muted-foreground">{description.length}/2000</span>
              </div>
              <Textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={6}
                required
                maxLength={2000}
                placeholder="Расскажи о себе — опыт, достижения, почему хочешь в клан..."
                className="resize-none"
              />
            </div>

            {error && (
              <div className="bg-destructive/10 border border-destructive/30 text-destructive text-sm rounded-lg px-4 py-3">
                {error}
              </div>
            )}

            <Button type="submit" disabled={createTicket.isPending} className="w-full">
              <Send size={15} />
              {createTicket.isPending ? t('common.loading') : t('tickets.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
