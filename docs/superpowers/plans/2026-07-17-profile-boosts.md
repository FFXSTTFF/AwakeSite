# Profile Boosts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Игрок отмечает в профиле нужные ему бусты (4 фиксированных типа), отметки видны всем участникам клана в профиле, попапе карточек отрядов и на сводной странице `/boosts`.

**Architecture:** Таблица `PlayerBoostRequests` (unique UserId+BoostType) по паттерну PlayerBuildProofs; `PUT /api/profile/boosts` — полная замена набора одним SaveChangesAsync; обогащение `SquadMemberDto` через существующий `SquadMemberEnricher` батчем. Фронт: общий компонент `BoostChips` (тумблеры/read-only) + новый роут `/boosts`.

**Tech Stack:** C# ASP.NET Core 8 (Clean Architecture, CQRS/MediatR, FluentValidation assembly-scan, EF Core + PostgreSQL, xUnit+Moq+FluentAssertions) · React 19 + TanStack Router/Query + Tailwind + react-i18next.

**Spec:** `docs/superpowers/specs/2026-07-17-profile-boosts-design.md`

## Global Constraints

- Ветка: `feature/profile-boosts`. **База — merged master после PR #13** (или `feature/squad-builder`, если PR ещё не смержен): `SquadMemberEnricher` существует только там, от старого master ветвиться нельзя.
- Все пользовательские строки — русские, через i18n (`boosts.*` в `ru.json`; `en.json` — английские зеркала).
- TS: `noUnusedLocals`, `erasableSyntaxOnly` — **TS enum запрещён**, только `const`-объект + union (см. `BuildType` в `types/api.ts`).
- Enum на проводе — числа (в проекте нет `JsonStringEnumConverter`).
- Shell — PowerShell 5.1 без `&&`: команды по одной или через `;`.
- Тесты: `dotnet test` из корня worktree (сейчас 113/113 зелёные). Фронт: `npx tsc -b --noEmit` и `npm run build` из `frontend/awake-web`.
- Сообщения об ошибках бэкенда — русские, стиль соседних валидаторов («… обязателен.»).
- Commit message trailer: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: Домен + EF + миграция

**Files:**
- Create: `src/Awake.Domain/Enums/BoostType.cs`
- Create: `src/Awake.Domain/Entities/PlayerBoostRequest.cs`
- Modify: `src/Awake.Infrastructure/Persistence/AppDbContext.cs` (DbSet + OnModelCreating)
- Create (генерируется): `src/Awake.Infrastructure/Persistence/Migrations/*_AddPlayerBoostRequests.cs`

**Interfaces:**
- Produces: enum `BoostType { Damage = 0, ShortDamage = 1, Speed = 2, Defense = 3 }`; entity `PlayerBoostRequest { Guid UserId; User User; BoostType BoostType }` : `BaseEntity` (Id/CreatedAt/UpdatedAt из базы); `DbSet<PlayerBoostRequest> PlayerBoostRequests` на `AppDbContext`.

- [ ] **Step 1: Создать enum**

`src/Awake.Domain/Enums/BoostType.cs`:

```csharp
namespace Awake.Domain.Enums;

/// <summary>Типы бафов на КВ. Значения фиксированы — сериализуются числами в API.</summary>
public enum BoostType
{
    Damage = 0,       // Усиление
    ShortDamage = 1,  // Кратковременное усиление
    Speed = 2,        // Скорость
    Defense = 3,      // Защита
}
```

- [ ] **Step 2: Создать сущность**

`src/Awake.Domain/Entities/PlayerBoostRequest.cs` (по образцу `PlayerBuildProof.cs`):

```csharp
using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class PlayerBoostRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BoostType BoostType { get; set; }
}
```

- [ ] **Step 3: DbSet + конфигурация**

В `src/Awake.Infrastructure/Persistence/AppDbContext.cs`:

после строки `public DbSet<PlayerBuildProof> PlayerBuildProofs => Set<PlayerBuildProof>();` добавить:

```csharp
    public DbSet<PlayerBoostRequest> PlayerBoostRequests => Set<PlayerBoostRequest>();
```

В `OnModelCreating`, после блока `builder.Entity<PlayerBuildProof>(...)`:

```csharp
        builder.Entity<PlayerBoostRequest>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.BoostType }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
```

- [ ] **Step 4: Сборка**

Run: `dotnet build src/Awake.API`
Expected: 0 ошибок.

- [ ] **Step 5: Сгенерировать миграцию**

Run (из корня worktree):
```bash
dotnet ef migrations add AddPlayerBoostRequests --project src/Awake.Infrastructure --startup-project src/Awake.API
```
Expected: в `Migrations/` появился `*_AddPlayerBoostRequests.cs` с таблицей `PlayerBoostRequests`, уникальным индексом `(UserId, BoostType)` и FK на Users с Cascade.

⚠️ Известный шум: dotnet-ef 10.0.3 против пакетов 10.0.9 добавляет лишние `ToTable(...)` в snapshot. Если в диффе snapshot появились правки НЕ по PlayerBoostRequests — откатить их точечно (`git checkout -p` по snapshot), оставив только свой блок. Так делали в INV/SB.

- [ ] **Step 6: Проверить, что миграция не тронула чужие таблицы**

Run: `git diff --stat src/Awake.Infrastructure`
Expected: только новая миграция + Designer + snapshot (в snapshot — только блок PlayerBoostRequest).

