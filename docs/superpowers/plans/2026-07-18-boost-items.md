# Бусты из базы SC (предметы вместо типов) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Игрок отмечает в профиле конкретные предметы-усиления из базы STALCRAFT (по одному на каждый из 4 типов), тип предмета определяется автоматически из его JSON.

**Architecture:** Синк предметов дополнительно тянет supply-предметы и их `effect_type`; кэш хранит `BoostType?` у предмета; `PlayerBoostRequest` получает `ItemId`; все read-поверхности отдают обогащённые карточки `{boostType, itemId, name, icon}`. Спека: `docs/superpowers/specs/2026-07-18-boost-items-design.md`.

**Tech Stack:** ASP.NET Core 8 (Clean Architecture, MediatR, FluentValidation, EF Core/PostgreSQL, xUnit+Moq+FluentAssertions), React 19 + TanStack Router/Query + Tailwind + react-i18next.

## Global Constraints

- Ветка `feature/boost-items` от master 35d0edc (уже создана).
- RU-строки только через i18n — добавлять ключи в `ru.json` И `en.json`.
- TS: `enum` запрещён (erasableSyntaxOnly) — const-объект + union; `noUnusedLocals`.
- Enum на проводе — числа (нет JsonStringEnumConverter).
- Свои эндпоинты `api/profile/*` — `[Authorize]` без ранг-гейта; чужое/сводка — `[RankAuthorize(UserRank.Member)]`.
- Мутации: `NoContent()` / `Problem(detail: result.Error, statusCode: 400)`.
- Маппинг effect_type → BoostType: `long_time_medicine`→Damage(0), `short_time_medicine`→ShortDamage(1), `mobility`→Speed(2), `protection`→Defense(3), прочее/нет → null (не буст).
- Фильтр ранга: только `RANK_VETERAN`, `RANK_MASTER`, `RANK_LEGEND`.
- Коммиты с трейлером `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- `routeTree.gen.ts` регенерится кратким запуском `npm run dev` (НЕ `npx tsr generate`).

---

### Task 1: BoostEffectParser + ItemDto.BoostType + ItemRanks (Application)

**Files:**
- Create: `src/Awake.Application/Features/Items/BoostEffectParser.cs`
- Create: `src/Awake.Application/Features/Items/ItemRanks.cs`
- Modify: `src/Awake.Application/Features/Items/Dtos/ItemDto.cs`
- Test: `tests/Awake.Unit.Tests/Features/Items/BoostEffectParserTests.cs`

**Interfaces:**
- Produces: `ItemDto(string Id, string Category, string NameRu, string Icon, string Color, BoostType? BoostType = null)`; `BoostEffectParser.ExtractEffectType(JsonElement root): string?`; `BoostEffectParser.MapToBoostType(string? effectType): BoostType?`; `ItemRanks.VeteranPlus: IReadOnlySet<string>`.

- [ ] **Step 1: Написать падающие тесты**

```csharp
using System.Text.Json;
using Awake.Application.Features.Items;
using Awake.Domain.Enums;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Items;

public class BoostEffectParserTests
{
    private const string ItemJsonWithEffect = """
    {
      "infoBlocks": [
        { "type": "list", "elements": [
          { "type": "key-value",
            "key": { "type": "translation", "key": "stalker.tooltip.medicine.info.effect_type", "lines": { "ru": "Назначение" } },
            "value": { "type": "translation", "key": "item.effects.effect_type.long_time_medicine", "lines": { "ru": "Усиление" } } }
        ] }
      ]
    }
    """;

    [Fact]
    public void ExtractEffectType_FindsNestedKeyValueBlock()
    {
        using var doc = JsonDocument.Parse(ItemJsonWithEffect);
        BoostEffectParser.ExtractEffectType(doc.RootElement).Should().Be("long_time_medicine");
    }

