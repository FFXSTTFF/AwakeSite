# Player Profile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stub "Данные игрока" card on the ticket detail page with real STALCRAFT stats scraped from stalcrafthq.com, cached with stale-while-revalidate.

**Architecture:** `StalcraftHqDataSource` scrapes `stalcrafthq.com/characters/EU/{nickname}` using HtmlAgilityPack; `PlayerDataAggregator` holds a `ConcurrentDictionary` cache (TTL 1h, stale-while-revalidate); the existing `GetTicketByIdQueryHandler` already calls the aggregator and embeds `playerData` in `TicketDetailDto` — we type it properly as `PlayerProfile?` instead of `object?`; the frontend card replaces the raw JSON `<pre>` dump.

**Tech Stack:** C# HtmlAgilityPack 1.11.x, IHttpClientFactory, ConcurrentDictionary; TypeScript + TanStack Query (already in use); React

## Global Constraints

- Target framework: net10.0
- Server always `EU` — hardcoded in scraper, not configurable
- Cache TTL: `TimeSpan.FromHours(1)`
- No new DB migrations
- All UI copy in Russian via `frontend/awake-web/src/i18n/ru.json`
- No separate `/api/players` endpoint — player data stays embedded in `TicketDetailDto`

---

## File Map

**Create:**
- `src/Awake.Domain/ValueObjects/ClanEntry.cs`
- `src/Awake.Domain/ValueObjects/PlayerProfile.cs`
- `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StalcraftHqDataSource.cs`
- `tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs`
- `tests/Awake.Unit.Tests/Features/PlayerData/StalcraftHqDataSourceTests.cs`

**Modify:**
- `src/Awake.Application/Common/Models/PlayerDataResult.cs`
- `src/Awake.Infrastructure/ExternalServices/PlayerData/IPlayerDataSource.cs`
- `src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs`
- `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs` → deleted in Task 2
- `src/Awake.Infrastructure/Awake.Infrastructure.csproj`
- `src/Awake.Infrastructure/DependencyInjection.cs`
- `src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs`
- `src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`
- `tests/Awake.Unit.Tests/Features/Tickets/GetTicketByIdQueryHandlerTests.cs`
- `frontend/awake-web/src/types/api.ts`
- `frontend/awake-web/src/i18n/ru.json`
- `frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx`

---

### Task 1: Domain records + type cleanup + stale-while-revalidate cache

**Files:**
- Create: `src/Awake.Domain/ValueObjects/ClanEntry.cs`
- Create: `src/Awake.Domain/ValueObjects/PlayerProfile.cs`
- Modify: `src/Awake.Application/Common/Models/PlayerDataResult.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/PlayerData/IPlayerDataSource.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs`
- Modify: `src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs`
- Modify: `src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs`
- Modify: `tests/Awake.Unit.Tests/Features/Tickets/GetTicketByIdQueryHandlerTests.cs`
- Test: `tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs`

**Interfaces:**
- Produces: `PlayerProfile(int Kills, int Deaths, double KdRatio, string Accuracy, string Playtime, IReadOnlyList<ClanEntry> ClanHistory)`, `ClanEntry(string ClanName, string ClanTag, string Since)`, `PlayerDataResult(string Nickname, PlayerProfile? Profile)`, `IPlayerDataSource.TryGetDataAsync → Task<PlayerProfile?>`

- [ ] **Step 1: Create ClanEntry**

```csharp
// src/Awake.Domain/ValueObjects/ClanEntry.cs
namespace Awake.Domain.ValueObjects;

public record ClanEntry(string ClanName, string ClanTag, string Since);
```

- [ ] **Step 2: Create PlayerProfile**

```csharp
// src/Awake.Domain/ValueObjects/PlayerProfile.cs
namespace Awake.Domain.ValueObjects;

public record PlayerProfile(
    int Kills,
    int Deaths,
    double KdRatio,
    string Accuracy,
    string Playtime,
    IReadOnlyList<ClanEntry> ClanHistory
);
```

- [ ] **Step 3: Update PlayerDataResult**

```csharp
// src/Awake.Application/Common/Models/PlayerDataResult.cs
using Awake.Domain.ValueObjects;

namespace Awake.Application.Common.Models;

public record PlayerDataResult(string Nickname, PlayerProfile? Profile);
```