- [ ] **Step 7: Commit**

```bash
git add src/Awake.Domain src/Awake.Infrastructure
git commit -m "feat(boosts): PlayerBoostRequest entity + BoostType enum + migration"
```

---

### Task 2: Репозиторий + DI

**Files:**
- Create: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerBoostRequestRepository.cs`
- Create: `src/Awake.Infrastructure/Persistence/Repositories/PlayerBoostRequestRepository.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs` (строка ~36, после IPlayerBuildProofRepository)

**Interfaces:**
- Produces (сигнатуры, на них завязаны Tasks 3–4):

```csharp
Task<IReadOnlyList<BoostType>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default); // с Include(User)
Task ReplaceForUserAsync(Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct = default);
```

- [ ] **Step 1: Интерфейс**

`src/Awake.Application/Common/Interfaces/Repositories/IPlayerBoostRequestRepository.cs`:

```csharp
using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerBoostRequestRepository
{
    Task<IReadOnlyList<BoostType>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    /// <summary>С Include(User) — для сводки, чтобы не ходить за никами вторым запросом.</summary>
    Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Полная замена набора пользователя: remove старых + add новых одним SaveChangesAsync.</summary>
    Task ReplaceForUserAsync(Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct = default);
}
```

- [ ] **Step 2: Реализация**

`src/Awake.Infrastructure/Persistence/Repositories/PlayerBoostRequestRepository.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerBoostRequestRepository(AppDbContext context) : IPlayerBoostRequestRepository
{
    public async Task<IReadOnlyList<BoostType>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.BoostType)
            .Select(x => x.BoostType)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerBoostRequest>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerBoostRequest>> GetAllAsync(CancellationToken ct = default) =>
        await context.PlayerBoostRequests
            .AsNoTracking()
            .Include(x => x.User)
            .ToListAsync(ct);

    public async Task ReplaceForUserAsync(
        Guid userId, IReadOnlyList<BoostType> types, CancellationToken ct = default)
    {
        var existing = await context.PlayerBoostRequests
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        context.PlayerBoostRequests.RemoveRange(existing);
        context.PlayerBoostRequests.AddRange(
            types.Select(t => new PlayerBoostRequest { UserId = userId, BoostType = t }));
        await context.SaveChangesAsync(ct); // одна транзакция — атомарная замена
    }
}
```

- [ ] **Step 3: Регистрация в DI**

В `src/Awake.Infrastructure/DependencyInjection.cs` после `services.AddScoped<IPlayerBuildProofRepository, PlayerBuildProofRepository>();`:

```csharp
        services.AddScoped<IPlayerBoostRequestRepository, PlayerBoostRequestRepository>();
```

- [ ] **Step 4: Сборка + прогон существующих тестов**

Run: `dotnet build src/Awake.API` затем `dotnet test`
Expected: 0 ошибок, 113/113 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Awake.Application src/Awake.Infrastructure
git commit -m "feat(boosts): IPlayerBoostRequestRepository + EF implementation + DI"
```

---

### Task 3: Application — команда и запросы (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Boosts/Commands/SetMyBoosts/SetMyBoostsCommand.cs`
- Create: `src/Awake.Application/Features/Boosts/Commands/SetMyBoosts/SetMyBoostsCommandHandler.cs`
- Create: `src/Awake.Application/Features/Boosts/Commands/SetMyBoosts/SetMyBoostsCommandValidator.cs`
- Create: `src/Awake.Application/Features/Boosts/Queries/GetMyBoosts/GetMyBoostsQuery.cs`
- Create: `src/Awake.Application/Features/Boosts/Queries/GetMyBoosts/GetMyBoostsQueryHandler.cs`
- Create: `src/Awake.Application/Features/Boosts/Queries/GetBoostsSummary/GetBoostsSummaryQuery.cs`
- Create: `src/Awake.Application/Features/Boosts/Queries/GetBoostsSummary/BoostSummaryEntryDto.cs`
- Create: `src/Awake.Application/Features/Boosts/Queries/GetBoostsSummary/GetBoostsSummaryQueryHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Boosts/BoostsTests.cs`

**Interfaces:**
- Consumes: `IPlayerBoostRequestRepository` (Task 2).
- Produces:
  - `SetMyBoostsCommand(Guid UserId, IReadOnlyList<BoostType> BoostTypes)` : `IRequest<Result<bool>>`
  - `GetMyBoostsQuery(Guid UserId)` : `IRequest<IReadOnlyList<BoostType>>`
  - `GetBoostsSummaryQuery()` : `IRequest<IReadOnlyList<BoostSummaryEntryDto>>`
  - `record BoostSummaryEntryDto(Guid UserId, string Username, string? GameNickname, IReadOnlyList<BoostType> BoostTypes)`

- [ ] **Step 1: Написать падающие тесты**

