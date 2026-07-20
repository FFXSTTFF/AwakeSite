# Резерв во вкладке «Отряды» + клик по участнику → профиль — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Показать во вкладке «Отряды» участников клана без отряда («резерв») и сделать ники участников кликабельными — ведут на их профиль (`/players/{userId}`).

**Architecture:** Бекенд — новый MediatR-запрос `GetSquadReserveQuery`, переиспользующий существующий `SquadMemberEnricher` (та же логика обогащения, что и в `GetSquadsQueryHandler`/билдере), плюс отдельный эндпоинт `GET /api/squads/reserve` — без изменений формы существующего `GET /api/squads`. Фронтенд — новый компонент `ReserveCard`, плюс перевод `SquadCard` с целиком-кликабельной обёртки `<Link>` на `<div>` с ручной навигацией и вложенными кликабельными никами (`stopPropagation`), плюс ссылка на нике в таблице участников отряда.

**Tech Stack:** ASP.NET Core (MediatR, xUnit + Moq + FluentAssertions), React 19 + Vite + TanStack Router + TanStack Query + Tailwind.

**Spec:** `docs/superpowers/specs/2026-07-19-squad-reserve-and-profile-links-design.md`

## Global Constraints

- Резерв виден всем с рангом Member+ (как и вся вкладка «Отряды»), не только офицерам.
- Резерв = участники клана (Member+), не входящие ни в один `Squad`.
- Сортировка резерва — по `Kd` по убыванию, `null` — в конец (как в пуле билдера).
- Клик по нику участника (везде во вкладке «Отряды») → `/players/{userId}` — существующий маршрут, без изменения бекенд-прав доступа.
- Форма ответа `GET /api/squads` (`SquadDto[]`) не меняется — её потребляет и дашборд.
- Все тексты UI — на русском.

---

### Task 1: `GetSquadReserveQuery` (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadReserve/GetSquadReserveQuery.cs`
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadReserve/ReserveMemberDto.cs`
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadReserve/GetSquadReserveQueryHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Squads/GetSquadReserveQueryHandlerTests.cs`

**Interfaces:**
- Consumes: существующие `IUserRepository.GetByMinRankAsync(UserRank, CancellationToken)`, `ISquadRepository.GetAllWithMembersAsync(CancellationToken)`, `SquadMemberEnricher.ComputeAsync(...)` (сигнатура: `(IReadOnlyList<User> users, IPlayerInventoryRepository, IPlayerBuildProofRepository, IPlayerBoostRequestRepository, IItemCacheService, IPlayerStatsSnapshotRepository, CancellationToken)` → `IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostItemDto> Boosts)>`), `PlayerFlagsDto(bool Bio, bool Combat, bool Sniper, bool Speed, bool Vitality)`, `BoostItemDto(BoostType BoostType, string ItemId, string Name, string? Icon)`.
- Produces: `record ReserveMemberDto(Guid UserId, string Username, string? GameNickname, PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostItemDto> Boosts)`; `record GetSquadReserveQuery : IRequest<IReadOnlyList<ReserveMemberDto>>` — используется контроллером в Task 2.

- [ ] **Step 1: Написать DTO и запрос**

`src/Awake.Application/Features/Squads/Queries/GetSquadReserve/ReserveMemberDto.cs`:

```csharp
using Awake.Application.Features.Boosts.Dtos;
using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public record ReserveMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    PlayerFlagsDto Flags,
    double? Kd,
    IReadOnlyList<BoostItemDto> Boosts);
```

`src/Awake.Application/Features/Squads/Queries/GetSquadReserve/GetSquadReserveQuery.cs`:

```csharp
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public record GetSquadReserveQuery : IRequest<IReadOnlyList<ReserveMemberDto>>;
```

- [ ] **Step 2: Написать падающие тесты**

