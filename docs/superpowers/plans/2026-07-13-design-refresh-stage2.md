# Design Refresh — Stage 2 (внутренние страницы + мобильная навигация) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Раскатать утверждённый визуальный язык этапа 1 на внутренние страницы (дашборд — приоритет), рестайлнуть sidebar, заменить мобильный гамбургер нижней таб-панелью, добавить скелетоны и пустые состояния, закрыть UI-долги финального ревью этапа 1.

**Architecture:** Только фронтенд (React 19 + Vite + Tailwind v4 + shadcn/ui), бэкенд не трогаем. Общие примитивы (BrandMark, discordLoginUrl, фикс Badge) — первым таском, дальше по странице за таск. Мобильная навигация — новый компонент `MobileTabBar`, монтируется в layout `_auth.tsx`; гамбургер и мобильный оверлей из Sidebar удаляются.

**Tech Stack:** React 19, TanStack Router/Query, Tailwind v4 (конфиг подключён через `@config`), shadcn/ui (Button, Card, Badge, Skeleton, Select, Table), lucide-react, zustand.

**Spec:** `docs/superpowers/specs/2026-07-13-design-refresh-design.md` (раздел 3). Стиль этапа 1 утверждён пользователем вживую.

## Global Constraints

- Палитра без изменений: акцент `#3ddc84`, HSL-токены в `index.css` НЕ менять.
- Свечения: фоновые пятна/орбы — непрозрачность ≤ 0.14; hover-тени интерактивных элементов — ≤ 0.35.
- Анимации — CSS + IntersectionObserver (`Reveal` уже есть). НЕ добавлять framer-motion/motion/GSAP и никакие новые npm-зависимости.
- Весь пользовательский текст — русский (бренд «Awake [LOVE]», «STALCRAFT» — исключения).
- Загрузка — компонент `Skeleton` из `@/components/ui/skeleton`, НЕ текст «Загрузка…».
- Мобильный брейкпоинт: `<768px` (`md:`). Нижняя панель видна только `md:hidden`; sidebar — только `hidden md:flex` (уже так).
- Существующее поведение (гарды, i18n-ключи, мутации, query-ключи) НЕ менять — только вид.
- Страницы `/_auth/*` требуют логина — автоматические скриншоты для них невозможны; проверка вёрстки внутренних страниц — живая приёмка пользователем, автоматика — tsc/build/тесты + скриншот-регрессия лендинга/логина.
- Заголовки страниц (h1): единый стиль `text-2xl font-black tracking-tight text-foreground` (дашборд — `text-3xl`).
- Рабочая ветка: текущая `worktree-design-refresh` (worktree D:\Awake\.claude\worktrees\design-refresh).

---

### Task 1: Общие примитивы + UI-долги этапа 1

**Files:**
- Create: `frontend/awake-web/src/components/BrandMark.tsx`
- Create: `frontend/awake-web/src/lib/discord.ts`
- Modify: `frontend/awake-web/src/components/ui/badge.tsx` (варианты default/secondary/destructive)
- Modify: `frontend/awake-web/src/components/landing/LandingNav.tsx`
- Modify: `frontend/awake-web/src/components/landing/HeroSection.tsx`
- Modify: `frontend/awake-web/src/components/landing/LeaderboardSection.tsx`
- Modify: `frontend/awake-web/src/routes/login.tsx`

**Interfaces:**
- Consumes: ничего нового.
- Produces: `<BrandMark size?: 'sm' | 'lg', className?: string />`; `discordLoginUrl: string` из `@/lib/discord`. Task 2 использует `BrandMark` в Sidebar.

- [ ] **Step 1: BrandMark**

`frontend/awake-web/src/components/BrandMark.tsx`:

```tsx
import { cn } from '@/lib/utils'

// Единый бренд-блок «точка + Awake [LOVE]» (лендинг, логин, sidebar)
export function BrandMark({
  size = 'sm',
  className,
}: {
  size?: 'sm' | 'lg'
  className?: string
}) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
      <span
        className={cn(
          'font-bold text-foreground',
          size === 'lg' && 'text-2xl font-black tracking-tight',
        )}
      >
        Awake <span className="text-accent">[LOVE]</span>
      </span>
    </div>
  )
}
```

