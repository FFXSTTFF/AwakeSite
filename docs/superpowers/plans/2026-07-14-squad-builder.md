# Билдер отрядов (этап 2) — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Офицер собирает отряды по 5 человек драг-н-дропом из пула, видя по каждому бойцу 5 флагов инвентаря и КД.

**Architecture:** Один агрегирующий запрос `GET /api/squads/builder` (составы + пул + флаги из инвентаря этапа 1 + КД из снапшотов) и одна команда `POST /api/squads/{id}/move-member` (идемпотентный перенос с авто-снятием из старого отряда). Фронт: страница `/squads/builder` на @dnd-kit/core c кнопочным fallback для мобилы.

**Tech Stack:** ASP.NET Core (net10.0), EF Core, MediatR, xUnit+Moq; React + TanStack Query/Router, Tailwind, **@dnd-kit/core (единственная новая зависимость, одобрена спекой)**.

**Spec:** `docs/superpowers/specs/2026-07-14-squad-builder-inventory-design.md` (раздел «Этап 2»). Этап 1 (инвентарь) уже в master.

## Global Constraints

- Ветка `feature/squad-builder` (создана от a8e593f). Один коммит на задачу.
- Новые зависимости: ТОЛЬКО `@dnd-kit/core` на фронте (Task 4). NuGet — ничего.
- Права: билдер (query и move) — `RankAuthorize(UserRank.Officer)`. Существующие эндпоинты отрядов (Colonel+) не трогать.
- Лимит отряда 5 и «один игрок — один отряд» уже enforced в AddSquadMemberCommandHandler — не дублировать логику, переиспользовать репозиторий.
- Пул = пользователи ранга Member+ БЕЗ отряда. КД = `PlayerStatsSnapshot.KdRatio` по `GameNickname`; нет ника или снапшота → `kd: null` → на UI «—».
- Флаги — только через `PlayerFlagsCalculator` этапа 1, никакой второй реализации.
- RU-тексты; палитра/HSL-токены не трогаются; тёмная тема.
- `dotnet test` — 97 существующих остаются зелёными; tsc/build фронта — 0 ошибок.
- Цепочки команд — через Bash-инструмент (PowerShell 5.1 не понимает `&&`).

---

### Task 1: Bulk-методы репозиториев + уникальный индекс SquadMember.UserId

**Files:**
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerInventoryRepository.cs`
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerBuildProofRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/PlayerInventoryRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/PlayerBuildProofRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/AppDbContext.cs` (индекс)
- Create (генерируется): `src/Awake.Infrastructure/Persistence/Migrations/*_SquadMemberUserUnique.cs`

**Interfaces:**
- Produces (для Task 2):
  - `IPlayerInventoryRepository.GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)` → `Task<IReadOnlyList<PlayerInventoryItem>>`
  - `IPlayerBuildProofRepository.GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)` → `Task<IReadOnlyList<PlayerBuildProof>>` (без Image — projection как в GetByUserAsync)

- [ ] **Step 1: Интерфейсы — добавить по методу**

В `IPlayerInventoryRepository.cs` после `GetByUserAsync`:
```csharp
    Task<IReadOnlyList<PlayerInventoryItem>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
```
В `IPlayerBuildProofRepository.cs` после `GetByUserAsync` (тот же контракт «без Image»):
```csharp
    /// <summary>Без поля Image — только метаданные (как GetByUserAsync).</summary>
    Task<IReadOnlyList<PlayerBuildProof>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
```

- [ ] **Step 2: Реализации**

`PlayerInventoryRepository.cs`:
```csharp
    public async Task<IReadOnlyList<PlayerInventoryItem>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerInventoryItems
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(ct);
```
`PlayerBuildProofRepository.cs` (та же projection, что в GetByUserAsync):
```csharp
    public async Task<IReadOnlyList<PlayerBuildProof>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await context.PlayerBuildProofs
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new PlayerBuildProof
            {
                Id = x.Id,
                UserId = x.UserId,
                BuildType = x.BuildType,
                ContentType = x.ContentType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
            })
            .ToListAsync(ct);
```

- [ ] **Step 3: Уникальный индекс «один игрок — один отряд»**

В `AppDbContext.OnModelCreating` найти конфигурацию `SquadMember` (если её нет — добавить блок рядом с новыми) и добавить:
```csharp
        modelBuilder.Entity<SquadMember>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
        });
```
(Если для SquadMember уже есть конфигурация в файле или в отдельном IEntityTypeConfiguration — добавить индекс туда, не создавая второй блок.)