`tests/Awake.Unit.Tests/Features/Squads/GetSquadReserveQueryHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquadReserve;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadReserveQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadReserveQueryHandler BuildHandler() => new(
        _users.Object, _squads.Object, _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object);

    private void SetupEmptyAux()
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_MemberInSquad_ExcludedFromReserve()
    {
        var inSquad = new User { Username = "inSquad", Rank = UserRank.Member };
        var free = new User { Username = "free", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([inSquad, free]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync([new Squad
               {
                   Name = "Alpha", Number = 1,
                   Members = [new SquadMember { UserId = inSquad.Id, User = inSquad }],
               }]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Username.Should().Be("free");
    }

    [Fact]
    public async Task Handle_GuestRank_ExcludedEvenIfNoSquad()
    {
        // GetByMinRankAsync(Member) сам исключает Guest — хендлер не фильтрует ранг повторно,
        // тест фиксирует контракт: гость никогда не приходит от репозитория.
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SortsByKdDescending_NullsLast()
    {
        var high = new User { Username = "high", GameNickname = "High", Rank = UserRank.Member };
        var low = new User { Username = "low", GameNickname = "Low", Rank = UserRank.Member };
        var none = new User { Username = "none", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([none, low, high]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([
                      new PlayerStatsSnapshot { GameNickname = "High", KdRatio = 3.0 },
                      new PlayerStatsSnapshot { GameNickname = "Low", KdRatio = 1.0 },
                  ]);

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Select(r => r.Username).Should().Equal("high", "low", "none");
    }

    [Fact]
    public async Task Handle_EnrichesFlagsAndBoosts()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member };
        _users.Setup(u => u.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([user]);
        _squads.Setup(s => s.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new Awake.Application.Features.Items.Dtos.ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetSquadReserveQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Flags.Bio.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Убедиться, что тесты не компилируются**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~GetSquadReserve"`
Expected: ошибка компиляции — `GetSquadReserveQueryHandler` не существует.

- [ ] **Step 4: Написать хендлер**

`src/Awake.Application/Features/Squads/Queries/GetSquadReserve/GetSquadReserveQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadReserve;

public class GetSquadReserveQueryHandler(
    IUserRepository userRepository,
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadReserveQuery, IReadOnlyList<ReserveMemberDto>>
{
    public async Task<IReadOnlyList<ReserveMemberDto>> Handle(
        GetSquadReserveQuery request, CancellationToken cancellationToken)
    {
        var eligible = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);

        var squadUserIds = squads
            .SelectMany(s => s.Members)
            .Select(m => m.UserId)
            .ToHashSet();

        var reserve = eligible.Where(u => !squadUserIds.Contains(u.Id)).ToList();

        var enriched = await Squads.SquadMemberEnricher.ComputeAsync(
            reserve, inventoryRepository, proofRepository, boostRepository, itemCache, snapshotRepository, cancellationToken);

        return reserve
            .Select(u => new ReserveMemberDto(
                u.Id, u.Username, u.GameNickname,
                enriched[u.Id].Flags, enriched[u.Id].Kd, enriched[u.Id].Boosts))
            .OrderByDescending(r => r.Kd ?? -1)
            .ToList();
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~GetSquadReserve"`
Expected: все 4 теста PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Awake.Application/Features/Squads/Queries/GetSquadReserve/ tests/Awake.Unit.Tests/Features/Squads/GetSquadReserveQueryHandlerTests.cs
git commit -m "feat(squads): запрос GetSquadReserve — участники клана без отряда"
```

---

### Task 2: `GET /api/squads/reserve`

**Files:**
- Modify: `src/Awake.API/Controllers/SquadsController.cs`

**Interfaces:**
- Consumes: `GetSquadReserveQuery` (Task 1).
- Produces: HTTP-контракт `GET /api/squads/reserve` → `ReserveMemberDto[]` (camelCase JSON) — используется фронтендом в Task 4.

- [ ] **Step 1: Добавить using и эндпоинт**

В `src/Awake.API/Controllers/SquadsController.cs` добавить в блок using:

```csharp
using Awake.Application.Features.Squads.Queries.GetSquadReserve;
```

Добавить метод сразу после `GetAll` (после строки `}` закрывающей `GetAll`, перед `GetById`):

```csharp
    [HttpGet("reserve")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetReserve(CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadReserveQuery(), ct);
        return Ok(result);
    }