- [ ] **Step 4: Update IPlayerDataSource**

```csharp
// src/Awake.Infrastructure/ExternalServices/PlayerData/IPlayerDataSource.cs
using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public interface IPlayerDataSource
{
    Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default);
}
```

- [ ] **Step 5: Update StubDataSource to match new interface**

```csharp
// src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs
using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public class StubDataSource : IPlayerDataSource
{
    public Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
        => Task.FromResult<PlayerProfile?>(null);
}
```

- [ ] **Step 6: Rewrite PlayerDataAggregator with stale-while-revalidate cache**

```csharp
// src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs
using System.Collections.Concurrent;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public class PlayerDataAggregator : IPlayerDataAggregator
{
    private readonly IReadOnlyList<IPlayerDataSource> _sources;
    private readonly ConcurrentDictionary<string, (PlayerProfile Profile, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public PlayerDataAggregator(IEnumerable<IPlayerDataSource> sources) =>
        _sources = sources.ToList();

    public async Task<PlayerDataResult> GetPlayerDataAsync(string nickname, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(nickname, out var entry))
        {
            if (DateTime.UtcNow - entry.CachedAt < Ttl)
                return new PlayerDataResult(nickname, entry.Profile);

            // Stale — return immediately, refresh in background
            _ = Task.Run(() => RefreshAsync(nickname), CancellationToken.None);
            return new PlayerDataResult(nickname, entry.Profile);
        }

        var profile = await FetchAsync(nickname, ct);
        if (profile is not null)
            _cache[nickname] = (profile, DateTime.UtcNow);

        return new PlayerDataResult(nickname, profile);
    }

    private async Task RefreshAsync(string nickname)
    {
        var profile = await FetchAsync(nickname, CancellationToken.None);
        if (profile is not null)
            _cache[nickname] = (profile, DateTime.UtcNow);
    }

    private async Task<PlayerProfile?> FetchAsync(string nickname, CancellationToken ct)
    {
        foreach (var source in _sources)
        {
            var result = await source.TryGetDataAsync(nickname, ct);
            if (result is not null) return result;
        }
        return null;
    }
}
```

- [ ] **Step 7: Write failing aggregator tests**

```csharp
// tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;
using Awake.Infrastructure.ExternalServices.PlayerData;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.PlayerData;

public class PlayerDataAggregatorTests
{
    private static readonly PlayerProfile FakeProfile =
        new(100, 50, 2.0, "86%", "10 days", []);

    private static PlayerDataAggregator BuildAggregator(params IPlayerDataSource[] sources) =>
        new(sources);

    [Fact]
    public async Task GetPlayerDataAsync_CacheMiss_FetchesFromSource()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
              .ReturnsAsync(FakeProfile);
        var agg = BuildAggregator(source.Object);

        var result = await agg.GetPlayerDataAsync("Alice");

        result.Profile.Should().Be(FakeProfile);
        source.Verify(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerDataAsync_FreshCacheHit_DoesNotFetchAgain()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
              .ReturnsAsync(FakeProfile);
        var agg = BuildAggregator(source.Object);

        await agg.GetPlayerDataAsync("Alice");       // populate cache
        var result = await agg.GetPlayerDataAsync("Alice");  // should hit cache

        result.Profile.Should().Be(FakeProfile);
        source.Verify(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerDataAsync_SourceReturnsNull_ReturnsNullProfile()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((PlayerProfile?)null);
        var agg = BuildAggregator(source.Object);

        var result = await agg.GetPlayerDataAsync("Ghost");

        result.Profile.Should().BeNull();
    }

    [Fact]
    public async Task GetPlayerDataAsync_FirstSourceNull_TriesNextSource()
    {
        var nullSource = new Mock<IPlayerDataSource>();
        nullSource.Setup(s => s.TryGetDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerProfile?)null);

        var realSource = new Mock<IPlayerDataSource>();
        realSource.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(FakeProfile);

        var agg = BuildAggregator(nullSource.Object, realSource.Object);

        var result = await agg.GetPlayerDataAsync("Alice");

        result.Profile.Should().Be(FakeProfile);
    }
}
```

- [ ] **Step 8: Run tests to verify they fail**

