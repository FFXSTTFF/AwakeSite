# Смена экипировки из инвентаря — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Игрок редактирует карточку «Экипировка» в своём профиле, выбирая предметы из своего инвентаря; надетое хранится в jsonb-колонке `User.Loadout` с fallback'ом на экипировку из заявки.

**Architecture:** Бекенд — Clean Architecture + MediatR: новая команда `UpdateMyLoadout` в фиче Inventory, jsonb-колонка у `User` (паттерн `TicketConfiguration`), эндпоинт `PUT /api/profile/loadout` в `InventoryController`. Фронтенд — карточка «Экипировка» выносится из `PlayerProfileView` в новый компонент `LoadoutCard` с режимом редактирования (выбор только из предметов инвентаря).

**Tech Stack:** ASP.NET Core (MediatR, EF Core + Npgsql jsonb, Moq + FluentAssertions + xUnit), React 19 + Vite + TanStack Query + Tailwind.

**Spec:** `docs/superpowers/specs/2026-07-19-loadout-from-inventory-design.md`

## Global Constraints

- Все тексты UI и сообщения об ошибках — на русском.
- Ошибки бизнес-логики — `Result<T>.Failure("...")`, никаких исключений для валидации.
- DI только через конструктор (primary constructors, как в существующих хендлерах).
- Заточка (upgrade) — целое 0–15 включительно.
- Клиент шлёт в слоте только `itemId` + `upgrade`; имя и иконку предмета сервер берёт из `IItemCacheService` (не доверяем клиенту).
- Фронтенд: существующие паттерны — TanStack Query с ключами `['inventory', 'my']` и `['players', 'me']`, компоненты shadcn из `@/components/ui/*`.

---

### Task 1: `User.Loadout` — домен, EF-конфигурация, миграция

