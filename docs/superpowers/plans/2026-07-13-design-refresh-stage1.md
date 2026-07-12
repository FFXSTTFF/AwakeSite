# Design Refresh — Stage 1 (фундамент + лендинг) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Публичный лендинг `/` в новом визуальном стиле (референс lunar-zone.com в зелёной гамме Static Green) + публичный эндпоинт топа игроков + обновлённый визуальный фундамент (шрифт, фон, карточки, кнопки, скелетоны, reveal-анимации) + рестайл логина.

**Architecture:** Бэкенд — один новый анонимный эндпоинт `GET /api/public/leaderboard` (MediatR-query поверх существующих репозиториев, IMemoryCache 5 мин). Фронтенд — новые утилити-классы в `index.css` (сетка, свечения, reveal), точечные правки shadcn-компонентов, новая папка `src/components/landing/` с секциями лендинга, `routes/index.tsx` перестаёт редиректить и становится лендингом.

**Tech Stack:** ASP.NET Core (net10, MediatR, Moq+FluentAssertions+xUnit), React 19 + Vite + TanStack Router/Query + Tailwind v4 + shadcn/ui, шрифт `@fontsource-variable/inter`.

**Spec:** `docs/superpowers/specs/2026-07-13-design-refresh-design.md`

## Global Constraints

- Палитра без изменений: фон `#0e0e0f`, карточки `#161618`, рамки `#1f1f22`, текст `#f0ede8`, muted `#6b6b72`, акцент `#3ddc84`. HSL-токены в `index.css` НЕ менять.
- Свечения — только приглушённые (непрозрачность ≤ 0.14), никакого неона.
- Анимации — CSS + IntersectionObserver. НЕ добавлять framer-motion/motion/GSAP.
- Шрифт Inter — только self-hosted через `@fontsource-variable/inter`. Никаких Google Fonts CDN.
- Весь пользовательский текст — русский.
- Leaderboard отдаёт ТОЛЬКО: игровой ник, киллы, точность, часы. Никаких Discord-id, внутренних id, рангов.
- Ссылка Discord-приглашения — `VITE_DISCORD_INVITE_URL`; если переменная пуста/отсутствует, кнопка Discord не рендерится.
- Рабочая ветка: `feature/design-refresh` от свежего `master` (изолированный worktree через superpowers:using-git-worktrees).
- JSON от API — camelCase (стандарт ASP.NET): `gameNickname`, `kills`, `accuracy`, `playtime`.

---

### Task 1: Backend — GetLeaderboardQuery + репозиторий + тесты