- [ ] **Step 2: discordLoginUrl**

`frontend/awake-web/src/lib/discord.ts`:

```ts
const API_URL = import.meta.env.VITE_API_URL ?? ''

// Единая точка входа в Discord OAuth (лендинг, hero, логин)
export const discordLoginUrl = `${API_URL}/api/auth/discord/login`
```

- [ ] **Step 3: Badge — убрать hover у неинтерактивных вариантов**

Бейджи в проекте нигде не кликабельны; после включения Tailwind-конфига они начали темнеть на hover. В `badge.tsx` заменить три варианта:

```ts
        default:
          "border-transparent bg-primary text-primary-foreground",
        secondary:
          "border-transparent bg-secondary text-secondary-foreground",
        destructive:
          "border-transparent bg-destructive text-destructive-foreground",
```

(строки `hover:bg-*/80` удаляются; вариант `outline` не трогать).

- [ ] **Step 4: Подключить примитивы на лендинге и логине**

В `LandingNav.tsx`: удалить строку `const API_URL = import.meta.env.VITE_API_URL ?? ''`; добавить импорты `import { BrandMark } from '@/components/BrandMark'` и `import { discordLoginUrl } from '@/lib/discord'`; заменить бренд-блок:

```tsx
        <BrandMark />
```

(вместо `<div className="flex items-center gap-2.5">...точка+span...</div>`), а `href={`${API_URL}/api/auth/discord/login`}` → `href={discordLoginUrl}`.

В `HeroSection.tsx`: удалить `const API_URL ...`, добавить `import { discordLoginUrl } from '@/lib/discord'`, заменить `href={`${API_URL}/api/auth/discord/login`}` → `href={discordLoginUrl}`.

В `login.tsx`: удалить `const API_URL ...`, добавить импорты `BrandMark` и `discordLoginUrl`; заменить внутренности Brand-блока:

```tsx
        {/* Brand */}
        <div className="mb-8 text-center">
          <BrandMark size="lg" className="mb-2 justify-center" />
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            STALCRAFT · Clan Platform
          </p>
        </div>
```

и `href={`${API_URL}/api/auth/discord/login`}` → `href={discordLoginUrl}`.

- [ ] **Step 5: LeaderboardSection — ветка ошибки**

В `LeaderboardSection.tsx` деструктурировать `isError`: `const { data, isLoading, isError } = useQuery({...})`, и в JSX между `isLoading ? (...)` и проверкой пустоты вставить ветку:

```tsx
          ) : isError ? (
            <p className="text-center text-muted-foreground">
              Не удалось загрузить статистику — обнови страницу.
            </p>
```

- [ ] **Step 6: Проверка сборки**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```

Expected: 0 ошибок.

- [ ] **Step 7: Commit**

```bash
git add frontend/awake-web
git commit -m "refactor(web): BrandMark + discordLoginUrl dedup, badge hover fix, leaderboard error state"
```

---

### Task 2: Sidebar — рестайл (десктоп)

**Files:**
- Modify: `frontend/awake-web/src/components/layout/Sidebar.tsx`

**Interfaces:**
- Consumes: `BrandMark` из Task 1.
- Produces: ничего нового (Task 3 удалит из этого файла мобильную часть).

- [ ] **Step 1: Бренд-блок через BrandMark**

В `SidebarContent` заменить Brand-блок:

```tsx
      {/* Brand */}
      <div className="px-4 py-5">
        <BrandMark />
        <p className="mt-1 pl-[18px] text-xs text-muted-foreground">STALCRAFT</p>
      </div>
```

(добавить импорт `import { BrandMark } from '@/components/BrandMark'`; убрать ставший ненужным JSX точки/спана).

- [ ] **Step 2: Активный пункт — зелёная полоса слева**

Заменить `className` в компоненте `NavLink`:

```tsx
      className={cn(
        'relative flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium transition-all duration-200',
        isActive(to)
          ? 'bg-accent/10 text-accent before:absolute before:left-0 before:top-1/2 before:h-5 before:w-0.5 before:-translate-y-1/2 before:rounded-full before:bg-accent'
          : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
      )}