**Files:**
- Modify: `src/Awake.Domain/Entities/User.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create (генерируется): `src/Awake.Infrastructure/Persistence/Migrations/*_AddUserLoadout.cs`

**Interfaces:**
- Produces: `User.Loadout` типа `Awake.Domain.ValueObjects.Loadout?` — используется в Task 2 (запись) и Task 3 (чтение).

- [ ] **Step 1: Добавить свойство в `User`**

В `src/Awake.Domain/Entities/User.cs` добавить using и свойство:

```csharp
using Awake.Domain.Common;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

namespace Awake.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Email { get; set; }
    public UserRank Rank { get; set; } = UserRank.Guest;
    public string? GameNickname { get; set; }

    // Discord OAuth — единственный способ входа; ключ связывания с заявками
    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>Надетая экипировка (выбирается из инвентаря). Null — показываем экипировку из заявки.</summary>
    public Loadout? Loadout { get; set; }

    public ICollection<SquadMember> SquadMemberships { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}
```

- [ ] **Step 2: jsonb-конвертация в `UserConfiguration`**

В `src/Awake.Infrastructure/Persistence/Configurations/UserConfiguration.cs` добавить usings `System.Text.Json` и `Awake.Domain.ValueObjects`, а в конец метода `Configure` — блок (точная копия паттерна из `TicketConfiguration`):

```csharp
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        builder.Property(x => x.Loadout)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<Loadout>(v, jsonOptions));
```

- [ ] **Step 3: Сборка**

Run: `dotnet build Awake.slnx`
Expected: Build succeeded, 0 ошибок.

- [ ] **Step 4: Сгенерировать миграцию**

Run (из корня репозитория):
```bash
dotnet ef migrations add AddUserLoadout --project src/Awake.Infrastructure --startup-project src/Awake.API
```
Expected: создан файл `*_AddUserLoadout.cs`, в `Up()` — `AddColumn<string>(name: "Loadout", table: "Users", type: "jsonb", nullable: true)`. (Имя таблицы может отличаться — проверить по фактическому файлу; менять его не нужно.)

- [ ] **Step 5: Повторная сборка**

Run: `dotnet build Awake.slnx`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Awake.Domain/Entities/User.cs src/Awake.Infrastructure/Persistence/Configurations/UserConfiguration.cs src/Awake.Infrastructure/Persistence/Migrations/
git commit -m "feat(loadout): User.Loadout jsonb-колонка + миграция AddUserLoadout"
```

---

### Task 2: Команда `UpdateMyLoadout` (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Inventory/Commands/UpdateMyLoadout/UpdateMyLoadoutCommand.cs`
- Create: `src/Awake.Application/Features/Inventory/Commands/UpdateMyLoadout/UpdateMyLoadoutCommandHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Inventory/UpdateMyLoadoutCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `User.Loadout` (Task 1); существующие `IUserRepository`, `IPlayerInventoryRepository`, `IItemCacheService` (`GetById(string)` → `ItemDto(Id, Category, NameRu, Icon, Color)` или null), `Result<T>`.
- Produces: `record LoadoutSlotRequest(string ItemId, int Upgrade)`; `record UpdateMyLoadoutCommand(Guid UserId, LoadoutSlotRequest? Sniper, LoadoutSlotRequest? Weapon, LoadoutSlotRequest? Armor) : IRequest<Result<bool>>` — используется контроллером в Task 3.

- [ ] **Step 1: Написать команду (нужна, чтобы тесты компилировались)**

`src/Awake.Application/Features/Inventory/Commands/UpdateMyLoadout/UpdateMyLoadoutCommand.cs`:

```csharp
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;

/// <summary>Слот в запросе: только id предмета и заточка; имя и иконку резолвит сервер.</summary>
public record LoadoutSlotRequest(string ItemId, int Upgrade);

public record UpdateMyLoadoutCommand(
    Guid UserId,
    LoadoutSlotRequest? Sniper,
    LoadoutSlotRequest? Weapon,
    LoadoutSlotRequest? Armor
) : IRequest<Result<bool>>;
```

- [ ] **Step 2: Написать падающие тесты**

`tests/Awake.Unit.Tests/Features/Inventory/UpdateMyLoadoutCommandHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class UpdateMyLoadoutCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly User _user;

    public UpdateMyLoadoutCommandHandlerTests()
    {
        _user = new User { Id = _userId, Username = "test" };
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_user);
    }

    private UpdateMyLoadoutCommandHandler BuildHandler() =>
        new(_users.Object, _inventory.Object, _cache.Object);

    private void SetupItem(string itemId, string category, string name = "Предмет")
    {
        _inventory.Setup(r => r.GetAsync(_userId, itemId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = itemId });
        _cache.Setup(c => c.GetById(itemId))
              .Returns(new ItemDto(itemId, category, name, "icon.png", ""));
    }

    [Fact]
    public async Task Handle_UserNotFound_Fails()
    {
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WeaponMissing_Fails()
    {
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, null, new LoadoutSlotRequest("a1", 0)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ArmorMissing_Fails()
    {
        SetupItem("w1", "weapon/assault_rifle");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public async Task Handle_UpgradeOutOfRange_Fails(int upgrade)
    {
        SetupItem("w1", "weapon/assault_rifle");
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", upgrade), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ItemNotInInventory_Fails()
    {
        SetupItem("a1", "armor/combat");
        _inventory.Setup(r => r.GetAsync(_userId, "w1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ItemGoneFromItemBase_Fails()
    {
        _inventory.Setup(r => r.GetAsync(_userId, "w1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = "w1" });
        _cache.Setup(c => c.GetById("w1")).Returns((ItemDto?)null);
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Valid_SavesLoadoutWithResolvedNames()
    {
        SetupItem("w1", "weapon/assault_rifle", "АК-74М");
        SetupItem("a1", "armor/combat", "Заря");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, null, new LoadoutSlotRequest("w1", 12), new LoadoutSlotRequest("a1", 5)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _user.Loadout.Should().NotBeNull();
        _user.Loadout!.Sniper.Should().BeNull();
        _user.Loadout.Weapon.ItemId.Should().Be("w1");
        _user.Loadout.Weapon.ItemName.Should().Be("АК-74М");
        _user.Loadout.Weapon.Upgrade.Should().Be(12);
        _user.Loadout.Armor.ItemName.Should().Be("Заря");
        _users.Verify(u => u.UpdateAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSniper_SavesSniperSlot()
    {
        SetupItem("s1", "weapon/sniper_rifle", "СВД");
        SetupItem("w1", "weapon/assault_rifle");
        SetupItem("a1", "armor/combat");

        var result = await BuildHandler().Handle(new UpdateMyLoadoutCommand(
            _userId, new LoadoutSlotRequest("s1", 3),
            new LoadoutSlotRequest("w1", 0), new LoadoutSlotRequest("a1", 0)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _user.Loadout!.Sniper!.ItemName.Should().Be("СВД");
        _user.Loadout.Sniper.Upgrade.Should().Be(3);
    }
}
```

- [ ] **Step 3: Убедиться, что тесты не компилируются (хендлера нет)**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~UpdateMyLoadout"`
Expected: ошибка компиляции — `UpdateMyLoadoutCommandHandler` не существует.

- [ ] **Step 4: Написать хендлер**

`src/Awake.Application/Features/Inventory/Commands/UpdateMyLoadout/UpdateMyLoadoutCommandHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;

public class UpdateMyLoadoutCommandHandler(
    IUserRepository users,
    IPlayerInventoryRepository inventory,
    IItemCacheService itemCache
) : IRequestHandler<UpdateMyLoadoutCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateMyLoadoutCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<bool>.Failure("Пользователь не найден.");

        if (request.Weapon is null || request.Armor is null)
            return Result<bool>.Failure("Укажи основное оружие и броню.");

        var (weapon, weaponError) = await BuildSlotAsync(request.UserId, request.Weapon, cancellationToken);
        if (weaponError is not null)
            return Result<bool>.Failure(weaponError);

        var (armor, armorError) = await BuildSlotAsync(request.UserId, request.Armor, cancellationToken);
        if (armorError is not null)
            return Result<bool>.Failure(armorError);

        LoadoutSlot? sniper = null;
        if (request.Sniper is not null)
        {
            (sniper, var sniperError) = await BuildSlotAsync(request.UserId, request.Sniper, cancellationToken);
            if (sniperError is not null)
                return Result<bool>.Failure(sniperError);
        }

        user.Loadout = new Loadout(sniper, weapon!, armor!);
        await users.UpdateAsync(user, cancellationToken);

        return Result<bool>.Success(true);
    }

    private async Task<(LoadoutSlot? Slot, string? Error)> BuildSlotAsync(
        Guid userId, LoadoutSlotRequest slot, CancellationToken ct)
    {
        if (slot.Upgrade is < 0 or > 15)
            return (null, "Заточка должна быть от 0 до 15.");

        if (await inventory.GetAsync(userId, slot.ItemId, ct) is null)
            return (null, "Предмет не найден в твоём инвентаре.");

        var item = itemCache.GetById(slot.ItemId);
        if (item is null)
            return (null, "Предмет не найден в базе.");

        return (new LoadoutSlot(slot.ItemId, item.NameRu, item.Icon, slot.Upgrade), null);
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~UpdateMyLoadout"`
Expected: все 9 тестов PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Awake.Application/Features/Inventory/Commands/UpdateMyLoadout/ tests/Awake.Unit.Tests/Features/Inventory/UpdateMyLoadoutCommandHandlerTests.cs
git commit -m "feat(loadout): команда UpdateMyLoadout — сохранение экипировки из инвентаря"
```

---

### Task 3: `PUT /api/profile/loadout` + приоритет `User.Loadout` в профиле

**Files:**
- Modify: `src/Awake.API/Controllers/InventoryController.cs`
- Modify: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQueryHandler.cs:43-45`
- Test: `tests/Awake.Unit.Tests/Features/Players/GetPlayerProfileQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `UpdateMyLoadoutCommand`, `LoadoutSlotRequest` (Task 2), `User.Loadout` (Task 1).
- Produces: HTTP-контракт `PUT /api/profile/loadout` c телом `{ sniper: {itemId, upgrade} | null, weapon: {itemId, upgrade}, armor: {itemId, upgrade} }` (camelCase), ответ 200 или 400 ProblemDetails с `detail` — используется фронтендом в Task 4.

- [ ] **Step 1: Тест на приоритет `User.Loadout` над заявкой**

Добавить в `tests/Awake.Unit.Tests/Features/Players/GetPlayerProfileQueryHandlerTests.cs`:

```csharp
    [Fact]
    public async Task Handle_UserLoadout_PreferredOverTicketLoadout()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        user.GameNickname = null;
        user.Loadout = new Loadout(null,
            new LoadoutSlot("w2", "Гроза", "icon", 8),
            new LoadoutSlot("a2", "СЕВА", "icon", 4));
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((SquadMember?)null);
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket
                {
                    AuthorId = id, GameNickname = "OopsITry",
                    Loadout = new Loadout(null,
                        new LoadoutSlot("w1", "АК из заявки", "icon", 0),
                        new LoadoutSlot("a1", "Броня из заявки", "icon", 0)),
                }]);
        _boosts.Setup(b => b.GetByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Loadout!.Weapon.ItemName.Should().Be("Гроза");
    }
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~GetPlayerProfile"`
Expected: новый тест FAIL (хендлер возвращает «АК из заявки»), остальные PASS.

- [ ] **Step 3: Изменить хендлер профиля**

В `GetPlayerProfileQueryHandler.cs` заменить блок (строки 43–45):

```csharp
        // Экипировка — из самой свежей заявки с заполненным Loadout
        var tickets = await ticketRepository.GetByAuthorAsync(user.Id, cancellationToken);
        var loadout = tickets.FirstOrDefault(t => t.Loadout is not null)?.Loadout;
```

на:

```csharp
        // Экипировка: сохранённая в профиле, иначе — из самой свежей заявки с Loadout
        var loadout = user.Loadout;
        if (loadout is null)
        {
            var tickets = await ticketRepository.GetByAuthorAsync(user.Id, cancellationToken);
            loadout = tickets.FirstOrDefault(t => t.Loadout is not null)?.Loadout;
        }
```

- [ ] **Step 4: Прогнать тесты профиля**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~GetPlayerProfile"`
Expected: все PASS.

- [ ] **Step 5: Добавить эндпоинт в `InventoryController`**

В `src/Awake.API/Controllers/InventoryController.cs` добавить using `Awake.Application.Features.Inventory.Commands.UpdateMyLoadout;`, рядом с `AddItemRequest` — запрос:

```csharp
public record UpdateLoadoutRequest(
    LoadoutSlotRequest? Sniper, LoadoutSlotRequest? Weapon, LoadoutSlotRequest? Armor);
```

и метод в секцию «Свой инвентарь» (после `DeleteMyProof`):

```csharp
    [HttpPut("api/profile/loadout")]
    public async Task<IActionResult> UpdateLoadout(UpdateLoadoutRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateMyLoadoutCommand(
            currentUser.UserId, request.Sniper, request.Weapon, request.Armor), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
```

- [ ] **Step 6: Сборка + все юнит-тесты**

Run: `dotnet build Awake.slnx && dotnet test tests/Awake.Unit.Tests`
Expected: Build succeeded, все тесты PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Awake.API/Controllers/InventoryController.cs src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQueryHandler.cs tests/Awake.Unit.Tests/Features/Players/GetPlayerProfileQueryHandlerTests.cs
git commit -m "feat(loadout): PUT /api/profile/loadout + приоритет User.Loadout в профиле"
```

---

### Task 4: Фронтенд — `LoadoutCard` с режимом редактирования

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts` (после `interface Loadout`, строка ~94)
- Modify: `frontend/awake-web/src/api/inventory.ts`
- Create: `frontend/awake-web/src/components/LoadoutCard.tsx`
- Modify: `frontend/awake-web/src/components/PlayerProfileView.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.profile.tsx`

**Interfaces:**
- Consumes: `PUT /profile/loadout` (Task 3), существующие `InventoryItem`, `PlayerInventory`, `Loadout`, `apiClient.put`, query-ключи `['inventory', 'my']` и `['players', 'me']`.
- Produces: `LoadoutCard({ loadout: Loadout | null; editable?: boolean })`; `PlayerProfileView` получает проп `editable?: boolean`.

- [ ] **Step 1: Типы запроса в `types/api.ts`**

После `export interface Loadout { ... }` добавить:

```ts
export interface LoadoutSlotRequest {
  itemId: string
  upgrade: number
}

export interface UpdateLoadoutRequest {
  sniper: LoadoutSlotRequest | null
  weapon: LoadoutSlotRequest
  armor: LoadoutSlotRequest
}
```

- [ ] **Step 2: Метод API в `api/inventory.ts`**

Дополнить импорт типов и объект:

```ts
import type { BuildType, PlayerInventory, UpdateLoadoutRequest } from '@/types/api'
```

В `inventoryApi` добавить:

```ts
  updateLoadout: (data: UpdateLoadoutRequest): Promise<void> =>
    apiClient.put('/profile/loadout', data),
```

- [ ] **Step 3: Компонент `LoadoutCard`**

Создать `frontend/awake-web/src/components/LoadoutCard.tsx`:

```tsx
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { inventoryApi } from '@/api/inventory'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { InventoryItem, Loadout } from '@/types/api'

type SlotKey = 'sniper' | 'weapon' | 'armor'

interface SlotDraft {
  itemId: string // '' — слот пуст
  upgrade: number
}

const SLOTS: { key: SlotKey; label: string; optional: boolean }[] = [
  { key: 'sniper', label: 'Снайперка', optional: true },
  { key: 'weapon', label: 'Основное оружие', optional: false },
  { key: 'armor', label: 'Броня', optional: false },
]

function itemsForSlot(items: InventoryItem[], key: SlotKey): InventoryItem[] {
  const usable = items.filter((i) => !i.unknown && i.category)
  if (key === 'armor') return usable.filter((i) => i.category!.startsWith('armor/'))
  if (key === 'sniper') return usable.filter((i) => i.category === 'weapon/sniper_rifle')
  return usable.filter(
    (i) => i.category!.startsWith('weapon') && i.category !== 'weapon/sniper_rifle',
  )
}

export function LoadoutCard({ loadout, editable }: { loadout: Loadout | null; editable?: boolean }) {
  const [editing, setEditing] = useState(false)

  if (!loadout && !editable) return null

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Экипировка</CardTitle>
        {editable && !editing && (
          <Button size="sm" variant="outline" className="gap-2" onClick={() => setEditing(true)}>
            <Pencil size={13} />
            Изменить
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {editing ? (
          <LoadoutEditor loadout={loadout} onClose={() => setEditing(false)} />
        ) : loadout ? (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {loadout.sniper && <LoadoutTile label="Снайперка" slot={loadout.sniper} />}
            <LoadoutTile label="Основное оружие" slot={loadout.weapon} />
            <LoadoutTile label="Броня" slot={loadout.armor} />
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            Экипировка не указана — нажми «Изменить» и выбери предметы из инвентаря.
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function LoadoutEditor({ loadout, onClose }: { loadout: Loadout | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [draft, setDraft] = useState<Record<SlotKey, SlotDraft>>({
    sniper: { itemId: loadout?.sniper?.itemId ?? '', upgrade: loadout?.sniper?.upgrade ?? 0 },
    weapon: { itemId: loadout?.weapon.itemId ?? '', upgrade: loadout?.weapon.upgrade ?? 0 },
    armor: { itemId: loadout?.armor.itemId ?? '', upgrade: loadout?.armor.upgrade ?? 0 },
  })

  const { data: inventory, isLoading } = useQuery({
    queryKey: ['inventory', 'my'],
    queryFn: inventoryApi.getMy,
  })

  const save = useMutation({
    mutationFn: inventoryApi.updateLoadout,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['players', 'me'] })
      onClose()
    },
    onError: (e: Error) => setError(e.message),
  })

  const items = inventory?.items ?? []
  const canSave = draft.weapon.itemId !== '' && draft.armor.itemId !== ''

  function setSlot(key: SlotKey, patch: Partial<SlotDraft>) {
    setDraft((d) => ({ ...d, [key]: { ...d[key], ...patch } }))
  }

  function handleSave() {
    setError(null)
    save.mutate({
      sniper: draft.sniper.itemId
        ? { itemId: draft.sniper.itemId, upgrade: draft.sniper.upgrade }
        : null,
      weapon: { itemId: draft.weapon.itemId, upgrade: draft.weapon.upgrade },
      armor: { itemId: draft.armor.itemId, upgrade: draft.armor.upgrade },
    })
  }

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Загружаем инвентарь…</p>
  }

  return (
    <div className="space-y-3">
      {SLOTS.map(({ key, label, optional }) => {
        const options = itemsForSlot(items, key)
        return (
          <div key={key} className="grid grid-cols-[1fr_auto] items-end gap-3">
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">
                {label}
                {!optional && ' *'}
              </p>
              {options.length === 0 ? (
                <p className="rounded-lg border border-border px-3 py-2 text-sm text-muted-foreground">
                  Нет подходящих предметов — добавь их в инвентарь ниже.
                </p>
              ) : (
                <select
                  aria-label={label}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-accent/50"
                  value={draft[key].itemId}
                  onChange={(e) => setSlot(key, { itemId: e.target.value })}
                >
                  <option value="">{optional ? '— без снайперки —' : '— выбери предмет —'}</option>
                  {options.map((item) => (
                    <option key={item.itemId} value={item.itemId}>
                      {item.name}
                    </option>
                  ))}
                </select>
              )}
            </div>
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">Заточка</p>
              <input
                type="number"
                aria-label={`Заточка: ${label}`}
                min={0}
                max={15}
                className="w-20 rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-accent/50 disabled:opacity-50"
                value={draft[key].upgrade}
                disabled={draft[key].itemId === ''}
                onChange={(e) =>
                  setSlot(key, {
                    upgrade: Math.max(0, Math.min(15, Number(e.target.value) || 0)),
                  })
                }
              />
            </div>
          </div>
        )
      })}

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex gap-2 pt-1">
        <Button size="sm" onClick={handleSave} disabled={!canSave || save.isPending}>
          {save.isPending ? 'Сохраняем…' : 'Сохранить'}
        </Button>
        <Button size="sm" variant="outline" onClick={onClose} disabled={save.isPending}>
          Отмена
        </Button>
      </div>
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
```

- [ ] **Step 4: Подключить `LoadoutCard` в `PlayerProfileView`**

В `frontend/awake-web/src/components/PlayerProfileView.tsx`:

1. Импорт: `import { LoadoutCard } from '@/components/LoadoutCard'`.
2. В `interface Props` добавить `editable?: boolean`; в сигнатуру компонента — `editable`.
3. Блок «Экипировка» (строки 99–109) заменить на:

```tsx
      {/* Экипировка */}
      <LoadoutCard loadout={loadout} editable={editable} />
