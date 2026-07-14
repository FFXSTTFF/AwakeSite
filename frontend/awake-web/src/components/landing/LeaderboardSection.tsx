import { useQuery } from '@tanstack/react-query'
import { Trophy } from 'lucide-react'
import { publicApi } from '@/api/public'
import { Reveal } from '@/components/Reveal'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

const MEDAL_CLASSES: Record<number, string> = {
  0: 'text-yellow-400',
  1: 'text-zinc-300',
  2: 'text-amber-600',
}

export function LeaderboardSection() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['public', 'leaderboard'],
    queryFn: publicApi.getLeaderboard,
    staleTime: 5 * 60_000,
  })

  return (
    <section id="leaderboard" className="relative py-24">
      {/* свечение без overflow-hidden: перетекает в соседние секции, шва нет
          (горизонтальный клип — на обёртке страницы) */}
      <div
        aria-hidden
        className="absolute left-1/2 top-0 h-96 w-[600px] -translate-x-1/2 rounded-full bg-accent/[0.07] blur-[130px]"
      />
      <div className="relative mx-auto max-w-4xl px-4">
        <Reveal>
          <h2 className="text-center text-3xl font-black tracking-tight md:text-4xl">
            Топ <span className="text-accent">игроков</span>
          </h2>
          <p className="mt-3 text-center text-muted-foreground">
            Лучшие бойцы клана по данным статистики
          </p>
        </Reveal>

        <Reveal delayMs={120} className="mt-12">
          {isLoading ? (
            <div className="space-y-2">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-14 w-full rounded-xl" />
              ))}
            </div>
          ) : isError ? (
            <p className="text-center text-muted-foreground">
              Не удалось загрузить статистику — обнови страницу.
            </p>
          ) : !data || data.length === 0 ? (
            <p className="text-center text-muted-foreground">
              Статистика появится совсем скоро.
            </p>
          ) : (
            <ol className="space-y-2">
              {data.map((entry, i) => (
                <li
                  key={entry.gameNickname}
                  className="flex items-center gap-4 rounded-xl border border-border bg-card px-4 py-3 transition-colors hover:border-accent/30"
                >
                  <span
                    className={cn(
                      'w-8 shrink-0 text-center text-lg font-black',
                      MEDAL_CLASSES[i] ?? 'text-muted-foreground',
                    )}
                  >
                    {i < 3 ? <Trophy size={18} className="inline" /> : i + 1}
                  </span>
                  <span className="min-w-0 flex-1 truncate font-bold">
                    {entry.gameNickname}
                  </span>
                  <span className="shrink-0 text-sm">
                    <span className="font-bold text-accent">{entry.kills.toLocaleString('ru-RU')}</span>
                    <span className="text-muted-foreground"> киллов</span>
                  </span>
                  <span className="hidden shrink-0 text-sm text-muted-foreground sm:inline">
                    {entry.accuracy}
                  </span>
                  <span className="hidden shrink-0 text-sm text-muted-foreground md:inline">
                    {entry.playtime}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </Reveal>
      </div>
    </section>
  )
}