- [ ] **Step 4: Миграция с зачисткой дублей**

```bash
dotnet ef migrations add SquadMemberUserUnique --project src/Awake.Infrastructure --startup-project src/Awake.API
```
В сгенерированной миграции ПЕРЕД `CreateIndex` вставить зачистку возможных дублей (страховка — приложение их не создаёт, но индекс не должен упасть на старых данных):
```csharp
            migrationBuilder.Sql("""
                DELETE FROM "SquadMembers" sm
                USING "SquadMembers" newer
                WHERE sm."UserId" = newer."UserId"
                  AND sm."JoinedAt" < newer."JoinedAt";
                """);
```

- [ ] **Step 5: Сборка + тесты + Commit**

Run: `dotnet build --nologo -v q` → 0 ошибок; `dotnet test --nologo -v q` → 97/97.
```bash
git add src
git commit -m "feat(api): bulk inventory lookups, unique squad membership index"
```

---

### Task 2: GetSquadBuilderQuery (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQuery.cs`
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs`
- Create: `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/SquadBuilderDtos.cs`
- Test: `tests/Awake.Unit.Tests/Features/Squads/GetSquadBuilderQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `ISquadRepository.GetAllWithMembersAsync()`; `IUserRepository.GetByMinRankAsync(UserRank.Member)`; `IPlayerInventoryRepository.GetByUserIdsAsync`; `IPlayerBuildProofRepository.GetByUserIdsAsync`; `IItemCacheService.GetById`; `IPlayerStatsSnapshotRepository.GetByNicknamesAsync`; `PlayerFlagsCalculator.Calculate(IEnumerable<ItemDto>, bool, bool)`; `PlayerFlagsDto`.
- Produces (для Task 3–4):
  - `BuilderFighterDto(Guid UserId, string Username, string? GameNickname, string? AvatarUrl, PlayerFlagsDto Flags, double? Kd)`
  - `BuilderSquadDto(Guid Id, string Name, int Number, IReadOnlyList<BuilderFighterDto> Members)`
  - `SquadBuilderDto(IReadOnlyList<BuilderSquadDto> Squads, IReadOnlyList<BuilderFighterDto> Pool)`
  - `GetSquadBuilderQuery()` → `Result<SquadBuilderDto>`

- [ ] **Step 1: DTO и Query**

```csharp
// src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/SquadBuilderDtos.cs
using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public record BuilderFighterDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    string? AvatarUrl,
    PlayerFlagsDto Flags,
    double? Kd);

public record BuilderSquadDto(
    Guid Id,
    string Name,
    int Number,
    IReadOnlyList<BuilderFighterDto> Members);

public record SquadBuilderDto(
    IReadOnlyList<BuilderSquadDto> Squads,
    IReadOnlyList<BuilderFighterDto> Pool);
```

```csharp
// src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQuery.cs
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public record GetSquadBuilderQuery : IRequest<Result<SquadBuilderDto>>;
```

- [ ] **Step 2: Handler**