**Files:**
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerStatsSnapshotRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/PlayerStatsSnapshotRepository.cs`
- Create: `src/Awake.Application/Features/Public/Queries/GetLeaderboard/LeaderboardEntryDto.cs`
- Create: `src/Awake.Application/Features/Public/Queries/GetLeaderboard/GetLeaderboardQuery.cs`
- Create: `src/Awake.Application/Features/Public/Queries/GetLeaderboard/GetLeaderboardQueryHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Public/GetLeaderboardQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IUserRepository.GetByMinRankAsync(UserRank minRank, CancellationToken)` → `IReadOnlyList<User>` (уже существует); `PlayerStatsSnapshot` (поля `GameNickname`, `Kills`, `Accuracy`, `Playtime`).
- Produces: `GetLeaderboardQuery(int Count = 10) : IRequest<IReadOnlyList<LeaderboardEntryDto>>`; `LeaderboardEntryDto(string GameNickname, int Kills, string Accuracy, string Playtime)`; `IPlayerStatsSnapshotRepository.GetByNicknamesAsync(IReadOnlyCollection<string>, CancellationToken)` → `IReadOnlyList<PlayerStatsSnapshot>`. Task 2 использует query, ничего больше.

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/Awake.Unit.Tests/Features/Public/GetLeaderboardQueryHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Public.Queries.GetLeaderboard;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Public;

public class GetLeaderboardQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetLeaderboardQueryHandler CreateHandler()
        => new(_users.Object, _snapshots.Object);

    private static User Member(string nickname) => new()
    {
        Id = Guid.NewGuid(), Username = nickname,
        Rank = UserRank.Member, GameNickname = nickname
    };

    private static PlayerStatsSnapshot Snapshot(string nickname, int kills) => new()
    {
        Id = Guid.NewGuid(), GameNickname = nickname, Kills = kills,
        Accuracy = "50%", Playtime = "100 ч.", FetchedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Handle_SortsByKillsDescending_AndLimitsToCount()
    {
        var users = Enumerable.Range(1, 12).Select(i => Member($"player{i}")).ToList();
        var snapshots = Enumerable.Range(1, 12)
            .Select(i => Snapshot($"player{i}", kills: i * 100)).ToList();

        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync(users);
        _snapshots.Setup(r => r.GetByNicknamesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(snapshots);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().HaveCount(10);
        result[0].GameNickname.Should().Be("player12");
        result[0].Kills.Should().Be(1200);
        result.Select(e => e.Kills).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Handle_SkipsUsersWithoutNickname()
    {
        var withNick = Member("sniper");
        var withoutNick = new User
        {
            Id = Guid.NewGuid(), Username = "ghost",
            Rank = UserRank.Member, GameNickname = null
        };

        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([withNick, withoutNick]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(
                It.Is<IReadOnlyCollection<string>>(n => n.Count == 1 && n.Contains("sniper")),
                It.IsAny<CancellationToken>()))
              .ReturnsAsync([Snapshot("sniper", 500)]);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().ContainSingle(e => e.GameNickname == "sniper");
    }

    [Fact]
    public async Task Handle_NoMembersWithNickname_ReturnsEmpty_WithoutQueryingSnapshots()
    {
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _snapshots.Verify(r => r.GetByNicknamesAsync(
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Убедиться, что тесты не компилируются/падают**

Run: `dotnet test tests/Awake.Unit.Tests --filter GetLeaderboard`
Expected: ошибка компиляции — `GetLeaderboardQuery` не существует.

- [ ] **Step 3: Реализация**

`src/Awake.Application/Features/Public/Queries/GetLeaderboard/LeaderboardEntryDto.cs`:

```csharp
namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public record LeaderboardEntryDto(
    string GameNickname,
    int Kills,
    string Accuracy,
    string Playtime);
```

`src/Awake.Application/Features/Public/Queries/GetLeaderboard/GetLeaderboardQuery.cs`:

```csharp
using MediatR;

namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public record GetLeaderboardQuery(int Count = 10) : IRequest<IReadOnlyList<LeaderboardEntryDto>>;
```

`src/Awake.Application/Features/Public/Queries/GetLeaderboard/GetLeaderboardQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Public.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandler(
    IUserRepository userRepository,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardEntryDto>>
{
    public async Task<IReadOnlyList<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var members = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);
        var nicknames = members
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .Distinct()
            .ToList();

        if (nicknames.Count == 0)
            return [];

        var snapshots = await snapshotRepository.GetByNicknamesAsync(nicknames, cancellationToken);

        return snapshots
            .OrderByDescending(s => s.Kills)
            .Take(request.Count)
            .Select(s => new LeaderboardEntryDto(s.GameNickname, s.Kills, s.Accuracy, s.Playtime))
            .ToList();
    }
}
```

В `IPlayerStatsSnapshotRepository.cs` добавить метод в интерфейс:

```csharp
Task<IReadOnlyList<PlayerStatsSnapshot>> GetByNicknamesAsync(
    IReadOnlyCollection<string> gameNicknames, CancellationToken ct = default);
```

В `PlayerStatsSnapshotRepository.cs` добавить реализацию:

```csharp
public async Task<IReadOnlyList<PlayerStatsSnapshot>> GetByNicknamesAsync(
    IReadOnlyCollection<string> gameNicknames, CancellationToken ct = default)
    => await context.PlayerStatsSnapshots
        .Where(s => gameNicknames.Contains(s.GameNickname))
        .ToListAsync(ct);