    [Fact]
    public void ExtractEffectType_NoBlock_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{ "infoBlocks": [] }""");
        BoostEffectParser.ExtractEffectType(doc.RootElement).Should().BeNull();
    }

    [Theory]
    [InlineData("long_time_medicine", BoostType.Damage)]
    [InlineData("short_time_medicine", BoostType.ShortDamage)]
    [InlineData("mobility", BoostType.Speed)]
    [InlineData("protection", BoostType.Defense)]
    public void MapToBoostType_KnownTypes(string effectType, BoostType expected) =>
        BoostEffectParser.MapToBoostType(effectType).Should().Be(expected);

    [Theory]
    [InlineData("healing")]
    [InlineData("accumulation")]
    [InlineData(null)]
    public void MapToBoostType_NonBoost_ReturnsNull(string? effectType) =>
        BoostEffectParser.MapToBoostType(effectType).Should().BeNull();
}
```

- [ ] **Step 2: Запустить — убедиться, что падают**

Run: `dotnet test --filter BoostEffectParserTests`
Expected: FAIL (BoostEffectParser не существует).

- [ ] **Step 3: Реализация**

`src/Awake.Application/Features/Items/ItemRanks.cs`:

```csharp
namespace Awake.Application.Features.Items;

/// <summary>Цвета рангов «Ветеран и выше» — единый фильтр для поиска и валидации.</summary>
public static class ItemRanks
{
    public static readonly IReadOnlySet<string> VeteranPlus =
        new HashSet<string> { "RANK_VETERAN", "RANK_MASTER", "RANK_LEGEND" };
}
```

`src/Awake.Application/Features/Items/BoostEffectParser.cs`:

```csharp
using System.Text.Json;
using Awake.Domain.Enums;

namespace Awake.Application.Features.Items;

/// <summary>
/// Вычитывает «Назначение» (effect_type) из JSON предмета stalzone-database
/// и маппит его на тип буста. Структура блока — key-value с ключом
/// "*.effect_type" и значением "item.effects.effect_type.<тип>".
/// </summary>
public static class BoostEffectParser
{
    public static string? ExtractEffectType(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("type", out var type) && type.ValueEqual("key-value")
                && root.TryGetProperty("key", out var key)
                && key.TryGetProperty("key", out var keyKey)
                && (keyKey.GetString()?.EndsWith(".effect_type") ?? false)
                && root.TryGetProperty("value", out var value)
                && value.TryGetProperty("key", out var valueKey))
            {
                var full = valueKey.GetString();
                return full?[(full.LastIndexOf('.') + 1)..];
            }
            foreach (var prop in root.EnumerateObject())
            {
                var found = ExtractEffectType(prop.Value);
                if (found is not null) return found;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                var found = ExtractEffectType(el);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static bool ValueEqual(this JsonElement el, string expected) =>
        el.ValueKind == JsonValueKind.String && el.GetString() == expected;

    public static BoostType? MapToBoostType(string? effectType) => effectType switch
    {
        "long_time_medicine" => BoostType.Damage,
        "short_time_medicine" => BoostType.ShortDamage,
        "mobility" => BoostType.Speed,
        "protection" => BoostType.Defense,
        _ => null,
    };
}
```

`ItemDto.cs` — добавить необязательный параметр (существующие 5-аргументные вызовы не ломаются):

```csharp
using Awake.Domain.Enums;

namespace Awake.Application.Features.Items.Dtos;

public record ItemDto(string Id, string Category, string NameRu, string Icon, string Color, BoostType? BoostType = null);
```

- [ ] **Step 4: Тесты зелёные**

Run: `dotnet test --filter BoostEffectParserTests`
Expected: 9/9 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Awake.Application/Features/Items tests/Awake.Unit.Tests/Features/Items
git commit -m "feat(items): boost effect parser, ItemRanks, ItemDto.BoostType"
```

---

### Task 2: Кэш SearchBoosts + синк supply-предметов (Infrastructure)

**Files:**
- Modify: `src/Awake.Application/Common/Interfaces/IItemCacheService.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/Items/ItemCacheService.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/Items/ItemSyncHostedService.cs`
- Test: `tests/Awake.Unit.Tests/Infrastructure/ItemCacheServiceTests.cs` (создать)

**Interfaces:**
- Consumes: `BoostEffectParser`, `ItemRanks.VeteranPlus`, `ItemDto.BoostType` из Task 1.
- Produces: `IItemCacheService.SearchBoosts(string q, BoostType boostType, int limit = 40): IEnumerable<ItemDto>`.

- [ ] **Step 1: Падающий тест на SearchBoosts**

```csharp
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Enums;
using Awake.Infrastructure.ExternalServices.Items;
using FluentAssertions;

namespace Awake.Unit.Tests.Infrastructure;

public class ItemCacheServiceTests
{
    private static ItemCacheService Loaded()
    {
        var cache = new ItemCacheService();
        cache.Load([
            new ItemDto("ozverin", "supply/medicine", "«Озверин»", "i1.png", "RANK_VETERAN", BoostType.ShortDamage),
            new ItemDto("olivie", "supply/food", "Салат оливье", "i2.png", "RANK_VETERAN", BoostType.Damage),
            new ItemDto("newbie-soup", "supply/food", "Суп новичка", "i3.png", "RANK_NEWBIE", BoostType.Damage),
            new ItemDto("topot", "supply/medicine", "«ТОПОТ»", "i4.png", "RANK_MASTER"), // healing → без типа
            new ItemDto("skif5", "armor/combined", "Скиф-5", "i5.png", "RANK_MASTER"),
        ]);
        return cache;
    }

    [Fact]
    public void SearchBoosts_FiltersByTypeAndRank()
    {
        var result = Loaded().SearchBoosts("", BoostType.Damage).ToList();
        result.Should().ContainSingle(x => x.Id == "olivie"); // newbie-soup отсечён рангом
    }

    [Fact]
    public void SearchBoosts_EmptyQuery_ReturnsAllOfType()
    {
        Loaded().SearchBoosts("", BoostType.ShortDamage).Should().ContainSingle(x => x.Id == "ozverin");
    }

    [Fact]
    public void SearchBoosts_QueryFiltersByName()
    {
        Loaded().SearchBoosts("оливье", BoostType.Damage).Should().ContainSingle(x => x.Id == "olivie");
        Loaded().SearchBoosts("зюзюблик", BoostType.Damage).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Запустить — FAIL** (`dotnet test --filter ItemCacheServiceTests`)

- [ ] **Step 3: Реализация**

`IItemCacheService.cs` — добавить метод:

```csharp
IEnumerable<ItemDto> SearchBoosts(string q, BoostType boostType, int limit = 40);
```

(и `using Awake.Domain.Enums;`)

`ItemCacheService.cs` — заменить приватный `AllowedColors` на `ItemRanks.VeteranPlus` (удалить поле, обновить `Search`) и добавить:

```csharp
public IEnumerable<ItemDto> SearchBoosts(string q, BoostType boostType, int limit = 40) =>
    _items.Values
        .Where(x => x.BoostType == boostType)
        .Where(x => ItemRanks.VeteranPlus.Contains(x.Color))
        .Where(x => string.IsNullOrEmpty(q) || x.NameRu.Contains(q, StringComparison.OrdinalIgnoreCase))
        .OrderBy(x => x.NameRu, StringComparer.OrdinalIgnoreCase)
        .Take(limit);
```

`ItemSyncHostedService.SyncAsync` — расширить выборку и обогащение (заменить существующий конвейер `items`):

```csharp
var relevant = entries
    .Where(e => !string.IsNullOrEmpty(e.Data) &&
                (e.Data.StartsWith("/items/weapon/") ||
                 e.Data.StartsWith("/items/armor/") ||
                 e.Data.StartsWith("/items/supply/")))
    .ToList();

// Для supply-предметов вычитываем effect_type из JSON самого предмета.
// Ошибка одного предмета не валит синк — предмет остаётся без типа буста.
var boostTypes = new Dictionary<string, BoostType?>();
var semaphore = new SemaphoreSlim(8);
await Task.WhenAll(relevant
    .Where(e => e.Data.StartsWith("/items/supply/"))
    .Select(async e =>
    {
        await semaphore.WaitAsync();
        try
        {
            var itemJson = await client.GetStringAsync(IconBase + e.Data);
            using var doc = JsonDocument.Parse(itemJson);
            var effect = BoostEffectParser.ExtractEffectType(doc.RootElement);
            lock (boostTypes) boostTypes[e.Data] = BoostEffectParser.MapToBoostType(effect);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch effect_type for {Item}", e.Data);
        }
        finally
        {
            semaphore.Release();
        }
    }));

var items = relevant
    .Select(e =>
    {
        var parts = e.Data.Split('/');
        var id = parts[^1].Replace(".json", "");
        var category = string.Join("/", parts[2..^1]);
        var nameRu = e.Name?.Lines?.GetValueOrDefault("ru") ?? id;
        var icon = IconBase + e.Icon;
        var boost = boostTypes.GetValueOrDefault(e.Data);
        return new ItemDto(id, category, nameRu, icon, e.Color ?? "", boost);
    })
    .Where(x => !string.IsNullOrEmpty(x.NameRu))
    .ToList();
```

(добавить `using Awake.Application.Features.Items;` и `using Awake.Domain.Enums;`)

- [ ] **Step 4: Тесты зелёные + полный прогон** (`dotnet test` — все существующие тоже PASS)

- [ ] **Step 5: Commit** — `feat(items): supply items with boost effect type in cache + SearchBoosts`

---

### Task 3: БД — PlayerBoostRequest.ItemId, миграция, репозиторий

**Files:**
- Modify: `src/Awake.Domain/Entities/PlayerBoostRequest.cs`
- Modify: `src/Awake.Infrastructure/Persistence/AppDbContext.cs` (конфиг PlayerBoostRequest)
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerBoostRequestRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/PlayerBoostRequestRepository.cs`
- Create: миграция `AddItemIdToPlayerBoostRequests`

**Interfaces:**
- Produces: `PlayerBoostRequest.ItemId: string`; `IPlayerBoostRequestRepository.GetByUserIdAsync → IReadOnlyList<PlayerBoostRequest>` (были только типы!); `ReplaceForUserAsync(Guid userId, IReadOnlyList<PlayerBoostRequest> requests, CancellationToken ct)`.

- [ ] **Step 1: Entity + конфиг**

`PlayerBoostRequest.cs` — добавить `public string ItemId { get; set; } = "";`

`AppDbContext.cs` — в блок `builder.Entity<PlayerBoostRequest>` добавить первой строкой:

```csharp
e.Property(x => x.ItemId).HasMaxLength(64);
```

- [ ] **Step 2: Репозиторий**

`IPlayerBoostRequestRepository.cs`:

```csharp
public interface IPlayerBoostRequestRepository
{
    Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    /// <summary>С Include(User) — для сводки, чтобы не ходить за никами вторым запросом.</summary>
    Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Полная замена набора пользователя: remove старых + add новых одним SaveChangesAsync.</summary>
    Task ReplaceForUserAsync(Guid userId, IReadOnlyList<PlayerBoostRequest> requests, CancellationToken ct = default);
}
```

(`using Awake.Domain.Enums;` больше не нужен — удалить.)

`PlayerBoostRequestRepository.cs` — `GetByUserIdAsync` теперь отдаёт сущности, `ReplaceForUserAsync` принимает готовые записи:

```csharp
public async Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdAsync(
    Guid userId, CancellationToken ct = default) =>
    await context.PlayerBoostRequests
        .AsNoTracking()
        .Where(x => x.UserId == userId)
        .OrderBy(x => x.BoostType)
        .ToListAsync(ct);

public async Task ReplaceForUserAsync(
    Guid userId, IReadOnlyList<PlayerBoostRequest> requests, CancellationToken ct = default)
{
    var existing = await context.PlayerBoostRequests
        .Where(x => x.UserId == userId)
        .ToListAsync(ct);
    context.PlayerBoostRequests.RemoveRange(existing);
    context.PlayerBoostRequests.AddRange(requests);
    await context.SaveChangesAsync(ct); // одна транзакция — атомарная замена
}
```

(остальные методы без изменений; НЕ компилируется до Task 4 — хендлеры зовут старые сигнатуры. Это ожидаемо: Task 3 коммитится вместе с Task 4? НЕТ — см. Step 3: правки хендлеров минимальны и входят сюда.)

- [ ] **Step 3: Временная совместимость хендлеров (минимальные правки, чтобы Task 3 компилировался)**

`GetMyBoostsQueryHandler.cs` — вернуть типы из сущностей:

```csharp
public async Task<IReadOnlyList<BoostType>> Handle(
    GetMyBoostsQuery request, CancellationToken cancellationToken) =>
    (await boostRepository.GetByUserIdAsync(request.UserId, cancellationToken))
        .Select(r => r.BoostType).ToList();
```

`SetMyBoostsCommandHandler.cs` — собрать сущности (ItemId пока пустой, Task 4 перепишет):

```csharp
var types = request.BoostTypes.Distinct().ToList();
await boostRepository.ReplaceForUserAsync(
    request.UserId,
    types.Select(t => new PlayerBoostRequest { UserId = request.UserId, BoostType = t }).ToList(),
    cancellationToken);
return Result<bool>.Success(true);
```

(добавить `using Awake.Domain.Entities;`)

В `tests/Awake.Unit.Tests/Features/Boosts/BoostsTests.cs` поправить моки под новые сигнатуры (компиляция; полноценные новые тесты — Task 4).

- [ ] **Step 4: Миграция**

Run (из корня worktree):

```bash
dotnet ef migrations add AddItemIdToPlayerBoostRequests --project src/Awake.Infrastructure --startup-project src/Awake.API
```

Затем в сгенерированной миграции в `Up()` ПЕРЕД `AddColumn` вставить очистку (старые записи без предмета бессмысленны; данные только тестовые):

```csharp
migrationBuilder.Sql("""DELETE FROM "PlayerBoostRequests";""");
```

Если dotnet-ef 10.0.3 добавит шум `ToTable` в снапшот — откатить шум точечно (известная особенность, см. прошлые миграции).

- [ ] **Step 5: Сборка + все тесты** (`dotnet build && dotnet test`) — PASS.

- [ ] **Step 6: Commit** — `feat(boosts): PlayerBoostRequest.ItemId + migration (wipe old rows)`

---

### Task 4: Application — BoostItemDto, SetMyBoosts с валидацией по кэшу, запросы

**Files:**
- Create: `src/Awake.Application/Features/Boosts/Dtos/BoostItemDto.cs`
- Create: `src/Awake.Application/Features/Boosts/BoostItemMapper.cs`
- Modify: `src/Awake.Application/Features/Boosts/Commands/SetMyBoosts/SetMyBoostsCommand.cs` (+Handler, +Validator)
- Modify: `src/Awake.Application/Features/Boosts/Queries/GetMyBoosts/GetMyBoostsQuery.cs` (+Handler)
- Modify: `src/Awake.Application/Features/Boosts/Queries/GetBoostsSummary/*` (DTO + Handler)
- Test: `tests/Awake.Unit.Tests/Features/Boosts/BoostsTests.cs` (переписать)

**Interfaces:**
- Consumes: репозиторий из Task 3, `IItemCacheService.GetById`, `ItemRanks.VeteranPlus`.
- Produces: `BoostItemDto(BoostType BoostType, string ItemId, string Name, string? Icon)`; `BoostSelectionDto(BoostType BoostType, string ItemId)`; `SetMyBoostsCommand(Guid UserId, IReadOnlyList<BoostSelectionDto> Selections)`; `GetMyBoostsQuery → IReadOnlyList<BoostItemDto>`; `BoostSummaryEntryDto(Guid UserId, string Username, string? GameNickname, IReadOnlyList<BoostItemDto> Boosts)`; `BoostItemMapper.ToDto(PlayerBoostRequest, IItemCacheService): BoostItemDto`.

- [ ] **Step 1: DTO и маппер**

`Dtos/BoostItemDto.cs`:

```csharp
using Awake.Domain.Enums;

namespace Awake.Application.Features.Boosts.Dtos;

public record BoostItemDto(BoostType BoostType, string ItemId, string Name, string? Icon);

public record BoostSelectionDto(BoostType BoostType, string ItemId);
```

`BoostItemMapper.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Boosts.Dtos;
using Awake.Domain.Entities;

namespace Awake.Application.Features.Boosts;

public static class BoostItemMapper
{
    /// <summary>Предмет мог исчезнуть из кэша после патча игры — тогда name = itemId, без иконки.</summary>
    public static BoostItemDto ToDto(PlayerBoostRequest request, IItemCacheService itemCache)
    {
        var item = itemCache.GetById(request.ItemId);
        return new BoostItemDto(request.BoostType, request.ItemId, item?.NameRu ?? request.ItemId, item?.Icon);
    }
}
```

- [ ] **Step 2: Падающие тесты (переписать BoostsTests.cs)**

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Dtos;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Application.Features.Boosts.Queries.GetMyBoosts;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Boosts;

public class BoostsTests
{
    private readonly Mock<IPlayerBoostRequestRepository> _repo = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ItemDto Ozverin => new("ozverin", "supply/medicine", "«Озверин»", "i.png", "RANK_VETERAN", BoostType.ShortDamage);

    // ── SetMyBoosts ──

    [Fact]
    public async Task SetMyBoosts_ValidSelection_Replaces()
    {
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin);
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.ShortDamage, "ozverin")]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(UserId,
            It.Is<IReadOnlyList<PlayerBoostRequest>>(l =>
                l.Count == 1 && l[0].ItemId == "ozverin" && l[0].BoostType == BoostType.ShortDamage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMyBoosts_UnknownItem_Fails()
    {
        _cache.Setup(c => c.GetById("ghost")).Returns((ItemDto?)null);
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "ghost")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _repo.Verify(r => r.ReplaceForUserAsync(It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<PlayerBoostRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetMyBoosts_TypeMismatch_Fails()
    {
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin); // ShortDamage
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Speed, "ozverin")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SetMyBoosts_LowRankItem_Fails()
    {
        _cache.Setup(c => c.GetById("soup")).Returns(
            new ItemDto("soup", "supply/food", "Суп", "i.png", "RANK_NEWBIE", BoostType.Damage));
        var handler = new SetMyBoostsCommandHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "soup")]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Validator_DuplicateType_Invalid()
    {
        var validator = new SetMyBoostsCommandValidator();
        var result = validator.Validate(new SetMyBoostsCommand(UserId, [
            new BoostSelectionDto(BoostType.Damage, "a"),
            new BoostSelectionDto(BoostType.Damage, "b"),
        ]));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_EmptyItemId_Invalid()
    {
        var validator = new SetMyBoostsCommandValidator();
        var result = validator.Validate(new SetMyBoostsCommand(UserId,
            [new BoostSelectionDto(BoostType.Damage, "")]));
        result.IsValid.Should().BeFalse();
    }

    // ── GetMyBoosts ──

    [Fact]
    public async Task GetMyBoosts_EnrichesFromCache_WithFallback()
    {
        _repo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
             .ReturnsAsync([
                 new PlayerBoostRequest { UserId = UserId, BoostType = BoostType.ShortDamage, ItemId = "ozverin" },
                 new PlayerBoostRequest { UserId = UserId, BoostType = BoostType.Speed, ItemId = "gone" },
             ]);
        _cache.Setup(c => c.GetById("ozverin")).Returns(Ozverin);
        _cache.Setup(c => c.GetById("gone")).Returns((ItemDto?)null);
        var handler = new GetMyBoostsQueryHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new GetMyBoostsQuery(UserId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("«Озверин»");
        result[1].Name.Should().Be("gone"); // исчез из кэша — фолбэк на itemId
        result[1].Icon.Should().BeNull();
    }

    // ── GetBoostsSummary ──

    [Fact]
    public async Task Summary_GroupsByUser_SortsByCountThenNickname()
    {
        var u1 = new User { Username = "b", GameNickname = "Bravo", Rank = UserRank.Member };
        var u2 = new User { Username = "a", GameNickname = "Alpha", Rank = UserRank.Member };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new PlayerBoostRequest { UserId = u1.Id, User = u1, BoostType = BoostType.Damage, ItemId = "x" },
            new PlayerBoostRequest { UserId = u1.Id, User = u1, BoostType = BoostType.Speed, ItemId = "y" },
            new PlayerBoostRequest { UserId = u2.Id, User = u2, BoostType = BoostType.Damage, ItemId = "x" },
        ]);
        _cache.Setup(c => c.GetById(It.IsAny<string>())).Returns((ItemDto?)null);
        var handler = new GetBoostsSummaryQueryHandler(_repo.Object, _cache.Object);

        var result = await handler.Handle(new GetBoostsSummaryQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(u1.Id); // 2 буста > 1
        result[0].Boosts.Select(b => b.BoostType).Should().Equal(BoostType.Damage, BoostType.Speed);
    }
}
```

- [ ] **Step 3: Запустить — FAIL** (`dotnet test --filter BoostsTests`)

- [ ] **Step 4: Реализация**

`SetMyBoostsCommand.cs`:

```csharp
using Awake.Application.Common.Models;
using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public record SetMyBoostsCommand(
    Guid UserId,
    IReadOnlyList<BoostSelectionDto> Selections) : IRequest<Result<bool>>;
```

`SetMyBoostsCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandValidator : AbstractValidator<SetMyBoostsCommand>
{
    public SetMyBoostsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("ID пользователя обязателен.");
        RuleFor(x => x.Selections).NotNull().WithMessage("Список бустов обязателен.");
        RuleFor(x => x.Selections)
            .Must(s => s == null || s.Select(x => x.BoostType).Distinct().Count() == s.Count)
            .WithMessage("Не больше одного предмета на тип буста.");
        RuleForEach(x => x.Selections).ChildRules(sel =>
        {
            sel.RuleFor(s => s.BoostType).IsInEnum().WithMessage("Недопустимый тип буста.");
            sel.RuleFor(s => s.ItemId).NotEmpty().WithMessage("ID предмета обязателен.");
        });
    }
}
```

`SetMyBoostsCommandHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Items;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
) : IRequestHandler<SetMyBoostsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetMyBoostsCommand request, CancellationToken cancellationToken)
    {
        // Защита от кривых запросов в обход UI: предмет существует,
        // тип совпадает (null у не-бустов не совпадёт), ранг Ветеран+.
        foreach (var sel in request.Selections)
        {
            var item = itemCache.GetById(sel.ItemId);
            if (item is null)
                return Result<bool>.Failure($"Предмет не найден: {sel.ItemId}");
            if (item.BoostType != sel.BoostType)
                return Result<bool>.Failure($"Предмет не подходит для этого слота: {item.NameRu}");
            if (!ItemRanks.VeteranPlus.Contains(item.Color))
                return Result<bool>.Failure($"Ранг предмета ниже Ветерана: {item.NameRu}");
        }

        var requests = request.Selections
            .Select(s => new PlayerBoostRequest
            {
                UserId = request.UserId,
                BoostType = s.BoostType,
                ItemId = s.ItemId,
            })
            .ToList();
        await boostRepository.ReplaceForUserAsync(request.UserId, requests, cancellationToken);
        return Result<bool>.Success(true);
    }
}
```

`GetMyBoostsQuery.cs`:

```csharp
using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public record GetMyBoostsQuery(Guid UserId) : IRequest<IReadOnlyList<BoostItemDto>>;
```

`GetMyBoostsQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts.Dtos;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public class GetMyBoostsQueryHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
) : IRequestHandler<GetMyBoostsQuery, IReadOnlyList<BoostItemDto>>
{
    public async Task<IReadOnlyList<BoostItemDto>> Handle(
        GetMyBoostsQuery request, CancellationToken cancellationToken) =>
        (await boostRepository.GetByUserIdAsync(request.UserId, cancellationToken))
            .Select(r => BoostItemMapper.ToDto(r, itemCache))
            .ToList();
}
```

`GetBoostsSummary` DTO + Handler:

```csharp
using Awake.Application.Features.Boosts.Dtos;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record BoostSummaryEntryDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    IReadOnlyList<BoostItemDto> Boosts);
```

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public class GetBoostsSummaryQueryHandler(
    IPlayerBoostRequestRepository boostRepository,
    IItemCacheService itemCache
) : IRequestHandler<GetBoostsSummaryQuery, IReadOnlyList<BoostSummaryEntryDto>>
{
    public async Task<IReadOnlyList<BoostSummaryEntryDto>> Handle(
        GetBoostsSummaryQuery request, CancellationToken cancellationToken)
    {
        var all = await boostRepository.GetAllAsync(cancellationToken);
        return all
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                return new BoostSummaryEntryDto(
                    g.Key,
                    user.Username,
                    user.GameNickname,
                    g.OrderBy(r => r.BoostType)
                     .Select(r => BoostItemMapper.ToDto(r, itemCache))
                     .ToList());
            })
            .OrderByDescending(e => e.Boosts.Count)
            .ThenBy(e => e.GameNickname ?? e.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
```

ВНИМАНИЕ: `BoostsController` перестанет компилироваться (старый `SetBoostsRequest`) — поправить его здесь же минимально НЕЛЬЗЯ откладывать: см. Task 5, но чтобы Task 4 компилировался, обнови контроллер сразу по коду из Task 5 Step 1 (это одна правка; ревьюер Task 4 увидит контроллер в диффе — это ок, отметь в отчёте).

- [ ] **Step 5: Тесты зелёные** (`dotnet test --filter BoostsTests`, затем полный `dotnet test` — упадут только тесты энричера? НЕТ: энричер ещё на старом коде и компилируется — `GetByUserIdsAsync` не менялся. Полный прогон PASS.)

- [ ] **Step 6: Commit** — `feat(boosts): item-based selections with cache validation + enriched cards`

---

### Task 5: Контроллеры — BoostsController + ItemsController.boostType

**Files:**
- Modify: `src/Awake.API/Controllers/BoostsController.cs`
- Modify: `src/Awake.API/Controllers/ItemsController.cs`

**Interfaces:**
- Consumes: `SetMyBoostsCommand(UserId, Selections)`, `BoostSelectionDto`, `IItemCacheService.SearchBoosts`.
- Produces: wire-контракты `PUT api/profile/boosts` тело `{ "selections": [{ "boostType": 1, "itemId": "ozverin" }] }`; `GET api/items/search?boostType=2&q=`.

- [ ] **Step 1: BoostsController** — заменить record и вызов:

```csharp
public record SetBoostsRequest(IReadOnlyList<BoostSelectionDto> Selections);
```

```csharp
var result = await sender.Send(
    new SetMyBoostsCommand(currentUser.UserId, request.Selections), ct);
```

(`using Awake.Application.Features.Boosts.Dtos;`; using `Awake.Domain.Enums` удалить, если не нужен)

- [ ] **Step 2: ItemsController** — параметр `boostType` снимает минимум «2 символа»:

```csharp
[HttpGet("search")]
public IActionResult Search(
    [FromQuery] string q = "",
    [FromQuery] string? category = null,
    [FromQuery] string? exclude = null,
    [FromQuery] BoostType? boostType = null)
{
    if (boostType is not null)
        return Ok(cache.SearchBoosts(q, boostType.Value));

    if (q.Length < 2 && string.IsNullOrEmpty(category))
        return Ok(Array.Empty<object>());

    var results = cache.Search(q, category, exclude);
    return Ok(results);
}
```

(`using Awake.Domain.Enums;`)

- [ ] **Step 3: Сборка + полный прогон** (`dotnet build && dotnet test`) — PASS. Быстрая ручная проверка не требуется (smoke — Task 9).

- [ ] **Step 4: Commit** — `feat(boosts): controllers accept item selections; items search by boost type`

---

### Task 6: Энричер и DTO отрядов/профиля → карточки предметов

**Files:**
- Modify: `src/Awake.Application/Features/Squads/SquadMemberEnricher.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquads/SquadDto.cs` (SquadMemberDto)
- Modify: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/PlayerProfileDto.cs` + его хендлер
- Modify: call-sites энричера (GetSquads/GetSquadById/GetSquadBuilder — только тип tuple)
- Test: `tests/Awake.Unit.Tests/Features/Squads/SquadMemberEnricherTests.cs` (+ обновить тесты отрядов/профиля, где замокан `_boosts`)

**Interfaces:**
- Consumes: `BoostItemMapper.ToDto`, репозиторий из Task 3.
- Produces: `SquadMemberEnricher.ComputeAsync → IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostItemDto> Boosts)>`; `SquadMemberDto(..., IReadOnlyList<BoostItemDto> Boosts)` (вместо BoostTypes); `PlayerProfileDto(..., IReadOnlyList<BoostItemDto> Boosts)`.

- [ ] **Step 1: Обновить тест энричера** — `ComputeAsync_BoostsGroupedPerUser` теперь ждёт карточки (кэш-мок пустой → фолбэк имени):

```csharp
[Fact]
public async Task ComputeAsync_BoostsGroupedPerUser()
{
    var user = new User { Username = "u1", Rank = UserRank.Member };
    SetupEmpty();
    _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync([
               new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Defense, ItemId = "astr" },
               new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Damage, ItemId = "olivie" },
           ]);

    var result = await SquadMemberEnricher.ComputeAsync(
        [user], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

    result[user.Id].Boosts.Select(b => b.BoostType).Should().Equal(BoostType.Damage, BoostType.Defense); // отсортировано
    result[user.Id].Boosts[0].Name.Should().Be("olivie"); // кэш пуст — фолбэк на itemId
}
```

Добавить тест изоляции пользователей (follow-up прошлого ревью):

```csharp
[Fact]
public async Task ComputeAsync_BoostsIsolatedBetweenUsers()
{
    var u1 = new User { Username = "u1", Rank = UserRank.Member };
    var u2 = new User { Username = "u2", Rank = UserRank.Member };
    SetupEmpty();
    _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync([new PlayerBoostRequest { UserId = u1.Id, BoostType = BoostType.Speed, ItemId = "x" }]);

    var result = await SquadMemberEnricher.ComputeAsync(
        [u1, u2], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

    result[u1.Id].Boosts.Should().HaveCount(1);
    result[u2.Id].Boosts.Should().BeEmpty();
}
```

- [ ] **Step 2: Запустить — FAIL** (`dotnet test --filter SquadMemberEnricherTests`)

- [ ] **Step 3: Реализация**

Энричер — тип результата и блок бустов:

```csharp
public static async Task<IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostItemDto> Boosts)>> ComputeAsync(...)
```

```csharp
IReadOnlyList<BoostItemDto> userBoosts = boostsByUser[u.Id]
    .OrderBy(b => b.BoostType)
    .Select(b => BoostItemMapper.ToDto(b, itemCache))
    .ToList();
```

(`using Awake.Application.Features.Boosts;` и `.Dtos`)

`SquadMemberDto` — последний параметр: `IReadOnlyList<BoostItemDto> Boosts` (имя меняется с BoostTypes!). Call-sites: `enriched[m.UserId].Boosts` вместо `.BoostTypes`. `PlayerProfileDto` — тип поля `Boosts` на `IReadOnlyList<BoostItemDto>`; его хендлер маппит через `BoostItemMapper.ToDto` (репозиторий уже отдаёт сущности после Task 3).

- [ ] **Step 4: Полный прогон** (`dotnet test`) — обновить остальные тест-файлы отрядов/профиля, где замокан `_boosts` (пустые списки — сигнатура мока не изменилась, `GetByUserIdsAsync` тот же; правки только там, где тесты читают `.BoostTypes`).

- [ ] **Step 5: Commit** — `feat(boosts): enricher and squad/profile DTOs carry boost item cards`

---

### Task 7: Фронт — типы, API, слоты в профиле, чипы, попап

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts`
- Modify: `frontend/awake-web/src/api/boosts.ts`, `frontend/awake-web/src/api/items.ts`
- Create: `frontend/awake-web/src/components/boosts/BoostSlotPicker.tsx`
- Rewrite: `frontend/awake-web/src/components/boosts/BoostChips.tsx` → read-only чипы предметов
- Modify: `frontend/awake-web/src/components/boosts/BoostsSection.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.players.$userId.tsx`
- Modify: `frontend/awake-web/src/components/squads/MemberHoverInfo.tsx`, `frontend/awake-web/src/components/squads/SquadCard.tsx`
- Modify: `frontend/awake-web/src/i18n/ru.json`, `en.json`

**Interfaces:**
- Consumes: wire-контракты из Task 5.
- Produces: `BoostItem { boostType, itemId, name, icon }`; `BoostSelection { boostType, itemId }`; `boostsApi.setMy(selections)`; `itemsApi.searchBoosts(q, boostType)`; `<BoostChips items short? />` (read-only); `<BoostSlotPicker boostType value onSelect onClear disabled />`.

- [ ] **Step 1: Типы** (`types/api.ts`)

```ts
export interface BoostItem {
  boostType: BoostType
  itemId: string
  name: string
  icon: string | null
}

export interface BoostSelection {
  boostType: BoostType
  itemId: string
}
```

`SquadMemberDto`: `boostTypes: BoostType[]` → `boosts: BoostItem[]`. `PlayerProfileDto`: `boosts: BoostItem[]`. `BoostSummaryEntry`: `boostTypes` → `boosts: BoostItem[]`.

- [ ] **Step 2: API**

`api/boosts.ts`:

```ts
import { apiClient } from './client'
import type { BoostItem, BoostSelection, BoostSummaryEntry } from '@/types/api'

export const boostsApi = {
  getMy: (): Promise<BoostItem[]> => apiClient.get('/profile/boosts'),
  setMy: (selections: BoostSelection[]): Promise<void> =>
    apiClient.put('/profile/boosts', { selections }),
  summary: (): Promise<BoostSummaryEntry[]> => apiClient.get('/boosts/summary'),
}
```

`api/items.ts` — добавить:

```ts
searchBoosts: (q: string, boostType: BoostType): Promise<ItemSearchResult[]> => {
  const params = new URLSearchParams({ q, boostType: String(boostType) })
  return apiClient.get<ItemSearchResult[]>(`/items/search?${params.toString()}`)
},
```

(импорт `BoostType` из types)

- [ ] **Step 3: BoostChips → read-only чипы предметов** (полная замена файла)

```tsx
import { useTranslation } from 'react-i18next'
import type { BoostItem } from '@/types/api'

// Read-only чипы предметов-бустов: иконка + название, тултип — тип буста.
export function BoostChips({ items, short = false }: { items: BoostItem[]; short?: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-wrap gap-1.5">
      {items.map((b) => (
        <span
          key={b.boostType}
          title={t(`boosts.types.${b.boostType}`)}
          className="flex items-center gap-1.5 rounded-md border border-accent/30 bg-accent/10 px-2 py-1 text-xs font-medium text-accent"
        >
          {b.icon && (
            <img
              src={b.icon}
              alt=""
              className="h-4 w-4 shrink-0 object-contain"
              onError={(e) => (e.currentTarget.style.display = 'none')}
            />
          )}
          <span className={short ? 'max-w-24 truncate' : undefined}>{b.name}</span>
        </span>
      ))}
    </div>
  )
}
```

- [ ] **Step 4: BoostSlotPicker** (новый; паттерн ItemCombobox, но открывается по фокусу с пустым запросом)

```tsx
import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search, X } from 'lucide-react'
import { itemsApi } from '@/api/items'
import type { BoostItem, BoostType, ItemSearchResult } from '@/types/api'

export function BoostSlotPicker({
  boostType,
  value,
  onSelect,
  onClear,
  disabled = false,
}: {
  boostType: BoostType
  value: BoostItem | null
  onSelect: (item: ItemSearchResult) => void
  onClear: () => void
  disabled?: boolean
}) {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [] } = useQuery({
    queryKey: ['items', 'boosts', boostType, query],
    queryFn: () => itemsApi.searchBoosts(query, boostType),
    enabled: open, // вариантов 3–32 — показываем список сразу по фокусу
    staleTime: 60_000,
  })

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  if (value) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-border bg-secondary px-3 py-2">
        {value.icon && (
          <img
            src={value.icon}
            alt=""
            className="h-6 w-6 shrink-0 object-contain"
            onError={(e) => (e.currentTarget.style.display = 'none')}
          />
        )}
        <span className="flex-1 text-sm text-foreground">{value.name}</span>
        <button
          type="button"
          onClick={onClear}
          disabled={disabled}
          className="shrink-0 text-muted-foreground transition-colors hover:text-destructive disabled:pointer-events-none disabled:opacity-60"
        >
          <X size={14} />
        </button>
      </div>
    )
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="flex items-center gap-2 rounded-lg border border-border bg-background px-3 py-2 transition-colors focus-within:border-accent/50">
        <Search size={14} className="shrink-0 text-muted-foreground" />
        <input
          type="text"
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          placeholder={t('boosts.searchPlaceholder')}
          value={query}
          disabled={disabled}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
        />
      </div>

      {open && results.length > 0 && (
        <div className="absolute z-50 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-border bg-card shadow-lg">
          {results.map((item) => (
            <button
              key={item.id}
              type="button"
              className="flex w-full items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-secondary"
              onMouseDown={(e) => {
                e.preventDefault()
                onSelect(item)
                setQuery('')
                setOpen(false)
              }}
            >
              <img
                src={item.icon}
                alt=""
                className="h-7 w-7 shrink-0 object-contain"
                onError={(e) => (e.currentTarget.style.display = 'none')}
              />
              <span className="flex-1 text-sm text-foreground">{item.nameRu}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 5: BoostsSection — 4 слота** (полная замена внутренностей)

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostSlotPicker } from '@/components/boosts/BoostSlotPicker'
import { Card, CardContent } from '@/components/ui/card'
import { ALL_BOOST_TYPES, type BoostItem, type BoostType, type ItemSearchResult } from '@/types/api'

export function BoostsSection() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: selected = [] } = useQuery({
    queryKey: ['boosts', 'my'],
    queryFn: boostsApi.getMy,
  })

  const mutation = useMutation({
    mutationFn: (next: BoostItem[]) =>
      boostsApi.setMy(next.map((b) => ({ boostType: b.boostType, itemId: b.itemId }))),
    onMutate: async (next: BoostItem[]) => {
      await queryClient.cancelQueries({ queryKey: ['boosts', 'my'] })
      const prev = queryClient.getQueryData<BoostItem[]>(['boosts', 'my'])
      queryClient.setQueryData(['boosts', 'my'], next)
      return { prev }
    },
    onError: (_err, _next, ctx) => {
      queryClient.setQueryData(['boosts', 'my'], ctx?.prev ?? [])
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['boosts'] })
      void queryClient.invalidateQueries({ queryKey: ['squads'] })
      void queryClient.invalidateQueries({ queryKey: ['players'] })
    },
  })

  function setSlot(type: BoostType, item: ItemSearchResult | null) {
    const rest = selected.filter((b) => b.boostType !== type)
    const next = item
      ? [...rest, { boostType: type, itemId: item.id, name: item.nameRu, icon: item.icon }]
      : rest
    mutation.mutate(next)
  }

  return (
    <Card className="mt-6">
      <CardContent className="pt-5 pb-5">
        <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{t('boosts.myHint')}</p>
        <div className="mt-4 space-y-3">
          {ALL_BOOST_TYPES.map((type) => (
            <div key={type} className="grid grid-cols-[10rem_1fr] items-center gap-3">
              <span className="text-sm text-muted-foreground">{t(`boosts.types.${type}`)}</span>
              <BoostSlotPicker
                boostType={type}
                value={selected.find((b) => b.boostType === type) ?? null}
                onSelect={(item) => setSlot(type, item)}
                onClear={() => setSlot(type, null)}
                disabled={mutation.isPending}
              />
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
```

- [ ] **Step 6: Read-only места**

`_auth.players.$userId.tsx`: `<BoostChips selected={profile.boosts} />` → `<BoostChips items={profile.boosts} />` (условие `profile.boosts.length > 0` остаётся).

`MemberHoverInfo.tsx`: проп `boosts?: BoostType[]` → `boosts?: BoostItem[]` (импорт типа), вызов `<BoostChips selected={boosts} short />` → `<BoostChips items={boosts} short />`.

`SquadCard.tsx`: `boosts={leader.boostTypes}` → `boosts={leader.boosts}`, `boosts={m.boostTypes}` → `boosts={m.boosts}`.

- [ ] **Step 7: i18n** — в `ru.json` блок `boosts` добавить/заменить ключи:

```json
"searchPlaceholder": "Найти предмет…",
"totals": "Итого нужно"
```

`en.json`: `"searchPlaceholder": "Find an item…", "totals": "Total needed"`. Ключи `typesShort` больше не используются — удалить из обоих файлов (проверить grep'ом, что нет потребителей; страница /boosts переделывается в Task 8).

ВНИМАНИЕ: `_auth.boosts.tsx` использует `typesShort` и `boostTypes` — до Task 8 tsc будет падать на этом файле. Чтобы Task 7 собирался, в этом же коммите замените в `_auth.boosts.tsx` ссылки минимально: `entry.boostTypes` → `entry.boosts.map((b) => b.boostType)` не нужно — проще: Task 7 и Task 8 РАЗРЕШЕНО проверять совместной сборкой, но коммиты раздельные. Итог для исполнителя Task 7: выполните `npx tsc -b --noEmit` и убедитесь, что ЕДИНСТВЕННЫЕ ошибки — в `src/routes/_auth.boosts.tsx`; зафиксируйте это в отчёте. Полная зелёная сборка — критерий Task 8.

- [ ] **Step 8: Commit** — `feat(boosts): item slot pickers in profile, item chips in read-only surfaces`

---

### Task 8: Фронт — /boosts: «Итого нужно» + «По игрокам»

**Files:**
- Rewrite: `frontend/awake-web/src/routes/_auth.boosts.tsx`

**Interfaces:**
- Consumes: `boostsApi.summary → BoostSummaryEntry{ boosts: BoostItem[] }`, `BoostChips`, i18n `boosts.totals`.

- [ ] **Step 1: Полная замена страницы**

```tsx
import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { UserRank, type BoostItem } from '@/types/api'

export const Route = createFileRoute('/_auth/boosts')({
  component: BoostsPage,
})

function BoostsPage() {
  const { t } = useTranslation()
  const { rank } = useAuth()

  const { data: entries = [], isLoading, isError } = useQuery({
    queryKey: ['boosts', 'summary'],
    queryFn: boostsApi.summary,
    enabled: rank >= UserRank.Member,
  })

  if (rank < UserRank.Member) return <Navigate to="/profile" />
  if (isLoading) return <p className="text-muted-foreground">{t('common.loading')}</p>
  if (isError) return <p className="text-destructive">{t('boosts.title')}: {t('auth.errors.networkError')}</p>

  // Итого: сколько игроков отметили каждый предмет
  const totals = new Map<string, { item: BoostItem; count: number }>()
  for (const entry of entries) {
    for (const b of entry.boosts) {
      const existing = totals.get(b.itemId)
      if (existing) existing.count += 1
      else totals.set(b.itemId, { item: b, count: 1 })
    }
  }
  const totalRows = [...totals.values()].sort((a, b) => b.count - a.count)

  return (
    <div>
      <h1 className="mb-6 text-xl font-semibold text-foreground">{t('boosts.title')}</h1>

      {entries.length === 0 ? (
        <Card>
          <CardContent className="pt-5 pb-5 text-center">
            <p className="text-sm text-muted-foreground">{t('boosts.empty')}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t('boosts.emptyHint')}</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          <Card>
            <CardContent className="pt-5 pb-5">
              <h2 className="mb-3 text-sm font-semibold text-foreground">{t('boosts.totals')}</h2>
              <div className="space-y-2">
                {totalRows.map(({ item, count }) => (
                  <div key={item.itemId} className="flex items-center gap-3">
                    {item.icon && (
                      <img
                        src={item.icon}
                        alt=""
                        className="h-6 w-6 shrink-0 object-contain"
                        onError={(e) => (e.currentTarget.style.display = 'none')}
                      />
                    )}
                    <span className="flex-1 text-sm text-foreground">{item.name}</span>
                    <span className="text-xs text-muted-foreground">{t(`boosts.types.${item.boostType}`)}</span>
                    <span className="w-10 text-right text-sm font-semibold text-accent">× {count}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-5 pb-5">
              <h2 className="mb-3 text-sm font-semibold text-foreground">{t('boosts.player')}</h2>
              <div className="space-y-3">
                {entries.map((entry) => (
                  <div key={entry.userId} className="flex flex-col gap-1.5 sm:flex-row sm:items-center sm:gap-4">
                    <Link
                      to="/players/$userId"
                      params={{ userId: entry.userId }}
                      className="w-40 shrink-0 text-sm font-medium text-foreground transition-colors hover:text-accent"
                    >
                      {entry.gameNickname ?? entry.username}
                    </Link>
                    <BoostChips items={entry.boosts} short />
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Проверка фронта целиком**

Run (из `frontend/awake-web`): `npx tsc -b --noEmit && npm run build && npx eslint src`
Expected: tsc/бilld чистые; eslint — 27 известных ошибок, НОВЫХ нет (роут-файл существовал, react-refresh долг не растёт). `grep -rn "typesShort" src` — пусто.

- [ ] **Step 3: Commit** — `feat(boosts): /boosts totals + per-player item cards`

---

### Task 9: Финальная проверка этапа

**Files:** нет новых — проверка. Выполняет контроллер сессии (по прецеденту).

- [ ] **Step 1: Бэкенд** — `dotnet test`: все PASS (≈128: 119 было + ~9 новых/переписанных).
- [ ] **Step 2: Фронт** — `npx tsc -b --noEmit`, `npm run build`, `npx eslint src` — без новых регрессий; `npm run dev` кратко, если нужен routeTree.
- [ ] **Step 3: Миграция на дев-стенде** — из worktree: `dotnet ef database update --project src/Awake.Infrastructure --startup-project src/Awake.API` (строка подключения: env `ConnectionStrings__Postgres` c Host=localhost — как в BOOST Task 9). Ожидается `AddItemIdToPlayerBoostRequests`.
- [ ] **Step 4: Rebuild api ИЗ WORKTREE** — `MSYS_NO_PATHCONV=1 docker compose -p featurestage-4 up -d --build api`. Дождаться в логах `Item cache refreshed` и убедиться, что предметов стало больше ~1900 (было ~1750 без supply): `docker compose -p featurestage-4 logs api | grep "Item cache"`.
- [ ] **Step 5: Smoke** — `GET http://localhost:5001/api/profile/boosts` без токена → 401; `GET http://localhost:5001/api/items/search?boostType=2` без токена → 401.
- [ ] **Step 6: Живая приёмка Playwright** (оснастка как в BOOST Task 9: скрипт в api-контейнер, JWT из `Jwt__Secret`, фронт `host.docker.internal:5173`, SPA-навигация). Сценарии:
  1. WANBAN: профиль → слот «Скорость» → фокус → дропдаун со списком (≥5 позиций: анаболики, «Батарейка»…) → выбрать «Энергетик «Батарейка»» (или первый) → чип с иконкой; GET `/api/profile/boosts` → `[{boostType:2, itemId:...}]` с name/icon.
  2. Слот «Усиление» → ввести «оливье» → выбрать → GET: 2 записи.
  3. Замена: в слоте «Усиление» ✕ → снова поиск → выбрать другой предмет → GET: itemId изменился.
  4. `/boosts`: блок «Итого нужно» с иконками и «× 1»; блок игроков с чипами WANBAN.
  5. Voin: `/boosts` открывается; публичный профиль WANBAN — карточка с чипами предметов.
  6. Прямой PUT с кривыми данными (fetch с JWT): несуществующий itemId → 400; itemId не того типа → 400.
  7. Пустое состояние: WANBAN снимает всё → «Пока никто не отметил нужные бусты».
- [ ] **Step 7: Ledger** — запись в `.superpowers/sdd/progress.md`.

---

## Self-Review (выполнен)

- **Spec coverage:** синк+кэш (T1–T2), БД+валидация+карточки (T3–T5), энричер/DTO (T6), профиль-слоты+read-only (T7), /boosts+итого (T8), приёмка (T9). Пустой q при boostType — T5 Step 2. Фолбэк исчезнувшего предмета — T4 (маппер+тест).
- **Type consistency:** `BoostItemDto/BoostSelectionDto` (C#) ↔ `BoostItem/BoostSelection` (TS); поле `Boosts`/`boosts` везде (переименование с BoostTypes/boostTypes — T6/T7 согласованы); `SearchBoosts(q, boostType, limit=40)` — T2/T5.
- **Компилируемость по задачам:** T3 включает минимальные правки хендлеров; T4 включает правку контроллера (отмечено в задаче); T7 допускает красный tsc только в `_auth.boosts.tsx` (зафиксировано критерием, T8 закрывает).
