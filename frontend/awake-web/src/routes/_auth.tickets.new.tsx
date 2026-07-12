import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketType } from '@/types/api'
import type { LoadoutSlot, Loadout } from '@/types/api'
import { ItemCombobox } from '@/components/ItemCombobox'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { ArrowLeft, Send } from 'lucide-react'

export const Route = createFileRoute('/_auth/tickets/new')({
  component: NewTicketPage,
})

function NewTicketPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [gameNickname, setGameNickname] = useState('')
  const [description, setDescription] = useState('')
  const [sniper, setSniper] = useState<LoadoutSlot | null>(null)
  const [weapon, setWeapon] = useState<LoadoutSlot | null>(null)
  const [armor, setArmor] = useState<LoadoutSlot | null>(null)
  const [error, setError] = useState<string | null>(null)

  const createTicket = useMutation({
    mutationFn: () => {
      const loadout: Loadout = {
        sniper,
        weapon: weapon!,
        armor: armor!,
      }
      return ticketsApi.create({
        gameNickname,
        type: TicketType.Recruitment,
        description,
        loadout,
      })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tickets'] })
      void navigate({ to: '/tickets' })
    },
    onError: () => setError(t('tickets.createError')),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!weapon || !armor) return
    setError(null)
    createTicket.mutate()
  }

  const canSubmit = !!weapon && !!armor && !!gameNickname.trim() && !!description.trim()

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
                rows={5}
                required
                maxLength={2000}
                placeholder="Расскажи о себе — опыт, достижения, почему хочешь в клан..."
                className="resize-none"
              />
            </div>

            {/* Loadout */}
            <div className="space-y-3">
              <label className="block text-sm font-medium text-muted-foreground">
                {t('tickets.loadout.title')}
              </label>

              <div className="space-y-1.5">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.sniperOptional')}</p>
                <ItemCombobox
                  categoryPrefix="weapon/sniper_rifle"
                  placeholder={t('tickets.loadout.search')}
                  value={sniper}
                  onChange={setSniper}
                />
              </div>

              <div className="space-y-1.5">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.weapon')} *</p>
                <ItemCombobox
                  categoryPrefix="weapon"
                  excludeCategory="weapon/sniper_rifle"
                  placeholder={t('tickets.loadout.search')}
                  value={weapon}
                  onChange={setWeapon}
                  required
                />
              </div>

              <div className="space-y-1.5">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.armor')} *</p>
                <ItemCombobox
                  categoryPrefix="armor"
                  placeholder={t('tickets.loadout.search')}
                  value={armor}
                  onChange={setArmor}
                  required
                />
              </div>
            </div>

            {error && (
              <div className="bg-destructive/10 border border-destructive/30 text-destructive text-sm rounded-lg px-4 py-3">
                {error}
              </div>
            )}

            <Button
              type="submit"
              disabled={createTicket.isPending || !canSubmit}
              className="w-full"
            >
              <Send size={15} />
              {createTicket.isPending ? t('common.loading') : t('tickets.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