```
cd D:\Awake\.claude\worktrees\feature+stage-4
dotnet test tests/Awake.Unit.Tests --filter "PlayerDataAggregatorTests" -v minimal
```

Expected: FAIL — old `PlayerDataResult` constructor signature mismatch

- [ ] **Step 9: Update TicketDetailDto — change PlayerData from object? to PlayerProfile?**

```csharp
// src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Features.Tickets.Dtos;

public record TicketDetailDto(
    Guid Id,
    TicketType Type,
    TicketStatus Status,
    string GameNickname,
    string AuthorUsername,
    string Description,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByUsername,
    IReadOnlyList<TicketCommentDto> Comments,
    PlayerProfile? PlayerData,
    Loadout? Loadout
);
```

- [ ] **Step 10: Update GetTicketByIdQueryHandler — pass pd.Profile**

```csharp
// src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using MediatR;

namespace Awake.Application.Features.Tickets.Queries.GetTicketById;

public class GetTicketByIdQueryHandler(
    ITicketRepository ticketRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IPlayerDataAggregator playerDataAggregator
) : IRequestHandler<GetTicketByIdQuery, Result<TicketDetailDto>>
{
    public async Task<Result<TicketDetailDto>> Handle(
        GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdWithDetailsAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result<TicketDetailDto>.Failure("Тикет не найден.");

        var isOfficerPlus = currentUserService.Rank >= UserRank.Officer;
        var isAuthor = ticket.AuthorId == currentUserService.UserId;

        if (!isOfficerPlus && !isAuthor)
            return Result<TicketDetailDto>.Failure("Нет доступа к этому тикету.");

        PlayerProfile? playerData = null;
        if (isOfficerPlus)
        {
            var pd = await playerDataAggregator.GetPlayerDataAsync(ticket.GameNickname, cancellationToken);
            playerData = pd.Profile;
        }

        string? reviewedByUsername = null;
        if (ticket.ReviewedBy.HasValue)
        {
            var reviewer = await userRepository.GetByIdAsync(ticket.ReviewedBy.Value, cancellationToken);
            reviewedByUsername = reviewer?.Username;
        }

        var comments = ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TicketCommentDto(
                c.Id,
                c.Author?.Username ?? c.DiscordAuthorName ?? "Discord",
                c.Content,
                c.CreatedAt))
            .ToList();

        var dto = new TicketDetailDto(
            ticket.Id, ticket.Type, ticket.Status, ticket.GameNickname,
            ticket.Author?.Username ?? ticket.DiscordUsername ?? "Discord",
            ticket.Description, ticket.CreatedAt,
            ticket.ReviewedAt, reviewedByUsername,
            comments, playerData, ticket.Loadout);

        return Result<TicketDetailDto>.Success(dto);
    }
}
```

- [ ] **Step 11: Update GetTicketByIdQueryHandlerTests — fix PlayerDataResult constructor**

