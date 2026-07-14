# Инвентарь игрока (этап 1 фичи «билдер отрядов») — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Игрок ведёт инвентарь (предметы из базы stalzone) и грузит пруф-скрины сборок; из этого вычисляются 5 флагов (био/боевая/снайперка/скорость/живучесть), которые видят офицеры.

**Architecture:** Две новые сущности (PlayerInventoryItem, PlayerBuildProof в Postgres, скрины bytea ≤ 2 МБ), флаги вычисляются на лету из ItemCacheService + наличия пруфов (не хранятся). CQRS как во всём проекте: MediatR-команды/запросы, репозитории с SaveChanges внутри, контроллер тонкий. Фронт: секция «Инвентарь и сборки» в своём профиле, строка флагов в чужом.

**Tech Stack:** ASP.NET Core 8 (net10.0 tfm), EF Core + Npgsql, MediatR, xUnit+Moq+FluentAssertions; React + TanStack Query/Router, Tailwind.

**Spec:** `docs/superpowers/specs/2026-07-14-squad-builder-inventory-design.md`

## Global Constraints

- Ветка: `feature/player-inventory` (уже создана от master). Каждая задача — один коммит.
- Никаких новых npm/NuGet-зависимостей на этом этапе (@dnd-kit — только в этапе 2, билдер).
- RU-тексты интерфейса и ошибок; тёмная тема; HSL-токены/палитра не трогаются.
- Enum-ы в JSON — числа (JsonStringEnumConverter в проекте НЕ включён): BuildType Speed=0, Vitality=1.
- Флаги: Био = `Category == "armor/combined"` и `Color` ∈ {`RANK_MASTER`,`RANK_LEGEND`}; Боевая = `Category == "armor/combat"` (любое качество); Снайперка = `Category == "weapon/sniper_rifle"`; Скорость/Живучесть = есть пруф соответствующего типа. Категории проверены по живому listing.json stalzone-database.
- Скрины: png/jpeg/webp, ≤ 2 МБ (2_097_152 байт), хранение в Postgres (bytea).
- Права: свой инвентарь — любой ранг; чужой инвентарь/флаги — Member+; чужие пруф-скрины смотреть/удалять — владелец или Officer+.
- Команды запускать из корня worktree; для цепочек `&&` использовать Bash (не PowerShell 5.1).
- Тесты: `dotnet test` — все существующие (75) должны оставаться зелёными.

---

### Task 1: Домен + EF (сущности, DbSet, миграция)

**Files:**
- Create: `src/Awake.Domain/Enums/BuildType.cs`
- Create: `src/Awake.Domain/Entities/PlayerInventoryItem.cs`
- Create: `src/Awake.Domain/Entities/PlayerBuildProof.cs`
- Modify: `src/Awake.Infrastructure/Persistence/AppDbContext.cs` (DbSet + конфигурация)
- Create (генерируется): `src/Awake.Infrastructure/Persistence/Migrations/*_AddPlayerInventory.cs`

**Interfaces:**
- Produces: сущности `PlayerInventoryItem { Guid UserId, User User, string ItemId }`, `PlayerBuildProof { Guid UserId, User User, BuildType BuildType, byte[] Image, string ContentType }`, enum `BuildType { Speed = 0, Vitality = 1 }` — используются задачами 2–5.

- [ ] **Step 1: Создать enum и сущности**

```csharp
// src/Awake.Domain/Enums/BuildType.cs
namespace Awake.Domain.Enums;

public enum BuildType
{
    Speed = 0,    // сборка на скорость («сапог»)
    Vitality = 1  // сборка на живучесть («сердечко»)
}
```

```csharp
// src/Awake.Domain/Entities/PlayerInventoryItem.cs
using Awake.Domain.Common;

namespace Awake.Domain.Entities;

public class PlayerInventoryItem : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>ID предмета из stalzone-database (например "1r79g").</summary>
    public string ItemId { get; set; } = string.Empty;
}
```

```csharp
// src/Awake.Domain/Entities/PlayerBuildProof.cs
using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class PlayerBuildProof : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BuildType BuildType { get; set; }
    public byte[] Image { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
}
```

- [ ] **Step 2: DbSet и конфигурация в AppDbContext**

Добавить к остальным DbSet:

```csharp
    public DbSet<PlayerInventoryItem> PlayerInventoryItems => Set<PlayerInventoryItem>();
    public DbSet<PlayerBuildProof> PlayerBuildProofs => Set<PlayerBuildProof>();
```

В `OnModelCreating` (рядом с конфигурацией существующих сущностей, стиль сохранить как в файле):

```csharp
        modelBuilder.Entity<PlayerInventoryItem>(e =>
        {
            e.Property(x => x.ItemId).HasMaxLength(64);
            e.HasIndex(x => new { x.UserId, x.ItemId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerBuildProof>(e =>
        {
            e.Property(x => x.ContentType).HasMaxLength(64);
            e.HasIndex(x => new { x.UserId, x.BuildType }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
```

- [ ] **Step 3: Сборка**

Run: `dotnet build --nologo -v q`
Expected: 0 Warning(s), 0 Error(s)

- [ ] **Step 4: Миграция**

Run (из корня worktree):
```bash
dotnet ef migrations add AddPlayerInventory --project src/Awake.Infrastructure --startup-project src/Awake.API
```
Expected: в `src/Awake.Infrastructure/Persistence/Migrations/` появились `*_AddPlayerInventory.cs` с таблицами `PlayerInventoryItems`, `PlayerBuildProofs` и двумя уникальными индексами. Если `dotnet ef` не установлен: `dotnet tool install -g dotnet-ef`, повторить.

Проверить в сгенерированной миграции: `Image = table.Column<byte[]>(type: "bytea", ...)`.

- [ ] **Step 5: Прогон тестов + Commit**

Run: `dotnet test --nologo -v q` → 75/75 passed (как до задачи).

```bash
git add src
git commit -m "feat(api): player inventory + build proof entities and migration"
```

---

### Task 2: Репозитории + DI

**Files:**
- Create: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerInventoryRepository.cs`
- Create: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerBuildProofRepository.cs`
- Create: `src/Awake.Infrastructure/Persistence/Repositories/PlayerInventoryRepository.cs`
- Create: `src/Awake.Infrastructure/Persistence/Repositories/PlayerBuildProofRepository.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs` (в блок Repositories)

