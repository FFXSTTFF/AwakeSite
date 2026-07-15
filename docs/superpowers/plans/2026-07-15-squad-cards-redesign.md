# Squad Cards Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the `/squads` card grid: show only in-game nicknames, let Officer+ rename squads inline, and let every viewer hover a member to see their armor/sniper/build icons + KD.

**Architecture:** Backend — extract a shared `SquadMemberEnricher` static helper (flags+KD computation, currently only in the Officer-only squad builder) and reuse it in the regular `GetSquadsQuery`/`GetSquadById` handlers so `SquadDto` carries `Flags`/`Kd` for every Member+ viewer; add a `RenameSquadCommand` following the existing `SetSquadLeaderCommand` pattern. Frontend — extract the squad card into its own `SquadCard` component (inline rename state needs per-card hooks, which a `.map()` callback can't safely own), add a viewport-clamped `MemberHoverInfo` hover popup (same clamping idea as the already-shipped `NotificationBell`, but its own simpler implementation), and wire nickname-only display.

**Tech Stack:** C# ASP.NET Core 10 (Clean Architecture + CQRS/MediatR, FluentValidation, EF Core), React 19 + TanStack Router/Query + Tailwind, xUnit + Moq + FluentAssertions for backend tests (no frontend unit test runner in this repo — frontend tasks verify via `tsc -b` + `eslint` + a live Playwright acceptance script, matching this project's established pattern).

## Global Constraints

- Squad name max length 100 chars (matches `SquadConfiguration.Property(x => x.Name).HasMaxLength(100)`).
- Rename endpoint gated `[RankAuthorize(UserRank.Officer)]` (Officer/Colonel/Leader).
- `/squads` list endpoint stays `[RankAuthorize(UserRank.Member)]` — no rank widening, just richer payload.
- Member without a linked `gameNickname` falls back to Discord `username` everywhere nickname is displayed on the card.
- No changes to `/squads/$squadId` (detail page) UI — it keeps showing Discord name + nickname as today, even though its DTO will now also carry `Flags`/`Kd` (unused by that page's UI).
- No new i18n keys — this file already mixes `t('squads.*')` calls with hardcoded Russian strings; new UI text follows the hardcoded-Russian convention already used for "Собрать отряды" / "Отрядов пока нет." in this file.

---

### Task 1: `SquadMemberEnricher` — extract shared flags+KD computation

**Files:**
- Create: `src/Awake.Application/Features/Squads/SquadMemberEnricher.cs`
- Test: `tests/Awake.Unit.Tests/Features/Squads/SquadMemberEnricherTests.cs`

**Interfaces:**
- Consumes: `Awake.Application.Common.Interfaces.IItemCacheService.GetById(string id) : ItemDto?`; `Awake.Application.Common.Interfaces.Repositories.IPlayerInventoryRepository.GetByUserIdsAsync(IReadOnlyCollection<Guid>, CancellationToken) : Task<IReadOnlyList<PlayerInventoryItem>>`; `IPlayerBuildProofRepository.GetByUserIdsAsync(...) : Task<IReadOnlyList<PlayerBuildProof>>`; `IPlayerStatsSnapshotRepository.GetByNicknamesAsync(IReadOnlyCollection<string>, CancellationToken) : Task<IReadOnlyList<PlayerStatsSnapshot>>`; `Awake.Application.Features.Inventory.PlayerFlagsCalculator.Calculate(IEnumerable<ItemDto>, bool hasSpeedProof, bool hasVitalityProof) : PlayerFlagsDto`; `Awake.Domain.Entities.User` (`Id`, `GameNickname`).
- Produces: `SquadMemberEnricher.ComputeAsync(IReadOnlyList<User> users, IPlayerInventoryRepository, IPlayerBuildProofRepository, IItemCacheService, IPlayerStatsSnapshotRepository, CancellationToken ct = default) : Task<IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd)>>` — used by Task 2 (refactor), Task 5, Task 6.

- [ ] **Step 1: Write the failing test**

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class SquadMemberEnricherTests
{
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private void SetupEmpty()
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task ComputeAsync_FlagsAndKd_ComputedPerUser()
    {
        var user = new User { Username = "u1", Rank = UserRank.Member, GameNickname = "Yap" };
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([new PlayerBuildProof { UserId = user.Id, BuildType = BuildType.Speed }]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 2.5 }]);

        var result = await SquadMemberEnricher.ComputeAsync(
            [user], _inventory.Object, _proofs.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[user.Id].Flags.Bio.Should().BeTrue();
        result[user.Id].Flags.Speed.Should().BeTrue();
        result[user.Id].Flags.Combat.Should().BeFalse();
        result[user.Id].Kd.Should().Be(2.5);
    }

    [Fact]
    public async Task ComputeAsync_NoNicknameOrSnapshot_KdNull()
    {
        var noNick = new User { Username = "u1", Rank = UserRank.Member };
        var noSnap = new User { Username = "u2", Rank = UserRank.Member, GameNickname = "Ghost" };
        SetupEmpty();

        var result = await SquadMemberEnricher.ComputeAsync(
            [noNick, noSnap], _inventory.Object, _proofs.Object, _cache.Object, _snapshots.Object, CancellationToken.None);

        result[noNick.Id].Kd.Should().BeNull();
        result[noSnap.Id].Kd.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Awake.Unit.Tests --filter SquadMemberEnricherTests`
Expected: FAIL (build error — `Awake.Application.Features.Squads.SquadMemberEnricher` does not exist)

- [ ] **Step 3: Write the implementation**

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Inventory;
using Awake.Application.Features.Inventory.Dtos;
using Awake.Application.Features.Items.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;

namespace Awake.Application.Features.Squads;

public static class SquadMemberEnricher
{
    public static async Task<IReadOnlyDictionary<Guid, (PlayerFlagsDto Flags, double? Kd)>> ComputeAsync(
        IReadOnlyList<User> users,
        IPlayerInventoryRepository inventoryRepository,
        IPlayerBuildProofRepository proofRepository,
        IItemCacheService itemCache,
        IPlayerStatsSnapshotRepository snapshotRepository,
        CancellationToken ct = default)
    {
        var ids = users.Select(u => u.Id).ToList();
        var inventories = await inventoryRepository.GetByUserIdsAsync(ids, ct);
        var proofs = await proofRepository.GetByUserIdsAsync(ids, ct);

        var nicknames = users
            .Where(u => !string.IsNullOrEmpty(u.GameNickname))
            .Select(u => u.GameNickname!)
            .ToList();
        var snapshots = (await snapshotRepository.GetByNicknamesAsync(nicknames, ct))
            .ToDictionary(s => s.GameNickname, StringComparer.OrdinalIgnoreCase);

        var itemsByUser = inventories.ToLookup(i => i.UserId);
        var proofsByUser = proofs.ToLookup(p => p.UserId);

        return users.ToDictionary(u => u.Id, u =>
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

            return (flags, kd);
        });
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Awake.Unit.Tests --filter SquadMemberEnricherTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/Awake.Application/Features/Squads/SquadMemberEnricher.cs tests/Awake.Unit.Tests/Features/Squads/SquadMemberEnricherTests.cs
git commit -m "feat: extract SquadMemberEnricher for shared flags+KD computation"
```

---

### Task 2: Refactor `GetSquadBuilderQueryHandler` to use `SquadMemberEnricher`

**Files:**
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs`
- Test (unchanged, must still pass): `tests/Awake.Unit.Tests/Features/Squads/GetSquadBuilderQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `SquadMemberEnricher.ComputeAsync(...)` from Task 1.
- Produces: same `IRequestHandler<GetSquadBuilderQuery, Result<SquadBuilderDto>>` contract as before — no change visible to callers.

- [ ] **Step 1: Run the existing tests to confirm current baseline passes**

Run: `dotnet test tests/Awake.Unit.Tests --filter GetSquadBuilderQueryHandlerTests`
Expected: PASS (4 tests) — this is the regression safety net for the refactor below.

- [ ] **Step 2: Replace the handler body to delegate to the enricher**

Replace the full contents of `src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs` with:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
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

        var enriched = await SquadMemberEnricher.ComputeAsync(
            allUsers, inventoryRepository, proofRepository, itemCache, snapshotRepository, cancellationToken);

        BuilderFighterDto ToFighter(User u) =>
            new(u.Id, u.Username, u.GameNickname, u.DiscordAvatarUrl, enriched[u.Id].Flags, enriched[u.Id].Kd);

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

- [ ] **Step 3: Run the existing tests again to confirm the refactor is behavior-preserving**

Run: `dotnet test tests/Awake.Unit.Tests --filter GetSquadBuilderQueryHandlerTests`
Expected: PASS (4 tests, same as Step 1 — output unchanged)

- [ ] **Step 4: Commit**

```bash
git add src/Awake.Application/Features/Squads/Queries/GetSquadBuilder/GetSquadBuilderQueryHandler.cs
git commit -m "refactor: GetSquadBuilderQueryHandler delegates flags+KD to SquadMemberEnricher"
```

---

### Task 3: `ISquadRepository.UpdateAsync` + `SquadRepository` implementation

**Files:**
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/ISquadRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/SquadRepository.cs`

**Interfaces:**
- Produces: `ISquadRepository.UpdateAsync(Squad squad, CancellationToken ct = default) : Task` — used by Task 4's `RenameSquadCommandHandler`.

- [ ] **Step 1: Add the method to the interface**

In `src/Awake.Application/Common/Interfaces/Repositories/ISquadRepository.cs`, add this line inside the interface body (after `UpdateMemberAsync`):

```csharp
    Task UpdateAsync(Squad squad, CancellationToken ct = default);
```

- [ ] **Step 2: Implement it in the repository**

In `src/Awake.Infrastructure/Persistence/Repositories/SquadRepository.cs`, add this method (after `UpdateMemberAsync`):

```csharp
    public async Task UpdateAsync(Squad squad, CancellationToken ct = default)
    {
        context.Squads.Update(squad);
        await context.SaveChangesAsync(ct);
    }
```

- [ ] **Step 3: Build to confirm it compiles**

Run: `dotnet build src/Awake.API`
Expected: Build succeeded, 0 errors (this interface change has no other implementers to update — `SquadRepository` is the only one)

- [ ] **Step 4: Commit**

```bash
git add src/Awake.Application/Common/Interfaces/Repositories/ISquadRepository.cs src/Awake.Infrastructure/Persistence/Repositories/SquadRepository.cs
git commit -m "feat: add ISquadRepository.UpdateAsync for squad rename"
```

---

### Task 4: `RenameSquadCommand` + validator + handler

**Files:**
- Create: `src/Awake.Application/Features/Squads/Commands/RenameSquad/RenameSquadCommand.cs`
- Create: `src/Awake.Application/Features/Squads/Commands/RenameSquad/RenameSquadCommandValidator.cs`
- Create: `src/Awake.Application/Features/Squads/Commands/RenameSquad/RenameSquadCommandHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Squads/RenameSquadCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ISquadRepository.GetByIdAsync(Guid, CancellationToken) : Task<Squad?>` (existing); `ISquadRepository.UpdateAsync(Squad, CancellationToken)` from Task 3.
- Produces: `RenameSquadCommand(Guid SquadId, string Name) : IRequest<Result<Unit>>` — used by Task 6 (controller endpoint).

- [ ] **Step 1: Write the failing test**

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Commands.RenameSquad;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class RenameSquadCommandHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Guid _squadId = Guid.NewGuid();

    private RenameSquadCommandHandler BuildHandler() => new(_squads.Object);

    [Fact]
    public async Task Handle_ValidName_TrimsAndPersists()
    {
        var squad = new Squad { Name = "Отряд 1", Number = 1 };
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync(squad);

        var result = await BuildHandler().Handle(
            new RenameSquadCommand(_squadId, "  Ночная смена  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        squad.Name.Should().Be("Ночная смена");
        _squads.Verify(r => r.UpdateAsync(squad, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SquadNotFound_Fails()
    {
        _squads.Setup(r => r.GetByIdAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync((Squad?)null);

        var result = await BuildHandler().Handle(
            new RenameSquadCommand(_squadId, "Ночная смена"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _squads.Verify(r => r.UpdateAsync(It.IsAny<Squad>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Awake.Unit.Tests --filter RenameSquadCommandHandlerTests`
Expected: FAIL (build error — `RenameSquadCommand`/`RenameSquadCommandHandler` do not exist)

- [ ] **Step 3: Write the command record**

```csharp
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public record RenameSquadCommand(Guid SquadId, string Name) : IRequest<Result<Unit>>;
```

- [ ] **Step 4: Write the validator**

```csharp
using FluentValidation;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public class RenameSquadCommandValidator : AbstractValidator<RenameSquadCommand>
{
    public RenameSquadCommandValidator()
    {
        RuleFor(x => x.SquadId).NotEmpty().WithMessage("ID отряда обязателен.");
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Название отряда обязательно.")
            .MaximumLength(100).WithMessage("Название отряда не длиннее 100 символов.");
    }
}
```

- [ ] **Step 5: Write the handler**

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.RenameSquad;

public class RenameSquadCommandHandler(ISquadRepository squadRepository)
    : IRequestHandler<RenameSquadCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(RenameSquadCommand request, CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdAsync(request.SquadId, cancellationToken);
        if (squad is null)
            return Result<Unit>.Failure("Отряд не найден.");

        squad.Name = request.Name.Trim();
        await squadRepository.UpdateAsync(squad, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/Awake.Unit.Tests --filter RenameSquadCommandHandlerTests`
Expected: PASS (2 tests)

- [ ] **Step 7: Commit**

```bash
git add src/Awake.Application/Features/Squads/Commands/RenameSquad tests/Awake.Unit.Tests/Features/Squads/RenameSquadCommandHandlerTests.cs
git commit -m "feat: add RenameSquadCommand (Officer+ squad rename)"
```

---

### Task 5: `SquadsController` rename endpoint

**Files:**
- Modify: `src/Awake.API/Controllers/SquadsController.cs`

**Interfaces:**
- Consumes: `RenameSquadCommand` from Task 4.
- Produces: `PUT /api/squads/{id:guid}/name` — used by Task 8's `squadsApi.rename`.

- [ ] **Step 1: Add the request record and using directive**

In `src/Awake.API/Controllers/SquadsController.cs`, add to the `using` block:

```csharp
using Awake.Application.Features.Squads.Commands.RenameSquad;
```

Add next to the other request records (`AddMemberRequest`, `SetLeaderRequest`, `MoveMemberRequest`):

```csharp
public record RenameSquadRequest(string Name);
```

- [ ] **Step 2: Add the endpoint**

Add inside the `SquadsController` class, after the `SetLeader` action:

```csharp
    [HttpPut("{id:guid}/name")]
    [RankAuthorize(UserRank.Officer)]
    public async Task<IActionResult> Rename(Guid id, RenameSquadRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RenameSquadCommand(id, request.Name), ct);
        return result.IsSuccess
            ? NoContent()
            : Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Awake.API`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Awake.API/Controllers/SquadsController.cs
git commit -m "feat: expose PUT /api/squads/{id}/name endpoint"
```

---

### Task 6: `SquadDto`/`SquadMemberDto` gain Flags+Kd; `GetSquadsQueryHandler` and `GetSquadByIdQueryHandler` populate them

**Files:**
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquads/SquadDto.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquads/GetSquadsQueryHandler.cs`
- Modify: `src/Awake.Application/Features/Squads/Queries/GetSquadById/GetSquadByIdQueryHandler.cs`
- Test: `tests/Awake.Unit.Tests/Features/Squads/GetSquadsQueryHandlerTests.cs` (new)
- Test: `tests/Awake.Unit.Tests/Features/Squads/GetSquadByIdQueryHandlerTests.cs` (new)

**Interfaces:**
- Consumes: `SquadMemberEnricher.ComputeAsync(...)` from Task 1.
- Produces: `SquadMemberDto` now has `Flags: PlayerFlagsDto` and `Kd: double?` — the JSON contract Task 7 (frontend types) mirrors as `flags: PlayerFlags` / `kd: number | null`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Awake.Unit.Tests/Features/Squads/GetSquadsQueryHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Items.Dtos;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadsQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetSquadsQueryHandler BuildHandler() => new(
        _squads.Object, _inventory.Object, _proofs.Object, _cache.Object, _snapshots.Object);

    private void SetupEmptyAux()
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_ReturnsMembersWithFlagsAndKd()
    {
        var user = new User { Username = "u1", GameNickname = "Yap" };
        var squad = new Squad
        {
            Name = "Alpha", Number = 2,
            Members = [new SquadMember { UserId = user.Id, User = user, IsLeader = true }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerInventoryItem { UserId = user.Id, ItemId = "skif5" }]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _cache.Setup(c => c.GetById("skif5"))
              .Returns(new ItemDto("skif5", "armor/combined", "Скиф-5", "i.png", "RANK_MASTER"));
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new PlayerStatsSnapshot { GameNickname = "Yap", KdRatio = 1.8 }]);

        var result = await BuildHandler().Handle(new GetSquadsQuery(), CancellationToken.None);

        var member = result.Should().ContainSingle().Which.Members.Should().ContainSingle().Subject;
        member.Flags.Bio.Should().BeTrue();
        member.Kd.Should().Be(1.8);
    }

    [Fact]
    public async Task Handle_MemberWithoutNicknameOrSnapshot_KdNull()
    {
        var user = new User { Username = "u1" };
        var squad = new Squad
        {
            Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = user.Id, User = user }],
        };
        _squads.Setup(r => r.GetAllWithMembersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([squad]);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadsQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Members.Should().ContainSingle()
            .Which.Kd.Should().BeNull();
    }
}
```

Create `tests/Awake.Unit.Tests/Features/Squads/GetSquadByIdQueryHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquadById;
using Awake.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Squads;

public class GetSquadByIdQueryHandlerTests
{
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<IPlayerInventoryRepository> _inventory = new();
    private readonly Mock<IPlayerBuildProofRepository> _proofs = new();
    private readonly Mock<IItemCacheService> _cache = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();
    private readonly Guid _squadId = Guid.NewGuid();

    private GetSquadByIdQueryHandler BuildHandler() => new(
        _squads.Object, _inventory.Object, _proofs.Object, _cache.Object, _snapshots.Object);

    private void SetupEmptyAux()
    {
        _inventory.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        _proofs.Setup(r => r.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);
        _snapshots.Setup(r => r.GetByNicknamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_ReturnsMemberWithFlags()
    {
        var user = new User { Username = "u1" };
        var squad = new Squad
        {
            Id = _squadId, Name = "Alpha", Number = 1,
            Members = [new SquadMember { UserId = user.Id, User = user }],
        };
        _squads.Setup(r => r.GetByIdWithMembersAsync(_squadId, It.IsAny<CancellationToken>())).ReturnsAsync(squad);
        SetupEmptyAux();

        var result = await BuildHandler().Handle(new GetSquadByIdQuery(_squadId), CancellationToken.None);

        result.Members.Should().ContainSingle().Which.Flags.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Awake.Unit.Tests --filter "GetSquadsQueryHandlerTests|GetSquadByIdQueryHandlerTests"`
Expected: FAIL (compile errors — `SquadMemberDto` has no `Flags`/`Kd` yet, handler constructors don't take the new dependencies yet)

- [ ] **Step 3: Update the DTO**

Replace `src/Awake.Application/Features/Squads/Queries/GetSquads/SquadDto.cs` with:

```csharp
using Awake.Application.Features.Inventory.Dtos;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public record SquadMemberDto(
    Guid UserId,
    string Username,
    string? GameNickname,
    bool IsLeader,
    DateTime JoinedAt,
    PlayerFlagsDto Flags,
    double? Kd);

public record SquadDto(
    Guid Id,
    string Name,
    int Number,
    IReadOnlyList<SquadMemberDto> Members,
    int MemberCount);
```

- [ ] **Step 4: Update `GetSquadsQueryHandler`**

Replace `src/Awake.Application/Features/Squads/Queries/GetSquads/GetSquadsQueryHandler.cs` with:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquads;

public class GetSquadsQueryHandler(
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadsQuery, IReadOnlyList<SquadDto>>
{
    public async Task<IReadOnlyList<SquadDto>> Handle(
        GetSquadsQuery request,
        CancellationToken cancellationToken)
    {
        var squads = await squadRepository.GetAllWithMembersAsync(cancellationToken);
        var allUsers = squads.SelectMany(s => s.Members).Select(m => m.User).ToList();
        var enriched = await SquadMemberEnricher.ComputeAsync(
            allUsers, inventoryRepository, proofRepository, itemCache, snapshotRepository, cancellationToken);

        return squads
            .OrderBy(s => s.Number)
            .Select(s => new SquadDto(
                s.Id,
                s.Name,
                s.Number,
                s.Members
                    .OrderByDescending(m => m.IsLeader)
                    .ThenBy(m => m.JoinedAt)
                    .Select(m => new SquadMemberDto(
                        m.UserId,
                        m.User.Username,
                        m.User.GameNickname,
                        m.IsLeader,
                        m.JoinedAt,
                        enriched[m.UserId].Flags,
                        enriched[m.UserId].Kd))
                    .ToList(),
                s.Members.Count))
            .ToList();
    }
}
```

- [ ] **Step 5: Update `GetSquadByIdQueryHandler`**

Replace `src/Awake.Application/Features/Squads/Queries/GetSquadById/GetSquadByIdQueryHandler.cs` with:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Squads.Queries.GetSquads;
using Awake.Domain.Entities;
using Awake.Domain.Exceptions;
using MediatR;

namespace Awake.Application.Features.Squads.Queries.GetSquadById;

public class GetSquadByIdQueryHandler(
    ISquadRepository squadRepository,
    IPlayerInventoryRepository inventoryRepository,
    IPlayerBuildProofRepository proofRepository,
    IItemCacheService itemCache,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetSquadByIdQuery, SquadDto>
{
    public async Task<SquadDto> Handle(
        GetSquadByIdQuery request,
        CancellationToken cancellationToken)
    {
        var squad = await squadRepository.GetByIdWithMembersAsync(request.SquadId, cancellationToken)
            ?? throw new NotFoundException(nameof(Squad), request.SquadId);

        var users = squad.Members.Select(m => m.User).ToList();
        var enriched = await SquadMemberEnricher.ComputeAsync(
            users, inventoryRepository, proofRepository, itemCache, snapshotRepository, cancellationToken);

        return new SquadDto(
            squad.Id,
            squad.Name,
            squad.Number,
            squad.Members
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.JoinedAt)
                .Select(m => new SquadMemberDto(
                    m.UserId,
                    m.User.Username,
                    m.User.GameNickname,
                    m.IsLeader,
                    m.JoinedAt,
                    enriched[m.UserId].Flags,
                    enriched[m.UserId].Kd))
                .ToList(),
            squad.Members.Count);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Awake.Unit.Tests --filter "GetSquadsQueryHandlerTests|GetSquadByIdQueryHandlerTests"`
Expected: PASS (3 tests)

- [ ] **Step 7: Commit**

```bash
git add src/Awake.Application/Features/Squads/Queries/GetSquads/SquadDto.cs src/Awake.Application/Features/Squads/Queries/GetSquads/GetSquadsQueryHandler.cs src/Awake.Application/Features/Squads/Queries/GetSquadById/GetSquadByIdQueryHandler.cs tests/Awake.Unit.Tests/Features/Squads/GetSquadsQueryHandlerTests.cs tests/Awake.Unit.Tests/Features/Squads/GetSquadByIdQueryHandlerTests.cs
git commit -m "feat: SquadDto carries member flags+KD for /api/squads and /api/squads/{id}"
```

---

### Task 7: Full backend test run

**Files:** none (verification task)

- [ ] **Step 1: Run the full unit test suite**

Run: `dotnet test tests/Awake.Unit.Tests`
Expected: All tests PASS, 0 failures (this catches anything the per-file filters in Tasks 1–6 missed, e.g. other tests touching `SquadDto`/`ISquadRepository`)

- [ ] **Step 2: Build the API project**

Run: `dotnet build src/Awake.API`
Expected: Build succeeded, 0 warnings introduced by this change

---

### Task 8: Frontend types + `squadsApi.rename`

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts`
- Modify: `frontend/awake-web/src/api/squads.ts`

**Interfaces:**
- Consumes: `PUT /api/squads/{id}/name` from Task 5; JSON shape from Task 6 (`flags`, `kd` on each member).
- Produces: `SquadMemberDto.flags: PlayerFlags`, `SquadMemberDto.kd: number | null`; `squadsApi.rename(squadId: string, name: string) : Promise<void>` — used by Task 10's `SquadCard`.

- [ ] **Step 1: Extend `SquadMemberDto`**

In `frontend/awake-web/src/types/api.ts`, replace:

```ts
export interface SquadMemberDto {
  userId: string
  username: string
  gameNickname: string | null
  isLeader: boolean
  joinedAt: string
}
```

with:

```ts
export interface SquadMemberDto {
  userId: string
  username: string
  gameNickname: string | null
  isLeader: boolean
  joinedAt: string
  flags: PlayerFlags
  kd: number | null
}
```

(`PlayerFlags` is declared later in the same file — TypeScript interfaces can reference each other regardless of declaration order within a module, so no import/reorder needed.)

- [ ] **Step 2: Add the rename API call**

In `frontend/awake-web/src/api/squads.ts`, add inside the `squadsApi` object (after `setLeader`):

```ts
  rename: (squadId: string, name: string) =>
    apiClient.put<void>(`/squads/${squadId}/name`, { name }),
```

- [ ] **Step 3: Type-check**

Run: `cd frontend/awake-web && npx tsc -b`
Expected: One error — `_auth.squads.index.tsx` no longer satisfies the shape it's using (fixed in Task 10). If Task 10 hasn't run yet, this error is expected; otherwise expect a clean pass.

- [ ] **Step 4: Commit**

```bash
git add frontend/awake-web/src/types/api.ts frontend/awake-web/src/api/squads.ts
git commit -m "feat: add flags/kd to SquadMemberDto and squadsApi.rename"
```

---

### Task 9: `MemberHoverInfo` component (viewport-safe hover popup)

**Files:**
- Create: `frontend/awake-web/src/components/squads/MemberHoverInfo.tsx`

**Interfaces:**
- Consumes: `InventoryFlags` from `frontend/awake-web/src/components/InventoryFlags.tsx` (props `flags: PlayerFlags`, `size?: 'sm' | 'md'`); `PlayerFlags` from `@/types/api`.
- Produces: `MemberHoverInfo({ nickname: string; flags: PlayerFlags; kd: number | null; children: ReactNode })` — used by Task 10's `SquadCard`.

- [ ] **Step 1: Write the component**

```tsx
import { useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { InventoryFlags } from '@/components/InventoryFlags'
import type { PlayerFlags } from '@/types/api'

const PANEL_WIDTH = 224
const VIEWPORT_MARGIN = 12

// Позиция считается от реального места строки на экране (position: fixed) и
// клэмпится в границы вьюпорта — карточки отрядов стоят в сетке на всю ширину
// страницы, и крайние карточки иначе выталкивали бы попап за экран.
export function MemberHoverInfo({
  nickname,
  flags,
  kd,
  children,
}: {
  nickname: string
  flags: PlayerFlags
  kd: number | null
  children: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [coords, setCoords] = useState<{ left: number; top: number }>()
  const anchorRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useLayoutEffect(() => {
    if (!open || !anchorRef.current) return

    function place() {
      const rect = anchorRef.current!.getBoundingClientRect()
      const panelHeight = panelRef.current?.offsetHeight ?? 0
      const left = Math.min(
        Math.max(rect.left, VIEWPORT_MARGIN),
        window.innerWidth - PANEL_WIDTH - VIEWPORT_MARGIN,
      )
      let top = rect.bottom + 6
      if (top + panelHeight > window.innerHeight - VIEWPORT_MARGIN) {
        top = Math.max(rect.top - panelHeight - 6, VIEWPORT_MARGIN)
      }
      setCoords({ left, top })
    }

    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  return (
    <div
      ref={anchorRef}
      className="relative"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      {children}
      {open && (
        <div
          ref={panelRef}
          style={{
            left: coords?.left,
            top: coords?.top,
            width: PANEL_WIDTH,
            visibility: coords ? 'visible' : 'hidden',
          }}
          className="fixed z-40 rounded-lg border border-border bg-popover p-3 text-xs shadow-xl"
        >
          <p className="font-semibold text-foreground">{nickname}</p>
          <p className="mt-1 text-muted-foreground">
            КД:{' '}
            <span className="font-bold text-foreground">
              {kd != null ? kd.toLocaleString('ru-RU', { maximumFractionDigits: 2 }) : '—'}
            </span>
          </p>
          <div className="mt-2">
            <InventoryFlags flags={flags} size="sm" />
          </div>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Type-check**

Run: `cd frontend/awake-web && npx tsc -b`
Expected: No new errors from this file (the pre-existing `_auth.squads.index.tsx` error from Task 8 may still show — fixed in Task 10)

- [ ] **Step 3: Commit**

```bash
git add frontend/awake-web/src/components/squads/MemberHoverInfo.tsx
git commit -m "feat: add viewport-safe MemberHoverInfo hover popup"
```

---

### Task 10: `SquadCard` component — nickname-only display, inline rename, hover popups

**Files:**
- Create: `frontend/awake-web/src/components/squads/SquadCard.tsx`
- Modify: `frontend/awake-web/src/routes/_auth.squads.index.tsx`

**Interfaces:**
- Consumes: `MemberHoverInfo` from Task 9; `squadsApi.rename` from Task 8; `SquadDto` from `@/types/api` (now including `flags`/`kd` per member).
- Produces: `SquadCard({ squad: SquadDto; canRename: boolean })` — used by `_auth.squads.index.tsx`.

**Why extract a component instead of editing the `.map()` inline:** the rename UI needs per-card `useState` (editing on/off, draft text) and a per-card `useMutation`. Calling hooks inside a `.map()` callback breaks React's fixed-hook-order rule the moment `squads.length` changes between renders — each card must be its own component instance so its hooks are isolated.

- [ ] **Step 1: Write `SquadCard.tsx`**

```tsx
import { useState, type MouseEvent } from 'react'
import { Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Crown, Pencil } from 'lucide-react'
import { squadsApi } from '@/api/squads'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { MemberHoverInfo } from '@/components/squads/MemberHoverInfo'
import { cn } from '@/lib/utils'
import type { SquadDto } from '@/types/api'

export function SquadCard({ squad, canRename }: { squad: SquadDto; canRename: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(squad.name)

  const rename = useMutation({
    mutationFn: (name: string) => squadsApi.rename(squad.id, name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['squads'] }),
  })

  const leader = squad.members.find((m) => m.isLeader)
  const others = squad.members.filter((m) => !m.isLeader)
  const pct = (squad.memberCount / 5) * 100
  const isFull = squad.memberCount >= 5

  function startEdit(e: MouseEvent) {
    e.preventDefault()
    e.stopPropagation()
    setDraft(squad.name)
    setEditing(true)
  }

  function commit() {
    const trimmed = draft.trim()
    setEditing(false)
    if (trimmed && trimmed !== squad.name) {
      rename.mutate(trimmed)
    }
  }

  function cancel() {
    setDraft(squad.name)
    setEditing(false)
  }

  return (
    <Link to="/squads/$squadId" params={{ squadId: squad.id }} className="group block">
      <Card className="h-full transition-all duration-200 group-hover:border-accent/30 group-hover:shadow-[0_0_20px_rgba(61,220,132,0.06)]">
        <CardContent className="pt-5 pb-5">
          {/* Header */}
          <div className="mb-4 flex items-start justify-between gap-2">
            {editing ? (
              <input
                autoFocus
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onClick={(e) => e.stopPropagation()}
                onKeyDown={(e) => {
                  e.stopPropagation()
                  if (e.key === 'Enter') commit()
                  if (e.key === 'Escape') cancel()
                }}
                onBlur={commit}
                disabled={rename.isPending}
                maxLength={100}
                className="min-w-0 flex-1 rounded-md border border-accent/40 bg-secondary px-2 py-1 text-base font-semibold text-foreground outline-none"
              />
            ) : (
              <div className="flex min-w-0 items-center gap-1.5">
                <h2 className="truncate text-base font-semibold text-foreground transition-colors group-hover:text-accent">
                  {squad.name}
                </h2>
                {canRename && (
                  <button
                    type="button"
                    onClick={startEdit}
                    aria-label="Переименовать отряд"
                    className="shrink-0 rounded p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                  >
                    <Pencil size={13} />
                  </button>
                )}
              </div>
            )}
            <Badge
              className={cn(
                'shrink-0 border text-xs font-medium',
                isFull
                  ? 'border-destructive/30 bg-destructive/10 text-destructive'
                  : 'border-accent/30 bg-accent/10 text-accent',
              )}
            >
              {t('squads.memberCount', { count: squad.memberCount })}
            </Badge>
          </div>

          {/* Capacity bar */}
          <div className="mb-4">
            <div className="h-1 overflow-hidden rounded-full bg-secondary">
              <div
                className={cn('h-full rounded-full transition-all', isFull ? 'bg-destructive/60' : 'bg-accent/70')}
                style={{ width: `${pct}%` }}
              />
            </div>
          </div>

          {/* Members */}
          <div className="space-y-2">
            {leader && (
              <MemberHoverInfo nickname={leader.gameNickname ?? leader.username} flags={leader.flags} kd={leader.kd}>
                <div className="flex items-center gap-2">
                  <Crown size={12} className="shrink-0 text-yellow-400" />
                  <span className="truncate text-sm font-medium text-foreground">
                    {leader.gameNickname ?? leader.username}
                  </span>
                </div>
              </MemberHoverInfo>
            )}
            {others.slice(0, leader ? 2 : 3).map((m) => (
              <MemberHoverInfo key={m.userId} nickname={m.gameNickname ?? m.username} flags={m.flags} kd={m.kd}>
                <div className="flex items-center gap-2 pl-5">
                  <span className="truncate text-sm text-muted-foreground">
                    {m.gameNickname ?? m.username}
                  </span>
                </div>
              </MemberHoverInfo>
            ))}
            {squad.memberCount > 3 && (
              <div className="pl-5 text-xs text-muted-foreground">
                {t('squads.more', { count: squad.memberCount - 3 })}
              </div>
            )}
            {squad.memberCount === 0 && (
              <div className="text-sm text-muted-foreground">{t('squads.noMembers')}</div>
            )}
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
```

- [ ] **Step 2: Rewrite `_auth.squads.index.tsx` to use `SquadCard`**

Replace the full contents of `frontend/awake-web/src/routes/_auth.squads.index.tsx` with:

```tsx
import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { squadsApi } from '@/api/squads'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { SquadCard } from '@/components/squads/SquadCard'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import { Shield, Wrench } from 'lucide-react'

export const Route = createFileRoute('/_auth/squads/')({
  component: SquadsPage,
})

function SquadsPage() {
  const { t } = useTranslation()
  const rank = useAuthStore((s) => s.user?.rank ?? 0)
  const { data: squads, isLoading } = useQuery({
    queryKey: ['squads'],
    queryFn: () => squadsApi.getAll(),
  })

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

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-black tracking-tight text-foreground">{t('squads.title')}</h1>
        {rank >= UserRank.Officer && (
          <Button asChild variant="outline" className="gap-2">
            <Link to="/squads/builder">
              <Wrench size={15} />
              Собрать отряды
            </Link>
          </Button>
        )}
      </div>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        {squads?.map((squad) => (
          <SquadCard key={squad.id} squad={squad} canRename={rank >= UserRank.Officer} />
        ))}
      </div>
      {!squads?.length && (
        <div className="rounded-xl border border-border bg-card py-16 text-center">
          <div className="mx-auto mb-3 flex h-11 w-11 items-center justify-center rounded-lg bg-accent/10">
            <Shield size={20} className="text-accent" />
          </div>
          <p className="text-sm text-muted-foreground">Отрядов пока нет.</p>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Type-check and lint**

Run: `cd frontend/awake-web && npx tsc -b && npx eslint src/components/squads/SquadCard.tsx src/routes/_auth.squads.index.tsx`
Expected: No errors from either command

- [ ] **Step 4: Commit**

```bash
git add frontend/awake-web/src/components/squads/SquadCard.tsx frontend/awake-web/src/routes/_auth.squads.index.tsx
git commit -m "feat: redesign squad cards — nickname-only, inline rename, hover popup"
```

---

### Task 11: Live acceptance test

**Files:** none (verification task — follows this project's established Docker+Playwright pattern, same harness used for the squad-builder feature's Task 5 acceptance and the notification-popup fix)

- [ ] **Step 1: Confirm the Docker backend stack and frontend dev server are running**

Run: `docker-compose -p featurestage-4 ps` — expect `api`, `db`, `flaresolverr` all `Up`.
Run: `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5173/` — expect `200`. Start either if not running (`docker-compose -p featurestage-4 up -d` / `cd frontend/awake-web && npm run dev`).

- [ ] **Step 2: Seed test data so a squad has flags/KD and a nicknameless member**

Using the project's synthetic-JWT + headless-Chromium harness (`mint-jwt.js` pattern from prior sessions, or `dotnet` seed script / direct SQL via `docker-compose exec db psql`), ensure at least one squad has:
- A leader with a linked `gameNickname`, at least one inventory item mapping to a known flag (e.g. an `armor/combined` item with `RANK_MASTER`/`RANK_LEGEND` quality for the Bio flag), and a `PlayerStatsSnapshot` row for their nickname with a non-null `KdRatio`.
- At least one member with `GameNickname = NULL` on their user row, to verify the Discord-username fallback.

- [ ] **Step 3: Write and run a Playwright script covering:**

1. Log in as a Member-rank synthetic session, navigate to `/squads`.
2. Assert every member row shows a nickname (or Discord fallback for the nicknameless member) and never shows the two-part "username · nickname" pattern.
3. Hover a member row with known flags/KD; assert the popup shows the correct KD text and that the flag icons reflect the seeded flags (e.g. Bio icon in the "active" style).
4. Assert `document.documentElement.scrollWidth <= document.documentElement.clientWidth` before/after the hover, on both a leftmost and rightmost grid-column card, at a narrow viewport (e.g. 1024px) — confirms the viewport clamp actually engages at the grid edges.
5. Assert the pencil/rename icon is NOT present for this Member-rank session.
6. Log in as an Officer-rank synthetic session; assert the pencil IS present; click it, type a new name, press Enter; assert the card immediately reflects the new name after refetch, and re-fetching `/squads` (or reloading) shows the persisted name.
7. Repeat the rename flow with Escape instead of Enter; assert the name reverts and no request was sent (or the name is unchanged after refetch).

Expected: all checks PASS. Save a screenshot of the hover popup on an edge-column card for a quick visual sanity check.

- [ ] **Step 4: Report results**

Summarize pass/fail counts. If anything fails, return to the relevant task above — do not patch symptoms in the acceptance script.