```csharp
// src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadBuilder;

public class GetSquadBuilderQueryHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadBuilderQuery, Result<SquadBuilderDto>>
{
    public async Task<Result<SquadBuilderDto>> Handle(
        GetSquadBuilderQuery request, CancellationToken cancellationToken)
    {
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);
        var eligible = await userRepository.GetByMinRankAsync(UserRank.Member, cancellationToken);

        // Все бойцы, которые появятся на экране: участники отрядов + пул
        var squadUserIds = squads
            .SelectMany(s => s.Members)
            .Select(m => m.UserId)
            .ToHashSet();
        var allUsers = eligible
            .Concat(squads.SelectMany(s => s.Members).Select(m => m.User))
            .Where(u => u is not null)
            .DistinctBy(u => u.Id)
            .ToList();
        var allIds = allUsers.Select(u => u.Id).ToList();

        var inventories = await inventoryRepository.GetByUserIdsAsync(allIds, cancellationToken);
        var proofs = await proofRepository.GetByUserIdsAsync(allIds, cancellationToken);

        var nicknames = allUsers
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .ToList();
        var snapshots = (await snapshotRepository.GetByNicknamesAsync(nicknames, cancellationToken))
            .ToDictionary(s => s.GameNickname, StringComparer.OrdinalIgnoreCase);

        var itemsByUser = inventories.ToLookup(i => i.UserId);
        var proofsByUser = proofs.ToLookup(p => p.UserId);

        BuilderFighterDto ToFighter(User u)
        {
            var known = itemsByUser[u.Id]
                .Select(entry => itemCache.GetById(entry.ItemId))
                .Where(i => i is not null)
                .Cast<ItemDto>();
            var userProofs = proofsByUser[u.Id].ToList();
            var flags = PlayerFlagsCalculator.Calculate(
                known,
                hasSpeedProof: userProofs.Any(p => p.BuildType == BuildType.Speed),
                hasVitalityProof: userProofs.Any(p => p.BuildType == BuildType.Vitality));

            double? kd = u.GameNickname is not null
                && snapshots.TryGetValue(u.GameNickname, out var snap)
                    ? snap.KdRatio
                    : null;

            return new BuilderFighterDto(u.Id, u.Username, u.GameNickname, u.DiscordAvatarUrl, flags, kd);
        }

        var fightersById = allUsers.ToDictionary(u => u.Id, ToFighter);

        var squadDtos = squads
            .OrderBy(s => s.Number)
            .Select(s => new BuilderSquadDto(
                s.Id, s.Name, s.Number,
                s.Members
                    .Where(m => fightersById.ContainsKey(m.UserId))
                    .Select(m => fightersById[m.UserId])
                    .ToList()))
            .ToList();

        var pool = allUsers
            .Where(u => !squadUserIds.Contains(u.Id))
            .Where(u => u.Rank >= UserRank.Member)
            .OrderByDescending(u => fightersById[u.Id].Kd ?? -1)
            .Select(u => fightersById[u.Id])
            .ToList();

        return Result<SquadBuilderDto>.Success(new SquadBuilderDto(squadDtos, pool));
    }
}
```

Примечание для реализации: `User.Username`, `User.GameNickname`, `User.DiscordAvatarUrl`, `User.Rank` — существующие поля; `SquadMember.User` — навигация, `GetAllWithMembersAsync` должен её включать (проверь Include в SquadRepository; если Include(m => m.User) нет — добавь `.ThenInclude(m => m.User)` в существующий метод, это не ломает других потребителей).

- [ ] **Step 3: Тесты**