**Interfaces:**
- Consumes: сущности из Task 1.
- Produces (для задач 3–5):
  - `IPlayerInventoryRepository`: `Task<IReadOnlyList<PlayerInventoryItem>> GetByUserAsync(Guid userId, CancellationToken ct = default)`, `Task<PlayerInventoryItem?> GetAsync(Guid userId, string itemId, CancellationToken ct = default)`, `Task AddAsync(PlayerInventoryItem item, CancellationToken ct = default)`, `Task RemoveAsync(PlayerInventoryItem item, CancellationToken ct = default)`
  - `IPlayerBuildProofRepository`: `Task<IReadOnlyList<PlayerBuildProof>> GetByUserAsync(Guid userId, CancellationToken ct = default)` (без Image — projection!), `Task<PlayerBuildProof?> GetAsync(Guid userId, BuildType type, CancellationToken ct = default)`, `Task AddAsync(...)`, `Task UpdateAsync(...)`, `Task RemoveAsync(...)`

- [ ] **Step 1: Интерфейсы**

```csharp
// src/Awake.Application/Common/Interfaces/Repositories/IPlayerInventoryRepository.cs
using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerInventoryRepository
{
    Task<IReadOnlyList<PlayerInventoryItem>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<PlayerInventoryItem?> GetAsync(Guid userId, string itemId, CancellationToken ct = default);
    Task AddAsync(PlayerInventoryItem item, CancellationToken ct = default);
    Task RemoveAsync(PlayerInventoryItem item, CancellationToken ct = default);
}
```

```csharp
// src/Awake.Application/Common/Interfaces/Repositories/IPlayerBuildProofRepository.cs
using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerBuildProofRepository
{
    /// <summary>Без поля Image (byte[]) — только метаданные, чтобы не тащить картинки списком.</summary>
    Task<IReadOnlyList<PlayerBuildProof>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<PlayerBuildProof?> GetAsync(Guid userId, BuildType type, CancellationToken ct = default);
    Task AddAsync(PlayerBuildProof proof, CancellationToken ct = default);
    Task UpdateAsync(PlayerBuildProof proof, CancellationToken ct = default);
    Task RemoveAsync(PlayerBuildProof proof, CancellationToken ct = default);
}
```

- [ ] **Step 2: Реализации**

Перед написанием открыть любой существующий репозиторий (например `NotificationRepository.cs` в той же папке) и повторить его стиль (конструктор с AppDbContext, SaveChangesAsync внутри методов записи).

```csharp
// src/Awake.Infrastructure/Persistence/Repositories/PlayerInventoryRepository.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerInventoryRepository(AppDbContext context) : IPlayerInventoryRepository
{
    public async Task<IReadOnlyList<PlayerInventoryItem>> GetByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerInventoryItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<PlayerInventoryItem?> GetAsync(Guid userId, string itemId, CancellationToken ct = default) =>
        context.PlayerInventoryItems
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ItemId == itemId, ct);

    public async Task AddAsync(PlayerInventoryItem item, CancellationToken ct = default)
    {
        context.PlayerInventoryItems.Add(item);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(PlayerInventoryItem item, CancellationToken ct = default)
    {
        context.PlayerInventoryItems.Remove(item);
        await context.SaveChangesAsync(ct);
    }
}
```

```csharp
// src/Awake.Infrastructure/Persistence/Repositories/PlayerBuildProofRepository.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerBuildProofRepository(AppDbContext context) : IPlayerBuildProofRepository
{
    public async Task<IReadOnlyList<PlayerBuildProof>> GetByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerBuildProofs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new PlayerBuildProof
            {
                Id = x.Id,
                UserId = x.UserId,
                BuildType = x.BuildType,
                ContentType = x.ContentType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                // Image намеренно не выбирается
            })
            .ToListAsync(ct);

    public Task<PlayerBuildProof?> GetAsync(Guid userId, BuildType type, CancellationToken ct = default) =>
        context.PlayerBuildProofs
            .FirstOrDefaultAsync(x => x.UserId == userId && x.BuildType == type, ct);

    public async Task AddAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Add(proof);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Update(proof);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(PlayerBuildProof proof, CancellationToken ct = default)
    {
        context.PlayerBuildProofs.Remove(proof);
        await context.SaveChangesAsync(ct);
    }
}
```

Примечание: `BaseEntity.Id`/`CreatedAt` имеют `init` — если projection с объект-инициализатором не компилируется по этой причине, заменить projection на `.Select(...)` с анонимным типом + маппинг вручную, либо (проще) оставить полную выборку БЕЗ поля Image через `EF.Property`, но НЕ грузить картинки списком.

- [ ] **Step 3: DI**

В `DependencyInjection.cs`, блок Repositories:

```csharp
        services.AddScoped<IPlayerInventoryRepository, PlayerInventoryRepository>();
        services.AddScoped<IPlayerBuildProofRepository, PlayerBuildProofRepository>();
```

- [ ] **Step 4: Сборка + Commit**

Run: `dotnet build --nologo -v q` → 0 ошибок.

```bash
git add src
git commit -m "feat(api): player inventory + build proof repositories"
```

---

### Task 3: Флаги + запрос инвентаря (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Inventory/PlayerFlagsCalculator.cs`
- Create: `src/Awake.Application/Features/Inventory/Dtos/InventoryDtos.cs`
- Create: `src/Awake.Application/Features/Inventory/Queries/GetPlayerInventory/GetPlayerInventoryQuery.cs`
- Create: `src/Awake.Application/Features/Inventory/Queries/GetPlayerInventory/GetPlayerInventoryQueryHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Inventory/PlayerFlagsCalculatorTests.cs`
- Test: `tests/Awake.Unit.Tests/Features/Inventory/GetPlayerInventoryQueryHandlerTests.cs`