```csharp
// tests/Awake.Unit.Tests/Features/Tickets/GetTicketByIdQueryHandlerTests.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Queries.GetTicketById;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using Moq;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Tickets;

public class GetTicketByIdQueryHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPlayerDataAggregator> _playerData = new();

    private GetTicketByIdQueryHandler BuildHandler() =>
        new(_repo.Object, _userRepo.Object, _currentUser.Object, _playerData.Object);

    private Ticket MakeTicket(Guid authorId, string authorName = "alice")
    {
        var author = new User { Id = authorId, Username = authorName };
        return new Ticket
        {
            Id = Guid.NewGuid(), AuthorId = authorId, Author = author,
            GameNickname = "AliceGame", Type = TicketType.Recruitment,
            Status = TicketStatus.Pending, Description = "I want to join",
            Comments = []
        };
    }

    [Fact]
    public async Task Handle_AuthorCanSeeOwnTicket()
    {
        var userId = Guid.NewGuid();
        var ticket = MakeTicket(userId);
        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var result = await BuildHandler().Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GameNickname.Should().Be("AliceGame");
        _playerData.Verify(p => p.GetPlayerDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OfficerGetsPlayerData()
    {
        var authorId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var ticket = MakeTicket(authorId);
        var profile = new PlayerProfile(1000, 500, 2.0, "75%", "100 days", []);
        var playerResult = new PlayerDataResult("AliceGame", profile);

        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(officerId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Officer);
        _playerData.Setup(p => p.GetPlayerDataAsync("AliceGame", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(playerResult);

        var result = await BuildHandler().Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlayerData.Should().NotBeNull();
        result.Value.PlayerData!.Kills.Should().Be(1000);
        _playerData.Verify(p => p.GetPlayerDataAsync("AliceGame", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OfficerGetsReviewedByUsername_WhenTicketReviewed()
    {
        var authorId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var ticket = MakeTicket(authorId);
        ticket.Status = TicketStatus.Approved;
        ticket.ReviewedBy = officerId;
        ticket.ReviewedAt = DateTime.UtcNow;
        var officerUser = new User { Id = officerId, Username = "bob" };

        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(officerId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Officer);
        _playerData.Setup(p => p.GetPlayerDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PlayerDataResult("AliceGame", null));
        _userRepo.Setup(r => r.GetByIdAsync(officerId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(officerUser);

        var result = await BuildHandler().Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReviewedByUsername.Should().Be("bob");
    }

    [Fact]
    public async Task Handle_NonOfficerCannotSeeOtherUsersTicket()
    {
        var authorId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ticket = MakeTicket(authorId);

        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(ticket);
        _currentUser.Setup(s => s.UserId).Returns(otherId);
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var result = await BuildHandler().Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFailure()
    {
        var ticketId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdWithDetailsAsync(ticketId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Ticket?)null);
        _currentUser.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _currentUser.Setup(s => s.Rank).Returns(UserRank.Member);

        var result = await BuildHandler().Handle(new GetTicketByIdQuery(ticketId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

- [ ] **Step 12: Update DependencyInjection.cs — aggregator to singleton**

In `src/Awake.Infrastructure/DependencyInjection.cs`, replace the player data section (lines 46-47):

```csharp
// Replace these two lines:
//   services.AddScoped<IPlayerDataSource, StubDataSource>();
//   services.AddScoped<IPlayerDataAggregator, PlayerDataAggregator>();
// With:
services.AddSingleton<IPlayerDataSource, StubDataSource>();
services.AddSingleton<IPlayerDataAggregator, PlayerDataAggregator>();
```

Note: StubDataSource stays registered as singleton for now; Task 2 swaps it for StalcraftHqDataSource.

- [ ] **Step 13: Build**

```
dotnet build src/Awake.API/Awake.API.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 14: Run all tests**

```
dotnet test tests/Awake.Unit.Tests -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 32, Skipped: 0` (28 existing + 4 new aggregator tests)

- [ ] **Step 15: Commit**

```
git add src/Awake.Domain/ValueObjects/ClanEntry.cs
git add src/Awake.Domain/ValueObjects/PlayerProfile.cs
git add src/Awake.Application/Common/Models/PlayerDataResult.cs
git add src/Awake.Infrastructure/ExternalServices/PlayerData/IPlayerDataSource.cs
git add src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs
git add src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs
git add src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs
git add src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs
git add src/Awake.Infrastructure/DependencyInjection.cs
git add tests/Awake.Unit.Tests/Features/Tickets/GetTicketByIdQueryHandlerTests.cs
git add tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs
git commit -m "feat(player): typed PlayerProfile domain model + stale-while-revalidate cache"
```

---

### Task 2: StalcraftHqDataSource — HtmlAgilityPack scraper + tests

**Files:**
- Modify: `src/Awake.Infrastructure/Awake.Infrastructure.csproj`
- Create: `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StalcraftHqDataSource.cs`
- Delete: `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs`
- Test: `tests/Awake.Unit.Tests/Features/PlayerData/StalcraftHqDataSourceTests.cs`

**Interfaces:**
- Consumes: `IPlayerDataSource`, `PlayerProfile`, `ClanEntry` from Task 1
- Consumes: `IHttpClientFactory` (already registered in ASP.NET Core)
- Produces: `StalcraftHqDataSource.Parse(string html) → PlayerProfile?` (internal static, directly testable)

- [ ] **Step 1: Write failing scraper tests**