```

4. Удалить локальную функцию `LoadoutTile` (строки 146–156) — она переехала в `LoadoutCard.tsx`.

- [ ] **Step 5: Включить редактирование на странице своего профиля**

В `frontend/awake-web/src/routes/_auth.profile.tsx` строку

```tsx
      <PlayerProfileView profile={profile} onRefresh={handleRefresh} refreshing={refreshing} />
```

заменить на

```tsx
      <PlayerProfileView profile={profile} onRefresh={handleRefresh} refreshing={refreshing} editable />
```

(Страница чужого профиля `_auth.players.$userId.tsx` не меняется — `editable` там не передаётся.)

- [ ] **Step 6: Проверка типов и линта**

Run: `cd frontend/awake-web && npm run build && npm run lint`
Expected: `tsc -b` и `vite build` без ошибок, eslint без ошибок.

- [ ] **Step 7: Commit**

```bash
git add frontend/awake-web/src/types/api.ts frontend/awake-web/src/api/inventory.ts frontend/awake-web/src/components/LoadoutCard.tsx frontend/awake-web/src/components/PlayerProfileView.tsx frontend/awake-web/src/routes/_auth.profile.tsx
git commit -m "feat(loadout): редактирование экипировки из инвентаря в профиле"
```

---

### Task 5: Миграция на дев-стенде + сквозная проверка

**Files:** нет изменений кода; только стенд.

**Interfaces:**
- Consumes: всё из Task 1–4; локальный стенд — docker compose проект `featurestage-4` (api → localhost:5001, db → localhost:5432), vite dev server → localhost:5173.

- [ ] **Step 1: Применить миграцию к базе стенда**

Пароль Postgres — в файле `.env` (в корне `D:\Awake`; если там нет — взять из `.claude/worktrees/feature+stage-4/.env`, переменная `POSTGRES_PASSWORD`). Затем:

```bash
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=awake_dev;Username=postgres;Password=<POSTGRES_PASSWORD>" \
  dotnet ef database update --project src/Awake.Infrastructure --startup-project src/Awake.API