`tests/Awake.Unit.Tests/Features/Boosts/BoostsTests.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Boosts;

public class BoostsTests
{
    private readonly Mock<IPlayerBoostRequestRepository> _repo = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task SetMyBoosts_DeduplicatesInput_AndReplaces()
    {
        var handler = new SetMyBoostsCommandHandler(_repo.Object);

        var result = await handler.Handle(new SetMyBoostsCommand(
            _userId, [BoostType.Speed, BoostType.Speed, BoostType.Damage]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(
            _userId,
            It.Is<IReadOnlyList<BoostType>>(l =>
                l.Count == 2 && l.Contains(BoostType.Speed) && l.Contains(BoostType.Damage)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetMyBoosts_EmptyList_ClearsAll()
    {
        var handler = new SetMyBoostsCommandHandler(_repo.Object);

        var result = await handler.Handle(
            new SetMyBoostsCommand(_userId, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForUserAsync(
            _userId,
            It.Is<IReadOnlyList<BoostType>>(l => l.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_UnknownEnumValue_Fails()
    {
        var validator = new SetMyBoostsCommandValidator();

        var result = validator.Validate(new SetMyBoostsCommand(_userId, [(BoostType)99]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ValidSet_Passes()
    {
        var validator = new SetMyBoostsCommandValidator();

        var result = validator.Validate(new SetMyBoostsCommand(
            _userId, [BoostType.Damage, BoostType.ShortDamage, BoostType.Speed, BoostType.Defense]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Summary_GroupsByUser_SortsByCountDescThenNick()
    {
        var alice = new User { Username = "alice", GameNickname = "Zorro" };   // 1 буст
        var bob = new User { Username = "bob", GameNickname = "Alpha" };       // 2 буста
        var carl = new User { Username = "carl", GameNickname = null };        // 2 буста, без ника
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PlayerBoostRequest { UserId = alice.Id, User = alice, BoostType = BoostType.Speed },
            new PlayerBoostRequest { UserId = bob.Id, User = bob, BoostType = BoostType.Damage },
            new PlayerBoostRequest { UserId = bob.Id, User = bob, BoostType = BoostType.Defense },
            new PlayerBoostRequest { UserId = carl.Id, User = carl, BoostType = BoostType.Speed },
            new PlayerBoostRequest { UserId = carl.Id, User = carl, BoostType = BoostType.Damage },
        ]);
        var handler = new GetBoostsSummaryQueryHandler(_repo.Object);

        var result = await handler.Handle(new GetBoostsSummaryQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
        // bob (2, «Alpha») раньше carl (2, ник null -> username «carl»), alice (1) последняя
        result[0].UserId.Should().Be(bob.Id);
        result[1].UserId.Should().Be(carl.Id);
        result[2].UserId.Should().Be(alice.Id);
        result[0].BoostTypes.Should().BeEquivalentTo([BoostType.Damage, BoostType.Defense]);
    }
}
```

- [ ] **Step 2: Убедиться, что тесты не компилируются (нет типов)**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~BoostsTests"`
Expected: ошибка компиляции — `SetMyBoostsCommand` не существует.

- [ ] **Step 3: Реализация — команда**

`SetMyBoostsCommand.cs`:

```csharp
using Awake.Application.Common.Models;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public record SetMyBoostsCommand(
    Guid UserId,
    IReadOnlyList<BoostType> BoostTypes) : IRequest<Result<bool>>;
```

`SetMyBoostsCommandHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Boosts.Commands.SetMyBoosts;

public class SetMyBoostsCommandHandler(
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<SetMyBoostsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        SetMyBoostsCommand request, CancellationToken cancellationToken)
    {
        var types = request.BoostTypes.Distinct().ToList();
        await boostRepository.ReplaceForUserAsync(request.UserId, types, cancellationToken);
        return Result<bool>.Success(true);
    }
}
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
        RuleFor(x => x.BoostTypes).NotNull().WithMessage("Список бустов обязателен.");
        RuleForEach(x => x.BoostTypes).IsInEnum().WithMessage("Недопустимый тип буста.");
    }
}
```

- [ ] **Step 4: Реализация — запросы**

`GetMyBoostsQuery.cs`:

```csharp
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public record GetMyBoostsQuery(Guid UserId) : IRequest<IReadOnlyList<BoostType>>;
```

`GetMyBoostsQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetMyBoosts;

public class GetMyBoostsQueryHandler(
    IPlayerBoostRequestRepository boostRepository
) : IRequestHandler<GetMyBoostsQuery, IReadOnlyList<BoostType>>
{
    public Task<IReadOnlyList<BoostType>> Handle(
        GetMyBoostsQuery request, CancellationToken cancellationToken) =>
        boostRepository.GetByUserIdAsync(request.UserId, cancellationToken);
}
```

`GetBoostsSummaryQuery.cs`:

```csharp
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record GetBoostsSummaryQuery() : IRequest<IReadOnlyList<BoostSummaryEntryDto>>;
```

`BoostSummaryEntryDto.cs`:

```csharp
using Awake.Domain.Enums;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public record BoostSummaryEntryDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    IReadOnlyList<BoostType> BoostTypes);
```

`GetBoostsSummaryQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Boosts.Queries.GetBoostsSummary;

public class GetBoostsSummaryQueryHandler(
    IPlayerBoostRequestRepository boostRepository
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
                    g.Select(r => r.BoostType).OrderBy(t => t).ToList());
            })
            .OrderByDescending(e => e.BoostTypes.Count)
            .ThenBy(e => e.GameNickname ?? e.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
```

- [ ] **Step 5: Прогнать тесты**

Run: `dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~BoostsTests"`
Expected: 5/5 PASS.

- [ ] **Step 6: Полный прогон**

Run: `dotnet test`
Expected: 118/118 PASS (113 старых + 5 новых).

- [ ] **Step 7: Commit**

