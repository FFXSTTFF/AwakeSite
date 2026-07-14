import { MessageSquare, ShieldCheck, Users } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Reveal } from '@/components/Reveal'

const DISCORD_INVITE = import.meta.env.VITE_DISCORD_INVITE_URL as string | undefined

const STEPS = [
  {
    icon: MessageSquare,
    title: 'Подай заявку',
    text: 'Напиши нам в Discord — расскажи о себе и своём опыте в STALCRAFT.',
  },
  {
    icon: Users,
    title: 'Пройди собеседование',
    text: 'Короткий разговор с офицером клана: цели, активность, отряд.',
  },
  {
    icon: ShieldCheck,
    title: 'Получи доступ',
    text: 'После принятия — доступ к платформе: статистика, отряды, тикеты.',
  },
] as const

export function JoinSection() {
  return (
    <section id="join" className="relative py-24">
      <div className="mx-auto max-w-6xl px-4">
        <Reveal>
          <h2 className="text-center text-3xl font-black tracking-tight md:text-4xl">
            Как <span className="text-accent">вступить</span>
          </h2>
          <p className="mt-3 text-center text-muted-foreground">Три шага до клана</p>
        </Reveal>

        <div className="mt-12 grid gap-6 md:grid-cols-3">
          {STEPS.map((step, i) => (
            <Reveal key={step.title} delayMs={i * 120}>
              <div className="h-full rounded-xl border border-border bg-card p-6 transition-all hover:-translate-y-0.5 hover:border-accent/30">
                <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-accent/10">
                  <step.icon size={20} className="text-accent" />
                </div>
                <h3 className="mt-4 text-lg font-bold">{`${i + 1}. ${step.title}`}</h3>
                <p className="mt-2 text-sm text-muted-foreground">{step.text}</p>
              </div>
            </Reveal>
          ))}
        </div>

        {DISCORD_INVITE && (
          <div className="mt-10 text-center">
            <Button asChild size="lg">
              <a href={DISCORD_INVITE} target="_blank" rel="noreferrer">
                Discord клана
              </a>
            </Button>
          </div>
        )}
      </div>
    </section>
  )
}