**Interfaces:**
- Consumes: репозитории из Task 2, `IItemCacheService.GetById(string id)` → `ItemDto(string Id, string Category, string NameRu, string Icon, string Color)`.
- Produces (для Task 5–6):
  - `PlayerFlagsDto(bool Bio, bool Combat, bool Sniper, bool Speed, bool Vitality)`
  - `InventoryItemDto(string ItemId, string Name, string? Icon, string? Color, string? Category, bool Unknown)`
  - `PlayerInventoryDto(IReadOnlyList<InventoryItemDto> Items, PlayerFlagsDto Flags)`
  - `GetPlayerInventoryQuery(Guid UserId)` → `Result<PlayerInventoryDto>`
  - `PlayerFlagsCalculator.Calculate(IEnumerable<ItemDto> knownItems, bool hasSpeedProof, bool hasVitalityProof)` → `PlayerFlagsDto` (static)

- [ ] **Step 1: DTO и калькулятор (нужны тестам для компиляции)**

```csharp
// src/Awake.Application/Features/Inventory/Dtos/InventoryDtos.cs
namespace Awake.Application.Features.Inventory.Dtos;

public record PlayerFlagsDto(bool Bio, bool Combat, bool Sniper, bool Speed, bool Vitality);

/// <summary>Unknown = предмет пропал из базы stalzone после пересинка; во флагах не участвует.</summary>
public record InventoryItemDto(
    string ItemId, string Name, string? Icon, string? Color, string? Category, bool Unknown);

public record PlayerInventoryDto(
    IReadOnlyList<InventoryItemDto> Items, PlayerFlagsDto Flags);
```

```csharp
// src/Awake.Application/Features/Inventory/PlayerFlagsCalculator.cs
using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;

namespace Awake.Application.Features.Inventory;

public static class PlayerFlagsCalculator
{
    private const string CombinedArmor = "armor/combined";
    private const string CombatArmor = "armor/combat";
    private const string SniperRifle = "weapon/sniper_rifle";
    private static readonly string[] BioQualities = ["RANK_MASTER", "RANK_LEGEND"];

    public static PlayerFlagsDto Calculate(
        IEnumerable<ItemDto> knownItems, bool hasSpeedProof, bool hasVitalityProof)
    {
        var items = knownItems.ToList();
        return new PlayerFlagsDto(
            Bio: items.Any(i => i.Category == CombinedArmor && BioQualities.Contains(i.Color)),
            Combat: items.Any(i => i.Category == CombatArmor),
            Sniper: items.Any(i => i.Category == SniperRifle),
            Speed: hasSpeedProof,
            Vitality: hasVitalityProof);
    }
}
```

- [ ] **Step 2: Тесты калькулятора (сначала убедиться, что зелёные vs логика)**

```csharp
// tests/Awake.Unit.Tests/Features/Inventory/PlayerFlagsCalculatorTests.cs
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Items.Dtos;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Inventory;

public class PlayerFlagsCalculatorTests
{
    private static ItemDto Item(string category, string color = "") =>
        new("id-" + Guid.NewGuid().ToString("N")[..6], category, "Предмет", "icon.png", color);

    [Theory]
    [InlineData("RANK_MASTER", true)]
    [InlineData("RANK_LEGEND", true)]
    [InlineData("RANK_VETERAN", false)] // комбинированная, но не мастерка/легенда
    [InlineData("DEFAULT", false)]
    public void Bio_RequiresCombinedArmor_MasterOrLegend(string color, bool expected)
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/combined", color)], false, false);
        flags.Bio.Should().Be(expected);
    }

    [Fact]
    public void Combat_AnyQualityCombatArmor()
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/combat", "RANK_NEWBIE")], false, false);
        flags.Combat.Should().BeTrue();
        flags.Bio.Should().BeFalse();
    }

    [Fact]
    public void Sniper_SniperRifleCategory()
    {
        var flags = PlayerFlagsCalculator.Calculate([Item("weapon/sniper_rifle")], false, false);
        flags.Sniper.Should().BeTrue();
    }

    [Fact]
    public void SpeedAndVitality_ComeFromProofs()
    {
        var flags = PlayerFlagsCalculator.Calculate([], hasSpeedProof: true, hasVitalityProof: true);
        flags.Speed.Should().BeTrue();
        flags.Vitality.Should().BeTrue();
        flags.Bio.Should().BeFalse();
    }

    [Fact]
    public void EmptyInventory_AllFalse()
    {
        var flags = PlayerFlagsCalculator.Calculate([], false, false);
        flags.Should().Be(new Awake.Application.Features.Inventory.Dtos.PlayerFlagsDto(
            false, false, false, false, false));
    }

    [Fact]
    public void ScientistArmor_DoesNotGiveBio()
    {
        // научная броня даёт биозащиту в игре, но по спеке био-флаг — только комбинированная
        var flags = PlayerFlagsCalculator.Calculate([Item("armor/scientist", "RANK_MASTER")], false, false);
        flags.Bio.Should().BeFalse();
    }
}
```

Run: `dotnet test --nologo -v q --filter PlayerFlagsCalculatorTests`
Expected: 9 passed (6 фактов, из них Theory ×4).

- [ ] **Step 3: Query + handler**

```csharp
// src/Awake.Application/Features/Inventory/Queries/GetPlayerInventory/GetPlayerInventoryQuery.cs
using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory.Dtos;
using MediatR;

namespace Awake.Application.Features.Inventory.Queries.GetPlayerInventory;

public record GetPlayerInventoryQuery(Guid UserId) : IRequest<Result<PlayerInventoryDto>>;
```

```csharp
// src/Awake.Application/Features/Inventory/Queries/GetPlayerInventory/GetPlayerInventoryQueryHandler.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Queries.GetPlayerInventory;

public class GetPlayerInventoryQueryHandler(
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache
) : IRequestHandler<GetPlayerInventoryQuery, Result<PlayerInventoryDto>>
{
    public async Task<Result<PlayerInventoryDto>> Handle(
        GetPlayerInventoryQuery request, CancellationToken cancellationToken)
    {
        var entries = await inventoryRepository.GetByUserAsync(request.UserId, cancellationToken);
        var proofs = await proofRepository.GetByUserAsync(request.UserId, cancellationToken);

        var known = new List<ItemDto>();
        var items = new List<InventoryItemDto>();
        foreach (var entry in entries)
        {
            var item = itemCache.GetById(entry.ItemId);
            if (item is null)
            {
                items.Add(new InventoryItemDto(entry.ItemId, "Неизвестный предмет",
                    null, null, null, Unknown: true));
            }
            else
            {
                known.Add(item);
                items.Add(new InventoryItemDto(item.Id, item.NameRu,
                    item.Icon, item.Color, item.Category, Unknown: false));
            }
        }

        var flags = PlayerFlagsCalculator.Calculate(
            known,
            hasSpeedProof: proofs.Any(p => p.BuildType == BuildType.Speed),
            hasVitalityProof: proofs.Any(p => p.BuildType == BuildType.Vitality));

        return Result<PlayerInventoryDto>.Success(new PlayerInventoryDto(items, flags));
    }
}
```

