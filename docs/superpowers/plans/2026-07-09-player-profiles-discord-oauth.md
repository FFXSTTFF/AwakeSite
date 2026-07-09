# Player Profiles + Discord OAuth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Discord OAuth becomes the only login method; hanging Discord tickets auto-link to accounts by DiscordUserId; player stats persist as DB snapshots; profile pages show stats, loadout, squad and rank.

**Architecture:** Clean Architecture + CQRS (MediatR). OAuth authorization-code flow lives entirely on the backend (`AuthController` → `IDiscordOAuthService` → `DiscordLoginCommand`). A new `PlayerStatsSnapshot` table keyed by game nickname stores stats written through by `PlayerDataAggregator`. New `PlayersController` serves profile DTOs assembled by a query handler.

**Tech Stack:** ASP.NET Core (net10.0), EF Core + PostgreSQL (jsonb), MediatR, xUnit + Moq + FluentAssertions, React 19 + TanStack Router/Query + Zustand + Tailwind.

**Spec:** `docs/superpowers/specs/2026-07-09-player-profiles-discord-oauth-design.md`

## Global Constraints

- All work happens in worktree `D:\Awake\.claude\worktrees\feature+stage-4` on branch `worktree-feature+stage-4`.
- Class/method names in English; comments in Russian or English matching surrounding file style; user-facing strings in Russian.
- `Result<T>` pattern for expected failures (no exceptions in Application layer).
- New env vars: `Discord__ClientSecret`, `Discord__OAuthRedirectUri` (compose), `DISCORD_CLIENT_SECRET`, `DISCORD_OAUTH_REDIRECT_URI` (.env). ClientId reuses existing `Discord__ApplicationId`.
- Frontend redirect base = `Cors:AllowedOrigin` config value (`http://localhost:5173` in dev).
- Run backend tests with: `dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj`
- Verify frontend with: `cd frontend/awake-web; npm run build`

---

### Task 1: Domain entities + EF configuration + migration

**Files:**
- Modify: `src/Awake.Domain/Entities/User.cs`
- Create: `src/Awake.Domain/Entities/PlayerStatsSnapshot.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create: `src/Awake.Infrastructure/Persistence/Configurations/PlayerStatsSnapshotConfiguration.cs`
- Modify: `src/Awake.Infrastructure/Persistence/AppDbContext.cs:9-16`

**Interfaces:**
- Produces: `User.DiscordUserId/DiscordUsername/DiscordAvatarUrl` (all `string?`), entity `PlayerStatsSnapshot { string GameNickname; int Kills; int Deaths; double KdRatio; string Accuracy; string Playtime; List<ClanEntry> ClanHistory; DateTime FetchedAt; }`, `AppDbContext.PlayerStatsSnapshots`

- [ ] **Step 1: Add Discord fields to User**

In `src/Awake.Domain/Entities/User.cs` add after `GameNickname`:

```csharp
    // Discord OAuth — единственный способ входа; ключ связывания с заявками
    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarUrl { get; set; }
```

- [ ] **Step 2: Create PlayerStatsSnapshot entity**

Create `src/Awake.Domain/Entities/PlayerStatsSnapshot.cs`:

```csharp
using Awake.Domain.Common;
using Awake.Domain.ValueObjects;

namespace Awake.Domain.Entities;

// Снапшот игровой статистики. Ключ — игровой ник, а не UserId:
// снапшот создаётся при Discord-заявке, когда аккаунта на сайте ещё нет.
public class PlayerStatsSnapshot : BaseEntity
{
    public string GameNickname { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public double KdRatio { get; set; }
    public string Accuracy { get; set; } = "—";
    public string Playtime { get; set; } = "—";
    public List<ClanEntry> ClanHistory { get; set; } = [];
    public DateTime FetchedAt { get; set; }
}
```

- [ ] **Step 3: Update UserConfiguration**

In `src/Awake.Infrastructure/Persistence/Configurations/UserConfiguration.cs`, replace the `PasswordHash` block and add Discord fields (PasswordHash no longer required — Discord users have none):

```csharp
        builder.Property(x => x.PasswordHash)
            .HasMaxLength(255);

        builder.Property(x => x.DiscordUserId)
            .HasMaxLength(30);

        builder.HasIndex(x => x.DiscordUserId)
            .IsUnique();

        builder.Property(x => x.DiscordUsername)
            .HasMaxLength(100);

        builder.Property(x => x.DiscordAvatarUrl)
            .HasMaxLength(300);
```

- [ ] **Step 4: Create PlayerStatsSnapshotConfiguration**

Create `src/Awake.Infrastructure/Persistence/Configurations/PlayerStatsSnapshotConfiguration.cs` (jsonb conversion mirrors `TicketConfiguration.Loadout`):

```csharp
using System.Text.Json;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Awake.Infrastructure.Persistence.Configurations;

public class PlayerStatsSnapshotConfiguration : IEntityTypeConfiguration<PlayerStatsSnapshot>
{
    public void Configure(EntityTypeBuilder<PlayerStatsSnapshot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GameNickname)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.GameNickname)
            .IsUnique();

        builder.Property(x => x.Accuracy).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Playtime).IsRequired().HasMaxLength(50);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        builder.Property(x => x.ClanHistory)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<ClanEntry>>(v, jsonOptions) ?? new List<ClanEntry>());
    }
}
```

- [ ] **Step 5: Register DbSet**

In `src/Awake.Infrastructure/Persistence/AppDbContext.cs` add after `DiscordGuildSettings`:

```csharp
    public DbSet<PlayerStatsSnapshot> PlayerStatsSnapshots => Set<PlayerStatsSnapshot>();
```

- [ ] **Step 6: Build + create migration**

```powershell
cd D:\Awake\.claude\worktrees\feature+stage-4
dotnet build src/Awake.Infrastructure/Awake.Infrastructure.csproj
dotnet ef migrations add AddDiscordAuthAndStatsSnapshots --project src/Awake.Infrastructure --startup-project src/Awake.API
```

Expected: migration created with `AddColumn DiscordUserId/DiscordUsername/DiscordAvatarUrl`, `AlterColumn PasswordHash (nullable: true)`, `CreateTable PlayerStatsSnapshots`, unique indexes.

- [ ] **Step 7: Run existing tests (regression check)**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj
```