```csharp
// tests/Awake.Unit.Tests/Features/PlayerData/StalcraftHqDataSourceTests.cs
using Awake.Infrastructure.ExternalServices.PlayerData.Sources;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.PlayerData;

public class StalcraftHqDataSourceTests
{
    // Minimal HTML fixture that approximates stalcrafthq.com structure.
    // If the real site returns different HTML, update only this fixture and selectors.
    private const string ValidHtml = """
        <html><body>
          <dl>
            <div><dt>Kills:</dt><dd>121 559</dd></div>
            <div><dt>Deaths:</dt><dd>48 879</dd></div>
            <div><dt>Accuracy:</dt><dd>86%</dd></div>
          </dl>
          <p>In-game for 388 days, 5 hours and 45 minutes</p>
          <div><span>[HARD] Try Hard</span></div>
          <div><span>[LOVE] Awake</span></div>
        </body></html>
        """;

    private const string EmptyHtml = "<html><body><p>Player not found</p></body></html>";

    [Fact]
    public void Parse_ValidHtml_ReturnsCorrectKills()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Kills.Should().Be(121559);
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsCorrectDeaths()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Deaths.Should().Be(48879);
    }

    [Fact]
    public void Parse_ValidHtml_ComputesKdRatio()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.KdRatio.Should().Be(Math.Round(121559.0 / 48879.0, 2));
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsAccuracy()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Accuracy.Should().Be("86%");
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsPlaytime()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Playtime.Should().Be("388 days, 5 hours and 45 minutes");
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsClanHistory()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.ClanHistory.Should().HaveCount(2);
        profile.ClanHistory[0].ClanTag.Should().Be("HARD");
        profile.ClanHistory[0].ClanName.Should().Be("Try Hard");
        profile.ClanHistory[1].ClanTag.Should().Be("LOVE");
    }

    [Fact]
    public void Parse_HtmlWithZeroStats_ReturnsNull()
    {
        StalcraftHqDataSource.Parse(EmptyHtml).Should().BeNull();
    }

    [Fact]
    public void Parse_NumbersWithSpaces_ParsesCorrectly()
    {
        var html = """
            <html><body>
              <dl>
                <div><dt>Kills:</dt><dd>1 000 000</dd></div>
                <div><dt>Deaths:</dt><dd>500 000</dd></div>
              </dl>
            </body></html>
            """;
        var profile = StalcraftHqDataSource.Parse(html);
        profile!.Kills.Should().Be(1000000);
        profile.Deaths.Should().Be(500000);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/Awake.Unit.Tests --filter "StalcraftHqDataSourceTests" -v minimal
```

Expected: FAIL — `StalcraftHqDataSource` does not exist yet

- [ ] **Step 3: Add HtmlAgilityPack to Infrastructure project**

In `src/Awake.Infrastructure/Awake.Infrastructure.csproj`, add inside the existing `<ItemGroup>` with PackageReferences:

```xml
<PackageReference Include="HtmlAgilityPack" Version="1.11.72" />
```

- [ ] **Step 4: Create StalcraftHqDataSource**