```bash
git add src/Awake.Application tests/Awake.Unit.Tests
git commit -m "feat(boosts): SetMyBoosts command + GetMyBoosts/GetBoostsSummary queries"
```

---

### Task 4: Обогащение SquadMemberDto и PlayerProfileDto

**Files:**
- Modify: `src/Awake.Application/Features/Squads/SquadMemberEnricher.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquads/SquadDto.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquads/GetSquadsQueryHandler.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquadById/GetSquadByIdQueryHandler.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs`
- Modify: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/PlayerProfileDto.cs`
- Modify: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQueryHandler.cs`
- Test (обновить): `tests/Awake.Unit.Tests/Features/Squads/SquadMemberEnricherTests.cs`, `GetSquadsQueryHandlerTests.cs`, `GetSquadByIdQueryHandlerTests.cs`, `GetSquadBuilderQueryHandlerTests.cs` — и тесты `GetPlayerProfile*`, если есть (проверить grep'ом).

**Interfaces:**
- Consumes: `IPlayerBoostRequestRepository.GetByUserIdsAsync` / `GetByUserIdAsync` (Task 2).
- Produces:
  - `SquadMemberEnricher.ComputeAsync(users, inventoryRepository, proofRepository, boostRepository, itemCache, snapshotRepository, ct)` → `IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostType> BoostTypes)>` — **новый параметр `boostRepository` четвёртым, до itemCache**.
  - `SquadMemberDto` — новое последнее поле `IReadOnlyList<BoostType> BoostTypes`.
  - `PlayerProfileDto` — новое последнее поле `IReadOnlyList<BoostType> Boosts`.

- [ ] **Step 1: Расширить энричер**

В `SquadMemberEnricher.cs`: добавить `using Awake.Domain.Enums;` уже есть — проверить; сигнатура и тело:

```csharp
    public static async Task<IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd, IReadOnlyList<BoostType> BoostTypes)>> ComputeAsync(
        IReadOnlyList<User> users,
        IPlayerInventoryRepository inventoryRepository,
        IPlayerBuildProofRepository proofRepository,
        IPlayerBoostRequestRepository boostRepository,
        IItemCacheService itemCache,
        IPlayerStatsSnapshotRepository snapshotRepository,
        CancellationToken ct = default)
```

После загрузки proofs добавить:

```csharp
        var boosts = await boostRepository.GetByUserIdsAsync(ids, ct);
        var boostsByUser = boosts.ToLookup(b => b.UserId);
```

В финальном `ToDictionary` вернуть тройку:

```csharp
            IReadOnlyList<BoostType> userBoosts = boostsByUser[u.Id]
                .Select(b => b.BoostType)
                .OrderBy(t => t)
                .ToList();

            return (flags, kd, userBoosts);
```

- [ ] **Step 2: SquadMemberDto**

`SquadDto.cs`:

```csharp
public record SquadMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    bool IsLeader,
    DateTime JoinedAt,
    PlayerFlagsDto Flags,
    double? Kd,
    IReadOnlyList<BoostType> BoostTypes);
```

(добавить `using Awake.Domain.Enums;`)

- [ ] **Step 3: Обновить три хендлера отрядов**

В каждый из `GetSquadsQueryHandler`, `GetSquadByIdQueryHandler`, `GetSquadBuilderQueryHandler`:
- добавить в primary constructor `IPlayerBoostRequestRepository boostRepository` (после `proofRepository`);
- в вызов `ComputeAsync` вставить `boostRepository` четвёртым аргументом (до `itemCache`);
- в `GetSquads`/`GetSquadById` при конструировании `SquadMemberDto` добавить последний аргумент `enriched[m.UserId].BoostTypes`;
- в `GetSquadBuilder` DTO не меняется — `ToFighter` продолжает использовать `.Flags`/`.Kd`, тройка это позволяет.

- [ ] **Step 4: PlayerProfileDto + handler**

`PlayerProfileDto.cs` — добавить последнее поле:

```csharp
public record PlayerProfileDto(
    Guid UserId,
    string Username,
    string? DiscordUsername,
    string? DiscordAvatarUrl,
    UserRank Rank,
    string? GameNickname,
    PlayerSquadDto? Squad,
    PlayerStatsDto? Stats,
    Loadout? Loadout,
    IReadOnlyList<BoostType> Boosts);
```

(`using Awake.Domain.Enums;` уже есть — там UserRank.)

`GetPlayerProfileQueryHandler.cs` — добавить в конструктор `IPlayerBoostRequestRepository boostRepository`, перед `return`:

```csharp
        var boosts = await boostRepository.GetByUserIdAsync(user.Id, cancellationToken);
```

и передать `boosts` последним аргументом в `new PlayerProfileDto(...)`.

- [ ] **Step 5: Починить существующие тесты**

Во всех четырёх файлах тестов отрядов добавить мок и прокинуть его:

```csharp
    private readonly Mock<IPlayerBoostRequestRepository> _boosts = new();
```

в `SetupEmpty`/setup-блоках:

```csharp
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
```

и `_boosts.Object` в вызовы `ComputeAsync` (4-м аргументом) / конструкторы хендлеров. Grep-проверка, что никто не забыт:
`grep -rn "ComputeAsync\|GetSquadsQueryHandler(\|GetSquadByIdQueryHandler(\|GetSquadBuilderQueryHandler(\|GetPlayerProfileQueryHandler(" tests/`

В `SquadMemberEnricherTests` добавить один новый тест на бусты:

```csharp
    [Fact]
    public async Task ComputeAsync_BoostsGroupedPerUser()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member };
        SetupEmpty();
        _boosts.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([
                   new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Defense },
                   new PlayerBoostRequest { UserId = user.Id, BoostType = BoostType.Damage },
               ]);

        var result = await SquadMemberEnricher.ComputeAsync(
            [user], _inventory.Object, _proofs.Object, _boosts.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[user.Id].BoostTypes.Should().Equal(BoostType.Damage, BoostType.Defense); // отсортировано
    }
```

- [ ] **Step 6: Полный прогон**

Run: `dotnet test`
Expected: 119/119 PASS (118 + 1 новый). Если тестов GetPlayerProfile не существовало — число не меняется от них.

- [ ] **Step 7: Commit**

```bash
git add src/Awake.Application tests/Awake.Unit.Tests
git commit -m "feat(boosts): enrich SquadMemberDto and PlayerProfileDto with boost types"
```

---

### Task 5: BoostsController

**Files:**
- Create: `src/Awake.API/Controllers/BoostsController.cs`

**Interfaces:**
- Consumes: `SetMyBoostsCommand`, `GetMyBoostsQuery`, `GetBoostsSummaryQuery` (Task 3); `ICurrentUserService.UserId`; `RankAuthorize` (существующий фильтр из `Awake.API.Filters`).
- Produces (контракт для фронта, Task 6):
  - `GET /api/profile/boosts` → `200 [0,2]` (числа BoostType)
  - `PUT /api/profile/boosts` body `{ "boostTypes": [0,2] }` → `204`
  - `GET /api/boosts/summary` → `200 [{ "userId": "...", "username": "...", "gameNickname": "...", "boostTypes": [0,3] }]`

- [ ] **Step 1: Контроллер**

`src/Awake.API/Controllers/BoostsController.cs` (гейты — как в `InventoryController`: свои `api/profile/*` без ранг-гейта, сводка Member+):

```csharp
using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Boosts.Commands.SetMyBoosts;
using Awake.Application.Features.Boosts.Queries.GetBoostsSummary;
using Awake.Application.Features.Boosts.Queries.GetMyBoosts;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

public record SetBoostsRequest(IReadOnlyList<BoostType> BoostTypes);

[ApiController]
[Authorize]
public class BoostsController(
    ISender sender,
    ICurrentUserService currentUser
) : ControllerBase
{
    // ── Свои бусты (любой ранг — как остальные api/profile/*) ──────────────

    [HttpGet("api/profile/boosts")]
    public async Task<IActionResult> GetMy(CancellationToken ct) =>
        Ok(await sender.Send(new GetMyBoostsQuery(currentUser.UserId), ct));

    [HttpPut("api/profile/boosts")]
    public async Task<IActionResult> SetMy(SetBoostsRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new SetMyBoostsCommand(currentUser.UserId, request.BoostTypes), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── Сводка по клану (Member+) ───────────────────────────────────────────

    [HttpGet("api/boosts/summary")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        Ok(await sender.Send(new GetBoostsSummaryQuery(), ct));
}
```

⚠️ Перед коммитом сверить с реальным `InventoryController`: точное имя/использование `RankAuthorize` и `ICurrentUserService.UserId` — скопировать их стиль byte-в-byte.

- [ ] **Step 2: Сборка + полный прогон**

Run: `dotnet build src/Awake.API` затем `dotnet test`
Expected: 0 ошибок, 119/119 PASS.

- [ ] **Step 3: Commit**

```bash
git add src/Awake.API
git commit -m "feat(boosts): BoostsController — my boosts CRUD + clan summary"
```

---

### Task 6: Фронт — типы, API, BoostChips, профили

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts`
- Create: `frontend/awake-web/src/api/boosts.ts`
- Create: `frontend/awake-web/src/components/boosts/BoostChips.tsx`
- Create: `frontend/awake-web/src/components/boosts/BoostsSection.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.profile.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.players.$userId.tsx`
- Modify: `frontend/awake-web/src/i18n/ru.json`, `frontend/awake-web/src/i18n/en.json`

**Interfaces:**
- Consumes: эндпоинты Task 5.
- Produces: `BoostType` const-объект; `boostsApi.getMy/setMy/summary`; `BoostChips({ selected, onToggle?, short? })` — без `onToggle` компонент read-only и рендерит **только активные** чипы; `BoostsSection` (карточка своего профиля); ключи `boosts.*`, `nav.boosts` в i18n. Query keys: `['boosts','my']`, `['boosts','summary']`.

- [ ] **Step 1: Типы**

В `types/api.ts` — после блока `BuildType` добавить:

```ts
export const BoostType = {
  Damage: 0,
  ShortDamage: 1,
  Speed: 2,
  Defense: 3,
} as const
export type BoostType = (typeof BoostType)[keyof typeof BoostType]

export const ALL_BOOST_TYPES: readonly BoostType[] = [
  BoostType.Damage,
  BoostType.ShortDamage,
  BoostType.Speed,
  BoostType.Defense,
]

export interface BoostSummaryEntry {
  userId: string
  username: string
  gameNickname: string | null
  boostTypes: BoostType[]
}
```

В `SquadMemberDto` добавить поле `boostTypes: BoostType[]` (после `kd`). В `PlayerProfileDto` добавить `boosts: BoostType[]` (последним).

- [ ] **Step 2: API-клиент**

`src/api/boosts.ts`:

```ts
import { apiClient } from './client'
import type { BoostSummaryEntry, BoostType } from '@/types/api'

export const boostsApi = {
  getMy: (): Promise<BoostType[]> => apiClient.get('/profile/boosts'),
  setMy: (boostTypes: BoostType[]): Promise<void> =>
    apiClient.put('/profile/boosts', { boostTypes }),
  summary: (): Promise<BoostSummaryEntry[]> => apiClient.get('/boosts/summary'),
}
```

- [ ] **Step 3: i18n**

В `ru.json` — в `nav` добавить `"boosts": "Бусты"`, и новую секцию верхнего уровня (после `profile`):

```json
"boosts": {
  "title": "Бусты",
  "myTitle": "Нужные бусты",
  "myHint": "Отметьте бафы, которые вам нужны на КВ — их увидят офицеры и участники клана",
  "types": { "0": "Усиление", "1": "Кратковременное усиление", "2": "Скорость", "3": "Защита" },
  "typesShort": { "0": "Усиление", "1": "Кратк. усиление", "2": "Скорость", "3": "Защита" },
  "player": "Игрок",
  "empty": "Пока никто не отметил нужные бусты",
  "emptyHint": "Отметить нужные бусты можно в своём профиле"
}
```

В `en.json` — зеркально:

```json
"boosts": {
  "title": "Boosts",
  "myTitle": "Needed boosts",
  "myHint": "Mark the buffs you need for clan wars — officers and clan members will see them",
  "types": { "0": "Damage", "1": "Short damage", "2": "Speed", "3": "Defense" },
  "typesShort": { "0": "Damage", "1": "Short dmg", "2": "Speed", "3": "Defense" },
  "player": "Player",
  "empty": "Nobody has marked needed boosts yet",
  "emptyHint": "You can mark yours on your profile page"
}
```

и `"boosts": "Boosts"` в `nav` файла `en.json`.

- [ ] **Step 4: BoostChips**

`src/components/boosts/BoostChips.tsx`:

```tsx
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { ALL_BOOST_TYPES, type BoostType } from '@/types/api'

// Без onToggle — read-only: показываются только активные чипы (пустые — шум).
// С onToggle — все 4 типа как тумблеры.
export function BoostChips({
  selected,
  onToggle,
  short = false,
}: {
  selected: BoostType[]
  onToggle?: (type: BoostType) => void
  short?: boolean
}) {
  const { t } = useTranslation()
  const readonly = !onToggle
  const visible = readonly ? ALL_BOOST_TYPES.filter((b) => selected.includes(b)) : ALL_BOOST_TYPES

  return (
    <div className="flex flex-wrap gap-1.5">
      {visible.map((type) => {
        const active = selected.includes(type)
        const label = t(`boosts.${short ? 'typesShort' : 'types'}.${type}`)
        const cls = cn(
          'rounded-md border px-2 py-1 text-xs font-medium transition-colors',
          active
            ? 'border-accent/30 bg-accent/10 text-accent'
            : 'border-border bg-secondary/50 text-muted-foreground',
        )
        return readonly ? (
          <span key={type} className={cls}>
            {label}
          </span>
        ) : (
          <button
            key={type}
            type="button"
            onClick={() => onToggle(type)}
            className={cn(cls, 'cursor-pointer hover:border-accent/50')}
          >
            {label}
          </button>
        )
      })}
    </div>
  )
}
```

- [ ] **Step 5: BoostsSection (свой профиль)**

`src/components/boosts/BoostsSection.tsx`:

```tsx
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import type { BoostType } from '@/types/api'

export function BoostsSection() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: selected = [] } = useQuery({
    queryKey: ['boosts', 'my'],
    queryFn: boostsApi.getMy,
  })

  const mutation = useMutation({
    mutationFn: boostsApi.setMy,
    onMutate: async (next: BoostType[]) => {
      await queryClient.cancelQueries({ queryKey: ['boosts', 'my'] })
      const prev = queryClient.getQueryData<BoostType[]>(['boosts', 'my'])
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

  function toggle(type: BoostType) {
    const next = selected.includes(type)
      ? selected.filter((x) => x !== type)
      : [...selected, type]
    mutation.mutate(next)
  }

  return (
    <Card className="mt-6">
      <CardContent className="pt-5 pb-5">
        <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{t('boosts.myHint')}</p>
        <div className="mt-4">
          <BoostChips selected={selected} onToggle={toggle} />
        </div>
      </CardContent>
    </Card>
  )
}
```

- [ ] **Step 6: Вставить в свой профиль**

В `_auth.profile.tsx` — импорт `import { BoostsSection } from '@/components/boosts/BoostsSection'` и в return между `PlayerProfileView` и `InventorySection`:

```tsx
    <>
      <PlayerProfileView profile={profile} onRefresh={handleRefresh} refreshing={refreshing} />
      <BoostsSection />
      <InventorySection />
    </>
```

- [ ] **Step 7: Публичный профиль**

В `_auth.players.$userId.tsx` — импорты:

```tsx
import { useTranslation } from 'react-i18next'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
```

`const { t } = useTranslation()` в начале компонента. Обернуть return во фрагмент и после `PlayerProfileView` добавить:

```tsx
      {profile.boosts.length > 0 && (
        <Card className="mt-6">
          <CardContent className="pt-5 pb-5">
            <h2 className="text-base font-semibold text-foreground">{t('boosts.myTitle')}</h2>
            <div className="mt-4">
              <BoostChips selected={profile.boosts} />
            </div>
          </CardContent>
        </Card>
      )}
```

- [ ] **Step 8: Проверка типов и сборки**

Run (из `frontend/awake-web`): `npx tsc -b --noEmit` затем `npm run build`
Expected: 0 ошибок. ⚠️ `SquadMemberDto.boostTypes` обязателен — если tsc укажет места, где `SquadMemberDto` конструируется литералом, добавить туда `boostTypes: []` (по опыту SCR таких мест не было).

- [ ] **Step 9: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(boosts): frontend types, api, BoostChips, profile sections"
```

---

### Task 7: Фронт — бусты в попапе карточек отрядов

**Files:**
- Modify: `frontend/awake-web/src/components/squads/MemberHoverInfo.tsx`
- Modify: `frontend/awake-web/src/components/squads/SquadCard.tsx`

**Interfaces:**
- Consumes: `BoostChips` (Task 6), `SquadMemberDto.boostTypes` (Task 6).
- Produces: `MemberHoverInfo` — новый **опциональный** проп `boosts?: BoostType[]` (default `[]`, чтобы другие call-sites не сломались).

- [ ] **Step 1: MemberHoverInfo**

Импорты: `import { BoostChips } from '@/components/boosts/BoostChips'` и `import type { BoostType, PlayerFlags } from '@/types/api'`. Сигнатура:

```tsx
export function MemberHoverInfo({
  nickname,
  flags,
  kd,
  boosts = [],
  children,
}: {
  nickname: string
  flags: PlayerFlags
  kd: number | null
  boosts?: BoostType[]
  children: ReactNode
}) {
```

В панели попапа после блока `<div className="mt-2"><InventoryFlags ... /></div>` добавить:

```tsx
          {boosts.length > 0 && (
            <div className="mt-2">
              <BoostChips selected={boosts} short />
            </div>
          )}
```

- [ ] **Step 2: SquadCard передаёт бусты**

В `SquadCard.tsx` — в оба вызова `MemberHoverInfo` добавить проп:

лидер: `<MemberHoverInfo nickname={...} flags={leader.flags} kd={leader.kd} boosts={leader.boostTypes}>`
остальные: `<MemberHoverInfo key={m.userId} nickname={...} flags={m.flags} kd={m.kd} boosts={m.boostTypes}>`

- [ ] **Step 3: Проверить другие call-sites**

Run: `grep -rn "MemberHoverInfo" frontend/awake-web/src --include="*.tsx"`
Expected: только `MemberHoverInfo.tsx` и `SquadCard.tsx`. Если есть другие — проп опционален, они не ломаются; передать бусты и туда, если там есть `SquadMemberDto`.

- [ ] **Step 4: tsc**

Run (из `frontend/awake-web`): `npx tsc -b --noEmit`
Expected: 0 ошибок.

- [ ] **Step 5: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(boosts): show member boosts in squad card hover popup"
```

---

### Task 8: Фронт — сводная страница /boosts + навигация

**Files:**
- Create: `frontend/awake-web/src/routes/_auth.boosts.tsx`
- Modify: `frontend/awake-web/src/components/layout/Sidebar.tsx`
- Modify: `frontend/awake-web/src/components/layout/MobileTabBar.tsx`

**Interfaces:**
- Consumes: `boostsApi.summary` (Task 6), `ALL_BOOST_TYPES`, `useAuth`, `UserRank`.
- Produces: роут `/boosts` (Member+; Guest — редирект на `/profile`).

**Осознанное отклонение от спеки:** мобильный таб-бар уже полон (4 таба + «Ещё») — пункт «Бусты» на мобиле идёт в лист «Ещё», а не отдельным табом. В сайдбаре — полноценный пункт.

- [ ] **Step 1: Страница**

`src/routes/_auth.boosts.tsx`:

```tsx
import { createFileRoute, Link, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Check, Minus } from 'lucide-react'
import { boostsApi } from '@/api/boosts'
import { BoostChips } from '@/components/boosts/BoostChips'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { ALL_BOOST_TYPES, UserRank } from '@/types/api'

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

  const counts = new Map(
    ALL_BOOST_TYPES.map((type) => [
      type,
      entries.filter((e) => e.boostTypes.includes(type)).length,
    ]),
  )

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
        <>
          {/* Десктоп: таблица игроки × типы */}
          <Card className="hidden md:block">
            <CardContent className="pt-5 pb-5">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border text-left">
                      <th className="py-2 pr-4 text-xs font-medium text-muted-foreground">
                        {t('boosts.player')}
                      </th>
                      {ALL_BOOST_TYPES.map((type) => (
                        <th key={type} className="px-3 py-2 text-center text-xs font-medium text-muted-foreground">
                          <div>{t(`boosts.typesShort.${type}`)}</div>
                          <div className="mt-0.5 font-semibold text-accent">{counts.get(type)}</div>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {entries.map((entry) => (
                      <tr key={entry.userId} className="border-b border-border/50 last:border-0">
                        <td className="py-2.5 pr-4">
                          <Link
                            to="/players/$userId"
                            params={{ userId: entry.userId }}
                            className="font-medium text-foreground transition-colors hover:text-accent"
                          >
                            {entry.gameNickname ?? entry.username}
                          </Link>
                        </td>
                        {ALL_BOOST_TYPES.map((type) => (
                          <td key={type} className="px-3 py-2.5 text-center">
                            {entry.boostTypes.includes(type) ? (
                              <Check size={15} className="inline text-accent" />
                            ) : (
                              <Minus size={15} className="inline text-muted-foreground/40" />
                            )}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>

          {/* Мобила: карточки */}
          <div className="space-y-2 md:hidden">
            {entries.map((entry) => (
              <Card key={entry.userId}>
                <CardContent className="pt-4 pb-4">
                  <Link
                    to="/players/$userId"
                    params={{ userId: entry.userId }}
                    className="text-sm font-medium text-foreground"
                  >
                    {entry.gameNickname ?? entry.username}
                  </Link>
                  <div className="mt-2">
                    <BoostChips selected={entry.boostTypes} short />
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Sidebar**

В `Sidebar.tsx`: импорт `Zap` добавить в блок lucide-импортов. В `navLinks` после строки с `/squads`:

```tsx
    ...(isMemberPlus ? [{ to: '/boosts' as const, label: t('nav.boosts'), icon: Zap }] : []),
```

- [ ] **Step 3: MobileTabBar — пункт в листе «Ещё»**

Импорт `Zap` в lucide-блок. В листе «Ещё» перед `<Link to="/settings" ...>`:

```tsx
              {isMemberPlus && (
                <Link
                  to="/boosts"
                  onClick={() => setMoreOpen(false)}
                  className="flex items-center gap-3 rounded-md px-3 py-2.5 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <Zap size={16} />
                  {t('nav.boosts')}
                </Link>
              )}
```

- [ ] **Step 4: tsc + build (роут-дерево перегенерируется на dev/build)**

Run (из `frontend/awake-web`): `npm run build`
Expected: 0 ошибок; в `routeTree.gen.ts` появился `/_auth/boosts`. Если tsc ругается на несуществующий роут до генерации — сначала `npm run dev` на пару секунд или `npx tsr generate` (как принято в проекте — посмотреть scripts в package.json).

- [ ] **Step 5: Commit**

```bash
git add frontend/awake-web
git commit -m "feat(boosts): clan boosts summary page + nav entries"
```

---

### Task 9: Финальная проверка этапа

**Files:** нет новых — проверка.

- [ ] **Step 1: Полный бэкенд-прогон**

Run: `dotnet test`
Expected: 119/119 PASS.

- [ ] **Step 2: Фронт**

Run (из `frontend/awake-web`): `npx tsc -b --noEmit` затем `npm run build`
Expected: 0 ошибок. Новых eslint-ошибок сверх известного pre-existing долга (25 × react-refresh) быть не должно: `npx eslint src --max-warnings=0` — сравнить с master.

- [ ] **Step 3: Миграция на дев-стенде**

Стенд: compose-проект `featurestage-4`, db на `localhost:5432`. Применить миграцию как в INV Task 7:

```bash
dotnet ef database update --project src/Awake.Infrastructure --startup-project src/Awake.API
```

Expected: `Applying migration '..._AddPlayerBoostRequests'`. Затем пересобрать api-образ (иначе стенд без новых эндпоинтов — урок SCR Task 11):

```bash
MSYS_NO_PATHCONV=1 docker compose -p featurestage-4 up -d --build api
```

- [ ] **Step 4: Smoke API**

Без токена — 401:

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/profile/boosts
curl -s -o /dev/null -w "%{http_code}" http://localhost:5001/api/boosts/summary
```

Expected: `401` оба.

- [ ] **Step 5: Живая приёмка (Playwright в api-контейнере)**

По накатанной оснастке (скрипт копируется `docker cp`, запуск бандл-нодой `/app/.playwright/node/linux-x64/node`, JWT минтится внутри контейнера через `process.env.Jwt__Secret`, фронт `http://host.docker.internal:5173`, только SPA-навигация — authStore без persist). Сценарии:

1. WANBAN (Colonel): профиль → в карточке «Нужные бусты» кликнуть «Усиление» и «Скорость» → чипы стали активными; перезапросить `GET /api/profile/boosts` — `[0,2]`.
2. Тумблер обратно: клик по «Усиление» → чип погас, `GET` — `[2]`.
3. Страница `/boosts` (клик по пункту «Бусты» в сайдбаре): WANBAN в таблице, галочка в колонке «Скорость», счётчик колонки ≥ 1.
4. Попап на `/squads`: hover по WANBAN → в попапе чип «Скорость» (WANBAN должен состоять в отряде — он лидер в сиде).
5. Voin (Member): `/boosts` открывается (read-only, без ошибок); публичный профиль WANBAN — карточка «Нужные бусты» с чипом «Скорость».
6. Пустое состояние: снять все отметки WANBAN → на `/boosts` при отсутствии других отметок — «Пока никто не отметил нужные бусты» (или строка WANBAN исчезла).

Expected: все проверки PASS, скриншот сводной страницы и попапа в скретчпад.

- [ ] **Step 6: Commit (если были фиксы) + ledger**

Обновить `.superpowers/sdd/progress.md` записью об этапе.