Expected: all pass (40+).

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(domain): Discord fields on User + PlayerStatsSnapshot entity and migration"
```

---

### Task 2: Snapshot repository + aggregator write-through

**Files:**
- Create: `src/Awake.Application/Common/Interfaces/Repositories/IPlayerStatsSnapshotRepository.cs`
- Create: `src/Awake.Infrastructure/Persistence/Repositories/PlayerStatsSnapshotRepository.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs` (repositories block)
- Test: `tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs`

**Interfaces:**
- Consumes: `PlayerStatsSnapshot` (Task 1), `PlayerProfile` record (existing), `IPlayerDataSource` (existing)
- Produces:
  - `IPlayerStatsSnapshotRepository { Task<PlayerStatsSnapshot?> GetByNicknameAsync(string gameNickname, CancellationToken ct = default); Task UpsertAsync(string gameNickname, PlayerProfile profile, CancellationToken ct = default); }`
  - `IPlayerDataAggregator.ForceRefreshAsync(string gameNickname, CancellationToken ct = default)` returning `Task<bool>` (false = rate-limited)

- [ ] **Step 1: Create repository interface**

Create `src/Awake.Application/Common/Interfaces/Repositories/IPlayerStatsSnapshotRepository.cs`:

```csharp
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface IPlayerStatsSnapshotRepository
{
    Task<PlayerStatsSnapshot?> GetByNicknameAsync(string gameNickname, CancellationToken ct = default);
    Task UpsertAsync(string gameNickname, PlayerProfile profile, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create repository implementation**

Create `src/Awake.Infrastructure/Persistence/Repositories/PlayerStatsSnapshotRepository.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Awake.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class PlayerStatsSnapshotRepository(AppDbContext context) : IPlayerStatsSnapshotRepository
{
    public async Task<PlayerStatsSnapshot?> GetByNicknameAsync(string gameNickname, CancellationToken ct = default)
        => await context.PlayerStatsSnapshots
            .FirstOrDefaultAsync(s => s.GameNickname == gameNickname, ct);

    public async Task UpsertAsync(string gameNickname, PlayerProfile profile, CancellationToken ct = default)
    {
        var existing = await context.PlayerStatsSnapshots
            .FirstOrDefaultAsync(s => s.GameNickname == gameNickname, ct);

        if (existing is null)
        {
            existing = new PlayerStatsSnapshot { GameNickname = gameNickname };
            context.PlayerStatsSnapshots.Add(existing);
        }

        existing.Kills = profile.Kills;
        existing.Deaths = profile.Deaths;
        existing.KdRatio = profile.KdRatio;
        existing.Accuracy = profile.Accuracy;
        existing.Playtime = profile.Playtime;
        existing.ClanHistory = profile.ClanHistory.ToList();
        existing.FetchedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Register in DI**

In `src/Awake.Infrastructure/DependencyInjection.cs` repositories block add:

```csharp
        services.AddScoped<IPlayerStatsSnapshotRepository, PlayerStatsSnapshotRepository>();
```

- [ ] **Step 4: Extend IPlayerDataAggregator interface**

In `src/Awake.Application/Common/Interfaces/IPlayerDataAggregator.cs` add to the interface:

```csharp
    /// Принудительное обновление статистики (кнопка на профиле).
    /// false — отклонено rate-limit'ом (чаще 1 раза в 10 минут на ник).
    Task<bool> ForceRefreshAsync(string gameNickname, CancellationToken ct = default);
```

- [ ] **Step 5: Write failing aggregator tests**

Create `tests/Awake.Unit.Tests/Features/PlayerData/PlayerDataAggregatorTests.cs`.
Note: `PlayerDataAggregator` is a singleton while the repository is scoped, so the aggregator takes `IServiceScopeFactory`. Tests wire a real `ServiceCollection` with the mocked repo registered as scoped:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.ValueObjects;
using Awake.Infrastructure.ExternalServices.PlayerData;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Awake.Unit.Tests.Features.PlayerData;

public class PlayerDataAggregatorTests
{
    private static readonly PlayerProfile Profile =
        new(100, 50, 2.0, "45%", "10 days", []);

    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();
    private readonly Mock<IPlayerDataSource> _source = new();

    private PlayerDataAggregator BuildAggregator()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _snapshots.Object);
        var provider = services.BuildServiceProvider();
        return new PlayerDataAggregator(
            [_source.Object],
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task GetPlayerData_SuccessfulFetch_SavesSnapshot()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);

        var result = await BuildAggregator().GetPlayerDataAsync("Nick");

        result.Profile.Should().Be(Profile);
        _snapshots.Verify(r => r.UpsertAsync("Nick", Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerData_FailedFetch_DoesNotSaveSnapshot()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerProfile?)null);

        var result = await BuildAggregator().GetPlayerDataAsync("Nick");

        result.Profile.Should().BeNull();
        _snapshots.Verify(r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<PlayerProfile>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForceRefresh_FirstCall_FetchesAndReturnsTrue()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);

        var ok = await BuildAggregator().ForceRefreshAsync("Nick");

        ok.Should().BeTrue();
        _source.Verify(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRefresh_SecondCallWithin10Minutes_ReturnsFalse()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);
        var aggregator = BuildAggregator();

        await aggregator.ForceRefreshAsync("Nick");
        var second = await aggregator.ForceRefreshAsync("Nick");

        second.Should().BeFalse();
        _source.Verify(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter PlayerDataAggregatorTests
```

Expected: FAIL — constructor signature mismatch (`PlayerDataAggregator` takes only sources), `ForceRefreshAsync` not defined.

- [ ] **Step 7: Rework PlayerDataAggregator**

Replace `src/Awake.Infrastructure/ExternalServices/PlayerData/PlayerDataAggregator.cs` contents:

```csharp
using System.Collections.Concurrent;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Awake.Infrastructure.ExternalServices.PlayerData;

public class PlayerDataAggregator : IPlayerDataAggregator
{
    private readonly IReadOnlyList<IPlayerDataSource> _sources;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, (PlayerProfile Profile, DateTime CachedAt)> _cache = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastForceRefresh = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ForceRefreshCooldown = TimeSpan.FromMinutes(10);

    public PlayerDataAggregator(IEnumerable<IPlayerDataSource> sources, IServiceScopeFactory scopeFactory)
    {
        _sources = sources.ToList();
        _scopeFactory = scopeFactory;
    }

    public async Task<PlayerDataResult> GetPlayerDataAsync(string nickname, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(nickname, out var entry))
        {
            if (DateTime.UtcNow - entry.CachedAt < Ttl)
                return new PlayerDataResult(nickname, entry.Profile);

            _ = Task.Run(() => RefreshAsync(nickname), CancellationToken.None);
            return new PlayerDataResult(nickname, entry.Profile);
        }

        var profile = await FetchAsync(nickname, ct);
        return new PlayerDataResult(nickname, profile);
    }

    public async Task<bool> ForceRefreshAsync(string gameNickname, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last = _lastForceRefresh.GetOrAdd(gameNickname, DateTime.MinValue);
        if (now - last < ForceRefreshCooldown) return false;
        _lastForceRefresh[gameNickname] = now;

        await FetchAsync(gameNickname, ct);
        return true;
    }

    private async Task RefreshAsync(string nickname)
        => await FetchAsync(nickname, CancellationToken.None);

    private async Task<PlayerProfile?> FetchAsync(string nickname, CancellationToken ct)
    {
        PlayerProfile? profile = null;

        foreach (var source in _sources)
        {
            var result = await source.TryGetDataAsync(nickname, ct);
            if (result is null) continue;

            if (profile is null)
            {
                profile = result;
                if (IsComplete(profile)) break;
                // Otherwise keep going — next source may fill accuracy / playtime / clan history
            }
            else
            {
                profile = Merge(profile, result);
                if (IsComplete(profile)) break;
            }
        }

        if (profile is not null)
        {
            _cache[nickname] = (profile, DateTime.UtcNow);
            await SaveSnapshotAsync(nickname, profile, ct);
        }

        return profile;
    }

    // Write-through: каждый успешный fetch сохраняет снапшот в БД,
    // чтобы профиль открывался мгновенно и данные переживали рестарты.
    private async Task SaveSnapshotAsync(string nickname, PlayerProfile profile, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPlayerStatsSnapshotRepository>();
        await repo.UpsertAsync(nickname, profile, ct);
    }

    private static bool IsComplete(PlayerProfile p) =>
        p.ClanHistory.Count > 0 && HasValue(p.Accuracy) && HasValue(p.Playtime);

    // Primary source wins; secondary only fills fields the primary left as placeholder
    private static PlayerProfile Merge(PlayerProfile primary, PlayerProfile secondary) =>
        primary with
        {
            Accuracy    = HasValue(primary.Accuracy) ? primary.Accuracy : secondary.Accuracy,
            Playtime    = HasValue(primary.Playtime) ? primary.Playtime : secondary.Playtime,
            ClanHistory = primary.ClanHistory.Count > 0 ? primary.ClanHistory : secondary.ClanHistory
        };

    private static bool HasValue(string s) =>
        !string.IsNullOrWhiteSpace(s) && s != "—";
}
```

Note: `using Awake.Application.Common.Interfaces;` covers `IPlayerDataSource` only if it lives there — `IPlayerDataSource` is in `Awake.Infrastructure.ExternalServices.PlayerData` (same namespace), no extra using needed. The old private cache-write inside `GetPlayerDataAsync` is replaced by the write inside `FetchAsync`.

- [ ] **Step 8: Run tests to verify they pass**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj
```

Expected: PASS, including existing suites.

- [ ] **Step 9: Commit**

```powershell
git add -A
git commit -m "feat(player): persist stats snapshots (write-through) + force refresh with cooldown"
```

---

### Task 3: Discord OAuth service (Infrastructure)

**Files:**
- Create: `src/Awake.Application/Common/Interfaces/IDiscordOAuthService.cs`
- Create: `src/Awake.Infrastructure/ExternalServices/Discord/DiscordOAuthService.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs` (Discord block)
- Test: `tests/Awake.Unit.Tests/Features/Auth/DiscordOAuthServiceTests.cs`

**Interfaces:**
- Produces:
  - `record DiscordUserInfo(string Id, string Username, string? GlobalName, string? AvatarUrl);`
  - `IDiscordOAuthService { string GetAuthorizationUrl(string state); Task<DiscordUserInfo?> ExchangeCodeAsync(string code, CancellationToken ct = default); }`

- [ ] **Step 1: Create interface + record**

Create `src/Awake.Application/Common/Interfaces/IDiscordOAuthService.cs`:

```csharp
namespace Awake.Application.Common.Interfaces;

public record DiscordUserInfo(string Id, string Username, string? GlobalName, string? AvatarUrl);

public interface IDiscordOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<DiscordUserInfo?> ExchangeCodeAsync(string code, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write failing test for user JSON parsing**

Create `tests/Awake.Unit.Tests/Features/Auth/DiscordOAuthServiceTests.cs`:

```csharp
using System.Text.Json;
using Awake.Infrastructure.ExternalServices.Discord;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Auth;

public class DiscordOAuthServiceTests
{
    private static JsonElement Json(string s) => JsonSerializer.Deserialize<JsonElement>(s);

    [Fact]
    public void ParseUser_FullPayload_MapsAllFields()
    {
        var json = Json("""
            { "id": "111222333", "username": "oops", "global_name": "OopsITry", "avatar": "abc123" }
            """);

        var user = DiscordOAuthService.ParseUser(json);

        user.Id.Should().Be("111222333");
        user.Username.Should().Be("oops");
        user.GlobalName.Should().Be("OopsITry");
        user.AvatarUrl.Should().Be("https://cdn.discordapp.com/avatars/111222333/abc123.png");
    }

    [Fact]
    public void ParseUser_NullAvatarAndGlobalName_ReturnsNulls()
    {
        var json = Json("""
            { "id": "111", "username": "oops", "global_name": null, "avatar": null }
            """);

        var user = DiscordOAuthService.ParseUser(json);

        user.GlobalName.Should().BeNull();
        user.AvatarUrl.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter DiscordOAuthServiceTests
```

Expected: FAIL — `DiscordOAuthService` does not exist.

- [ ] **Step 4: Implement DiscordOAuthService**

Create `src/Awake.Infrastructure/ExternalServices/Discord/DiscordOAuthService.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Awake.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Discord;

// Discord OAuth2 authorization code flow. Client secret никогда не покидает сервер.
public class DiscordOAuthService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordOAuthService> logger) : IDiscordOAuthService
{
    private const string ApiBase = "https://discord.com/api/v10";

    public string GetAuthorizationUrl(string state)
    {
        var clientId = configuration["Discord:ApplicationId"];
        var redirectUri = configuration["Discord:OAuthRedirectUri"];
        return $"https://discord.com/oauth2/authorize?client_id={clientId}" +
               $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
               $"&scope=identify&state={Uri.EscapeDataString(state)}";
    }

    public async Task<DiscordUserInfo?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var tokenResp = await httpClient.PostAsync($"{ApiBase}/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = configuration["Discord:ApplicationId"] ?? "",
                    ["client_secret"] = configuration["Discord:ClientSecret"] ?? "",
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = configuration["Discord:OAuthRedirectUri"] ?? "",
                }), ct);

            if (!tokenResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord token exchange failed: {Status}", tokenResp.StatusCode);
                return null;
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>(ct);
            var accessToken = tokenJson.GetProperty("access_token").GetString();

            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/users/@me");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
            var userResp = await httpClient.SendAsync(req, ct);

            if (!userResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord users/@me failed: {Status}", userResp.StatusCode);
                return null;
            }

            var userJson = await userResp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return ParseUser(userJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord OAuth exchange failed");
            return null;
        }
    }

    internal static DiscordUserInfo ParseUser(JsonElement json)
    {
        var id = json.GetProperty("id").GetString() ?? "";
        var username = json.GetProperty("username").GetString() ?? "";
        var globalName = json.TryGetProperty("global_name", out var gn) && gn.ValueKind == JsonValueKind.String
            ? gn.GetString() : null;
        var avatarHash = json.TryGetProperty("avatar", out var av) && av.ValueKind == JsonValueKind.String
            ? av.GetString() : null;
        var avatarUrl = avatarHash is null
            ? null
            : $"https://cdn.discordapp.com/avatars/{id}/{avatarHash}.png";
        return new DiscordUserInfo(id, username, globalName, avatarUrl);
    }
}
```

- [ ] **Step 5: Register in DI**

In `src/Awake.Infrastructure/DependencyInjection.cs` Discord block add:

```csharp
        services.AddHttpClient<IDiscordOAuthService, DiscordOAuthService>();
```

- [ ] **Step 6: Run tests to verify they pass**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter DiscordOAuthServiceTests
```

Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(auth): Discord OAuth service — authorize URL + code exchange"
```

---

### Task 4: DiscordLoginCommand — find-or-create user + ticket linking

**Files:**
- Create: `src/Awake.Application/Features/Auth/Commands/DiscordLogin/DiscordLoginCommand.cs`
- Create: `src/Awake.Application/Features/Auth/Commands/DiscordLogin/DiscordLoginCommandHandler.cs`
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/IUserRepository.cs`
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/ITicketRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/UserRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/TicketRepository.cs`
- Test: `tests/Awake.Unit.Tests/Features/Auth/DiscordLoginCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `DiscordUserInfo` (Task 3), `LoginResponse(string AccessToken, string Username, UserRank Rank, string UserId)` (existing), `ITokenService` (existing)
- Produces:
  - `record DiscordLoginCommand(DiscordUserInfo DiscordUser) : IRequest<Result<LoginResponse>>;`
  - `IUserRepository.GetByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default)` → `Task<User?>`
  - `ITicketRepository.GetUnlinkedByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default)` → `Task<IReadOnlyList<Ticket>>`

- [ ] **Step 1: Add repository methods (interfaces)**

`IUserRepository.cs` — add:

```csharp
    Task<User?> GetByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default);