```

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/Awake.Unit.Tests`
Expected: все зелёные (старые 57 + 3 новых).

- [ ] **Step 5: Commit**

```bash
git add src/Awake.Application src/Awake.Infrastructure tests/Awake.Unit.Tests
git commit -m "feat(api): add GetLeaderboardQuery for public top players"
```

---

### Task 2: Backend — PublicController с кэшем 5 минут

**Files:**
- Create: `src/Awake.API/Controllers/PublicController.cs`
- Modify: `src/Awake.API/Program.cs` (строка ~19, рядом с `AddControllers`)

**Interfaces:**
- Consumes: `GetLeaderboardQuery` из Task 1.
- Produces: HTTP `GET /api/public/leaderboard` → `200 OK`, JSON-массив `[{ gameNickname, kills, accuracy, playtime }]`, доступен без авторизации. Фронт (Task 4) ходит именно сюда.

- [ ] **Step 1: Зарегистрировать IMemoryCache**

В `src/Awake.API/Program.cs` после строки `builder.Services.AddControllers();` добавить:

```csharp
builder.Services.AddMemoryCache();
```

- [ ] **Step 2: Контроллер**

`src/Awake.API/Controllers/PublicController.cs`:

```csharp
using Awake.Application.Features.Public.Queries.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(ISender sender, IMemoryCache cache) : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Публичный топ для лендинга: кэш 5 минут, чтобы не грузить БД
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(CancellationToken ct)
    {
        var entries = await cache.GetOrCreateAsync("public:leaderboard", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await sender.Send(new GetLeaderboardQuery(), ct);
        });
        return Ok(entries);
    }
}
```

- [ ] **Step 3: Сборка и живая проверка без токена**

```bash
dotnet build src/Awake.API
docker-compose build api && docker-compose up -d api
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/public/leaderboard
curl -s http://localhost:5001/api/public/leaderboard
```

Expected: `200`; JSON-массив (в dev-базе есть минимум один снапшот YapYaps → массив непустой, поля camelCase: `gameNickname`, `kills`, `accuracy`, `playtime`). Заголовок Authorization НЕ передавать — эндпоинт анонимный.

- [ ] **Step 4: Прогнать все тесты**

Run: `dotnet test`
Expected: все зелёные.

- [ ] **Step 5: Commit**

```bash
git add src/Awake.API
git commit -m "feat(api): public leaderboard endpoint with 5-min cache"
```

---

### Task 3: Frontend — фундамент стиля (шрифт, фон, базовые компоненты)

**Files:**
- Modify: `frontend/awake-web/package.json` (через `npm install`)
- Modify: `frontend/awake-web/src/index.css`
- Modify: `frontend/awake-web/src/components/ui/button.tsx` (строка 12, вариант `default`)
- Modify: `frontend/awake-web/src/components/ui/card.tsx` (строка 12, класс `rounded-lg`)
- Create: `frontend/awake-web/src/components/ui/skeleton.tsx`
- Create: `frontend/awake-web/src/components/Reveal.tsx`

**Interfaces:**
- Produces: CSS-классы `.reveal`/`.reveal-visible` (управляются компонентом `Reveal`); компонент `<Reveal delayMs={число} className="...">{children}</Reveal>`; компонент `<Skeleton className="..." />`. Task 4–6 используют их.

- [ ] **Step 1: Установить шрифт**

```bash
cd frontend/awake-web && npm install @fontsource-variable/inter
```

- [ ] **Step 2: index.css — шрифт, сетка-фон, reveal**

Заменить содержимое `frontend/awake-web/src/index.css` на:

```css
@import "tailwindcss";
@import "@fontsource-variable/inter";

@layer base {
  :root {
    --background: 240 6% 6%;
    --foreground: 38 21% 93%;
    --card: 240 5% 9%;
    --card-foreground: 38 21% 93%;
    --popover: 240 5% 9%;
    --popover-foreground: 38 21% 93%;
    --primary: 147 68% 55%;
    --primary-foreground: 240 6% 6%;
    --secondary: 240 5% 13%;
    --secondary-foreground: 38 21% 93%;
    --muted: 240 5% 13%;
    --muted-foreground: 240 4% 43%;
    --accent: 147 68% 55%;
    --accent-foreground: 240 6% 6%;
    --destructive: 0 62% 55%;
    --destructive-foreground: 38 21% 93%;
    --border: 240 5% 17%;
    --input: 240 5% 13%;
    --ring: 147 68% 55%;
    --radius: 0.5rem;
  }
}

body {
  margin: 0;
  background-color: hsl(var(--background));
  color: hsl(var(--foreground));
  font-family: 'Inter Variable', system-ui, -apple-system, sans-serif;
  /* Едва заметная сетка-миллиметровка по всему сайту (референс lunar-zone) */
  background-image:
    linear-gradient(rgba(240, 237, 232, 0.03) 1px, transparent 1px),
    linear-gradient(90deg, rgba(240, 237, 232, 0.03) 1px, transparent 1px);
  background-size: 64px 64px;
}

/* Появление секций при скролле; класс reveal-visible вешает компонент Reveal */
.reveal {
  opacity: 0;
  transform: translateY(16px);
  transition: opacity 0.6s ease, transform 0.6s ease;
}

.reveal-visible {
  opacity: 1;
  transform: none;
}

@media (prefers-reduced-motion: reduce) {
  .reveal {
    transition: none;
    transform: none;
    opacity: 1;
  }
}
```

- [ ] **Step 3: Кнопка — свечение primary на hover**

В `button.tsx` заменить строку варианта `default`:

```ts
default: "bg-primary text-primary-foreground hover:bg-primary/90 transition-shadow hover:shadow-[0_0_20px_rgba(61,220,132,0.35)]",
```

- [ ] **Step 4: Карточка — скругление побольше**

В `card.tsx` в компоненте `Card` заменить `"rounded-lg border bg-card text-card-foreground shadow-sm"` на `"rounded-xl border bg-card text-card-foreground shadow-sm"`.

- [ ] **Step 5: Skeleton (shadcn)**

`frontend/awake-web/src/components/ui/skeleton.tsx`:

```tsx
import * as React from "react"

import { cn } from "@/lib/utils"

function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn("animate-pulse rounded-md bg-secondary", className)} {...props} />
  )
}

export { Skeleton }
```

- [ ] **Step 6: Reveal-компонент**

`frontend/awake-web/src/components/Reveal.tsx`:

```tsx
import { useEffect, useRef, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

// Плавное появление блока при попадании во вьюпорт (CSS-классы в index.css)
export function Reveal({
  children,
  className,
  delayMs = 0,
}: {
  children: ReactNode
  className?: string
  delayMs?: number
}) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          window.setTimeout(() => el.classList.add('reveal-visible'), delayMs)
          observer.disconnect()
        }
      },
      { threshold: 0.15 },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [delayMs])

  return (
    <div ref={ref} className={cn('reveal', className)}>
      {children}
    </div>
  )
}
```

- [ ] **Step 7: Проверка сборки**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```

Expected: 0 ошибок tsc, сборка Vite успешна.

- [ ] **Step 8: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(web): visual foundation - Inter font, grid bg, reveal, skeleton, glow button"
```

---

### Task 4: Frontend — публичный API-клиент + секции «Топ игроков» и «Как вступить»

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts` (добавить интерфейс в конец файла)
- Create: `frontend/awake-web/src/api/public.ts`
- Create: `frontend/awake-web/src/components/landing/LeaderboardSection.tsx`
- Create: `frontend/awake-web/src/components/landing/JoinSection.tsx`

**Interfaces:**
- Consumes: `apiClient.get<T>(path)` из `@/api/client`; `Skeleton`, `Reveal` из Task 3; эндпоинт `/public/leaderboard` из Task 2.
- Produces: `publicApi.getLeaderboard(): Promise<LeaderboardEntryDto[]>`; компоненты `<LeaderboardSection />` и `<JoinSection />` (без пропсов). Task 5 вставляет их в страницу.

- [ ] **Step 1: Тип DTO**

В конец `frontend/awake-web/src/types/api.ts` добавить:

```ts
export interface LeaderboardEntryDto {
  gameNickname: string
  kills: number
  accuracy: string
  playtime: string
}
```

- [ ] **Step 2: API-клиент**

`frontend/awake-web/src/api/public.ts`:

```ts
import { apiClient } from './client'
import type { LeaderboardEntryDto } from '@/types/api'