```csharp
// src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StalcraftHqDataSource.cs
using System.Text.RegularExpressions;
using Awake.Domain.ValueObjects;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.PlayerData.Sources;

public class StalcraftHqDataSource(
    IHttpClientFactory httpClientFactory,
    ILogger<StalcraftHqDataSource> logger) : IPlayerDataSource
{
    private const string Server = "EU";

    public async Task<PlayerProfile?> TryGetDataAsync(string nickname, CancellationToken ct = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("stalcrafthq");
            var response = await client.GetAsync(
                $"/characters/{Server}/{Uri.EscapeDataString(nickname)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(ct);
            return Parse(html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch STALCRAFT profile for {Nickname}", nickname);
            return null;
        }
    }

    // internal so tests can call Parse() directly without HTTP
    internal static PlayerProfile? Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var kills = ParseStat(doc, "Kills");
        var deaths = ParseStat(doc, "Deaths");

        if (kills == 0 && deaths == 0) return null;

        var accuracy = ParseText(doc, "Accuracy") ?? "—";
        var playtime = ParsePlaytime(doc) ?? "—";
        var clanHistory = ParseClanHistory(doc);
        var kd = deaths > 0 ? Math.Round(kills / (double)deaths, 2) : (double)kills;

        return new PlayerProfile(kills, deaths, kd, accuracy, playtime, clanHistory);
    }

    private static int ParseStat(HtmlDocument doc, string label)
    {
        var raw = GetDdValue(doc, label);
        if (raw is null) return 0;
        // Strip thousand separators: regular space, non-breaking space ( ), comma
        var cleaned = Regex.Replace(
            HtmlEntity.DeEntitize(raw).Trim(),
            @"[\s ,]", "");
        return int.TryParse(cleaned, out var n) ? n : 0;
    }

    private static string? ParseText(HtmlDocument doc, string label)
    {
        var raw = GetDdValue(doc, label);
        return raw is null ? null : HtmlEntity.DeEntitize(raw).Trim();
    }

    private static string? GetDdValue(HtmlDocument doc, string label)
    {
        // Matches <dt>Label:</dt> or <dt>Label</dt>
        var dt = doc.DocumentNode.SelectSingleNode(
            $"//dt[normalize-space(.)='{label}:' or normalize-space(.)='{label}']");
        if (dt is null) return null;

        var dd = dt.SelectSingleNode("following-sibling::dd[1]")
                ?? dt.ParentNode?.SelectSingleNode(".//dd");
        return dd?.InnerText;
    }

    private static string? ParsePlaytime(HtmlDocument doc)
    {
        const string marker = "In-game for ";
        var node = doc.DocumentNode
            .SelectSingleNode($"//*[contains(normalize-space(.), '{marker}')]");
        if (node is null) return null;

        var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        return text[(idx + marker.Length)..]
            .Split('\n')[0]   // take only the first line
            .Trim();
    }

    private static IReadOnlyList<ClanEntry> ParseClanHistory(HtmlDocument doc)
    {
        var entries = new List<ClanEntry>();
        var seen = new HashSet<string>();

        // Find any element whose text contains [TAG] pattern
        var nodes = doc.DocumentNode.SelectNodes(
            "//*[contains(text(),'[') and contains(text(),']')]");
        if (nodes is null) return entries;

        foreach (var node in nodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            var tagMatch = Regex.Match(text, @"\[([A-Z0-9]{1,8})\]");
            if (!tagMatch.Success || !seen.Add(tagMatch.Groups[1].Value)) continue;

            var tag = tagMatch.Groups[1].Value;
            var name = Regex.Replace(text, @"\[[A-Z0-9]+\]", "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            entries.Add(new ClanEntry(name, tag, ""));
        }
        return entries;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test tests/Awake.Unit.Tests --filter "StalcraftHqDataSourceTests" -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 6: Update DependencyInjection.cs — add named HttpClient, swap StubDataSource**

Replace the player data section in `src/Awake.Infrastructure/DependencyInjection.cs`:

```csharp
// Remove these lines:
//   services.AddSingleton<IPlayerDataSource, StubDataSource>();
//   services.AddSingleton<IPlayerDataAggregator, PlayerDataAggregator>();

// Add:
services.AddHttpClient("stalcrafthq", c =>
{
    c.BaseAddress = new Uri("https://stalcrafthq.com");
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
    c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
    c.Timeout = TimeSpan.FromSeconds(10);
});
services.AddSingleton<IPlayerDataSource, StalcraftHqDataSource>();
services.AddSingleton<IPlayerDataAggregator, PlayerDataAggregator>();
```

Also add the using at the top of the file:
```csharp
using Awake.Infrastructure.ExternalServices.PlayerData.Sources;
```

- [ ] **Step 7: Delete StubDataSource**

Delete the file: `src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs`

- [ ] **Step 8: Build**

```
dotnet build src/Awake.API/Awake.API.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Run all tests**

```
dotnet test tests/Awake.Unit.Tests -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 40` (32 from Task 1 + 8 scraper tests)

- [ ] **Step 10: Commit**

```
git add src/Awake.Infrastructure/Awake.Infrastructure.csproj
git add src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StalcraftHqDataSource.cs
git add src/Awake.Infrastructure/DependencyInjection.cs
git add tests/Awake.Unit.Tests/Features/PlayerData/StalcraftHqDataSourceTests.cs
git rm src/Awake.Infrastructure/ExternalServices/PlayerData/Sources/StubDataSource.cs
git commit -m "feat(player): StalcraftHqDataSource — HtmlAgilityPack scraper with EU server"
```

---