```

`ITicketRepository.cs` — add:

```csharp
    Task<IReadOnlyList<Ticket>> GetUnlinkedByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default);
```

- [ ] **Step 2: Implement repository methods**

`UserRepository.cs` — add:

```csharp
    public async Task<User?> GetByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, ct);
```

`TicketRepository.cs` — add:

```csharp
    public async Task<IReadOnlyList<Ticket>> GetUnlinkedByDiscordUserIdAsync(string discordUserId, CancellationToken ct = default)
        => await context.Tickets
            .Where(t => t.DiscordUserId == discordUserId && t.AuthorId == null)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
```

- [ ] **Step 3: Create command**

Create `src/Awake.Application/Features/Auth/Commands/DiscordLogin/DiscordLoginCommand.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Models;
using Awake.Application.Features.Auth.Commands.Login;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.DiscordLogin;

public record DiscordLoginCommand(DiscordUserInfo DiscordUser) : IRequest<Result<LoginResponse>>;
```

Note: `LoginResponse` stays in namespace `Awake.Application.Features.Auth.Commands.Login` — the Login command/handler/validator files are deleted in Task 5, but `LoginResponse.cs` is kept.

- [ ] **Step 4: Write failing handler tests**

Create `tests/Awake.Unit.Tests/Features/Auth/DiscordLoginCommandHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Auth.Commands.DiscordLogin;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Auth;