export const publicApi = {
  getLeaderboard: () => apiClient.get<LeaderboardEntryDto[]>('/public/leaderboard'),
}
```

- [ ] **Step 3: Секция «Топ игроков»**

`frontend/awake-web/src/components/landing/LeaderboardSection.tsx`:

```tsx
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
  const { data, isLoading } = useQuery({
    queryKey: ['public', 'leaderboard'],
    queryFn: publicApi.getLeaderboard,
    staleTime: 5 * 60_000,
  })

  return (
    <section id="leaderboard" className="relative py-24">
      {/* приглушённое свечение за заголовком секции */}
      <div
        aria-hidden
        className="absolute left-1/2 top-0 h-64 w-[600px] -translate-x-1/2 rounded-full bg-accent/10 blur-[100px]"
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
```

- [ ] **Step 4: Секция «Как вступить»**

`frontend/awake-web/src/components/landing/JoinSection.tsx`:

```tsx
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
```

- [ ] **Step 5: Проверка сборки**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```

Expected: 0 ошибок (компоненты ещё не подключены к роуту — это Task 5).

- [ ] **Step 6: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(web): leaderboard and join sections for landing"
```

---

### Task 5: Frontend — навбар, hero, футер и сборка лендинга на `/`

**Files:**
- Create: `frontend/awake-web/src/components/landing/LandingNav.tsx`
- Create: `frontend/awake-web/src/components/landing/HeroSection.tsx`
- Create: `frontend/awake-web/src/components/landing/LandingFooter.tsx`
- Modify: `frontend/awake-web/src/routes/index.tsx` (полная замена — сейчас там редирект)

**Interfaces:**
- Consumes: `<LeaderboardSection />`, `<JoinSection />` из Task 4; `Reveal` из Task 3; `useAuthStore` (селектор `isAuthenticated`); `Button` (`asChild`, `variant`, `size`).
- Produces: публичная страница `/` (лендинг). Больше никто ничего не потребляет.

- [ ] **Step 1: Навбар**

`frontend/awake-web/src/components/landing/LandingNav.tsx`:

```tsx
import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/store/authStore'

const API_URL = import.meta.env.VITE_API_URL ?? ''

export function LandingNav() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-border/60 bg-background/70 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <div className="flex items-center gap-2.5">
          <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
          <span className="font-bold">
            Awake <span className="text-accent">[LOVE]</span>
          </span>
        </div>
        {isAuthenticated ? (
          <Button asChild size="sm">
            <Link to="/dashboard">В дашборд</Link>
          </Button>
        ) : (
          <Button asChild size="sm">
            <a href={`${API_URL}/api/auth/discord/login`}>Войти</a>
          </Button>
        )}
      </div>
    </header>
  )
}
```

- [ ] **Step 2: Hero**

`frontend/awake-web/src/components/landing/HeroSection.tsx`:

```tsx
import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Reveal } from '@/components/Reveal'
import { useAuthStore } from '@/store/authStore'

const API_URL = import.meta.env.VITE_API_URL ?? ''