- [ ] **Step 4: Тесты хэндлера**

```csharp
// tests/Awake.Unit.Tests/Features/Inventory/GetPlayerInventoryQueryHandlerTests.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Queries.GetPlayerInventory;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class GetPlayerInventoryQueryHandlerTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    private GetPlayerInventoryQueryHandler BuildHandler() =>
        new(_inventory.Object, _proofs.Object, _cache.Object);

    [Fact]
    public async Task Handle_KnownAndUnknownItems_FlagsOnlyFromKnown()
    {
        _inventory.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlayerInventoryItem { UserId = _userId, ItemId = "known-armor" },
                new PlayerInventoryItem { UserId = _userId, ItemId = "gone-item" },
            ]);
        _proofs.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlayerBuildProof { UserId = _userId, BuildType = BuildType.Speed }]);
        _cache.Setup(c => c.GetById("known-armor"))
            .Returns(new ItemDto("known-armor", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _cache.Setup(c => c.GetById("gone-item")).Returns((ItemDto?)null);

        var result = await BuildHandler().Handle(
            new GetPlayerInventoryQuery(_userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Items.Should().HaveCount(2);
        dto.Items[0].Name.Should().Be("Скиф-5");
        dto.Items[1].Unknown.Should().BeTrue();
        dto.Flags.Bio.Should().BeTrue();
        dto.Flags.Speed.Should().BeTrue();
        dto.Flags.Vitality.Should().BeFalse();
        dto.Flags.Combat.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmptyInventory_EmptyDtoAllFlagsFalse()
    {
        _inventory.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await BuildHandler().Handle(
            new GetPlayerInventoryQuery(_userId), CancellationToken.None);

        result.Value!.Items.Should().BeEmpty();
        result.Value.Flags.Should().Be(
            new Awake.Application.Features.Inventory.Dtos.PlayerFlagsDto(false, false, false, false, false));
    }
}
```

- [ ] **Step 5: Прогон + Commit**

Run: `dotnet test --nologo -v q` → все зелёные (75 + 11 новых).

```bash
git add src tests
git commit -m "feat(api): inventory query with derived player flags"
```

---

### Task 4: Команды инвентаря и пруфов (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Inventory/Commands/AddInventoryItem/AddInventoryItemCommand.cs` (+Handler)
- Create: `src/Awake.Application/Features/Inventory/Commands/RemoveInventoryItem/RemoveInventoryItemCommand.cs` (+Handler)
- Create: `src/Awake.Application/Features/Inventory/Commands/UploadBuildProof/UploadBuildProofCommand.cs` (+Handler)
- Create: `src/Awake.Application/Features/Inventory/Commands/DeleteBuildProof/DeleteBuildProofCommand.cs` (+Handler)
- Test: `tests/Awake.Unit.Tests/Features/Inventory/InventoryCommandsTests.cs`

**Interfaces:**
- Consumes: репозитории Task 2, `IItemCacheService`, DTO Task 3.
- Produces (для Task 5):
  - `AddInventoryItemCommand(Guid UserId, string ItemId)` → `Result<bool>`
  - `RemoveInventoryItemCommand(Guid UserId, string ItemId)` → `Result<bool>`
  - `UploadBuildProofCommand(Guid UserId, BuildType Type, byte[] Image, string ContentType)` → `Result<bool>`
  - `DeleteBuildProofCommand(Guid UserId, BuildType Type)` → `Result<bool>` (права проверяет контроллер)

- [ ] **Step 1: Команды и хэндлеры**

```csharp
// src/Awake.Application/Features/Inventory/Commands/AddInventoryItem/AddInventoryItemCommand.cs
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.AddInventoryItem;

public record AddInventoryItemCommand(Guid UserId, string ItemId) : IRequest<Result<bool>>;
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/AddInventoryItem/AddInventoryItemCommandHandler.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.AddInventoryItem;

public class AddInventoryItemCommandHandler(
    IPlayerInventoryRepository repository,
    IItemCacheService itemCache
) : IRequestHandler<AddInventoryItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        AddInventoryItemCommand request, CancellationToken cancellationToken)
    {
        if (itemCache.GetById(request.ItemId) is null)
            return Result<bool>.Failure("Предмет не найден в базе.");

        if (await repository.GetAsync(request.UserId, request.ItemId, cancellationToken) is not null)
            return Result<bool>.Failure("Этот предмет уже в инвентаре.");

        await repository.AddAsync(new PlayerInventoryItem
        {
            UserId = request.UserId,
            ItemId = request.ItemId,
        }, cancellationToken);

        return Result<bool>.Success(true);
    }
}
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/RemoveInventoryItem/RemoveInventoryItemCommand.cs
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;

public record RemoveInventoryItemCommand(Guid UserId, string ItemId) : IRequest<Result<bool>>;
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/RemoveInventoryItem/RemoveInventoryItemCommandHandler.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;

public class RemoveInventoryItemCommandHandler(
    IPlayerInventoryRepository repository
) : IRequestHandler<RemoveInventoryItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var entry = await repository.GetAsync(request.UserId, request.ItemId, cancellationToken);
        if (entry is null)
            return Result<bool>.Failure("Предмета нет в инвентаре.");

        await repository.RemoveAsync(entry, cancellationToken);
        return Result<bool>.Success(true);
    }
}
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/UploadBuildProof/UploadBuildProofCommand.cs
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UploadBuildProof;

public record UploadBuildProofCommand(
    Guid UserId, BuildType Type, byte[] Image, string ContentType) : IRequest<Result<bool>>;
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/UploadBuildProof/UploadBuildProofCommandHandler.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.UploadBuildProof;

public class UploadBuildProofCommandHandler(
    IPlayerBuildProofRepository repository
) : IRequestHandler<UploadBuildProofCommand, Result<bool>>
{
    public const int MaxImageBytes = 2_097_152; // 2 МБ
    private static readonly string[] AllowedContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public async Task<Result<bool>> Handle(
        UploadBuildProofCommand request, CancellationToken cancellationToken)
    {
        if (request.Image.Length == 0)
            return Result<bool>.Failure("Файл пуст.");
        if (request.Image.Length > MaxImageBytes)
            return Result<bool>.Failure("Файл больше 2 МБ — сожми скрин и попробуй ещё раз.");
        if (!AllowedContentTypes.Contains(request.ContentType))
            return Result<bool>.Failure("Поддерживаются только PNG, JPEG и WebP.");

        var existing = await repository.GetAsync(request.UserId, request.Type, cancellationToken);
        if (existing is not null)
        {
            existing.Image = request.Image;
            existing.ContentType = request.ContentType;
            existing.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            await repository.AddAsync(new PlayerBuildProof
            {
                UserId = request.UserId,
                BuildType = request.Type,
                Image = request.Image,
                ContentType = request.ContentType,
            }, cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/DeleteBuildProof/DeleteBuildProofCommand.cs
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.DeleteBuildProof;

public record DeleteBuildProofCommand(Guid UserId, BuildType Type) : IRequest<Result<bool>>;
```