```

- [ ] **Step 2: Сборка**

Run: `dotnet build Awake.slnx`
Expected: Build succeeded, 0 ошибок. (Маршрут `GET /api/squads/reserve` не конфликтует с `GET /api/squads/{id:guid}`, т.к. `reserve` не парсится как `Guid`.)

- [ ] **Step 3: Полный прогон юнит-тестов**

Run: `dotnet test tests/Awake.Unit.Tests`
Expected: все тесты PASS (включая Task 1).

- [ ] **Step 4: Commit**

```bash
git add src/Awake.API/Controllers/SquadsController.cs
git commit -m "feat(squads): эндпоинт GET /api/squads/reserve"
```

---

### Task 3: Фронтенд — типы и API-клиент

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts` (после `interface SquadDto`, см. текущее расположение рядом с `SquadMemberDto`/`SquadDto`)
- Modify: `frontend/awake-web/src/api/squads.ts`

**Interfaces:**
- Consumes: `GET /api/squads/reserve` (Task 2).
- Produces: `interface ReserveMember`; `squadsApi.getReserve(): Promise<ReserveMember[]>` — используется в Task 4.

- [ ] **Step 1: Тип в `types/api.ts`**

Найти существующий `export interface SquadDto { ... }` (там же, где `SquadMemberDto`) и добавить сразу после него:

```ts
export interface ReserveMember {
  userId: string
  username: string
  gameNickname: string | null
  flags: PlayerFlags
  kd: number | null
  boosts: BoostItem[]
}
```

(Типы `PlayerFlags` и `BoostItem` уже определены в этом файле и используются `SquadMemberDto`/`BuilderFighter` — переиспользуются без изменений.)

- [ ] **Step 2: Метод API в `api/squads.ts`**

В `frontend/awake-web/src/api/squads.ts` добавить импорт типа и метод:

```ts
import type { ReserveMember, SquadDto } from '@/types/api'
```

(Заменить существующую строку `import type { SquadDto } from '@/types/api'` на строку выше.)

В объект `squadsApi` добавить:

```ts
  getReserve: () => apiClient.get<ReserveMember[]>('/squads/reserve'),
```

- [ ] **Step 3: Проверка типов**

Run: `cd frontend/awake-web && npm run build`
Expected: `tsc -b` и `vite build` без ошибок.

- [ ] **Step 4: Commit**

```bash
git add frontend/awake-web/src/types/api.ts frontend/awake-web/src/api/squads.ts
git commit -m "feat(squads): типы и API-клиент для резерва"
```

---

### Task 4: `ReserveCard` — карточка резерва на `/squads`

**Files:**
- Create: `frontend/awake-web/src/components/squads/ReserveCard.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.squads.index.tsx`

**Interfaces:**
- Consumes: `squadsApi.getReserve` (Task 3), `ReserveMember` (Task 3), существующий `MemberHoverInfo({ nickname, flags, kd, boosts, children })` (`@/components/squads/MemberHoverInfo`).
- Produces: `ReserveCard()` — самодостаточный компонент без пропсов, тянет свои данные через `useQuery`.

- [ ] **Step 1: Компонент `ReserveCard`**

Создать `frontend/awake-web/src/components/squads/ReserveCard.tsx`:

```tsx
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
```

- [ ] **Step 2: Подключить в `_auth.squads.index.tsx`**

В `frontend/awake-web/src/routes/_auth.squads.index.tsx` добавить импорт:

```tsx
import { ReserveCard } from '@/components/squads/ReserveCard'
```

Изменить блок рендера сетки (сейчас — строки 50–54):

```tsx
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        {squads?.map((squad) => (
          <SquadCard key={squad.id} squad={squad} canRename={rank >= UserRank.Officer} />
        ))}
      </div>
```

на:

```tsx
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        {squads?.map((squad) => (
          <SquadCard key={squad.id} squad={squad} canRename={rank >= UserRank.Officer} />
        ))}
        <ReserveCard />
      </div>
```

- [ ] **Step 3: Проверка типов и сборки**

Run: `cd frontend/awake-web && npm run build`
Expected: без ошибок.

- [ ] **Step 4: Commit**

```bash
git add frontend/awake-web/src/components/squads/ReserveCard.tsx frontend/awake-web/src/routes/_auth.squads.index.tsx
git commit -m "feat(squads): карточка «Резерв» на вкладке отрядов"
```

---

### Task 5: Клик по нику → профиль в `SquadCard` и на странице отряда