```

(остальное в NavLink — иконка, label, ChevronRight — без изменений).

- [ ] **Step 3: Ссылка «Настройки» — тот же стиль**

У Link на `/settings` заменить className на идентичный NavLink-овскому (тот же тернарный блок, что в Step 2, только `py-2` вместо `py-2.5`).

- [ ] **Step 4: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
git add frontend/awake-web
git commit -m "feat(web): restyle sidebar - brand mark, green active indicator"
```

---

### Task 3: Мобильная нижняя таб-панель

**Files:**
- Create: `frontend/awake-web/src/components/layout/MobileTabBar.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.tsx`
- Modify: `frontend/awake-web/src/components/layout/Sidebar.tsx` (удалить гамбургер и мобильный оверлей)

**Interfaces:**
- Consumes: `useAuthStore` (`user`, `logout`), `NotificationBell`, `Badge`, `UserRank`, i18n-ключи `users.ranks.*`, `nav.settings`, `nav.manage`, `nav.logout`.
- Produces: `<MobileTabBar />` без пропсов — монтируется в `_auth.tsx`.

- [ ] **Step 1: MobileTabBar**

`frontend/awake-web/src/components/layout/MobileTabBar.tsx`:

```tsx
import { Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  FileText,
  LayoutDashboard,
  LogOut,
  MoreHorizontal,
  Settings,
  Shield,
  UserCircle,
  Users,
} from 'lucide-react'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import { NotificationBell } from '@/components/layout/NotificationBell'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const TABS = [
  { to: '/dashboard' as const, label: 'Дашборд', icon: LayoutDashboard },
  { to: '/squads' as const, label: 'Отряды', icon: Shield },
  { to: '/tickets' as const, label: 'Тикеты', icon: FileText },
  { to: '/profile' as const, label: 'Профиль', icon: UserCircle },
]

const RANK_CLASSES: Record<number, string> = {
  [UserRank.Guest]: 'bg-secondary text-muted-foreground border-border',
  [UserRank.Member]: 'bg-blue-400/10 text-blue-400 border-blue-400/30',
  [UserRank.Officer]: 'bg-accent/10 text-accent border-accent/30',
  [UserRank.Colonel]: 'bg-yellow-400/10 text-yellow-400 border-yellow-400/30',
  [UserRank.Leader]: 'bg-destructive/10 text-destructive border-destructive/30',
}

// Нижняя навигация на мобиле (<md): 4 таба + «Ещё» с листом
// (настройки, управление по рангу, уведомления, выход)
export function MobileTabBar() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const [moreOpen, setMoreOpen] = useState(false)
  const pathname = useRouterState().location.pathname

  const isColonelPlus = (user?.rank ?? 0) >= UserRank.Colonel

  function isActive(path: string) {
    return pathname === path || pathname.startsWith(path + '/')
  }

  function handleLogout() {
    logout()
    setMoreOpen(false)
    void navigate({ to: '/login' })
  }

  return (
    <div className="md:hidden">
      {/* Лист «Ещё» */}
      {moreOpen && (
        <div className="fixed inset-0 z-40" onClick={() => setMoreOpen(false)}>
          <div className="absolute inset-0 bg-black/50" />
          <div
            className="absolute inset-x-0 bottom-16 rounded-t-xl border-t border-border bg-card p-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-center justify-between gap-2">
              <span className="truncate text-sm font-medium text-foreground">
                {user?.username}
              </span>
              <div className="flex shrink-0 items-center gap-1.5">
                <NotificationBell />
                <Badge className={cn('h-5 border px-1.5 py-0 text-[10px]', RANK_CLASSES[user?.rank ?? 0])}>
                  {t(`users.ranks.${user?.rank ?? 0}`)}
                </Badge>
              </div>
            </div>
            <nav className="space-y-1">
              <Link
                to="/settings"
                onClick={() => setMoreOpen(false)}
                className="flex items-center gap-3 rounded-md px-3 py-2.5 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
              >
                <Settings size={16} />
                {t('nav.settings')}
              </Link>
              {isColonelPlus && (
                <Link
                  to="/manage/users"
                  onClick={() => setMoreOpen(false)}
                  className="flex items-center gap-3 rounded-md px-3 py-2.5 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <Users size={16} />
                  {t('nav.manage')}
                </Link>
              )}
              <Button
                variant="ghost"
                onClick={handleLogout}
                className="w-full justify-start gap-3 px-3 text-sm text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              >
                <LogOut size={16} />
                {t('nav.logout')}
              </Button>
            </nav>
          </div>
        </div>
      )}

      {/* Панель */}
      <nav className="fixed inset-x-0 bottom-0 z-50 flex h-16 border-t border-border bg-card/95 backdrop-blur-md">
        {TABS.map((tab) => (
          <Link
            key={tab.to}
            to={tab.to}
            onClick={() => setMoreOpen(false)}
            className={cn(
              'flex flex-1 flex-col items-center justify-center gap-1 text-[10px] font-medium transition-colors',
              isActive(tab.to) ? 'text-accent' : 'text-muted-foreground',
            )}
          >
            <tab.icon size={18} />
            {tab.label}
          </Link>
        ))}
        <button
          type="button"
          onClick={() => setMoreOpen((v) => !v)}
          className={cn(
            'flex flex-1 flex-col items-center justify-center gap-1 text-[10px] font-medium transition-colors',
            moreOpen ? 'text-accent' : 'text-muted-foreground',
          )}
        >
          <MoreHorizontal size={18} />
          Ещё
        </button>
      </nav>
    </div>
  )
}
```

