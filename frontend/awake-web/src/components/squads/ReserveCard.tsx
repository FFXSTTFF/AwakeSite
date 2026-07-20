import { Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { Users } from 'lucide-react'
import { squadsApi } from '@/api/squads'
import { Card, CardContent } from '@/components/ui/card'
import { MemberHoverInfo } from '@/components/squads/MemberHoverInfo'

export function ReserveCard() {
  const { data: members, isLoading } = useQuery({
    queryKey: ['squads', 'reserve'],
    queryFn: squadsApi.getReserve,
  })

  if (isLoading || !members) return null

  return (
    <Card>
      <CardContent className="pt-5 pb-5">
        <div className="mb-4 flex items-center justify-between gap-2">
          <h2 className="flex items-center gap-2 text-base font-semibold text-foreground">
            <Users size={14} className="text-accent" />
            Резерв
          </h2>
          <span className="text-xs font-medium text-muted-foreground">{members.length}</span>
        </div>

        <div className="space-y-2">
          {members.length === 0 ? (
            <div className="text-sm text-muted-foreground">Все бойцы в отрядах.</div>
          ) : (
            members.map((m) => (
              <MemberHoverInfo
                key={m.userId}
                nickname={m.gameNickname ?? m.username}
                flags={m.flags}
                kd={m.kd}
                boosts={m.boosts}
              >
                <Link
                  to="/players/$userId"
                  params={{ userId: m.userId }}
                  className="block truncate text-sm text-foreground transition-colors hover:text-accent"
                >
                  {m.gameNickname ?? m.username}
                </Link>
              </MemberHoverInfo>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  )
}