export function HeroSection() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <section className="relative overflow-hidden pb-24 pt-32">
      {/* приглушённое зелёное свечение за заголовком */}
      <div
        aria-hidden
        className="absolute -top-40 left-1/4 h-[600px] w-[600px] rounded-full bg-accent/10 blur-[120px]"
      />
      <div className="relative mx-auto grid max-w-6xl items-center gap-12 px-4 md:grid-cols-2">
        <Reveal>
          <div>
            <span className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <span className="h-1.5 w-1.5 rounded-full bg-accent" />
              Клановая платформа STALCRAFT
            </span>
            <h1 className="mt-6 text-4xl font-black leading-[1.05] tracking-tight md:text-6xl">
              Играем вместе.
              <br />
              Побеждаем <span className="text-accent">вместе</span>.
            </h1>
            <p className="mt-6 max-w-md text-lg text-muted-foreground">
              Awake [LOVE] — клан STALCRAFT со своей платформой: статистика
              игроков, отряды и рекрутинг в одном месте.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              {isAuthenticated ? (
                <Button asChild size="lg">
                  <Link to="/dashboard">Открыть дашборд</Link>
                </Button>
              ) : (
                <Button asChild size="lg">
                  <a href={`${API_URL}/api/auth/discord/login`}>Войти через Discord</a>
                </Button>
              )}
              <Button asChild variant="outline" size="lg">
                <a href="#join">Как вступить</a>
              </Button>
            </div>
          </div>
        </Reveal>

        {/* Кольцо-декор: зарезервированное место под будущий арт клана */}
        <Reveal delayMs={150} className="hidden justify-center md:flex">
          <div className="relative h-80 w-80">
            <div className="absolute inset-0 rounded-full bg-accent/10 blur-3xl" />
            <div className="absolute inset-0 rounded-full border-[14px] border-accent/80" />
            <div className="absolute inset-6 rounded-full border border-accent/20" />
            <div className="absolute inset-0 flex items-center justify-center">
              <span className="text-3xl font-black tracking-widest text-accent">[LOVE]</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  )
}
```

- [ ] **Step 3: Футер**

`frontend/awake-web/src/components/landing/LandingFooter.tsx`:

```tsx
export function LandingFooter() {
  return (
    <footer className="border-t border-border py-8">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-3 px-4 text-sm text-muted-foreground md:flex-row">
        <span>
          Awake <span className="text-accent">[LOVE]</span> · STALCRAFT
        </span>
        <span>© {new Date().getFullYear()} stalcraftclans.cc</span>
      </div>
    </footer>
  )
}
```

- [ ] **Step 4: Собрать лендинг на `/`**

Полностью заменить `frontend/awake-web/src/routes/index.tsx`:

```tsx
import { createFileRoute } from '@tanstack/react-router'
import { LandingNav } from '@/components/landing/LandingNav'
import { HeroSection } from '@/components/landing/HeroSection'
import { LeaderboardSection } from '@/components/landing/LeaderboardSection'
import { JoinSection } from '@/components/landing/JoinSection'
import { LandingFooter } from '@/components/landing/LandingFooter'

export const Route = createFileRoute('/')({
  component: LandingPage,
})

// Публичная витрина клана: редиректа на /login больше нет,
// авторизованные видят кнопки «В дашборд» вместо «Войти»
function LandingPage() {
  return (
    <div className="min-h-screen bg-background">
      <LandingNav />
      <main>
        <HeroSection />
        <LeaderboardSection />
        <JoinSection />
      </main>
      <LandingFooter />
    </div>
  )
}
```

- [ ] **Step 5: Проверка сборки и живой смоук**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```

Expected: 0 ошибок. Затем при работающем Vite dev (`http://localhost:5173`):

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5173/
```

Expected: `200`. Открыть `/` — лендинг рендерится, hero и секции видны, топ подтягивает данные (или показывает «Статистика появится совсем скоро»).

- [ ] **Step 6: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(web): public landing page with hero, leaderboard, join sections"
```

---

### Task 6: Рестайл логина + финальная проверка этапа (скриншоты)

**Files:**
- Modify: `frontend/awake-web/src/routes/login.tsx`

**Interfaces:**
- Consumes: текущую разметку login.tsx (карточка + кнопка Discord) — сохранить поведение (`validateSearch` с `error`, ссылка на `${API_URL}/api/auth/discord/login`).
- Produces: ничего нового для других задач; это финальная задача этапа.