### Task 3: Frontend — profile card in ticket detail

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts`
- Modify: `frontend/awake-web/src/i18n/ru.json`
- Modify: `frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx`

**Interfaces:**
- Consumes: `ticket.playerData` now typed as `PlayerProfile | null` (from Task 1 backend change)
- Produces: profile card UI with kills/deaths/K/D/accuracy/playtime/clan history

- [ ] **Step 1: Add types to api.ts**

In `frontend/awake-web/src/types/api.ts`, add after the `ItemSearchResult` interface:

```ts
export interface ClanEntry {
  clanName: string
  clanTag: string
  since: string
}

export interface PlayerProfile {
  kills: number
  deaths: number
  kdRatio: number
  accuracy: string
  playtime: string
  clanHistory: ClanEntry[]
}
```

Also update `TicketDetailDto` — change `playerData: unknown | null` to:
```ts
playerData: PlayerProfile | null
```

- [ ] **Step 2: Add i18n keys to ru.json**

In `frontend/awake-web/src/i18n/ru.json`, add a `"profile"` section at the top level (after `"tickets"`):

```json
"profile": {
  "title": "Данные игрока",
  "kills": "Убийства",
  "deaths": "Смерти",
  "kd": "К/Д",
  "accuracy": "Точность",
  "playtime": "Время в игре",
  "clanHistory": "История кланов",
  "unavailable": "Данные временно недоступны"
}
```

- [ ] **Step 3: Rewrite "Player data" card in ticket detail page**

In `frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx`, replace the `{/* Player data */}` block (lines 145–161 in the current file) with:

```tsx
{/* Player data */}
{isOfficerPlus && (
  <Card>
    <CardHeader className="pb-3">
      <CardTitle className="text-sm font-medium">{t('profile.title')}</CardTitle>
    </CardHeader>
    <CardContent>
      {ticket.playerData ? (
        <div className="space-y-2.5">
          <ProfileRow label={t('profile.kills')} value={ticket.playerData.kills.toLocaleString('ru-RU')} />
          <ProfileRow label={t('profile.deaths')} value={ticket.playerData.deaths.toLocaleString('ru-RU')} />
          <ProfileRow label={t('profile.kd')} value={ticket.playerData.kdRatio.toString()} />
          <ProfileRow label={t('profile.accuracy')} value={ticket.playerData.accuracy} />
          <ProfileRow label={t('profile.playtime')} value={ticket.playerData.playtime} />
          {ticket.playerData.clanHistory.length > 0 && (
            <>
              <Separator />
              <p className="text-xs text-muted-foreground font-medium pt-0.5">
                {t('profile.clanHistory')}
              </p>
              {ticket.playerData.clanHistory.map((clan, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-xs font-mono text-accent shrink-0">[{clan.clanTag}]</span>
                  <span className="text-sm text-foreground">{clan.clanName}</span>
                </div>
              ))}
            </>
          )}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('profile.unavailable')}</p>
      )}
    </CardContent>
  </Card>
)}
```

Also add the `ProfileRow` helper function at the bottom of the file, before the existing `LoadoutRow` function:

```tsx
function ProfileRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs text-muted-foreground w-28 shrink-0">{label}</span>
      <span className="text-sm text-foreground">{value}</span>
    </div>
  )
}
```

- [ ] **Step 4: TypeScript check**

```
cd frontend/awake-web
npx tsc --noEmit
```

Expected: no output (0 errors)

- [ ] **Step 5: Run backend tests one final time**

```
cd D:\Awake\.claude\worktrees\feature+stage-4
dotnet test tests/Awake.Unit.Tests -v minimal
```

Expected: `Passed! - Failed: 0, Passed: 40`

- [ ] **Step 6: Commit**

```
git add frontend/awake-web/src/types/api.ts
git add frontend/awake-web/src/i18n/ru.json
git add "frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx"
git commit -m "feat(frontend): player profile card in ticket detail — kills/deaths/K/D/accuracy/playtime/clan history"
```

---

## Post-Implementation Note

The HTML selectors in `StalcraftHqDataSource` are based on a `<dt>/<dd>` pattern (most common for stats pages). If stalcrafthq.com uses a different HTML structure and the scraper returns `null` for real nicknames, update only `GetDdValue`, `ParsePlaytime`, and `ParseClanHistory` in `StalcraftHqDataSource.cs` — the tests will guide you: update the HTML fixture to match the real site's structure, fix the selectors, and re-run.