**Files:**
- Modify: `frontend/awake-web/src/components/squads/SquadCard.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.squads.$squadId.tsx`

**Interfaces:**
- Consumes: существующий маршрут `/players/$userId` (без изменений), `SquadDto`/`SquadMemberDto` (без изменений).

- [ ] **Step 1: `SquadCard.tsx` — заменить корневой `Link` на `div` с навигацией**

В `frontend/awake-web/src/components/squads/SquadCard.tsx` добавить в импорты `useNavigate`:

```tsx
import { Link, useNavigate } from '@tanstack/react-router'
```

Добавить `const navigate = useNavigate()` в начало компонента (сразу после `const { t } = useTranslation()`).

Заменить открывающий и закрывающий теги корневого `<Link>` (строки 58–59 и 166–167):

```tsx
    <Link to="/squads/$squadId" params={{ squadId: squad.id }} className="group block">
      <Card className="h-full transition-all duration-200 group-hover:border-accent/30 group-hover:shadow-[0_0_20px_rgba(61,220,132,0.06)]">
```

на:

```tsx
    <div
      role="link"
      tabIndex={0}
      onClick={() => navigate({ to: '/squads/$squadId', params: { squadId: squad.id } })}
      onKeyDown={(e) => {
        if (e.key === 'Enter') navigate({ to: '/squads/$squadId', params: { squadId: squad.id } })
      }}
      className="group block cursor-pointer"
    >
      <Card className="h-full transition-all duration-200 group-hover:border-accent/30 group-hover:shadow-[0_0_20px_rgba(61,220,132,0.06)]">
```

и закрывающий тег:

```tsx
        </CardContent>
      </Card>
    </Link>
```

на:

```tsx
        </CardContent>
      </Card>
    </div>
```

- [ ] **Step 2: Сделать ники лидера и участников кликабельными**

В том же файле блок отображения лидера (сейчас):

```tsx
            {leader && (
              <MemberHoverInfo
                nickname={leader.gameNickname ?? leader.username}
                flags={leader.flags}
                kd={leader.kd}
                boosts={leader.boosts}
              >
                <div className="flex items-center gap-2">
                  <Crown size={12} className="shrink-0 text-yellow-400" />
                  <span className="truncate text-sm font-medium text-foreground">
                    {leader.gameNickname ?? leader.username}
                  </span>
                </div>
              </MemberHoverInfo>
            )}
```

заменить на:

```tsx
            {leader && (
              <MemberHoverInfo
                nickname={leader.gameNickname ?? leader.username}
                flags={leader.flags}
                kd={leader.kd}
                boosts={leader.boosts}
              >
                <div className="flex items-center gap-2">
                  <Crown size={12} className="shrink-0 text-yellow-400" />
                  <Link
                    to="/players/$userId"
                    params={{ userId: leader.userId }}
                    onClick={(e) => e.stopPropagation()}
                    className="truncate text-sm font-medium text-foreground transition-colors hover:text-accent"
                  >
                    {leader.gameNickname ?? leader.username}
                  </Link>
                </div>
              </MemberHoverInfo>
            )}
```

Блок остальных участников (сейчас):

```tsx
            {others.slice(0, leader ? 2 : 3).map((m) => (
              <MemberHoverInfo
                key={m.userId}
                nickname={m.gameNickname ?? m.username}
                flags={m.flags}
                kd={m.kd}
                boosts={m.boosts}
              >
                <div className="flex items-center gap-2 pl-5">
                  <span className="truncate text-sm text-muted-foreground">
                    {m.gameNickname ?? m.username}
                  </span>
                </div>
              </MemberHoverInfo>
            ))}
```

заменить на:

```tsx
            {others.slice(0, leader ? 2 : 3).map((m) => (
              <MemberHoverInfo
                key={m.userId}
                nickname={m.gameNickname ?? m.username}
                flags={m.flags}
                kd={m.kd}
                boosts={m.boosts}
              >
                <div className="flex items-center gap-2 pl-5">
                  <Link
                    to="/players/$userId"
                    params={{ userId: m.userId }}
                    onClick={(e) => e.stopPropagation()}
                    className="truncate text-sm text-muted-foreground transition-colors hover:text-accent"
                  >
                    {m.gameNickname ?? m.username}
                  </Link>
                </div>
              </MemberHoverInfo>
            ))}
```