```csharp
// tests/Awake.Unit.Tests/Features/Squads/GetSquadBuilderQueryHandlerTests.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads.Queries.GetSquadBuilder;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadBuilderQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadBuilderQueryHandler BuildHandler() => new(
        _squads.Object, _users.Object, _inventory.Object,
        _proofs.Object, _cache.Object, _snapshots.Object);

    private static User MakeUser(UserRank rank = UserRank.Member, string? nickname = null) =>
        new() { Username = "u_" + Guid.NewGuid().ToString("N")[..6], Rank = rank, GameNickname = nickname };

    private void SetupEmptyAux(params Guid[] ids)
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_SplitsUsersIntoSquadsAndPool()
    {
        var inSquad = MakeUser();
        var free = MakeUser();
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = inSquad.Id, User = inSquad }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([inSquad, free]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        var dto = result.Value!;
        dto.Squads.Should().ContainSingle().Which.Members
            .Should().ContainSingle().Which.UserId.Should().Be(inSquad.Id);
        dto.Pool.Should().ContainSingle().Which.UserId.Should().Be(free.Id);
    }

    [Fact]
    public async Task Handle_FlagsAndKd_ComputedPerFighter()
    {
        var user = MakeUser(nickname: "Yap");
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([user]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([new PlayerBuildProof { UserId = user.Id, BuildType = BuildType.Speed }]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 2.5 }]);

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        var fighter = result.Value!.Pool.Should().ContainSingle().Subject;
        fighter.Flags.Bio.Should().BeTrue();
        fighter.Flags.Speed.Should().BeTrue();
        fighter.Flags.Combat.Should().BeFalse();
        fighter.Kd.Should().Be(2.5);
    }

    [Fact]
    public async Task Handle_NoNicknameOrSnapshot_KdNull()
    {
        var noNick = MakeUser();
        var noSnap = MakeUser(nickname: "Ghost");
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([noNick, noSnap]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        result.Value!.Pool.Should().HaveCount(2)
            .And.OnlyContain(f => f.Kd == null);
    }

    [Fact]
    public async Task Handle_GuestNotInPool()
    {
        // GetByMinRankAsync(Member) по контракту не возвращает гостей —
        // но участник отряда с рангом Guest (понижен после добавления) не должен попасть в пул
        var demoted = MakeUser(rank: UserRank.Guest);
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = demoted.Id, User = demoted }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _users.Setup(r => r.GetByMinRankAsync(UserRank.Member, It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadBuilderQuery(), CancellationToken.None);

        result.Value!.Squads.Should().ContainSingle().Which.Members.Should().HaveCount(1);
        result.Value.Pool.Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Прогон + Commit**

Run: `dotnet test --nologo -v q` → 97 + 4 новых, все зелёные.
```bash
git add src tests
git commit -m "feat(api): squad builder aggregate query - fighters with flags and kd"
```

---

### Task 3: MoveSquadMemberCommand + эндпоинты билдера (TDD)

**Files:**
- Create: `src/Awake.Application/Features/Squads/Commands/MoveMember/MoveSquadMemberCommand.cs`
- Create: `src/Awake.Application/Features/Squads/Commands/MoveMember/MoveSquadMemberCommandHandler.cs`
- Modify: `src/Awake.API/Controllers/SquadsController.cs` (два эндпоинта)
- Test: `tests/Awake.Unit.Tests/Features/Squads/MoveSquadMemberCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ISquadRepository` (GetByIdAsync, GetMemberCountAsync, GetMembershipByUserIdAsync, RemoveMemberAsync, AddMemberAsync), `IUserRepository.GetByIdAsync`, DTO Task 2.
- Produces (для Task 4):
  - `MoveSquadMemberCommand(Guid SquadId, Guid UserId)` → `Result<bool>`
  - `GET  /api/squads/builder` → `SquadBuilderDto` (Officer+)
  - `POST /api/squads/{id:guid}/move-member` body `{ "userId": "..." }` → 204/400 (Officer+)
  - `DELETE /api/squads/{id:guid}/builder-members/{userId:guid}` → 204/400 (Officer+, переиспользует существующую RemoveSquadMemberCommand; отдельный маршрут, потому что существующий DELETE members Colonel+ и его гейт не трогаем)

- [ ] **Step 1: Команда и хэндлер**

```csharp
// src/Awake.Application/Features/Squads/Commands/MoveMember/MoveSquadMemberCommand.cs
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.MoveMember;

/// <summary>Переносит игрока в отряд, автоматически убирая из текущего. Идемпотентна.</summary>
public record MoveSquadMemberCommand(Guid SquadId, Guid UserId) : IRequest<Result<bool>>;
```

```csharp
// src/Awake.Application/Features/Squads/Commands/MoveMember/MoveSquadMemberCommandHandler.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.Entities;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.MoveMember;

