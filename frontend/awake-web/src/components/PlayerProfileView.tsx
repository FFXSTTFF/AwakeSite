import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { PlayerProfileDto } from '@/types/api'

const RANK_LABELS: Record<number, string> = {
  0: 'Гость', 1: 'Боец', 2: 'Офицер', 3: 'Полковник', 4: 'Лидер',
}

function timeAgo(iso: string): string {
  const hours = Math.floor((Date.now() - new Date(iso).getTime()) / 3_600_000)
  if (hours < 1) return 'меньше часа назад'
  if (hours < 24) return `${hours} ч. назад`
  return `${Math.floor(hours / 24)} дн. назад`
}

interface Props {
  profile: PlayerProfileDto
  onRefresh?: () => void
  refreshing?: boolean
}

export function PlayerProfileView({ profile, onRefresh, refreshing }: Props) {
  const { stats, loadout, squad } = profile

  return (
    <div className="flex flex-col gap-6">
      {/* Шапка: аватар + ники + ранг + отряд */}
      <div className="flex items-center gap-4">
        {profile.discordAvatarUrl ? (
          <img
            src={profile.discordAvatarUrl}
            alt={profile.username}
            className="w-16 h-16 rounded-full border border-border"
          />
        ) : (
          <div className="w-16 h-16 rounded-full bg-muted flex items-center justify-center text-2xl">
            {profile.username[0]?.toUpperCase()}
          </div>
        )}
        <div>
          <h1 className="text-xl font-bold text-foreground">{profile.username}</h1>
          <p className="text-sm text-muted-foreground">
            {profile.gameNickname ?? 'игровой ник не привязан'}
            {profile.discordUsername && ` · @${profile.discordUsername}`}
          </p>
          <div className="flex gap-2 mt-1">
            <Badge>{RANK_LABELS[profile.rank]}</Badge>
            {squad && (
              <Badge variant="outline">
                Отряд {squad.number} «{squad.name}»{squad.isLeader && ' · лидер'}
              </Badge>
            )}
          </div>
        </div>
      </div>

      {/* Статистика */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Игровая статистика</CardTitle>
          {stats && onRefresh && (
            <div className="flex items-center gap-3">
              <span className="text-xs text-muted-foreground">
                обновлено {timeAgo(stats.fetchedAt)}
              </span>
              <Button size="sm" variant="outline" onClick={onRefresh} disabled={refreshing}>
                {refreshing ? 'Обновляется…' : 'Обновить'}
              </Button>
            </div>
          )}
        </CardHeader>
        <CardContent>
          {stats ? (
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
              <StatTile label="Убийства" value={stats.kills.toLocaleString('ru-RU')} />
              <StatTile label="Смерти" value={stats.deaths.toLocaleString('ru-RU')} />
              <StatTile label="К/Д" value={stats.kdRatio.toFixed(2)} />
              <StatTile label="Точность" value={stats.accuracy} />
              <StatTile label="Время в игре" value={stats.playtime} />
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              Статистика ещё не загружена.
              {onRefresh && (
                <Button size="sm" variant="outline" className="ml-2" onClick={onRefresh} disabled={refreshing}>
                  {refreshing ? 'Загружается…' : 'Загрузить'}
                </Button>
              )}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Экипировка */}
      {loadout && (
        <Card>
          <CardHeader><CardTitle>Экипировка</CardTitle></CardHeader>
          <CardContent className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {loadout.sniper && <LoadoutTile label="Снайперка" slot={loadout.sniper} />}
            <LoadoutTile label="Основное оружие" slot={loadout.weapon} />
            <LoadoutTile label="Броня" slot={loadout.armor} />
          </CardContent>
        </Card>
      )}

      {/* История кланов */}
      {stats && stats.clanHistory.length > 0 && (
        <Card>
          <CardHeader><CardTitle>История кланов</CardTitle></CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1">
              {stats.clanHistory.map((c) => (
                <li key={c.clanTag} className="text-sm text-foreground">
                  <span className="text-accent font-medium">[{c.clanTag}]</span> {c.clanName}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function StatTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-bold text-foreground">{value}</p>
    </div>
  )
}

function LoadoutTile({ label, slot }: { label: string; slot: { itemName: string; upgrade: number } }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-sm font-medium text-foreground">
        {slot.itemName}
        {slot.upgrade > 0 && <span className="text-accent"> +{slot.upgrade}</span>}
      </p>
    </div>
  )
}