public class DiscordLoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<ITokenService> _tokens = new();

    private static readonly DiscordUserInfo Info =
        new("111222333", "oops", "OopsITry", "https://cdn.discordapp.com/avatars/111222333/a.png");

    private DiscordLoginCommandHandler BuildHandler() =>
        new(_users.Object, _tickets.Object, _tokens.Object);

    public DiscordLoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("jwt");
        _tickets.Setup(t => t.GetUnlinkedByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_NewDiscordUser_CreatesGuestAccount()
    {
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.DiscordUserId.Should().Be("111222333");
        saved.Username.Should().Be("OopsITry");   // global_name предпочтительнее username
        saved.Rank.Should().Be(UserRank.Guest);
        saved.DiscordAvatarUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ExistingUser_LogsInWithoutCreating()
    {
        var user = new User { Username = "OopsITry", DiscordUserId = "111222333", Rank = UserRank.Member };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        var result = await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Rank.Should().Be(UserRank.Member);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HangingTickets_LinksThemAndCopiesNickname()
    {
        var user = new User { DiscordUserId = "111222333", Username = "OopsITry" };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        var older = new Ticket { DiscordUserId = "111222333", GameNickname = "OldNick",
            CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var newer = new Ticket { DiscordUserId = "111222333", GameNickname = "FreshNick",
            CreatedAt = DateTime.UtcNow };
        // Репозиторий возвращает по убыванию CreatedAt
        _tickets.Setup(t => t.GetUnlinkedByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
                .ReturnsAsync([newer, older]);

        await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        newer.AuthorId.Should().Be(user.Id);
        older.AuthorId.Should().Be(user.Id);
        user.GameNickname.Should().Be("FreshNick");
        _tickets.Verify(t => t.UpdateAsync(newer, It.IsAny<CancellationToken>()), Times.Once);
        _tickets.Verify(t => t.UpdateAsync(older, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoHangingTickets_IsIdempotent()
    {
        var user = new User { DiscordUserId = "111222333", Username = "OopsITry", GameNickname = "KeepMe" };
        _users.Setup(u => u.GetByDiscordUserIdAsync("111222333", It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        await BuildHandler().Handle(new DiscordLoginCommand(Info), CancellationToken.None);

        user.GameNickname.Should().Be("KeepMe");
        _tickets.Verify(t => t.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter DiscordLoginCommandHandlerTests
```

Expected: FAIL — `DiscordLoginCommandHandler` does not exist.

- [ ] **Step 6: Implement handler**

Create `src/Awake.Application/Features/Auth/Commands/DiscordLogin/DiscordLoginCommandHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Auth.Commands.Login;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.DiscordLogin;

public class DiscordLoginCommandHandler(
    IUserRepository userRepository,
    ITicketRepository ticketRepository,
    ITokenService tokenService
) : IRequestHandler<DiscordLoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        DiscordLoginCommand request, CancellationToken cancellationToken)
    {
        var info = request.DiscordUser;

        var user = await userRepository.GetByDiscordUserIdAsync(info.Id, cancellationToken);
        var isNew = user is null;

        if (user is null)
        {
            user = new User
            {
                Username = info.GlobalName ?? info.Username,
                Rank = UserRank.Guest,
                DiscordUserId = info.Id,
            };
        }

        // Обновляем Discord-инфо при каждом входе (ник/аватар могли смениться)
        user.DiscordUsername = info.Username;
        user.DiscordAvatarUrl = info.AvatarUrl;

        // Связывание висячих заявок: только AuthorId == null → идемпотентно
        var hangingTickets = await ticketRepository
            .GetUnlinkedByDiscordUserIdAsync(info.Id, cancellationToken);

        if (isNew)
            await userRepository.AddAsync(user, cancellationToken);

        foreach (var ticket in hangingTickets)
        {
            ticket.AuthorId = user.Id;
            await ticketRepository.UpdateAsync(ticket, cancellationToken);
        }

        // GameNickname — из самой свежей заявки (репозиторий сортирует по убыванию CreatedAt)
        if (hangingTickets.Count > 0)
            user.GameNickname = hangingTickets[0].GameNickname;

        if (!isNew)
            await userRepository.UpdateAsync(user, cancellationToken);
        else if (hangingTickets.Count > 0)
            await userRepository.UpdateAsync(user, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(user);
        return Result<LoginResponse>.Success(
            new LoginResponse(accessToken, user.Username, user.Rank, user.Id.ToString()));
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter DiscordLoginCommandHandlerTests
```

Expected: PASS (4 tests).

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(auth): DiscordLoginCommand — find-or-create user, link hanging tickets"
```

---

### Task 5: AuthController rework — OAuth endpoints, remove password auth

**Files:**
- Modify: `src/Awake.API/Controllers/AuthController.cs` (full rewrite)
- Delete: `src/Awake.Application/Features/Auth/Commands/Login/LoginCommand.cs`, `LoginCommandHandler.cs`, `LoginCommandValidator.cs` (keep `LoginResponse.cs`)
- Delete: `src/Awake.Application/Features/Auth/Commands/Register/` (entire folder)
- Delete: password-auth tests in `tests/Awake.Unit.Tests/` (find via `Get-ChildItem -Recurse -Filter "*.cs" tests | Select-String "RegisterCommand|LoginCommand"`)
- Modify: `docker-compose.yml`, `.env.example`, `src/Awake.API/appsettings.Development.json`

**Interfaces:**
- Consumes: `IDiscordOAuthService` (Task 3), `DiscordLoginCommand` (Task 4), `ITokenService`, `IRefreshTokenRepository` (existing)
- Produces: `GET /api/auth/discord/login` (302 → discord.com), `GET /api/auth/discord/callback?code&state` (302 → frontend `/auth/callback#accessToken=..&username=..&rank=..&userId=..`), `POST /api/auth/refresh` (unchanged)

- [ ] **Step 1: Rewrite AuthController**

Replace `src/Awake.API/Controllers/AuthController.cs` contents:

```csharp
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Auth.Commands.DiscordLogin;
using Awake.Application.Features.Auth.Commands.Refresh;
using Awake.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    ISender sender,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IDiscordOAuthService discordOAuth,
    IConfiguration configuration
) : ControllerBase
{
    private const string StateCookie = "discord_oauth_state";

    [HttpGet("discord/login")]
    public IActionResult DiscordLogin()
    {
        var state = Guid.NewGuid().ToString("N");

        // SameSite=Lax: кука должна выжить top-level redirect discord.com → наш callback
        Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/discord",
            MaxAge = TimeSpan.FromMinutes(10)
        });

        return Redirect(discordOAuth.GetAuthorizationUrl(state));
    }

    [HttpGet("discord/callback")]
    public async Task<IActionResult> DiscordCallback(
        [FromQuery] string? code, [FromQuery] string? state, CancellationToken ct)
    {
        var frontendUrl = configuration["Cors:AllowedOrigin"] ?? "";

        var expectedState = Request.Cookies[StateCookie];
        Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api/auth/discord" });

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) ||
            string.IsNullOrEmpty(expectedState) || state != expectedState)
        {
            return Redirect($"{frontendUrl}/login?error=discord");
        }

        var discordUser = await discordOAuth.ExchangeCodeAsync(code, ct);
        if (discordUser is null)
            return Redirect($"{frontendUrl}/login?error=discord");

        var result = await sender.Send(new DiscordLoginCommand(discordUser), ct);
        if (!result.IsSuccess)
            return Redirect($"{frontendUrl}/login?error=discord");

        var rawToken = tokenService.GenerateRefreshToken();
        await refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = Guid.Parse(result.Value!.UserId),
            Token = rawToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        }, ct);

        Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });

        // Токен во fragment (#) — не попадает в серверные логи и Referer
        var fragment = $"accessToken={Uri.EscapeDataString(result.Value.AccessToken)}" +
                       $"&username={Uri.EscapeDataString(result.Value.Username)}" +
                       $"&rank={(int)result.Value.Rank}" +
                       $"&userId={result.Value.UserId}";
        return Redirect($"{frontendUrl}/auth/callback#{fragment}");
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized();

        var command = new RefreshCommand(token);
        var result = await sender.Send(command, ct);

        if (!result.IsSuccess)
            return Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);

        Response.Cookies.Append("refreshToken", result.Value!.NewRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });

        return Ok(new
        {
            result.Value.AccessToken,
            result.Value.Username,
            result.Value.Rank,
            result.Value.UserId,
        });
    }
}
```

- [ ] **Step 2: Delete password auth code**

```powershell
Remove-Item src/Awake.Application/Features/Auth/Commands/Login/LoginCommand.cs
Remove-Item src/Awake.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs
Remove-Item src/Awake.Application/Features/Auth/Commands/Login/LoginCommandValidator.cs
Remove-Item -Recurse src/Awake.Application/Features/Auth/Commands/Register
```

Find and delete password-auth tests:

```powershell
Get-ChildItem -Recurse -Filter "*.cs" tests | Select-String -List "RegisterCommand|LoginCommand" | Select-Object Path
```

Delete each listed file (they test deleted handlers). If other files merely reference `LoginResponse`, keep them.

- [ ] **Step 3: Build and fix compile errors**

```powershell
dotnet build
```

Expected errors to fix: any lingering references to `RegisterCommand`/`LoginCommand` (e.g. request records in AuthController are already gone). `IPasswordHasher`/`PasswordHasherService` stay registered but unused by auth (still referenced? if nothing references them, leave the service in place — removal is out of scope).

- [ ] **Step 4: Configuration — docker-compose, .env.example, appsettings**

`docker-compose.yml` api environment — add:

```yaml
      Discord__ClientSecret: "${DISCORD_CLIENT_SECRET}"
      Discord__OAuthRedirectUri: "http://localhost:5001/api/auth/discord/callback"
```

`.env.example` — add:

```
DISCORD_CLIENT_SECRET=your_oauth_client_secret_here
```

`src/Awake.API/appsettings.Development.json` — inside the existing `Discord` section add:

```json
    "OAuthRedirectUri": "http://localhost:5000/api/auth/discord/callback"
```

- [ ] **Step 5: Run all tests**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj
```

Expected: PASS (password-auth tests removed, everything else green).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(auth)!: Discord OAuth is the only login — remove password register/login"
```

---

### Task 6: Profile API — query handler + PlayersController

**Files:**
- Create: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/PlayerProfileDto.cs`
- Create: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQuery.cs`
- Create: `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQueryHandler.cs`
- Create: `src/Awake.API/Controllers/PlayersController.cs`
- Modify: `src/Awake.Application/Common/Interfaces/Repositories/ISquadRepository.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Repositories/SquadRepository.cs`
- Test: `tests/Awake.Unit.Tests/Features/Players/GetPlayerProfileQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IPlayerStatsSnapshotRepository` (Task 2), `IPlayerDataAggregator.ForceRefreshAsync` (Task 2), `ITicketRepository.GetByAuthorAsync` (existing), `ICurrentUserService` (existing)
- Produces:
  - `ISquadRepository.GetMembershipByUserIdAsync(Guid userId, CancellationToken ct = default)` → `Task<SquadMember?>` (with `Squad` included)
  - `record GetPlayerProfileQuery(Guid UserId) : IRequest<Result<PlayerProfileDto>>;`
  - DTO (см. Step 2)
  - `GET /api/players/me`, `GET /api/players/{userId:guid}`, `POST /api/players/me/stats/refresh`

- [ ] **Step 1: Add squad membership lookup**

`ISquadRepository.cs` — add:

```csharp
    Task<SquadMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken ct = default);
```

`SquadRepository.cs` — add:

```csharp
    public async Task<SquadMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.SquadMembers
            .Include(m => m.Squad)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
```

- [ ] **Step 2: Create DTO**

Create `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/PlayerProfileDto.cs`:

```csharp
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public record PlayerSquadDto(Guid Id, string Name, int Number, bool IsLeader);

public record PlayerStatsDto(
    int Kills, int Deaths, double KdRatio,
    string Accuracy, string Playtime,
    IReadOnlyList<ClanEntry> ClanHistory,
    DateTime FetchedAt);

public record PlayerProfileDto(
    Guid UserId,
    string Username,
    string? DiscordUsername,
    string? DiscordAvatarUrl,
    UserRank Rank,
    string? GameNickname,
    PlayerSquadDto? Squad,
    PlayerStatsDto? Stats,
    Loadout? Loadout);
```

- [ ] **Step 3: Create query**

Create `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQuery.cs`:

```csharp
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public record GetPlayerProfileQuery(Guid UserId) : IRequest<Result<PlayerProfileDto>>;
```

- [ ] **Step 4: Write failing handler tests**

Create `tests/Awake.Unit.Tests/Features/Players/GetPlayerProfileQueryHandlerTests.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Players.Queries.GetPlayerProfile;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using Awake.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Players;

public class GetPlayerProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISquadRepository> _squads = new();
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();

    private GetPlayerProfileQueryHandler BuildHandler() =>
        new(_users.Object, _squads.Object, _tickets.Object, _snapshots.Object);

    private static User MakeUser(Guid id) => new()
    {
        Id = id, Username = "OopsITry", Rank = UserRank.Member,
        GameNickname = "OopsITry", DiscordUsername = "oops",
        DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/1/a.png",
    };

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FullProfile_MapsEverything()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var squad = new Squad { Name = "Alpha", Number = 1 };
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SquadMember { Squad = squad, SquadId = squad.Id, UserId = id, IsLeader = true });

        var loadout = new Loadout(null,
            new LoadoutSlot("w1", "AK-74", "icon", 5),
            new LoadoutSlot("a1", "Armor", "icon", 3));
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket { AuthorId = id, GameNickname = "OopsITry", Loadout = loadout }]);

        _snapshots.Setup(s => s.GetByNicknameAsync("OopsITry", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlayerStatsSnapshot
                  {
                      GameNickname = "OopsITry", Kills = 100, Deaths = 50, KdRatio = 2.0,
                      Accuracy = "45%", Playtime = "10 days",
                      ClanHistory = [new ClanEntry("Awake", "LOVE", "")],
                      FetchedAt = DateTime.UtcNow,
                  });

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Squad!.Name.Should().Be("Alpha");
        dto.Squad.IsLeader.Should().BeTrue();
        dto.Stats!.Kills.Should().Be(100);
        dto.Stats.ClanHistory.Should().HaveCount(1);
        dto.Loadout!.Weapon.ItemName.Should().Be("AK-74");
    }

    [Fact]
    public async Task Handle_NoSquadNoStatsNoLoadout_ReturnsNulls()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        user.GameNickname = null;
        _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _squads.Setup(s => s.GetMembershipByUserIdAsync(id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((SquadMember?)null);
        _tickets.Setup(t => t.GetByAuthorAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

        var result = await BuildHandler().Handle(new GetPlayerProfileQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Squad.Should().BeNull();
        result.Value.Stats.Should().BeNull();
        result.Value.Loadout.Should().BeNull();
    }
}
```

Note: `LoadoutSlot` is `record LoadoutSlot(string ItemId, string ItemName, string ItemIcon, int Upgrade = 0)` — the positional args above match it.

- [ ] **Step 5: Run tests to verify they fail**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter GetPlayerProfileQueryHandlerTests
```

Expected: FAIL — handler does not exist.

- [ ] **Step 6: Implement handler**

Create `src/Awake.Application/Features/Players/Queries/GetPlayerProfile/GetPlayerProfileQueryHandler.cs`:

```csharp
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Players.Queries.GetPlayerProfile;

public class GetPlayerProfileQueryHandler(
    IUserRepository userRepository,
    ISquadRepository squadRepository,
    ITicketRepository ticketRepository,
    IPlayerStatsSnapshotRepository snapshotRepository
) : IRequestHandler<GetPlayerProfileQuery, Result<PlayerProfileDto>>
{
    public async Task<Result<PlayerProfileDto>> Handle(
        GetPlayerProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<PlayerProfileDto>.Failure("Пользователь не найден.");

        var membership = await squadRepository
            .GetMembershipByUserIdAsync(user.Id, cancellationToken);
        var squad = membership is null
            ? null
            : new PlayerSquadDto(membership.SquadId, membership.Squad.Name,
                membership.Squad.Number, membership.IsLeader);

        PlayerStatsDto? stats = null;
        if (!string.IsNullOrEmpty(user.GameNickname))
        {
            var snapshot = await snapshotRepository
                .GetByNicknameAsync(user.GameNickname, cancellationToken);
            if (snapshot is not null)
            {
                stats = new PlayerStatsDto(
                    snapshot.Kills, snapshot.Deaths, snapshot.KdRatio,
                    snapshot.Accuracy, snapshot.Playtime,
                    snapshot.ClanHistory, snapshot.FetchedAt);
            }
        }

        // Экипировка — из самой свежей заявки с заполненным Loadout
        var tickets = await ticketRepository.GetByAuthorAsync(user.Id, cancellationToken);
        var loadout = tickets.FirstOrDefault(t => t.Loadout is not null)?.Loadout;

        return Result<PlayerProfileDto>.Success(new PlayerProfileDto(
            user.Id, user.Username, user.DiscordUsername, user.DiscordAvatarUrl,
            user.Rank, user.GameNickname, squad, stats, loadout));
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj --filter GetPlayerProfileQueryHandlerTests
```

Expected: PASS (3 tests).

- [ ] **Step 8: Create PlayersController**

Create `src/Awake.API/Controllers/PlayersController.cs`:

```csharp
using Awake.API.Filters;
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Players.Queries.GetPlayerProfile;
using Awake.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/players")]
[Authorize]
public class PlayersController(
    ISender sender,
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPlayerDataAggregator playerData
) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerProfileQuery(currentUser.UserId), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    [HttpGet("{userId:guid}")]
    [RankAuthorize(UserRank.Member)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPlayerProfileQuery(userId), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    // 202 сразу; обновление в фоне (FlareSolverr — 15–30 c). 429 — кулдаун 10 минут.
    [HttpPost("me/stats/refresh")]
    public async Task<IActionResult> RefreshMyStats(CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct);
        if (user is null || string.IsNullOrEmpty(user.GameNickname))
            return Problem(detail: "Игровой ник не привязан.", statusCode: StatusCodes.Status400BadRequest);

        var nickname = user.GameNickname;
        var accepted = await Task.Run(async () =>
        {
            // Проверка кулдауна синхронна внутри ForceRefreshAsync; сам fetch — в фоне
            return await playerData.ForceRefreshAsync(nickname, CancellationToken.None);
        }, CancellationToken.None);

        return accepted
            ? Accepted()
            : Problem(detail: "Обновлять статистику можно не чаще раза в 10 минут.",
                statusCode: StatusCodes.Status429TooManyRequests);
    }
}
```

Note: `ForceRefreshAsync` performs the fetch inline (15–30 s). To return 202 immediately while still honoring the cooldown, split it: the controller calls `ForceRefreshAsync` wrapped in `Task.Run` **without awaiting the fetch**. Simplest correct version — change the controller body to:

```csharp
        var nickname = user.GameNickname;
        // Кулдаун проверяем через TryBeginForceRefresh-семантику: запускаем задачу,
        // но ответ отдаём сразу. ForceRefreshAsync вернёт false мгновенно при кулдауне.
        var refreshTask = playerData.ForceRefreshAsync(nickname, CancellationToken.None);
        var completedQuickly = await Task.WhenAny(refreshTask, Task.Delay(500, ct)) == refreshTask;
        if (completedQuickly && !await refreshTask)
            return Problem(detail: "Обновлять статистику можно не чаще раза в 10 минут.",
                statusCode: StatusCodes.Status429TooManyRequests);

        return Accepted();
```

Use this second version (cooldown rejection returns instantly; a real fetch takes >500 ms so we return 202 while it continues in background).

- [ ] **Step 9: Build + run all tests**

```powershell
dotnet build
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj
```

Expected: build clean, all tests PASS.

- [ ] **Step 10: Commit**

```powershell
git add -A
git commit -m "feat(players): profile API — GET me/{userId}, background stats refresh"
```

---

### Task 7: Frontend — Discord-only auth

**Files:**
- Modify: `frontend/awake-web/src/routes/login.tsx` (full rewrite)
- Create: `frontend/awake-web/src/routes/auth.callback.tsx`
- Delete: `frontend/awake-web/src/routes/register.tsx`
- Modify: `frontend/awake-web/src/api/auth.ts`
- Modify: `frontend/awake-web/src/types/api.ts` (remove `RegisterResponse`)

**Interfaces:**
- Consumes: `GET /api/auth/discord/login` (Task 5), fragment params `accessToken, username, rank, userId` (Task 5), `useAuthStore.login(user, token)` (existing)
- Produces: routes `/login`, `/auth/callback`

- [ ] **Step 1: Rewrite login page**

Replace `frontend/awake-web/src/routes/login.tsx` contents:

```tsx
import { createFileRoute } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'

export const Route = createFileRoute('/login')({
  component: LoginPage,
  validateSearch: (search: Record<string, unknown>): { error?: string } => ({
    error: typeof search.error === 'string' ? search.error : undefined,
  }),
})

const API_URL = import.meta.env.VITE_API_URL ?? ''

function LoginPage() {
  const { error } = Route.useSearch()

  return (
    <div className="min-h-screen bg-background flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        {/* Brand */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-2">
            <div className="w-2 h-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
            <span className="font-bold text-foreground text-lg">
              Awake <span className="text-accent">[LOVE]</span>
            </span>
          </div>
          <p className="text-xs text-muted-foreground">STALCRAFT · Clan Platform</p>
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
              <p className="text-sm text-destructive text-center">
                Не удалось войти через Discord. Попробуй ещё раз.
              </p>
            )}
            <Button asChild className="w-full">
              <a href={`${API_URL}/api/auth/discord/login`}>
                Войти через Discord
              </a>
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
```

Note: `Button` supports `asChild` (Radix Slot) — verified in `frontend/awake-web/src/components/ui/button.tsx:39-44`.

- [ ] **Step 2: Create callback route**

Create `frontend/awake-web/src/routes/auth.callback.tsx`:

```tsx
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect } from 'react'
import { useAuthStore } from '@/store/authStore'
import type { UserRank } from '@/types/api'

export const Route = createFileRoute('/auth/callback')({
  component: AuthCallbackPage,
})

function AuthCallbackPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  useEffect(() => {
    const params = new URLSearchParams(window.location.hash.slice(1))
    const accessToken = params.get('accessToken')
    const username = params.get('username')
    const rank = params.get('rank')
    const userId = params.get('userId')

    // Убираем токен из адресной строки сразу после чтения
    window.history.replaceState(null, '', window.location.pathname)

    if (!accessToken || !username || rank === null || !userId) {
      void navigate({ to: '/login', search: { error: 'discord' } })
      return
    }

    login(
      { userId, username, rank: Number(rank) as UserRank },
      accessToken,
    )
    void navigate({ to: '/dashboard' })
  }, [login, navigate])

  return (
    <div className="min-h-screen bg-background flex items-center justify-center">
      <p className="text-muted-foreground">Входим…</p>
    </div>
  )
}
```

- [ ] **Step 3: Delete register page + clean API layer**

```powershell
Remove-Item frontend/awake-web/src/routes/register.tsx
```

Replace `frontend/awake-web/src/api/auth.ts` contents:

```ts
import { apiClient } from './client'
import type { LoginResponse } from '@/types/api'

export const authApi = {
  refresh: () => apiClient.post<LoginResponse>('/auth/refresh'),
}
```

In `frontend/awake-web/src/types/api.ts` delete the `RegisterResponse` interface.
Search for leftover references (login form links, i18n keys usage is fine to leave):

```powershell
cd frontend/awake-web
npx tsc --noEmit 2>&1 | Select-Object -First 30
```

Fix every error (typical: imports of `RegisterResponse`, `<Link to="/register">`).

- [ ] **Step 4: Build to verify**

```powershell
cd frontend/awake-web
npm run build
```

Expected: build succeeds; routeTree regenerates with `/auth/callback`, without `/register`.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(web)!: Discord-only login — OAuth callback route, remove register page"
```

---

### Task 8: Frontend — profile pages

**Files:**
- Create: `frontend/awake-web/src/api/players.ts`
- Create: `frontend/awake-web/src/components/PlayerProfileView.tsx`
- Create: `frontend/awake-web/src/routes/_auth.profile.tsx`
- Create: `frontend/awake-web/src/routes/_auth.players.$userId.tsx`
- Modify: `frontend/awake-web/src/types/api.ts` (profile types)
- Modify: `frontend/awake-web/src/components/layout/Sidebar.tsx` (nav link «Профиль»)

**Interfaces:**
- Consumes: `GET /api/players/me`, `GET /api/players/{userId}`, `POST /api/players/me/stats/refresh` (Task 6), `Loadout`, `ClanEntry`, `UserRank` types (existing)
- Produces: routes `/profile`, `/players/$userId`

- [ ] **Step 1: Add profile types**

In `frontend/awake-web/src/types/api.ts` add:

```ts
export interface PlayerSquadDto {
  id: string
  name: string
  number: number
  isLeader: boolean
}

export interface PlayerStatsDto {
  kills: number
  deaths: number
  kdRatio: number
  accuracy: string
  playtime: string
  clanHistory: ClanEntry[]
  fetchedAt: string
}

export interface PlayerProfileDto {
  userId: string
  username: string
  discordUsername: string | null
  discordAvatarUrl: string | null
  rank: UserRank
  gameNickname: string | null
  squad: PlayerSquadDto | null
  stats: PlayerStatsDto | null
  loadout: Loadout | null
}
```

- [ ] **Step 2: Create players API module**

Create `frontend/awake-web/src/api/players.ts`:

```ts
import { apiClient } from './client'
import type { PlayerProfileDto } from '@/types/api'

export const playersApi = {
  getMyProfile: () => apiClient.get<PlayerProfileDto>('/players/me'),
  getProfile: (userId: string) => apiClient.get<PlayerProfileDto>(`/players/${userId}`),
  refreshMyStats: () => apiClient.post<void>('/players/me/stats/refresh'),
}
```

- [ ] **Step 3: Create shared profile view component**

Create `frontend/awake-web/src/components/PlayerProfileView.tsx` (used by both routes; refresh button only on own profile):

```tsx
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { PlayerProfileDto } from '@/types/api'

const RANK_LABELS: Record<number, string> = {
  0: 'Гость', 1: 'Боец', 2: 'Офицер', 3: 'Полковник', 4: 'Лидер',
}

function timeAgo(iso: string): string {
  const hours = Math.floor((Date.now() - new Date(iso).getTime()) / 3_600_000)
  if (hours < 1) return 'меньше часа назад'
  if (hours < 24) return `${hours} ч. назад`
  return `${Math.floor(hours / 24)} дн. назад`
}

interface Props {
  profile: PlayerProfileDto
  onRefresh?: () => void
  refreshing?: boolean
}

export function PlayerProfileView({ profile, onRefresh, refreshing }: Props) {
  const { stats, loadout, squad } = profile

  return (
    <div className="flex flex-col gap-6">
      {/* Шапка: аватар + ники + ранг + отряд */}
      <div className="flex items-center gap-4">
        {profile.discordAvatarUrl ? (
          <img
            src={profile.discordAvatarUrl}
            alt={profile.username}
            className="w-16 h-16 rounded-full border border-border"
          />
        ) : (
          <div className="w-16 h-16 rounded-full bg-muted flex items-center justify-center text-2xl">
            {profile.username[0]?.toUpperCase()}
          </div>
        )}
        <div>
          <h1 className="text-xl font-bold text-foreground">{profile.username}</h1>
          <p className="text-sm text-muted-foreground">
            {profile.gameNickname ?? 'игровой ник не привязан'}
            {profile.discordUsername && ` · @${profile.discordUsername}`}
          </p>
          <div className="flex gap-2 mt-1">
            <Badge>{RANK_LABELS[profile.rank]}</Badge>
            {squad && (
              <Badge variant="outline">
                Отряд {squad.number} «{squad.name}»{squad.isLeader && ' · лидер'}
              </Badge>
            )}
          </div>
        </div>
      </div>

      {/* Статистика */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Игровая статистика</CardTitle>
          {stats && onRefresh && (
            <div className="flex items-center gap-3">
              <span className="text-xs text-muted-foreground">
                обновлено {timeAgo(stats.fetchedAt)}
              </span>
              <Button size="sm" variant="outline" onClick={onRefresh} disabled={refreshing}>
                {refreshing ? 'Обновляется…' : 'Обновить'}
              </Button>
            </div>
          )}
        </CardHeader>
        <CardContent>
          {stats ? (
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
              <StatTile label="Убийства" value={stats.kills.toLocaleString('ru-RU')} />
              <StatTile label="Смерти" value={stats.deaths.toLocaleString('ru-RU')} />
              <StatTile label="К/Д" value={stats.kdRatio.toFixed(2)} />
              <StatTile label="Точность" value={stats.accuracy} />
              <StatTile label="Время в игре" value={stats.playtime} />
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              Статистика ещё не загружена.
              {onRefresh && (
                <Button size="sm" variant="outline" className="ml-2" onClick={onRefresh} disabled={refreshing}>
                  {refreshing ? 'Загружается…' : 'Загрузить'}
                </Button>
              )}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Экипировка */}
      {loadout && (
        <Card>
          <CardHeader><CardTitle>Экипировка</CardTitle></CardHeader>
          <CardContent className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {loadout.sniper && <LoadoutTile label="Снайперка" slot={loadout.sniper} />}
            <LoadoutTile label="Основное оружие" slot={loadout.weapon} />
            <LoadoutTile label="Броня" slot={loadout.armor} />
          </CardContent>
        </Card>
      )}

      {/* История кланов */}
      {stats && stats.clanHistory.length > 0 && (
        <Card>
          <CardHeader><CardTitle>История кланов</CardTitle></CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1">
              {stats.clanHistory.map((c) => (
                <li key={c.clanTag} className="text-sm text-foreground">
                  <span className="text-accent font-medium">[{c.clanTag}]</span> {c.clanName}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

function StatTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-bold text-foreground">{value}</p>
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

- [ ] **Step 4: Create own-profile route**

Create `frontend/awake-web/src/routes/_auth.profile.tsx`:

```tsx
import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { playersApi } from '@/api/players'
import { PlayerProfileView } from '@/components/PlayerProfileView'

export const Route = createFileRoute('/_auth/profile')({
  component: ProfilePage,
})

function ProfilePage() {
  const queryClient = useQueryClient()
  const [refreshing, setRefreshing] = useState(false)

  const { data: profile, isLoading, error } = useQuery({
    queryKey: ['players', 'me'],
    queryFn: playersApi.getMyProfile,
  })

  async function handleRefresh() {
    setRefreshing(true)
    try {
      await playersApi.refreshMyStats()
      // Обновление идёт в фоне 15–30 c — перезапрашиваем профиль с задержкой
      setTimeout(() => {
        void queryClient.invalidateQueries({ queryKey: ['players', 'me'] })
        setRefreshing(false)
      }, 30_000)
    } catch {
      setRefreshing(false)
    }
  }

  if (isLoading) return <p className="text-muted-foreground">Загрузка…</p>
  if (error || !profile) return <p className="text-destructive">Не удалось загрузить профиль.</p>

  return (
    <PlayerProfileView profile={profile} onRefresh={handleRefresh} refreshing={refreshing} />
  )
}
```

- [ ] **Step 5: Create other-player route**

Create `frontend/awake-web/src/routes/_auth.players.$userId.tsx`:

```tsx
import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { playersApi } from '@/api/players'
import { ApiError } from '@/api/client'
import { PlayerProfileView } from '@/components/PlayerProfileView'
import { useAuth } from '@/hooks/useAuth'
import { UserRank } from '@/types/api'

export const Route = createFileRoute('/_auth/players/$userId')({
  component: PlayerPage,
})

function PlayerPage() {
  const { userId } = Route.useParams()
  const { rank, user } = useAuth()

  const { data: profile, isLoading, error } = useQuery({
    queryKey: ['players', userId],
    queryFn: () => playersApi.getProfile(userId),
    retry: false,
  })

  // Гость может смотреть только свой профиль
  if (rank < UserRank.Member && user?.userId !== userId) {
    return <Navigate to="/profile" />
  }
  if (user?.userId === userId) {
    return <Navigate to="/profile" />
  }

  if (isLoading) return <p className="text-muted-foreground">Загрузка…</p>
  if (error instanceof ApiError && error.status === 403) return <Navigate to="/profile" />
  if (error || !profile) return <p className="text-destructive">Не удалось загрузить профиль.</p>

  return <PlayerProfileView profile={profile} />
}
```

- [ ] **Step 6: Add nav link**

In `frontend/awake-web/src/components/layout/Sidebar.tsx`:

Add `UserCircle` to the existing lucide-react import (line 9-19):

```tsx
import {
  LayoutDashboard,
  Shield,
  FileText,
  Settings,
  Users,
  UserCircle,
  LogOut,
  Menu,
  X,
  ChevronRight,
} from 'lucide-react'
```

Add the profile item to `navLinks` (line 42-46), right after dashboard:

```tsx
  const navLinks = [
    { to: '/dashboard' as const, label: t('nav.dashboard'), icon: LayoutDashboard },
    { to: '/profile' as const, label: 'Профиль', icon: UserCircle },
    { to: '/squads' as const, label: t('nav.squads'), icon: Shield },
    { to: '/tickets' as const, label: t('nav.tickets'), icon: FileText },
  ]
```

(Plain string label is fine — RU is the default locale; add `nav.profile` i18n key later with the Stage 5 localization pass.)

- [ ] **Step 7: Build**

```powershell
cd frontend/awake-web
npm run build
```

Expected: success; routeTree includes `/_auth/profile` and `/_auth/players/$userId`.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(web): player profile pages — stats, loadout, squad, clan history"
```

---

### Task 9: End-to-end verification

**Files:** none (verification only)

**Interfaces:**
- Consumes: everything above

- [ ] **Step 1: Configure Discord OAuth app**

In Discord Developer Portal → the bot's application → OAuth2:
- Add redirect URI `http://localhost:5001/api/auth/discord/callback` (docker) and `http://localhost:5000/api/auth/discord/callback` (local dev)
- Copy Client Secret → `.env` as `DISCORD_CLIENT_SECRET=...`

- [ ] **Step 2: Rebuild and start containers**

```powershell
cd D:\Awake\.claude\worktrees\feature+stage-4
docker-compose up --build -d
docker-compose logs -f api
```

Expected in logs: migration `AddDiscordAuthAndStatsSnapshots` applied.

- [ ] **Step 3: Full flow test**

1. Create a Discord ticket (Submit Application) with a real game nickname → logs show stats fetch → row appears in `PlayerStatsSnapshots` (`docker-compose exec db psql -U postgres -d awake_dev -c 'SELECT "GameNickname", "Kills", "FetchedAt" FROM "PlayerStatsSnapshots";'`).
2. Open `http://localhost:5173/login` → «Войти через Discord» → authorize → lands on `/dashboard`.
3. Check DB: `SELECT "Username", "DiscordUserId", "GameNickname" FROM "Users";` — Discord fields filled, `GameNickname` copied from the ticket; ticket's `AuthorId` now set.
4. Open `/profile` — stats, loadout and rank displayed instantly.
5. Press «Обновить» → 202; second press within 10 min → error toast/message (429).
6. Log in as a second (guest) user, try `/players/{first-user-id}` → redirected to own profile.

- [ ] **Step 4: Full test suite + builds**

```powershell
dotnet test tests/Awake.Unit.Tests/Awake.Unit.Tests.csproj
cd frontend/awake-web; npm run build
```

Expected: all green.

- [ ] **Step 5: Final commit + push**

```powershell
git add -A
git commit -m "chore: e2e verification fixes for Discord OAuth profiles" --allow-empty
git push origin worktree-feature+stage-4
```