public class MoveSquadMemberCommandHandler(
    ISquadRepository squadRepository,
    IUserRepository userRepository
) : IRequestHandler<MoveSquadMemberCommand, Result<bool>>
{
    public const int MaxSquadSize = 5;

    public async Task<Result<bool>> Handle(
        MoveSquadMemberCommand request, CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<bool>.Failure("Отряд не найден.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<bool>.Failure("Пользователь не найден.");

        var current = await squadRepository.GetMembershipByUserIdAsync(request.UserId, cancellationToken);
        if (current is not null && current.SquadId == request.SquadId)
            return Result<bool>.Success(false); // уже там — no-op

        var count = await squadRepository.GetMemberCountAsync(request.SquadId, cancellationToken);
        if (count >= MaxSquadSize)
            return Result<bool>.Failure("Отряд укомплектован (5/5).");

        if (current is not null)
            await squadRepository.RemoveMemberAsync(current.SquadId, request.UserId, cancellationToken);

        await squadRepository.AddMemberAsync(new SquadMember
        {
            SquadId = request.SquadId,
            UserId = request.UserId,
            IsLeader = false,
        }, cancellationToken);

        return Result<bool>.Success(true);
    }
}
```

- [ ] **Step 2: Эндпоинты в SquadsController**

Добавить в начало файла using'и:
```csharp
using Awake.Application.Features.Squads.Commands.MoveMember;
using Awake.Application.Features.Squads.Queries.GetSquadBuilder;
```
Record рядом с существующими:
```csharp
public record MoveMemberRequest(Guid UserId);
```
Экшены в конец класса:
```csharp
    // ── Билдер отрядов (Officer+, спека этапа 2) ────────────────────────────

    [HttpGet("builder")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> GetBuilder(CancellationToken ct)
    {
        var result = await sender.Send(new GetSquadBuilderQuery(), ct);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/move-member")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> MoveMember(Guid id, MoveMemberRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new MoveSquadMemberCommand(id, request.UserId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    // Удаление в пул из билдера: существующий DELETE members остаётся Colonel+,
    // билдер по спеке редактируют Officer+ — отдельный маршрут с тем же хэндлером
    [HttpDelete("{id:guid}/builder-members/{userId:guid}")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> RemoveMemberFromBuilder(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveSquadMemberCommand(id, userId), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
```
ВАЖНО: `[HttpGet("builder")]` должен объявляться так (литеральный сегмент), маршрут `{id:guid}` его не перехватит благодаря constraint.

- [ ] **Step 3: Тесты**

```csharp
// tests/Awake.Unit.Tests/Features/Squads/MoveSquadMemberCommandHandlerTests.cs
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Commands.MoveMember;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class MoveSquadMemberCommandHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Guid _squadId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private MoveSquadMemberCommandHandler BuildHandler() => new(_squads.Object, _users.Object);

    private void SetupBase(int targetCount = 0, SquadMember? currentMembership = null)
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Squad { Name = "Alpha", Number = 1 });
        _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Username = "bob" });
        _squads.Setup(r => r.GetMembershipByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(currentMembership);
        _squads.Setup(r => r.GetMemberCountAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(targetCount);
    }

    [Fact]
    public async Task Handle_FromPool_AddsWithoutRemove()
    {
        SetupBase();

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.Value.Should().BeTrue();
        _squads.Verify(r => r.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _squads.Verify(r => r.AddMemberAsync(
            It.Is<SquadMember>(m => m.SquadId == _squadId && m.UserId == _userId && !m.IsLeader),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FromOtherSquad_RemovesThenAdds()
    {
        var oldSquadId = Guid.NewGuid();
        SetupBase(currentMembership: new SquadMember { SquadId = oldSquadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.Value.Should().BeTrue();
        _squads.Verify(r => r.RemoveMemberAsync(oldSquadId, _userId, It.IsAny<CancellationToken>()), Times.Once);
        _squads.Verify(r => r.AddMemberAsync(It.IsAny<SquadMember>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameSquad_NoOp()
    {
        SetupBase(currentMembership: new SquadMember { SquadId = _squadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        _squads.Verify(r => r.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _squads.Verify(r => r.AddMemberAsync(It.IsAny<SquadMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TargetFull_Fails_WithoutRemoving()
    {
        var oldSquadId = Guid.NewGuid();
        SetupBase(targetCount: 5, currentMembership: new SquadMember { SquadId = oldSquadId, UserId = _userId });

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Отряд укомплектован (5/5).");
        _squads.Verify(r => r.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SquadNotFound_Fails()
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((Squad?)null);

        var result = await BuildHandler().Handle(
            new MoveSquadMemberCommand(_squadId, _userId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

- [ ] **Step 4: Прогон + Commit**

Run: `dotnet test --nologo -v q` → все зелёные (+5).
```bash
git add src tests
git commit -m "feat(api): move squad member command and builder endpoints"
```

---

### Task 4: Фронтенд — страница билдера с драг-н-дропом

**Files:**
- Modify: `frontend/awake-web/package.json` (+`@dnd-kit/core` — через `npm install @dnd-kit/core`)
- Modify: `frontend/awake-web/src/types/api.ts` (типы билдера)
- Create: `frontend/awake-web/src/api/squadBuilder.ts`
- Create: `frontend/awake-web/src/components/builder/FighterCard.tsx`
- Create: `frontend/awake-web/src/routes/_auth.squads.builder.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.squads.tsx` (кнопка «Собрать отряды» для Officer+)

**Interfaces:**
- Consumes: `GET /api/squads/builder` → `{ squads: [{id,name,number,members:[Fighter]}], pool: [Fighter] }`, `Fighter = { userId, username, gameNickname, avatarUrl, flags: PlayerFlags, kd }`; `POST /api/squads/{id}/move-member { userId }` → 204; `DELETE /api/squads/{id}/builder-members/{userId}` → 204 (все три — Officer+, из Task 3). Существующий `DELETE /api/squads/{id}/members/{userId}` (Colonel+) в билдере НЕ используется.
- Produces: страница `/squads/builder`.

- [ ] **Step 1: Зависимость**

```bash
cd frontend/awake-web && npm install @dnd-kit/core
```
Проверить: в package.json появился `@dnd-kit/core` (^6.x), ничего больше.

- [ ] **Step 2: Типы и api-клиент**

В `types/api.ts`:
```typescript
export interface BuilderFighter {
  userId: string
  username: string
  gameNickname: string | null
  avatarUrl: string | null
  flags: PlayerFlags
  kd: number | null
}

export interface BuilderSquad {
  id: string
  name: string
  number: number
  members: BuilderFighter[]
}

export interface SquadBuilderData {
  squads: BuilderSquad[]
  pool: BuilderFighter[]
}
```

```typescript
// frontend/awake-web/src/api/squadBuilder.ts
import { apiClient } from './client'
import type { SquadBuilderData } from '@/types/api'

export const squadBuilderApi = {
  get: (): Promise<SquadBuilderData> => apiClient.get('/squads/builder'),
  moveMember: (squadId: string, userId: string): Promise<void> =>
    apiClient.post(`/squads/${squadId}/move-member`, { userId }),
  removeMember: (squadId: string, userId: string): Promise<void> =>
    apiClient.delete(`/squads/${squadId}/builder-members/${userId}`),
}
```

- [ ] **Step 3: FighterCard (draggable, с попапом флагов и КД)**

```tsx
// frontend/awake-web/src/components/builder/FighterCard.tsx
import { useDraggable } from '@dnd-kit/core'
import { CSS } from '@dnd-kit/utilities'
import { InventoryFlags } from '@/components/InventoryFlags'
import { cn } from '@/lib/utils'
import type { BuilderFighter } from '@/types/api'

// @dnd-kit/utilities приходит транзитивно с @dnd-kit/core; если import падает —
// заменить style на: transform ? { transform: `translate(${transform.x}px, ${transform.y}px)` } : undefined

export function FighterCard({ fighter }: { fighter: BuilderFighter }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: fighter.userId,
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={transform ? { transform: CSS.Translate.toString(transform) } : undefined}
      className={cn(
        'group relative flex cursor-grab items-center gap-2 rounded-lg border border-border bg-card px-2.5 py-2 text-sm transition-colors hover:border-accent/30',
        isDragging && 'z-50 opacity-70 shadow-lg',
      )}
    >
      {fighter.avatarUrl ? (
        <img src={fighter.avatarUrl} alt="" className="h-6 w-6 shrink-0 rounded-full" />
      ) : (
        <span className="h-6 w-6 shrink-0 rounded-full bg-secondary" />
      )}
      <span className="min-w-0 flex-1 truncate font-medium">
        {fighter.gameNickname ?? fighter.username}
      </span>
      <InventoryFlags flags={fighter.flags} size="sm" />

      {/* Ховер-попап: расшифровка + КД */}
      <div className="pointer-events-none absolute left-1/2 top-full z-40 mt-1 hidden w-56 -translate-x-1/2 rounded-lg border border-border bg-popover p-3 text-xs shadow-xl group-hover:block">
        <p className="font-semibold">{fighter.gameNickname ?? fighter.username}</p>
        <p className="mt-1 text-muted-foreground">
          КД: <span className="font-bold text-foreground">{fighter.kd != null ? fighter.kd.toLocaleString('ru-RU', { maximumFractionDigits: 2 }) : '—'}</span>
        </p>
        <ul className="mt-2 space-y-0.5 text-muted-foreground">
          <li>{fighter.flags.bio ? '✓' : '✗'} Био-броня</li>
          <li>{fighter.flags.combat ? '✓' : '✗'} Боевая броня</li>
          <li>{fighter.flags.sniper ? '✓' : '✗'} Снайперка</li>
          <li>{fighter.flags.speed ? '✓' : '✗'} Сборка на скорость</li>
          <li>{fighter.flags.vitality ? '✓' : '✗'} Сборка на живучесть</li>
        </ul>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Страница билдера**

```tsx
// frontend/awake-web/src/routes/_auth.squads.builder.tsx
import { createFileRoute, redirect } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { DndContext, DragOverlay, useDroppable, type DragEndEvent, type DragStartEvent } from '@dnd-kit/core'
import { UserMinus, Users } from 'lucide-react'
import { squadBuilderApi } from '@/api/squadBuilder'
import { FighterCard } from '@/components/builder/FighterCard'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import type { BuilderFighter, BuilderSquad } from '@/types/api'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/_auth/squads/builder')({
  beforeLoad: () => {
    if ((useAuthStore.getState().user?.rank ?? 0) < UserRank.Officer) {
      throw redirect({ to: '/squads' })
    }
  },
  component: SquadBuilderPage,
})

const POOL_ID = 'pool'

function SquadBuilderPage() {
  const queryClient = useQueryClient()
  const [active, setActive] = useState<BuilderFighter | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['squads', 'builder'],
    queryFn: squadBuilderApi.get,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['squads', 'builder'] })

  const move = useMutation({
    mutationFn: ({ squadId, userId }: { squadId: string; userId: string }) =>
      squadBuilderApi.moveMember(squadId, userId),
    onSuccess: () => { setError(null); void invalidate() },
    onError: (e: Error) => { setError(e.message); void invalidate() },
  })
  const remove = useMutation({
    mutationFn: ({ squadId, userId }: { squadId: string; userId: string }) =>
      squadBuilderApi.removeMember(squadId, userId),
    onSuccess: () => { setError(null); void invalidate() },
    onError: (e: Error) => { setError(e.message); void invalidate() },
  })

  function findFighter(id: string): BuilderFighter | null {
    if (!data) return null
    return (
      data.pool.find((f) => f.userId === id) ??
      data.squads.flatMap((s) => s.members).find((f) => f.userId === id) ??
      null
    )
  }

  function squadOf(userId: string): BuilderSquad | null {
    return data?.squads.find((s) => s.members.some((m) => m.userId === userId)) ?? null
  }

  function onDragStart(e: DragStartEvent) {
    setActive(findFighter(String(e.active.id)))
  }

  function onDragEnd(e: DragEndEvent) {
    setActive(null)
    const userId = String(e.active.id)
    const target = e.over?.id != null ? String(e.over.id) : null
    if (!target) return

    const from = squadOf(userId)
    if (target === POOL_ID) {
      if (from) remove.mutate({ squadId: from.id, userId })
      return
    }
    if (from?.id === target) return
    move.mutate({ squadId: target, userId })
  }

  if (isLoading || !data) {
    return (
      <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
        <Skeleton className="h-96 rounded-xl" />
        <div className="grid gap-4 sm:grid-cols-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black tracking-tight text-foreground">Билдер отрядов</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Перетащи бойца из пула в отряд. Наведи, чтобы увидеть экипировку и КД.
          </p>
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
      </div>

      <DndContext onDragStart={onDragStart} onDragEnd={onDragEnd}>
        <div className="grid items-start gap-4 lg:grid-cols-[320px_1fr]">
          <PoolColumn fighters={data.pool} squads={data.squads} onMove={(squadId, userId) => move.mutate({ squadId, userId })} />
          <div className="grid gap-4 sm:grid-cols-2">
            {data.squads.map((squad) => (
              <SquadCard
                key={squad.id}
                squad={squad}
                onRemove={(userId) => remove.mutate({ squadId: squad.id, userId })}
              />
            ))}
            {data.squads.length === 0 && (
              <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
            )}
          </div>
        </div>
        <DragOverlay>{active ? <FighterCard fighter={active} /> : null}</DragOverlay>
      </DndContext>
    </div>
  )
}

function PoolColumn({
  fighters,
  squads,
  onMove,
}: {
  fighters: BuilderFighter[]
  squads: BuilderSquad[]
  onMove: (squadId: string, userId: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: POOL_ID })
  const [search, setSearch] = useState('')

  const filtered = fighters.filter((f) =>
    (f.gameNickname ?? f.username).toLowerCase().includes(search.toLowerCase()),
  )

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'rounded-xl border border-border bg-card p-3 transition-colors',
        isOver && 'border-accent/50 bg-accent/5',
      )}
    >
      <p className="mb-2 text-sm font-semibold">Пул ({fighters.length})</p>
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Поиск по нику…"
        className="mb-3 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm outline-none focus:border-accent/50"
      />
      <div className="space-y-1.5">
        {filtered.length === 0 ? (
          <p className="py-4 text-center text-xs text-muted-foreground">
            {fighters.length === 0 ? 'Все бойцы распределены.' : 'Никого не нашлось.'}
          </p>
        ) : (
          filtered.map((f) => (
            <div key={f.userId} className="space-y-1">
              <FighterCard fighter={f} />
              {/* мобильный fallback без перетаскивания */}
              <MobileAssign fighter={f} squads={squads} onMove={onMove} />
            </div>
          ))
        )}
      </div>
    </div>
  )
}

function MobileAssign({
  fighter,
  squads,
  onMove,
}: {
  fighter: BuilderFighter
  squads: BuilderSquad[]
  onMove: (squadId: string, userId: string) => void
}) {
  return (
    <select
      aria-label={`Назначить ${fighter.gameNickname ?? fighter.username} в отряд`}
      className="w-full rounded-md border border-border bg-background px-2 py-1 text-xs text-muted-foreground md:hidden"
      value=""
      onChange={(e) => e.target.value && onMove(e.target.value, fighter.userId)}
    >
      <option value="">→ в отряд…</option>
      {squads.map((s) => (
        <option key={s.id} value={s.id} disabled={s.members.length >= 5}>
          {s.name} ({s.members.length}/5)
        </option>
      ))}
    </select>
  )
}

function SquadCard({
  squad,
  onRemove,
}: {
  squad: BuilderSquad
  onRemove: (userId: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: squad.id })
  const full = squad.members.length >= 5

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'rounded-xl border border-border bg-card p-3 transition-colors',
        isOver && !full && 'border-accent/50 bg-accent/5',
        isOver && full && 'border-destructive/50 bg-destructive/5',
      )}
    >
      <div className="mb-2 flex items-center justify-between">
        <p className="flex items-center gap-2 text-sm font-semibold">
          <Users size={14} className="text-accent" />
          {squad.name}
        </p>
        <span className={cn('text-xs font-bold', full ? 'text-accent' : 'text-muted-foreground')}>
          {squad.members.length}/5
        </span>
      </div>
      <div className="space-y-1.5">
        {squad.members.map((m) => (
          <div key={m.userId} className="flex items-center gap-1.5">
            <div className="min-w-0 flex-1">
              <FighterCard fighter={m} />
            </div>
            <button
              type="button"
              aria-label={`Убрать ${m.gameNickname ?? m.username} из отряда`}
              onClick={() => onRemove(m.userId)}
              className="shrink-0 rounded p-1.5 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
            >
              <UserMinus size={14} />
            </button>
          </div>
        ))}
        {squad.members.length === 0 && (
          <p className="py-3 text-center text-xs text-muted-foreground">Перетащи бойцов сюда</p>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Кнопка «Собрать отряды» на странице отрядов**

В `_auth.squads.tsx`: получить ранг (`useAuth()` или `useAuthStore`), рядом с заголовком добавить для Officer+:
```tsx
        {rank >= UserRank.Officer && (
          <Button asChild variant="outline" className="gap-2">
            <Link to="/squads/builder">
              <Wrench size={15} />
              Собрать отряды
            </Link>
          </Button>
        )}
```
(импорты `Link`, `Wrench` из lucide-react, `UserRank`, `Button` — по месту; вписать в существующую разметку заголовка, не ломая скелетоны/пустое состояние.)

- [ ] **Step 6: Проверка + Commit**

```bash
cd frontend/awake-web && npx tsc -b && npm run build
```
Expected: 0 ошибок.
```bash
git add frontend/awake-web
git commit -m "feat(web): squad builder page - drag and drop, fighter flags, kd"
```

---

### Task 5: Финальная проверка этапа

**Files:** нет изменений кода (кроме находок — по согласованию с контроллером).

- [ ] **Step 1: Полный прогон**

```bash
dotnet build --nologo -v q && dotnet test --nologo -v q
cd frontend/awake-web && npx tsc -b && npm run build
```
Expected: 0 ошибок, все тесты зелёные (97 + ~9 новых).

- [ ] **Step 2: Дев-стенд**

Пересобрать api (`docker-compose -p featurestage-4 up -d --build api`), проверить лог миграции SquadMemberUserUnique, smoke: `GET /api/squads/builder` без токена → 401.

- [ ] **Step 3: Чек-лист живой приёмки**

Офицером: страница «Отряды» → кнопка «Собрать отряды»; перетаскивание пул→отряд, отряд→отряд, отряд→пул; лимит 5/5 (подсветка + ошибка); ховер-попап с КД и расшифровкой флагов; поиск в пуле; мобильный селект «→ в отряд»; участником (Member): кнопки нет, /squads/builder редиректит на /squads.