(Кнопка переименования `startEdit` уже вызывает `e.stopPropagation()` — строка `onClick={startEdit}` внутри неё не меняется, `startEdit` сам вызывает `e.stopPropagation()`.)

- [ ] **Step 3: Ссылка на нике в таблице участников отряда**

В `frontend/awake-web/src/routes/_auth.squads.$squadId.tsx` добавить импорт `Link`:

```tsx
import { createFileRoute, Link } from '@tanstack/react-router'
```

Заменить блок (сейчас, строки 67–76):

```tsx
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {member.isLeader && <Crown size={13} className="text-yellow-400 shrink-0" />}
                        <span className={member.isLeader ? 'text-foreground font-medium' : 'text-foreground'}>
                          {member.username}
                        </span>
                        {member.gameNickname && (
                          <span className="text-muted-foreground text-sm">({member.gameNickname})</span>
                        )}
                      </div>
                    </TableCell>
```

на:

```tsx
                    <TableCell>
                      <div className="flex items-center gap-2">
                        {member.isLeader && <Crown size={13} className="text-yellow-400 shrink-0" />}
                        <Link
                          to="/players/$userId"
                          params={{ userId: member.userId }}
                          className={
                            member.isLeader
                              ? 'text-foreground font-medium transition-colors hover:text-accent'
                              : 'text-foreground transition-colors hover:text-accent'
                          }
                        >
                          {member.username}
                        </Link>
                        {member.gameNickname && (
                          <span className="text-muted-foreground text-sm">({member.gameNickname})</span>
                        )}
                      </div>
                    </TableCell>
```

- [ ] **Step 4: Проверка типов и сборки**

Run: `cd frontend/awake-web && npm run build`
Expected: без ошибок.

- [ ] **Step 5: Commit**

```bash
git add frontend/awake-web/src/components/squads/SquadCard.tsx frontend/awake-web/src/routes/_auth.squads.\$squadId.tsx
git commit -m "feat(squads): клик по нику участника открывает его профиль"
```

---

### Task 6: Сквозная проверка на дев-стенде

**Files:** нет изменений кода; только стенд и ручная проверка.

**Interfaces:**
- Consumes: всё из Task 1–5; локальный стенд — docker compose проект `featurestage-4` (api → localhost:5001, db → localhost:5432 через контейнер в сети compose — см. [[project_dev_stand_pitfalls]]), vite dev server → localhost:5173.

- [ ] **Step 1: Пересобрать api-контейнер стенда**

Из корня `D:\Awake`:

```bash
docker compose -p featurestage-4 --env-file .claude/worktrees/feature+stage-4/.env up -d --build api
```

Expected: `featurestage-4-api-1` пересобран и Up.

- [ ] **Step 2: Смоук API**

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/public/leaderboard
```
Expected: `200`.

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/squads/reserve
```
Expected: `401` (эндпоинт существует и закрыт авторизацией; `404` означал бы, что маршрут не подхватился).

- [ ] **Step 3: Проверка в браузере**

Открыть `http://localhost:5173/squads` (авторизованным Member+):

1. Рядом с карточками отрядов видна карточка «Резерв» со счётчиком и списком (или «Все бойцы в отрядах.», если резерв пуст).
2. Ховер на нике в резерве показывает попап с КД/флагами/бустами (как у участников отряда).
3. Клик по нику в резерве открывает `/players/{userId}` — профиль этого участника.
4. Клик по нику лидера/участника внутри карточки отряда открывает профиль участника, а не страницу отряда.
5. Клик по пустому месту карточки отряда (не по нику) по-прежнему открывает страницу отряда.
6. На странице отряда (`/squads/{id}`) клик по имени в таблице открывает профиль участника.

Expected: все шесть пунктов проходят.

- [ ] **Step 4: Финальный прогон тестов**

Run: `dotnet test tests/Awake.Unit.Tests`
Expected: все PASS.

```bash
git add docs/superpowers/plans/2026-07-19-squad-reserve.md
git commit -m "docs: план реализации резерва во вкладке отрядов"
```