- [ ] **Step 2: Монтаж в layout**

`frontend/awake-web/src/routes/_auth.tsx` — компонент `AuthLayout` заменить на:

```tsx
import { MobileTabBar } from '@/components/layout/MobileTabBar'
// (в существующие импорты)

function AuthLayout() {
  return (
    <div className="flex min-h-screen bg-bg-page">
      <Sidebar />
      <main className="min-h-screen flex-1 overflow-auto">
        <div className="mx-auto max-w-5xl px-6 pb-24 pt-8 md:pb-8">
          <Outlet />
        </div>
      </main>
      <MobileTabBar />
    </div>
  )
}
```

(`pt-16` на мобиле был под гамбургер — он удаляется; `pb-24` — запас под нижнюю панель).

- [ ] **Step 3: Удалить гамбургер и мобильный оверлей из Sidebar**

В `Sidebar.tsx`: удалить состояние `const [open, setOpen] = useState(false)`, вызовы `setOpen(...)` (в `NavLink` onClick, `handleLogout`, Link настроек), JSX-блоки «Mobile hamburger» (Button с Menu/X) и «Mobile overlay» (fixed div), импорты `Menu`, `X`, `useState`, и вернуть из компонента только десктопный `<aside>`. Итоговый return:

```tsx
  return (
    <aside className="sticky top-0 hidden h-screen min-h-screen w-60 shrink-0 flex-col border-r border-border bg-card md:flex">
      <SidebarContent />
    </aside>
  )
```