```csharp
// src/Awake.Application/Features/Inventory/Commands/DeleteBuildProof/DeleteBuildProofCommandHandler.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Inventory.Commands.DeleteBuildProof;

public class DeleteBuildProofCommandHandler(
    IPlayerBuildProofRepository repository
) : IRequestHandler<DeleteBuildProofCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteBuildProofCommand request, CancellationToken cancellationToken)
    {
        var proof = await repository.GetAsync(request.UserId, request.Type, cancellationToken);
        if (proof is null)
            return Result<bool>.Failure("Пруф не найден.");

        await repository.RemoveAsync(proof, cancellationToken);
        return Result<bool>.Success(true);
    }
}
```

- [ ] **Step 2: Тесты команд**

```csharp
// tests/Awake.Unit.Tests/Features/Inventory/InventoryCommandsTests.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.AddInventoryItem;
using Awake.Application.Features.Inventory.Commands.DeleteBuildProof;
using Awake.Application.Features.Inventory.Commands.UploadBuildProof;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Inventory;

public class InventoryCommandsTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task AddItem_UnknownInCache_Fails()
    {
        _cache.Setup(c => c.GetById("nope")).Returns((ItemDto?)null);
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "nope"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _inventory.Verify(r => r.AddAsync(It.IsAny<PlayerInventoryItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItem_Duplicate_Fails()
    {
        _cache.Setup(c => c.GetById("armor1"))
              .Returns(new ItemDto("armor1", "armor/combat", "Броня", "i.png", ""));
        _inventory.Setup(r => r.GetAsync(_userId, "armor1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerInventoryItem { UserId = _userId, ItemId = "armor1" });
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "armor1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AddItem_Valid_Saves()
    {
        _cache.Setup(c => c.GetById("armor1"))
              .Returns(new ItemDto("armor1", "armor/combat", "Броня", "i.png", ""));
        _inventory.Setup(r => r.GetAsync(_userId, "armor1", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerInventoryItem?)null);
        var handler = new AddInventoryItemCommandHandler(_inventory.Object, _cache.Object);

        var result = await handler.Handle(
            new AddInventoryItemCommand(_userId, "armor1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _inventory.Verify(r => r.AddAsync(
            It.Is<PlayerInventoryItem>(i => i.UserId == _userId && i.ItemId == "armor1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("text/html")]
    public async Task UploadProof_BadContentType_Fails(string contentType)
    {
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, [1, 2, 3], contentType), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UploadProof_TooLarge_Fails()
    {
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);
        var big = new byte[UploadBuildProofCommandHandler.MaxImageBytes + 1];

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, big, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UploadProof_New_Adds()
    {
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Speed, It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerBuildProof?)null);
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Speed, [1, 2, 3], "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _proofs.Verify(r => r.AddAsync(
            It.Is<PlayerBuildProof>(p => p.UserId == _userId && p.BuildType == BuildType.Speed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadProof_Existing_Replaces()
    {
        var existing = new PlayerBuildProof
        {
            UserId = _userId, BuildType = BuildType.Vitality,
            Image = [9], ContentType = "image/png",
        };
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Vitality, It.IsAny<CancellationToken>()))
               .ReturnsAsync(existing);
        var handler = new UploadBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(new UploadBuildProofCommand(
            _userId, BuildType.Vitality, [1, 2], "image/webp"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Image.Should().Equal(1, 2);
        existing.ContentType.Should().Be("image/webp");
        _proofs.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _proofs.Verify(r => r.AddAsync(It.IsAny<PlayerBuildProof>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProof_Missing_Fails()
    {
        _proofs.Setup(r => r.GetAsync(_userId, BuildType.Speed, It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerBuildProof?)null);
        var handler = new DeleteBuildProofCommandHandler(_proofs.Object);

        var result = await handler.Handle(
            new DeleteBuildProofCommand(_userId, BuildType.Speed), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

- [ ] **Step 3: Прогон + Commit**

Run: `dotnet test --nologo -v q` → все зелёные.

```bash
git add src tests
git commit -m "feat(api): inventory item and build proof commands"
```

---

### Task 5: InventoryController (API-слой)

**Files:**
- Create: `src/Awake.API/Controllers/InventoryController.cs`

**Interfaces:**
- Consumes: команды/запрос Task 3–4, `ICurrentUserService { Guid UserId; UserRank Rank; }`, `RankAuthorizeAttribute`.
- Produces (для фронта, Task 6):
  - `GET  /api/profile/inventory` — свой (любой ранг) → `PlayerInventoryDto`
  - `POST /api/profile/inventory/items` body `{ "itemId": "..." }` → 200/400
  - `DELETE /api/profile/inventory/items/{itemId}` → 200/400
  - `POST /api/profile/build-proof` multipart: `type` (0|1), `file` → 200/400
  - `DELETE /api/profile/build-proof/{type}` → 200/400 (свой)
  - `GET  /api/players/{userId}/inventory` — Member+ → `PlayerInventoryDto`
  - `GET  /api/players/{userId}/build-proof/{type}/image` — владелец или Officer+ → файл
  - `DELETE /api/players/{userId}/build-proof/{type}` — владелец или Officer+ → 200/404

- [ ] **Step 1: Контроллер**

```csharp
// src/Awake.API/Controllers/InventoryController.cs
using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory.Commands.AddInventoryItem;
using Awake.Application.Features.Inventory.Commands.DeleteBuildProof;
using Awake.Application.Features.Inventory.Commands.RemoveInventoryItem;
using Awake.Application.Features.Inventory.Commands.UploadBuildProof;
using Awake.Application.Features.Inventory.Queries.GetPlayerInventory;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record AddItemRequest(string ItemId);