```

Expected: в логе `Applying migration '..._AddUserLoadout'`, завершение без ошибок.

- [ ] **Step 2: Пересобрать api-контейнер стенда**

Из корня `D:\Awake` (compose-проект работающего стенда называется `featurestage-4`; `--env-file` указать на тот же `.env`, что в Step 1):

```bash
docker compose -p featurestage-4 --env-file <путь к .env> up -d --build api
```

Expected: образ пересобрался, контейнер `featurestage-4-api-1` перезапущен, `docker ps` показывает его Up.

- [ ] **Step 3: Смоук API**

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/public/leaderboard
```
Expected: `200`.

Затем без токена:

```bash
curl -s -o /dev/null -w "%{http_code}" -X PUT http://localhost:5001/api/profile/loadout -H "Content-Type: application/json" -d '{"weapon":{"itemId":"x","upgrade":0},"armor":{"itemId":"x","upgrade":0}}'
```
Expected: `401` (эндпоинт существует и закрыт авторизацией; `404` означал бы, что рут не подхватился).

- [ ] **Step 4: Сквозная проверка в UI**

Убедиться, что vite dev server запущен (`http://localhost:5173`, иначе `cd frontend/awake-web && npm run dev`). Залогиниться, открыть «Профиль» и проверить:

1. Карточка «Экипировка» видна (даже если экипировки не было).
2. «Изменить» → в выпадающих списках только предметы из инвентаря, по категориям слотов.
3. Выбрать оружие + броню, поставить заточку, «Сохранить» → карточка показывает выбранное с «+N».
4. Обновить страницу — экипировка сохранилась.
5. Открыть чужой профиль — кнопки «Изменить» нет.

Expected: все пять пунктов проходят.

- [ ] **Step 5: Финальный прогон тестов и commit плана**

Run: `dotnet test tests/Awake.Unit.Tests`
Expected: все PASS.

```bash
git add docs/superpowers/plans/2026-07-19-loadout-from-inventory.md
git commit -m "docs: план реализации смены экипировки из инвентаря"
```