- [ ] **Step 4: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
git add frontend/awake-web
git commit -m "feat(web): mobile bottom tab bar replaces hamburger menu"
```

---

### Task 4: Дашборд — рестайл (приоритет пользователя)

**Files:**
- Modify: `frontend/awake-web/src/routes/_auth.dashboard.tsx`

**Interfaces:**
- Consumes: `Skeleton` из `@/components/ui/skeleton`.
- Produces: ничего для других задач.

- [ ] **Step 1: Заголовок и StatCard**

В `DashboardPage`: h1 → `className="text-3xl font-black tracking-tight text-foreground"`. Деструктурировать `isLoading` у обоих запросов (`{ data: squads, isLoading: squadsLoading }`, `{ data: tickets, isLoading: ticketsLoading }`).

`StatCard` заменить целиком (иконка теперь компонент, не ReactNode):

```tsx
function StatCard({ icon: Icon, label, value, tone }: {
  icon: React.ComponentType<{ size?: number; className?: string }>
  label: string
  value: number
  tone: 'blue' | 'green' | 'yellow' | 'purple'
}) {
  const styles = {
    blue:   { border: 'border-blue-400/20',   tile: 'bg-blue-400/10 text-blue-400' },
    green:  { border: 'border-accent/20',     tile: 'bg-accent/10 text-accent' },
    yellow: { border: 'border-yellow-400/20', tile: 'bg-yellow-400/10 text-yellow-400' },
    purple: { border: 'border-purple-400/20', tile: 'bg-purple-400/10 text-purple-400' },
  }[tone]

  return (
    <Card className={cn('border transition-transform hover:-translate-y-0.5', styles.border)}>
      <CardContent className="pb-4 pt-4">
        <div className={cn('mb-3 flex h-9 w-9 items-center justify-center rounded-lg', styles.tile)}>
          <Icon size={17} />
        </div>
        <div className="text-3xl font-black tracking-tight text-foreground">{value}</div>
        <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
      </CardContent>
    </Card>
  )
}
```

Вызовы StatCard обновить (иконки без JSX и без цветовых классов — цвет даёт плитка):

```tsx
        <StatCard icon={Users} label="Бойцов в клане" value={totalMembers} tone="blue" />
        <StatCard icon={Shield} label="Отрядов" value={squads?.length ?? 0} tone="green" />
        <StatCard icon={Clock} label="Ожидают рассмотрения" value={pendingTickets} tone="yellow" />
        <StatCard icon={FileText} label="На рассмотрении" value={activeTickets} tone="purple" />
```

- [ ] **Step 2: Скелетоны и пустые состояния**

Добавить импорт `import { Skeleton } from '@/components/ui/skeleton'`.

Stats row: обернуть — если `squadsLoading || ticketsLoading`, вместо четырёх StatCard рендерить:

```tsx
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-[118px] rounded-xl" />
        ))}