[ApiController]
[Authorize]
public class InventoryController(
    ISender sender,
    ICurrentUserService currentUser,
    IPlayerBuildProofRepository proofRepository
) : ControllerBase
{
    // ── Свой инвентарь (любой ранг) ─────────────────────────────────────────

    [HttpGet("api/profile/inventory")]
    public async Task<IActionResult> GetMyInventory(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerInventoryQuery(currentUser.UserId), ct);
        return Ok(result.Value);
    }

    [HttpPost("api/profile/inventory/items")]
    public async Task<IActionResult> AddItem(AddItemRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddInventoryItemCommand(currentUser.UserId, request.ItemId), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("api/profile/inventory/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(string itemId, CancellationToken ct)
    {
        var result = await sender.Send(
            new RemoveInventoryItemCommand(currentUser.UserId, itemId), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("api/profile/build-proof")]
    [RequestSizeLimit(4_194_304)] // запас над лимитом 2 МБ, чтобы отдавать своё сообщение об ошибке
    public async Task<IActionResult> UploadProof(
        [FromForm] BuildType type, IFormFile file, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var result = await sender.Send(new UploadBuildProofCommand(
            currentUser.UserId, type, ms.ToArray(), file.ContentType), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpDelete("api/profile/build-proof/{type}")]
    public async Task<IActionResult> DeleteMyProof(BuildType type, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeleteBuildProofCommand(currentUser.UserId, type), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── Чужой инвентарь ─────────────────────────────────────────────────────

    [HttpGet("api/players/{userId:guid}/inventory")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetInventory(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerInventoryQuery(userId), ct);
        return Ok(result.Value);
    }

    [HttpGet("api/players/{userId:guid}/build-proof/{type}/image")]
    public async Task<IActionResult> GetProofImage(Guid userId, BuildType type, CancellationToken ct)
    {
        if (!IsOwnerOrOfficer(userId))
            return Forbid();

        var proof = await proofRepository.GetAsync(userId, type, ct);
        return proof is null
            ? NotFound()
            : File(proof.Image, proof.ContentType);
    }

    [HttpDelete("api/players/{userId:guid}/build-proof/{type}")]
    public async Task<IActionResult> DeleteProof(Guid userId, BuildType type, CancellationToken ct)
    {
        if (!IsOwnerOrOfficer(userId))
            return Forbid();

        var result = await sender.Send(new DeleteBuildProofCommand(userId, type), ct);
        return result.IsSuccess
            ? Ok()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    // Пруф-скрины: смотреть/удалять может сам владелец и Officer+
    private bool IsOwnerOrOfficer(Guid userId) =>
        currentUser.UserId == userId || currentUser.Rank >= UserRank.Officer;
}
```

- [ ] **Step 2: Сборка + все тесты**

Run: `dotnet build --nologo -v q && dotnet test --nologo -v q`
Expected: 0 ошибок, все тесты зелёные.

- [ ] **Step 3: Ручная проверка эндпоинта (дев-окружение, если контейнеры подняты)**

Если featurestage-4 запущен и БД мигрирована — иначе шаг пропустить и отметить в отчёте:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/profile/inventory
```
Expected: 401 (без токена — эндпоинт защищён).

- [ ] **Step 4: Commit**

```bash
git add src
git commit -m "feat(api): inventory endpoints - items, build proofs, flags"
```

---

### Task 6: Фронтенд — секция «Инвентарь и сборки»

**Files:**
- Modify: `frontend/awake-web/src/api/client.ts` (добавить postForm и getBlob)
- Create: `frontend/awake-web/src/api/inventory.ts`
- Modify: `frontend/awake-web/src/types/api.ts` (типы инвентаря)
- Create: `frontend/awake-web/src/components/InventoryFlags.tsx`
- Create: `frontend/awake-web/src/components/InventorySection.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.profile.tsx` (секция под профилем)
- Modify: `frontend/awake-web/src/components/PlayerProfileView.tsx` (строка флагов в чужом профиле — через новый опциональный проп)
- Modify: `frontend/awake-web/src/routes/_auth.players.$userId.tsx` (передать проп)

**Interfaces:**
- Consumes: эндпоинты Task 5; `ItemCombobox({ categoryPrefix, excludeCategory?, placeholder, value: LoadoutSlot | null, onChange, required? })`; `LoadoutSlot { id, name, icon, color }` (см. `types/api.ts:80`).
- Produces: `<InventorySection />` (свой профиль), `<InventoryFlags userId={string} />` (чужой профиль).

- [ ] **Step 1: client.ts — form-data и blob**

Добавить в `frontend/awake-web/src/api/client.ts` после `request<T>` (использует те же BASE_URL/токен):

```typescript
async function requestForm<T>(path: string, form: FormData): Promise<T> {
  const token = useAuthStore.getState().accessToken
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`
  // Content-Type не ставим — браузер сам проставит multipart boundary

  const response = await fetch(`${BASE_URL}/api${path}`, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: form,
  })
  if (!response.ok) {
    let detail = `HTTP ${response.status}`
    try {
      const problem = (await response.json()) as { detail?: string }
      if (problem.detail) detail = problem.detail
    } catch {
      // ignore parse errors
    }
    throw new ApiError(response.status, detail)
  }
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function requestBlob(path: string): Promise<Blob> {
  const token = useAuthStore.getState().accessToken
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`
  const response = await fetch(`${BASE_URL}/api${path}`, {
    headers,
    credentials: 'include',
  })
  if (!response.ok) throw new ApiError(response.status, `HTTP ${response.status}`)
  return response.blob()
}
```

И расширить экспорт:

```typescript
export const apiClient = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
  postForm: <T>(path: string, form: FormData) => requestForm<T>(path, form),
  getBlob: (path: string) => requestBlob(path),
}
```

Примечание: `POST /api/profile/inventory/items` на пустой ответ `Ok()` вернёт пустое тело — обычный `request` упадёт на `response.json()`. Проверить: `Ok()` без аргументов отдаёт 200 с пустым телом → в `request<T>` перед `response.json()` добавить такую же text-проверку, как в requestForm:

```typescript
  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
```
(заменить существующий `return response.json() as Promise<T>`).

- [ ] **Step 2: Типы и api-клиент инвентаря**

В `types/api.ts` добавить:

```typescript
export enum BuildType {
  Speed = 0,
  Vitality = 1,
}

export interface PlayerFlags {
  bio: boolean
  combat: boolean
  sniper: boolean
  speed: boolean
  vitality: boolean
}

export interface InventoryItem {
  itemId: string
  name: string
  icon: string | null
  color: string | null
  category: string | null
  unknown: boolean
}

export interface PlayerInventory {
  items: InventoryItem[]
  flags: PlayerFlags
}
```

```typescript
// frontend/awake-web/src/api/inventory.ts
import { apiClient } from './client'
import type { BuildType, PlayerInventory } from '@/types/api'

export const inventoryApi = {
  getMy: (): Promise<PlayerInventory> => apiClient.get('/profile/inventory'),
  getFor: (userId: string): Promise<PlayerInventory> =>
    apiClient.get(`/players/${userId}/inventory`),
  addItem: (itemId: string): Promise<void> =>
    apiClient.post('/profile/inventory/items', { itemId }),
  removeItem: (itemId: string): Promise<void> =>
    apiClient.delete(`/profile/inventory/items/${itemId}`),
  uploadProof: (type: BuildType, file: File): Promise<void> => {
    const form = new FormData()
    form.set('type', String(type))
    form.set('file', file)
    return apiClient.postForm('/profile/build-proof', form)
  },
  deleteMyProof: (type: BuildType): Promise<void> =>
    apiClient.delete(`/profile/build-proof/${type}`),
  deleteProofFor: (userId: string, type: BuildType): Promise<void> =>
    apiClient.delete(`/players/${userId}/build-proof/${type}`),
  proofImageBlob: (userId: string, type: BuildType): Promise<Blob> =>
    apiClient.getBlob(`/players/${userId}/build-proof/${type}/image`),
}
```

- [ ] **Step 3: InventoryFlags (переиспользуется в чужом профиле и позже в билдере)**

```tsx
// frontend/awake-web/src/components/InventoryFlags.tsx
import { Biohazard, Crosshair, Footprints, Heart, Shield } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { PlayerFlags } from '@/types/api'

const FLAG_DEFS = [
  { key: 'bio', icon: Biohazard, label: 'Био (комбинированная броня)' },
  { key: 'combat', icon: Shield, label: 'Боевая броня' },
  { key: 'sniper', icon: Crosshair, label: 'Снайперка' },
  { key: 'speed', icon: Footprints, label: 'Сборка на скорость' },
  { key: 'vitality', icon: Heart, label: 'Сборка на живучесть' },
] as const

export function InventoryFlags({
  flags,
  size = 'md',
}: {
  flags: PlayerFlags
  size?: 'sm' | 'md'
}) {
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {FLAG_DEFS.map(({ key, icon: Icon, label }) => {
        const active = flags[key]
        return (
          <span
            key={key}
            title={label + (active ? '' : ' — нет')}
            className={cn(
              'inline-flex items-center justify-center rounded-md border',
              size === 'md' ? 'h-8 w-8' : 'h-6 w-6',
              active
                ? 'border-accent/30 bg-accent/10 text-accent'
                : 'border-border bg-secondary/50 text-muted-foreground/40',
            )}
          >
            <Icon size={size === 'md' ? 16 : 12} />
          </span>
        )
      })}
    </div>
  )
}
```

- [ ] **Step 4: InventorySection (свой профиль)**

```tsx
// frontend/awake-web/src/components/InventorySection.tsx
import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, Upload } from 'lucide-react'
import { inventoryApi } from '@/api/inventory'
import { ItemCombobox } from '@/components/ItemCombobox'
import { InventoryFlags } from '@/components/InventoryFlags'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuthStore } from '@/store/authStore'
import { BuildType } from '@/types/api'
import type { LoadoutSlot } from '@/types/api'
import { cn } from '@/lib/utils'

const PROOF_SLOTS = [
  { type: BuildType.Speed, title: 'Сборка на скорость', flagKey: 'speed' as const },
  { type: BuildType.Vitality, title: 'Сборка на живучесть', flagKey: 'vitality' as const },
]

export function InventorySection() {
  const queryClient = useQueryClient()
  const userId = useAuthStore((s) => s.user?.userId)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['inventory', 'my'],
    queryFn: inventoryApi.getMy,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['inventory', 'my'] })
  const onError = (e: Error) => setError(e.message)

  const addItem = useMutation({
    mutationFn: (itemId: string) => inventoryApi.addItem(itemId),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const removeItem = useMutation({
    mutationFn: (itemId: string) => inventoryApi.removeItem(itemId),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const uploadProof = useMutation({
    mutationFn: ({ type, file }: { type: BuildType; file: File }) =>
      inventoryApi.uploadProof(type, file),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })
  const deleteProof = useMutation({
    mutationFn: (type: BuildType) => inventoryApi.deleteMyProof(type),
    onSuccess: () => { setError(null); void invalidate() },
    onError,
  })

  if (isLoading || !data) {
    return <Skeleton className="mt-6 h-64 w-full rounded-xl" />
  }

  return (
    <Card className="mt-6">
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="text-sm font-medium">Инвентарь и сборки</CardTitle>
            <CardDescription>
              Отметь, что у тебя есть — офицеры видят эти флаги при сборе отрядов
            </CardDescription>
          </div>
          <InventoryFlags flags={data.flags} />
        </div>
      </CardHeader>
      <CardContent className="space-y-5 pt-2">
        {error && <p className="text-sm text-destructive">{error}</p>}

        {/* Добавление предмета: броня или оружие */}
        <div className="grid gap-3 sm:grid-cols-2">
          <ItemCombobox
            categoryPrefix="armor/"
            placeholder="Добавить броню…"
            value={null}
            onChange={(item: LoadoutSlot | null) => item && addItem.mutate(item.id)}
          />
          <ItemCombobox
            categoryPrefix="weapon/"
            placeholder="Добавить оружие…"
            value={null}
            onChange={(item: LoadoutSlot | null) => item && addItem.mutate(item.id)}
          />
        </div>

        {/* Список предметов */}
        {data.items.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Инвентарь пуст — добавь свою броню и оружие.
          </p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {data.items.map((item) => (
              <li
                key={item.itemId}
                className={cn(
                  'inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/50 py-1 pl-2 pr-1 text-sm',
                  item.unknown && 'opacity-60',
                )}
              >
                {item.icon && (
                  <img src={item.icon} alt="" className="h-5 w-5 object-contain" />
                )}
                <span className="max-w-[180px] truncate">{item.name}</span>
                <button
                  type="button"
                  aria-label={`Убрать ${item.name}`}
                  onClick={() => removeItem.mutate(item.itemId)}
                  className="rounded p-1 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                >
                  <Trash2 size={13} />
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* Пруфы сборок */}
        <div className="grid gap-3 sm:grid-cols-2">
          {PROOF_SLOTS.map((slot) => (
            <ProofSlot
              key={slot.type}
              title={slot.title}
              uploaded={data.flags[slot.flagKey]}
              uploading={uploadProof.isPending}
              userId={userId}
              type={slot.type}
              onUpload={(file) => uploadProof.mutate({ type: slot.type, file })}
              onDelete={() => deleteProof.mutate(slot.type)}
            />
          ))}
        </div>
      </CardContent>
    </Card>
  )
}

function ProofSlot({
  title,
  uploaded,
  uploading,
  userId,
  type,
  onUpload,
  onDelete,
}: {
  title: string
  uploaded: boolean
  uploading: boolean
  userId: string | undefined
  type: BuildType
  onUpload: (file: File) => void
  onDelete: () => void
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [preview, setPreview] = useState<string | null>(null)

  async function showProof() {
    if (!userId) return
    const blob = await inventoryApi.proofImageBlob(userId, type)
    setPreview(URL.createObjectURL(blob))
  }

  return (
    <div className="rounded-lg border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm font-medium">{title}</p>
        {uploaded ? (
          <span className="text-xs text-accent">пруф загружен</span>
        ) : (
          <span className="text-xs text-muted-foreground">нет пруфа</span>
        )}
      </div>
      <div className="mt-2 flex flex-wrap gap-2">
        <input
          ref={inputRef}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) onUpload(file)
            e.target.value = ''
          }}
        />
        <Button
          variant="outline"
          size="sm"
          className="gap-2"
          disabled={uploading}
          onClick={() => inputRef.current?.click()}
        >
          <Upload size={14} />
          {uploaded ? 'Заменить скрин' : 'Загрузить скрин'}
        </Button>
        {uploaded && (
          <>
            <Button variant="outline" size="sm" onClick={() => void showProof()}>
              Посмотреть
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={onDelete}
            >
              Удалить
            </Button>
          </>
        )}
      </div>
      {preview && (
        <button
          type="button"
          className="mt-3 block w-full"
          onClick={() => setPreview(null)}
          aria-label="Закрыть превью"
        >
          <img src={preview} alt={title} className="max-h-64 w-full rounded-md object-contain" />
        </button>
      )}
    </div>
  )
}
```

- [ ] **Step 5: Встроить в профили**

`_auth.profile.tsx` — после `<PlayerProfileView …/>` (внутри существующего фрагмента/обёртки):

```tsx
      <InventorySection />
```
с импортом `import { InventorySection } from '@/components/InventorySection'`. Если PlayerProfileView сейчас возвращается без обёртки — обернуть в `<>…</>`.

Чужой профиль: в `PlayerProfileView.tsx` добавить опциональный проп `flagsSlot?: React.ReactNode` и отрендерить его в шапке (рядом с ником/рангом, где визуально уместно по текущей разметке). В `_auth.players.$userId.tsx`:

```tsx
import { useQuery } from '@tanstack/react-query'
import { inventoryApi } from '@/api/inventory'
import { InventoryFlags } from '@/components/InventoryFlags'
```
```tsx
  const { data: inventory } = useQuery({
    queryKey: ['inventory', userId],
    queryFn: () => inventoryApi.getFor(userId),
    retry: false,
  })
```
```tsx
  return (
    <PlayerProfileView
      profile={profile}
      flagsSlot={inventory ? <InventoryFlags flags={inventory.flags} size="sm" /> : null}
    />
  )
```

- [ ] **Step 6: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```
Expected: 0 ошибок (предупреждения signalr/chunk-size — предсуществующие).

```bash
git add frontend/awake-web
git commit -m "feat(web): inventory section in profile - items, build proofs, flags"
```

---

### Task 7: Финальная проверка этапа

**Files:** нет изменений кода (кроме находок — по согласованию с контроллером).

- [ ] **Step 1: Полный прогон**

```bash
dotnet build --nologo -v q && dotnet test --nologo -v q
cd frontend/awake-web && npx tsc -b && npm run build
```
Expected: 0 ошибок; все unit-тесты зелёные (75 старых + ~20 новых).

- [ ] **Step 2: Дев-стенд**

Поднять/пересобрать featurestage-4 (`docker-compose -p featurestage-4 up -d --build api` из worktree), убедиться, что миграция применилась (см. логи api или `\dt` в psql: таблицы PlayerInventoryItems/PlayerBuildProofs). Прогнать smoke: 401 на `/api/profile/inventory` без токена.

- [ ] **Step 3: Отчёт для живой приёмки**

Перечислить, что проверяет пользователь вживую: добавление/удаление брони и оружия через поиск, авто-флаги (комбинированная мастерка → био; боевая → щиток; снайперка → прицел), загрузка/замена/удаление скринов сборок (флаги скорость/живучесть), превью скрина, чужой профиль — строка флагов, гость не видит чужой инвентарь.