- [ ] **Step 1: Атмосфера на логине**

В `frontend/awake-web/src/routes/login.tsx` заменить корневой `div` компонента `LoginPage` (строка `<div className="min-h-screen bg-background flex items-center justify-center px-4">`) на версию со свечением, а блок Brand — на крупный заголовок:

```tsx
function LoginPage() {
  const { error } = Route.useSearch()

  return (
    <div className="relative min-h-screen overflow-hidden bg-background flex items-center justify-center px-4">
      {/* приглушённое свечение за карточкой */}
      <div
        aria-hidden
        className="absolute left-1/2 top-1/3 h-[500px] w-[500px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent/10 blur-[120px]"
      />
      <div className="relative w-full max-w-sm">
        {/* Brand */}
        <div className="mb-8 text-center">
          <div className="mb-2 inline-flex items-center gap-2">
            <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
            <span className="text-2xl font-black tracking-tight text-foreground">
              Awake <span className="text-accent">[LOVE]</span>
            </span>
          </div>
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            STALCRAFT · Clan Platform
          </p>
        </div>

        <Card>
          <CardHeader className="pb-4">
            <CardTitle className="text-center">Вход</CardTitle>
            <CardDescription className="text-center">
              Используй свой Discord-аккаунт
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {error === 'discord' && (
              <p className="text-center text-sm text-destructive">
                Не удалось войти через Discord. Попробуй ещё раз.
              </p>
            )}
            <Button asChild className="w-full">
              <a href={`${API_URL}/api/auth/discord/login`}>Войти через Discord</a>
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Полная проверка сборки и тестов**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
dotnet test
```

Expected: 0 ошибок tsc, сборка чистая, все тесты зелёные.

- [ ] **Step 3: Скриншоты 1440px и 390px через Chromium в контейнере**

Написать во временный файл `pw-landing.js` (вне репозитория, например в scratchpad) и прогнать через node-драйвер Playwright в контейнере `api` (он умеет ходить на `host.docker.internal:5173`):

```js
const pw = require('/app/.playwright/package/index.js');
(async () => {
  const b = await pw.chromium.launch({ args: ['--no-sandbox'] });
  for (const [name, width, height] of [['desktop', 1440, 900], ['mobile', 390, 844]]) {
    const p = await b.newPage({ viewport: { width, height } });
    await p.goto('http://host.docker.internal:5173/', { waitUntil: 'networkidle' });
    await p.screenshot({ path: `/tmp/landing-${name}.png`, fullPage: true });
    await p.close();
  }
  const p = await b.newPage({ viewport: { width: 1440, height: 900 } });
  await p.goto('http://host.docker.internal:5173/login', { waitUntil: 'networkidle' });
  await p.screenshot({ path: '/tmp/login.png' });
  console.log('done');
  await b.close();
})().catch((e) => { console.error(e.message); process.exit(1); });
```

```bash
# из каталога с docker-compose.yml
cat pw-landing.js | docker-compose exec -T api /app/.playwright/node/linux-x64/node -
API_CID=$(docker-compose ps -q api)
docker cp "$API_CID":/tmp/landing-desktop.png ./landing-desktop.png
docker cp "$API_CID":/tmp/landing-mobile.png ./landing-mobile.png
docker cp "$API_CID":/tmp/login.png ./login.png
```

Просмотреть все три скриншота. Expected: сетка-фон видна, hero с кольцом на десктопе (кольцо скрыто на мобиле — это by design), секции не вылезают за экран по горизонтали на 390px, логин с центрированной карточкой и свечением.

- [ ] **Step 4: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(web): restyle login page with new visual language"
```

---

## После выполнения плана

Пользователь смотрит лендинг и логин вживую (`http://localhost:5173/`) и утверждает стиль. Правки — здесь же. После одобрения: план этапа 2 (внутренние страницы + нижняя мобильная панель) пишется отдельно на основе утверждённого стиля, затем ветка идёт по стандартному циклу finishing-a-development-branch (PR в master).