```

(внутри той же grid-обёртки, тернарником).

Внутри карточки отрядов: `{squadsLoading ? (<div className="space-y-3">{Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-5 w-full" />)}</div>) : ...существующий map...}`, а `{!squads?.length && <p ...>—</p>}` заменить на `{!squadsLoading && !squads?.length && <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>}`.

Аналогично карточка тикетов: скелетоны при `ticketsLoading`, пустое состояние `Тикетов пока нет.`.

- [ ] **Step 3: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
git add frontend/awake-web
git commit -m "feat(web): restyle dashboard - icon tiles, skeletons, empty states"
```

---

### Task 5: Профиль — стат-плитки с иконками + скелетон

**Files:**
- Modify: `frontend/awake-web/src/components/PlayerProfileView.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.profile.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.players.$userId.tsx`

**Interfaces:**
- Consumes: `Skeleton`.
- Produces: `PlayerProfileSkeleton` (именованный экспорт из `PlayerProfileView.tsx`, без пропсов) — используют оба роута этого таска.

- [ ] **Step 1: Стат-плитки с иконками**

В `PlayerProfileView.tsx` добавить импорты `import { Clock, Crosshair, Percent, Skull, Target } from 'lucide-react'` и `import { Skeleton } from '@/components/ui/skeleton'`. Заменить `StatTile` и вызовы:

```tsx
function StatTile({ icon: Icon, label, value }: {
  icon: React.ComponentType<{ size?: number; className?: string }>
  label: string
  value: string
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-4 transition-colors hover:border-accent/30">
      <div className="mb-2 flex h-8 w-8 items-center justify-center rounded-lg bg-accent/10">
        <Icon size={15} className="text-accent" />
      </div>
      <p className="text-xl font-black tracking-tight text-foreground">{value}</p>
      <p className="mt-0.5 text-xs text-muted-foreground">{label}</p>
    </div>
  )
}
```

```tsx
              <StatTile icon={Crosshair} label="Убийства" value={stats.kills.toLocaleString('ru-RU')} />
              <StatTile icon={Skull} label="Смерти" value={stats.deaths.toLocaleString('ru-RU')} />
              <StatTile icon={Target} label="К/Д" value={stats.kdRatio.toFixed(2)} />
              <StatTile icon={Percent} label="Точность" value={stats.accuracy} />
              <StatTile icon={Clock} label="Время в игре" value={stats.playtime} />
```

Заголовок шапки: `<h1 className="text-2xl font-black tracking-tight text-foreground">{profile.username}</h1>`.

- [ ] **Step 2: PlayerProfileSkeleton**

В конец `PlayerProfileView.tsx` добавить:

```tsx
export function PlayerProfileSkeleton() {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-4">
        <Skeleton className="h-16 w-16 rounded-full" />
        <div className="space-y-2">
          <Skeleton className="h-6 w-40" />
          <Skeleton className="h-4 w-56" />
          <Skeleton className="h-5 w-32" />
        </div>
      </div>
      <Skeleton className="h-52 w-full rounded-xl" />
      <Skeleton className="h-32 w-full rounded-xl" />
    </div>
  )
}
```

- [ ] **Step 3: Подключить скелетон в роуты**

`_auth.profile.tsx`: импорт `PlayerProfileSkeleton` (из того же модуля, что `PlayerProfileView`); строку `if (isLoading) return <p className="text-muted-foreground">Загрузка…</p>` заменить на `if (isLoading) return <PlayerProfileSkeleton />`.

`_auth.players.$userId.tsx`: аналогично — `if (isLoading) return <PlayerProfileSkeleton />` (импорт добавить).

- [ ] **Step 4: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
git add frontend/awake-web
git commit -m "feat(web): profile stat tiles with icons, profile skeleton"
```

---

### Task 6: Списки — отряды, тикеты, управление, настройки

**Files:**
- Modify: `frontend/awake-web/src/routes/_auth.squads.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.tickets.index.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.manage.users.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.settings.tsx`

**Interfaces:**
- Consumes: `Skeleton`.
- Produces: ничего для других задач.

- [ ] **Step 1: Отряды — скелетоны, пустое состояние, заголовок**

`_auth.squads.tsx`: добавить импорты `Skeleton` и `Shield` (lucide). h1 → `text-2xl font-black tracking-tight`. Блок загрузки заменить:

```tsx
  if (isLoading) {
    return (
      <div>
        <h1 className="mb-6 text-2xl font-black tracking-tight text-foreground">{t('squads.title')}</h1>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }
```

После grid добавить пустое состояние (когда `!squads?.length`):

```tsx
      {!squads?.length && (
        <div className="rounded-xl border border-border bg-card py-16 text-center">
          <div className="mx-auto mb-3 flex h-11 w-11 items-center justify-center rounded-lg bg-accent/10">
            <Shield size={20} className="text-accent" />
          </div>
          <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
        </div>
      )}
```

- [ ] **Step 2: Тикеты — скелетоны, пустое состояние с CTA, заголовок**

`_auth.tickets.index.tsx`: добавить импорт `Skeleton`. h1 → `text-2xl font-black tracking-tight`. Блок загрузки заменить:

```tsx
  if (isLoading) {
    return (
      <div>
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-black tracking-tight text-foreground">{t('tickets.title')}</h1>
          <Skeleton className="h-9 w-28 rounded-md" />
        </div>
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-[72px] rounded-xl" />
          ))}
        </div>
      </div>
    )
  }
```

Пустое состояние заменить на:

```tsx
        <div className="rounded-xl border border-border bg-card py-16 text-center">
          <div className="mx-auto mb-3 flex h-11 w-11 items-center justify-center rounded-lg bg-accent/10">
            <FileText size={20} className="text-accent" />
          </div>
          <p className="mb-4 text-sm text-muted-foreground">{t('tickets.noTickets')}</p>
          <Button asChild size="sm">
            <Link to="/tickets/new" className="gap-1.5">
              <Plus size={14} />
              {t('tickets.new')}
            </Link>
          </Button>
        </div>
```

- [ ] **Step 3: Управление — скелетон + карточки на мобиле**

`_auth.manage.users.tsx`: добавить импорт `Skeleton`. h1 → `text-2xl font-black tracking-tight`. Блок загрузки:

```tsx
  if (isLoading) {
    return (
      <div>
        <h1 className="mb-6 text-2xl font-black tracking-tight text-foreground">{t('users.title')}</h1>
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }
```

Извлечь редактор ранга в компонент внутри файла (используется и в таблице, и в мобильных карточках; логика — ровно текущая из TableCell):

```tsx
function RankCell({ user, editing, currentUser, onChange }: {
  user: { id: string; rank: number }
  editing: boolean
  currentUser: { rank: number } | null
  onChange: (rank: number) => void
}) {
  const { t } = useTranslation()
  if (!editing) {
    return (
      <Badge className={cn('border text-xs', RANK_CLASSES[user.rank])}>
        {t(`users.ranks.${user.rank}`)}
      </Badge>
    )
  }
  return (
    <Select defaultValue={user.rank.toString()} onValueChange={(v) => onChange(Number(v))}>
      <SelectTrigger className="h-7 w-36 text-sm">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {ALL_RANKS
          .filter((r) => r !== UserRank.Leader || currentUser?.rank === UserRank.Leader)
          .map((r) => (
            <SelectItem key={r} value={r.toString()}>
              {t(`users.ranks.${r}`)}
            </SelectItem>
          ))}
      </SelectContent>
    </Select>
  )
}
```

В таблице заменить содержимое ячейки ранга на `<RankCell user={user} editing={editingId === user.id} currentUser={currentUser} onChange={(rank) => updateRank.mutate({ userId: user.id, rank })} />` (кнопка «Изменить ранг» остаётся в правой ячейке как есть). Таблицу обернуть в `<div className="hidden md:block">…</div>`, а после неё добавить мобильный список:

```tsx
          <div className="divide-y divide-border md:hidden">
            {users?.map((user) => (
              <div key={user.id} className="space-y-2 p-4">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-sm font-medium text-foreground">{user.username}</span>
                  <RankCell
                    user={user}
                    editing={editingId === user.id}
                    currentUser={currentUser}
                    onChange={(rank) => updateRank.mutate({ userId: user.id, rank })}
                  />
                </div>
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-xs text-muted-foreground">{user.email ?? '—'}</span>
                  {user.id !== currentUser?.userId && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setEditingId(editingId === user.id ? null : user.id)}
                      className="h-7 text-xs text-accent hover:bg-accent/10 hover:text-accent"
                    >
                      {editingId === user.id ? t('common.cancel') : t('users.changeRank')}
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
```

(оба блока — внутри существующего `<CardContent className="p-0">`).

- [ ] **Step 4: Настройки — заголовок**

`_auth.settings.tsx`: h1 → `className="mb-6 text-2xl font-black tracking-tight text-foreground"`.

- [ ] **Step 5: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
git add frontend/awake-web
git commit -m "feat(web): skeletons, empty states, mobile cards for list pages"
```

---

### Task 7: Финальная проверка этапа 2

**Files:** нет изменений кода (кроме случайных находок — фиксить только по согласованию с контроллером).

- [ ] **Step 1: Полный прогон**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
dotnet test
```

Expected: 0 ошибок tsc, сборка чистая, 60/60 тестов.

- [ ] **Step 2: Скриншот-регрессия публичных страниц**

Прогнать через Chromium в контейнере (compose-проект `featurestage-4`, скрипт по образцу scratchpad/pw-seam-check.js): лендинг 1440 и 390 (проверить `document.documentElement.scrollWidth === 390`), логин 1440. Expected: визуально без регрессий против текущего вида (бренд через BrandMark выглядит идентично), scrollWidth 390.

- [ ] **Step 3: Отчёт**

Внутренние страницы автоскриншотам недоступны (нужен логин) — перечислить в отчёте, что должен проверить пользователь вживую: дашборд (плитки, скелетоны при медленной сети), профиль (иконки в статистике), нижняя панель на телефоне (все 5 пунктов, лист «Ещё», уведомления), таблица управления на мобиле (карточки), пустые состояния.

---

## После выполнения плана

Пользователь принимает внутренние страницы вживую (десктоп + телефон). Правки — в этой же ветке. После одобрения — superpowers:finishing-a-development-branch (PR всей ветки design-refresh в master).
