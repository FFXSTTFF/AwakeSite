# Item Integration — Ticket Loadout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a loadout (3 item slots: sniper, main weapon, armor) to Recruitment tickets, backed by the EXBO-Studio/stalzone-database item database, with autocomplete search on the frontend.

**Architecture:** A singleton `ItemCacheService` holds items in memory; `ItemSyncHostedService` populates it on startup and refreshes every Wednesday (game update day). A new `GET /api/items/search` endpoint serves autocomplete results. The loadout snapshot (itemId + itemName + itemIcon) is stored as JSONB in the `Tickets` table.

**Tech Stack:** C# 12, ASP.NET Core 8, EF Core 9 (Npgsql), React 18, TanStack Query, Tailwind CSS, Moq + FluentAssertions (tests)

## Global Constraints

- All allowed item ranks: `RANK_VETERAN` (purple), `RANK_MASTER` (red), `RANK_LEGEND` (gold) — no other colors shown in search
- Item data source: `https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru/listing.json`
- Item icon base URL: `https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru`
- Sniper slot (`weapon/sniper_rifle`) is optional (nullable); main weapon and armor are required
- TicketType.Appeal is removed from UI only — enum value stays in code to avoid DB migration
- All new Russian UI text in `frontend/awake-web/src/i18n/ru.json`

---

## File Map

**Create:**
- `src/Awake.Application/Features/Items/Dtos/ItemDto.cs`
- `src/Awake.Application/Common/Interfaces/IItemCacheService.cs`
- `src/Awake.Infrastructure/ExternalServices/Items/ItemCacheService.cs`
- `src/Awake.Infrastructure/ExternalServices/Items/ItemSyncHostedService.cs`
- `src/Awake.Application/Features/Tickets/Dtos/LoadoutSlotDto.cs`
- `src/Awake.Application/Features/Tickets/Dtos/LoadoutDto.cs`
- `src/Awake.API/Controllers/ItemsController.cs`
- `src/Awake.Infrastructure/Persistence/Migrations/<timestamp>_AddTicketLoadout.cs` (generated)
- `tests/Awake.Unit.Tests/Features/Items/ItemCacheServiceTests.cs`
- `frontend/awake-web/src/api/items.ts`
- `frontend/awake-web/src/components/ItemCombobox.tsx`

**Modify:**
- `src/Awake.Infrastructure/DependencyInjection.cs`
- `src/Awake.Domain/Entities/Ticket.cs`
- `src/Awake.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`
- `src/Awake.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`
- `src/Awake.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`
- `src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs`
- `src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`
- `src/Awake.API/Controllers/TicketsController.cs`
- `src/Awake.Application/Common/Interfaces/IDiscordBotService.cs`
- `src/Awake.Infrastructure/ExternalServices/Discord/DiscordBotService.cs`
- `tests/Awake.Unit.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs`
- `frontend/awake-web/src/types/api.ts`
- `frontend/awake-web/src/i18n/ru.json`
- `frontend/awake-web/src/api/tickets.ts`
- `frontend/awake-web/src/routes/_auth.tickets.new.tsx`
- `frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx`

---

### Task 1: Item cache infrastructure

**Files:**
- Create: `src/Awake.Application/Features/Items/Dtos/ItemDto.cs`
- Create: `src/Awake.Application/Common/Interfaces/IItemCacheService.cs`
- Create: `src/Awake.Infrastructure/ExternalServices/Items/ItemCacheService.cs`
- Create: `src/Awake.Infrastructure/ExternalServices/Items/ItemSyncHostedService.cs`
- Create: `tests/Awake.Unit.Tests/Features/Items/ItemCacheServiceTests.cs`
- Modify: `src/Awake.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: `IItemCacheService.Search(string q, string? categoryPrefix, string? excludeCategoryPrefix)` → `IEnumerable<ItemDto>`
- Produces: `ItemDto(string Id, string Category, string NameRu, string Icon, string Color)`

- [ ] **Step 1: Create `ItemDto`**

```csharp
// src/Awake.Application/Features/Items/Dtos/ItemDto.cs
namespace Awake.Application.Features.Items.Dtos;

public record ItemDto(string Id, string Category, string NameRu, string Icon, string Color);
```

- [ ] **Step 2: Create `IItemCacheService`**

```csharp
// src/Awake.Application/Common/Interfaces/IItemCacheService.cs
using Awake.Application.Features.Items.Dtos;

namespace Awake.Application.Common.Interfaces;

public interface IItemCacheService
{
    void Load(IEnumerable<ItemDto> items);
    IEnumerable<ItemDto> Search(string q, string? categoryPrefix, string? excludeCategoryPrefix = null);
    int Count { get; }
}
```

- [ ] **Step 3: Write failing test**

```csharp
// tests/Awake.Unit.Tests/Features/Items/ItemCacheServiceTests.cs
using Awake.Application.Features.Items.Dtos;
using Awake.Infrastructure.ExternalServices.Items;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Items;

public class ItemCacheServiceTests
{
    private static ItemDto MakeItem(string id, string category, string name, string color) =>
        new(id, category, name, $"/icons/{category}/{id}.png", color);

    [Fact]
    public void Search_FiltersByColor_ExcludesLowRankItems()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("a1", "weapon/sniper_rifle", "СКТ-40", "RANK_VETERAN"),
            MakeItem("a2", "weapon/sniper_rifle", "Дешёвая снайперка", "RANK_NEWBIE"),
        ]);

        var results = svc.Search("С", null).ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("a1");
    }

    [Fact]
    public void Search_FiltersByCategory()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("s1", "weapon/sniper_rifle", "СКТ-40", "RANK_MASTER"),
            MakeItem("ar1", "armor/combat", "Страж", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "weapon/sniper_rifle").ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("s1");
    }

    [Fact]
    public void Search_ExcludesCategoryPrefix()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("s1", "weapon/sniper_rifle", "СКТ-40", "RANK_MASTER"),
        ]);

        var results = svc.Search("", "weapon", "weapon/sniper_rifle").ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("w1");
    }

    [Fact]
    public void Search_FiltersByNameQuery()
    {
        var svc = new ItemCacheService();
        svc.Load([
            MakeItem("w1", "weapon/assault_rifle", "АК-74М", "RANK_MASTER"),
            MakeItem("w2", "weapon/assault_rifle", "Страж-2", "RANK_MASTER"),
        ]);

        var results = svc.Search("АК", null).ToList();

        results.Should().HaveCount(1);
        results[0].Id.Should().Be("w1");
    }

    [Fact]
    public void Search_ReturnsMax15Results()
    {
        var svc = new ItemCacheService();
        svc.Load(Enumerable.Range(1, 20).Select(i =>
            MakeItem($"id{i}", "weapon/assault_rifle", $"Оружие {i}", "RANK_MASTER")));

        var results = svc.Search("", null).ToList();

        results.Should().HaveCount(15);
    }
}
```

- [ ] **Step 4: Run tests to confirm they fail**

```
cd D:\Awake\.claude\worktrees\feature+stage-4
dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~ItemCacheService" --no-build 2>&1 | tail -5
```
Expected: build error — `ItemCacheService` not found.

- [ ] **Step 5: Create `ItemCacheService`**

```csharp
// src/Awake.Infrastructure/ExternalServices/Items/ItemCacheService.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Items.Dtos;

namespace Awake.Infrastructure.ExternalServices.Items;

public class ItemCacheService : IItemCacheService
{
    private static readonly HashSet<string> AllowedColors =
        ["RANK_VETERAN", "RANK_MASTER", "RANK_LEGEND"];

    private Dictionary<string, ItemDto> _items = [];

    public int Count => _items.Count;

    public void Load(IEnumerable<ItemDto> items) =>
        _items = items.ToDictionary(x => x.Id);

    public IEnumerable<ItemDto> Search(string q, string? categoryPrefix, string? excludeCategoryPrefix = null) =>
        _items.Values
            .Where(x => AllowedColors.Contains(x.Color))
            .Where(x => categoryPrefix == null || x.Category.StartsWith(categoryPrefix))
            .Where(x => excludeCategoryPrefix == null || !x.Category.StartsWith(excludeCategoryPrefix))
            .Where(x => string.IsNullOrEmpty(q) || x.NameRu.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(15);
}
```

- [ ] **Step 6: Create `ItemSyncHostedService`**

```csharp
// src/Awake.Infrastructure/ExternalServices/Items/ItemSyncHostedService.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Items.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Awake.Infrastructure.ExternalServices.Items;

public class ItemSyncHostedService(
    IItemCacheService cache,
    IHttpClientFactory httpClientFactory,
    ILogger<ItemSyncHostedService> logger
) : IHostedService, IDisposable
{
    private const string ListingUrl =
        "https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru/listing.json";

    private const string IconBase =
        "https://raw.githubusercontent.com/EXBO-Studio/stalzone-database/main/ru";

    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SyncAsync();
        _timer = new Timer(_ => Task.Run(SyncAsync), null, TimeUntilNextWednesday(), TimeSpan.FromDays(7));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private async Task SyncAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("stalzone");
            var json = await client.GetStringAsync(ListingUrl);
            var entries = JsonSerializer.Deserialize<List<ListingEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (entries is null) return;

            var items = entries
                .Where(e => !string.IsNullOrEmpty(e.Data) &&
                            (e.Data.StartsWith("/items/weapon/") || e.Data.StartsWith("/items/armor/")))
                .Select(e =>
                {
                    var parts = e.Data.Split('/');
                    // e.Data = "/items/weapon/sniper_rifle/1r79g.json"
                    // parts  = ["", "items", "weapon", "sniper_rifle", "1r79g.json"]
                    var id = parts[^1].Replace(".json", "");
                    var category = string.Join("/", parts[2..^1]);
                    var nameRu = e.Name?.Lines?.GetValueOrDefault("ru") ?? id;
                    var icon = IconBase + e.Icon;
                    return new ItemDto(id, category, nameRu, icon, e.Color ?? "");
                })
                .Where(x => !string.IsNullOrEmpty(x.NameRu));

            cache.Load(items);
            logger.LogInformation("Item cache refreshed: {Count} items loaded", cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync item cache from stalzone-database");
        }
    }

    private static TimeSpan TimeUntilNextWednesday()
    {
        var now = DateTime.UtcNow;
        var daysUntil = ((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        var next = now.Date.AddDays(daysUntil);
        return next - now;
    }
}

file class ListingEntry
{
    public string Data { get; set; } = "";
    public string Icon { get; set; } = "";
    public ListingName? Name { get; set; }
    public string? Color { get; set; }
}

file class ListingName
{
    public Dictionary<string, string>? Lines { get; set; }
}
```

- [ ] **Step 7: Register services in DI**

In `src/Awake.Infrastructure/DependencyInjection.cs`, add after the Discord block:

```csharp
// Items cache
services.AddHttpClient("stalzone");
services.AddSingleton<IItemCacheService, ItemCacheService>();
services.AddHostedService<ItemSyncHostedService>();
```

- [ ] **Step 8: Run tests**

```
dotnet test tests/Awake.Unit.Tests --filter "FullyQualifiedName~ItemCacheService" -v minimal
```
Expected: 5 tests pass.

- [ ] **Step 9: Build backend to confirm no errors**

```
dotnet build src/Awake.API/Awake.API.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 10: Commit**

```
git add src/Awake.Application/Features/Items/ src/Awake.Application/Common/Interfaces/IItemCacheService.cs src/Awake.Infrastructure/ExternalServices/Items/ tests/Awake.Unit.Tests/Features/Items/ src/Awake.Infrastructure/DependencyInjection.cs
git commit -m "feat(items): in-memory cache + weekly sync from stalzone-database"
```

---

### Task 2: Items search API endpoint

**Files:**
- Create: `src/Awake.API/Controllers/ItemsController.cs`

**Interfaces:**
- Consumes: `IItemCacheService.Search(string q, string? categoryPrefix, string? excludeCategoryPrefix)`
- Produces: `GET /api/items/search?q=&category=&exclude=` → `ItemDto[]`

- [ ] **Step 1: Create `ItemsController`**

```csharp
// src/Awake.API/Controllers/ItemsController.cs
using Awake.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public class ItemsController(IItemCacheService cache) : ControllerBase
{
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string q = "", [FromQuery] string? category = null, [FromQuery] string? exclude = null)
    {
        if (q.Length < 2 && string.IsNullOrEmpty(category))
            return Ok(Array.Empty<object>());

        var results = cache.Search(q, category, exclude);
        return Ok(results);
    }
}
```

- [ ] **Step 2: Build and verify endpoint registered**

```
dotnet build src/Awake.API/Awake.API.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/Awake.API/Controllers/ItemsController.cs
git commit -m "feat(items): GET /api/items/search endpoint with category and exclude filters"
```

---

### Task 3: Ticket loadout data model + migration

**Files:**
- Create: `src/Awake.Application/Features/Tickets/Dtos/LoadoutSlotDto.cs`
- Create: `src/Awake.Application/Features/Tickets/Dtos/LoadoutDto.cs`
- Modify: `src/Awake.Domain/Entities/Ticket.cs`
- Modify: `src/Awake.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`
- Create: `src/Awake.Infrastructure/Persistence/Migrations/<timestamp>_AddTicketLoadout.cs` (generated)

**Interfaces:**
- Produces: `LoadoutSlotDto(string ItemId, string ItemName, string ItemIcon)`
- Produces: `LoadoutDto(LoadoutSlotDto? Sniper, LoadoutSlotDto Weapon, LoadoutSlotDto Armor)`
- Produces: `Ticket.Loadout` property of type `LoadoutDto?`

- [ ] **Step 1: Create `LoadoutSlotDto`**

```csharp
// src/Awake.Application/Features/Tickets/Dtos/LoadoutSlotDto.cs
namespace Awake.Application.Features.Tickets.Dtos;

public record LoadoutSlotDto(string ItemId, string ItemName, string ItemIcon);
```

- [ ] **Step 2: Create `LoadoutDto`**

```csharp
// src/Awake.Application/Features/Tickets/Dtos/LoadoutDto.cs
namespace Awake.Application.Features.Tickets.Dtos;

public record LoadoutDto(
    LoadoutSlotDto? Sniper,
    LoadoutSlotDto Weapon,
    LoadoutSlotDto Armor
);
```

- [ ] **Step 3: Add `Loadout` to `Ticket` entity**

In `src/Awake.Domain/Entities/Ticket.cs`, add after `DiscordChannelId`:

```csharp
using Awake.Application.Features.Tickets.Dtos;
// ... existing usings ...

// Inside the class, add:
public LoadoutDto? Loadout { get; set; }
```

Full file after edit:
```csharp
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Common;
using Awake.Domain.Enums;

namespace Awake.Domain.Entities;

public class Ticket : BaseEntity
{
    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }

    public string GameNickname { get; set; } = string.Empty;
    public TicketType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = [];

    public string? DiscordChannelId { get; set; }
    public LoadoutDto? Loadout { get; set; }
}
```

- [ ] **Step 4: Add JSONB value conversion in `TicketConfiguration`**

In `src/Awake.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`, add at the end of `Configure`:

```csharp
using System.Text.Json;
using Awake.Application.Features.Tickets.Dtos;
// Add to top of file

// Add inside Configure() before the closing brace:
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
builder.Property(x => x.Loadout)
    .HasColumnType("jsonb")
    .HasConversion(
        v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
        v => v == null ? null : JsonSerializer.Deserialize<LoadoutDto>(v, jsonOptions));
```

Full `Configure` method after edit:
```csharp
public void Configure(EntityTypeBuilder<Ticket> builder)
{
    builder.HasKey(x => x.Id);

    builder.HasOne(x => x.Author)
        .WithMany(x => x.Tickets)
        .HasForeignKey(x => x.AuthorId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Property(x => x.DiscordUserId).HasMaxLength(30);
    builder.Property(x => x.DiscordUsername).HasMaxLength(100);

    builder.HasOne<User>()
        .WithMany()
        .HasForeignKey(x => x.ReviewedBy)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Property(x => x.GameNickname)
        .IsRequired()
        .HasMaxLength(100);

    builder.Property(x => x.Description)
        .IsRequired()
        .HasMaxLength(2000);

    builder.Property(x => x.Status)
        .HasConversion<int>();

    builder.Property(x => x.Type)
        .HasConversion<int>();

    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    builder.Property(x => x.Loadout)
        .HasColumnType("jsonb")
        .HasConversion(
            v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
            v => v == null ? null : JsonSerializer.Deserialize<LoadoutDto>(v, jsonOptions));
}
```

Also add using at top of file:
```csharp
using System.Text.Json;
using Awake.Application.Features.Tickets.Dtos;
```

- [ ] **Step 5: Build to confirm no errors before migration**

```
dotnet build src/Awake.API/Awake.API.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Generate migration**

Stop the backend if running. Then:
```
dotnet ef migrations add AddTicketLoadout --project src/Awake.Infrastructure --startup-project src/Awake.API
```
Expected: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 7: Verify migration SQL adds loadout column**

Open the generated `..._AddTicketLoadout.cs` and confirm it contains:
```csharp
migrationBuilder.AddColumn<string>(
    name: "Loadout",
    table: "Tickets",
    type: "jsonb",
    nullable: true);
```

- [ ] **Step 8: Apply migration to database**

```
dotnet ef database update --project src/Awake.Infrastructure --startup-project src/Awake.API
```
Expected: `Done.`

- [ ] **Step 9: Commit**

```
git add src/Awake.Application/Features/Tickets/Dtos/LoadoutSlotDto.cs src/Awake.Application/Features/Tickets/Dtos/LoadoutDto.cs src/Awake.Domain/Entities/Ticket.cs src/Awake.Infrastructure/Persistence/Configurations/TicketConfiguration.cs src/Awake.Infrastructure/Persistence/Migrations/
git commit -m "feat(tickets): add Loadout JSONB column to Ticket entity"
```

---

### Task 4: Wire loadout through ticket pipeline + Discord embed

**Files:**
- Modify: `src/Awake.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`
- Modify: `src/Awake.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`
- Modify: `src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs`
- Modify: `src/Awake.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`
- Modify: `src/Awake.API/Controllers/TicketsController.cs`
- Modify: `src/Awake.Application/Common/Interfaces/IDiscordBotService.cs`
- Modify: `src/Awake.Infrastructure/ExternalServices/Discord/DiscordBotService.cs`
- Modify: `tests/Awake.Unit.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `LoadoutDto` from Task 3
- Produces: `CreateTicketCommand` with `LoadoutDto? Loadout`
- Produces: `TicketDetailDto` with `LoadoutDto? Loadout`

- [ ] **Step 1: Update `CreateTicketCommand`**

```csharp
// src/Awake.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs
using Awake.Application.Common.Models;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Tickets.Commands.CreateTicket;

public record CreateTicketCommand(
    string GameNickname,
    TicketType Type,
    string Description,
    LoadoutDto? Loadout
) : IRequest<Result<TicketListItemDto>>;
```

- [ ] **Step 2: Update `CreateTicketCommandHandler` to map Loadout**

Replace the `ticket` initialization block in the handler:

```csharp
var ticket = new Ticket
{
    AuthorId = user.Id,
    GameNickname = request.GameNickname,
    Type = request.Type,
    Description = request.Description,
    Status = TicketStatus.Pending,
    Loadout = request.Loadout,
};
```

- [ ] **Step 3: Update `TicketDetailDto`**

```csharp
// src/Awake.Application/Features/Tickets/Dtos/TicketDetailDto.cs
using Awake.Domain.Enums;

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
    object? PlayerData,
    LoadoutDto? Loadout
);
```

- [ ] **Step 4: Update `GetTicketByIdQueryHandler` to include Loadout in DTO**

Change the final `new TicketDetailDto(...)` call:

```csharp
var dto = new TicketDetailDto(
    ticket.Id, ticket.Type, ticket.Status, ticket.GameNickname,
    ticket.Author?.Username ?? ticket.DiscordUsername ?? "Discord",
    ticket.Description, ticket.CreatedAt,
    ticket.ReviewedAt, reviewedByUsername,
    comments, playerData, ticket.Loadout);
```

- [ ] **Step 5: Update `TicketsController` — accept Loadout in request body**

Replace the `CreateTicketRequest` record and `Create` action:

```csharp
public record CreateTicketRequest(string GameNickname, TicketType Type, string Description, LoadoutDto? Loadout);

// In Create action:
var command = new CreateTicketCommand(request.GameNickname, request.Type, request.Description, request.Loadout);
```

Also add using at top:
```csharp
using Awake.Application.Features.Tickets.Dtos;
```

- [ ] **Step 6: Update `IDiscordBotService` — add `LoadoutDto?` param to `PostTicketEmbedAsync`**

```csharp
Task PostTicketEmbedAsync(
    string channelId,
    Guid ticketId,
    string gameNickname,
    string description,
    string discordUsername,
    LoadoutDto? loadout = null,
    CancellationToken ct = default);
```

Also add using:
```csharp
using Awake.Application.Features.Tickets.Dtos;
```

- [ ] **Step 7: Update `DiscordBotService.PostTicketEmbedAsync` to show loadout in embed**

Replace the `PostTicketEmbedAsync` method signature and its `fields` array:

```csharp
public async Task PostTicketEmbedAsync(
    string channelId,
    Guid ticketId,
    string gameNickname,
    string description,
    string discordUsername,
    LoadoutDto? loadout = null,
    CancellationToken ct = default)
{
    if (!EnsureConfigured()) return;
    SetAuth();
    try
    {
        var fields = new List<object>
        {
            new { name = "Applicant", value = discordUsername, inline = true },
            new { name = "Game Nickname", value = gameNickname, inline = true },
            new { name = "Status", value = "⏳ Pending review", inline = false }
        };

        if (loadout is not null)
        {
            fields.Add(new { name = "🎯 Снайперка", value = loadout.Sniper?.ItemName ?? "—", inline = true });
            fields.Add(new { name = "⚔️ Основное оружие", value = loadout.Weapon.ItemName, inline = true });
            fields.Add(new { name = "🛡️ Броня", value = loadout.Armor.ItemName, inline = true });
        }

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = "📋 Application Details",
                    color = BrandColor,
                    description = $"**About the applicant:**\n{description}",
                    fields = fields.ToArray(),
                    footer = new { text = "Officers only: use the buttons below to make a decision." }
                }
            },
            components = new[]
            {
                new
                {
                    type = 1,
                    components = new object[]
                    {
                        new
                        {
                            type = 2,
                            style = 3,
                            label = "Approve",
                            custom_id = $"approve_ticket:{ticketId}",
                            emoji = new { name = "✅" }
                        },
                        new
                        {
                            type = 2,
                            style = 4,
                            label = "Reject",
                            custom_id = $"reject_ticket:{ticketId}",
                            emoji = new { name = "❌" }
                        },
                    }
                }
            }
        };
        await httpClient.PostAsJsonAsync($"{ApiBase}/channels/{channelId}/messages", payload, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to post ticket embed to {ChannelId}", channelId);
    }
}
```

Add `using Awake.Application.Features.Tickets.Dtos;` to the top of `DiscordBotService.cs`.

- [ ] **Step 8: Update `CreateTicketCommandHandlerTests` — fix missing mock + add Loadout test**

```csharp
// tests/Awake.Unit.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs
using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Tickets.Commands.CreateTicket;
using Awake.Application.Features.Tickets.Dtos;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDiscordNotifier> _discord = new();
    private readonly Mock<INotificationService> _notifications = new();

    private CreateTicketCommandHandler BuildHandler() =>
        new(_repo.Object, _userRepo.Object, _currentUser.Object, _discord.Object, _notifications.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesTicketAndNotifies()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "tester" };
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);

        Ticket? savedTicket = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Callback<Ticket, CancellationToken>((t, _) => savedTicket = t)
             .Returns(Task.CompletedTask);

        _discord.Setup(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        _notifications.Setup(n => n.CreateForRankAsync(
            It.IsAny<UserRank>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateTicketCommand("AliceInGame", TicketType.Recruitment, "I want to join.", null);

        var result = await BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AuthorUsername.Should().Be("tester");
        savedTicket!.Loadout.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CommandWithLoadout_SavesLoadoutOnTicket()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = "tester" };
        _currentUser.Setup(s => s.UserId).Returns(userId);
        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);

        Ticket? savedTicket = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
             .Callback<Ticket, CancellationToken>((t, _) => savedTicket = t)
             .Returns(Task.CompletedTask);

        _discord.Setup(d => d.NotifyNewTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        _notifications.Setup(n => n.CreateForRankAsync(
            It.IsAny<UserRank>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loadout = new LoadoutDto(
            Sniper: null,
            Weapon: new LoadoutSlotDto("w1", "АК-74М", "https://example.com/ak.png"),
            Armor: new LoadoutSlotDto("a1", "Страж", "https://example.com/armor.png")
        );

        var command = new CreateTicketCommand("AliceInGame", TicketType.Recruitment, "I want to join.", loadout);

        var result = await BuildHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        savedTicket!.Loadout.Should().NotBeNull();
        savedTicket.Loadout!.Weapon.ItemName.Should().Be("АК-74М");
        savedTicket.Loadout.Sniper.Should().BeNull();
    }
}
```

- [ ] **Step 9: Run all tests**

```
dotnet test tests/Awake.Unit.Tests -v minimal
```
Expected: All tests pass.

- [ ] **Step 10: Build full solution**

```
dotnet build src/Awake.API/Awake.API.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Commit**

```
git add src/Awake.Application/Features/Tickets/ src/Awake.API/Controllers/TicketsController.cs src/Awake.Application/Common/Interfaces/IDiscordBotService.cs src/Awake.Infrastructure/ExternalServices/Discord/DiscordBotService.cs tests/Awake.Unit.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs
git commit -m "feat(tickets): wire loadout through creation pipeline + Discord embed fields"
```

---

### Task 5: Frontend — types, i18n, and API clients

**Files:**
- Modify: `frontend/awake-web/src/types/api.ts`
- Modify: `frontend/awake-web/src/i18n/ru.json`
- Modify: `frontend/awake-web/src/api/tickets.ts`
- Create: `frontend/awake-web/src/api/items.ts`

**Interfaces:**
- Produces: `LoadoutSlot`, `Loadout` TypeScript interfaces
- Produces: `itemsApi.search(q, category?, exclude?)` → `ItemSearchResult[]`
- Produces: `ticketsApi.create` accepts optional `loadout`

- [ ] **Step 1: Add types to `types/api.ts`**

Add at the end of the existing file:

```ts
export interface LoadoutSlot {
  itemId: string
  itemName: string
  itemIcon: string
}

export interface Loadout {
  sniper: LoadoutSlot | null
  weapon: LoadoutSlot
  armor: LoadoutSlot
}

export interface ItemSearchResult {
  id: string
  category: string
  nameRu: string
  icon: string
  color: string
}
```

Also update `TicketDetailDto` interface to include `loadout`:

```ts
// Find existing TicketDetailDto interface and add loadout field:
loadout: Loadout | null
```

- [ ] **Step 2: Add loadout i18n keys to `ru.json`**

Add inside `"tickets"` object (after `"noPlayerData"`):

```json
"loadout": {
  "title": "Сборка",
  "sniper": "Снайперка",
  "sniperOptional": "Снайперка (необязательно)",
  "weapon": "Основное оружие",
  "armor": "Броня",
  "noSniper": "—",
  "search": "Начни вводить название...",
  "noResults": "Ничего не найдено"
}
```

- [ ] **Step 3: Create `api/items.ts`**

```ts
// frontend/awake-web/src/api/items.ts
import { apiClient } from './client'
import type { ItemSearchResult } from '@/types/api'

export const itemsApi = {
  search: (q: string, category?: string, exclude?: string): Promise<ItemSearchResult[]> => {
    const params = new URLSearchParams({ q })
    if (category) params.set('category', category)
    if (exclude) params.set('exclude', exclude)
    return apiClient.get<ItemSearchResult[]>(`/items/search?${params.toString()}`)
  },
}
```

- [ ] **Step 4: Update `api/tickets.ts` — add `loadout` to create payload**

```ts
// frontend/awake-web/src/api/tickets.ts
import { apiClient } from './client'
import type { TicketListItemDto, TicketDetailDto, TicketCommentDto, TicketType, TicketStatus, Loadout } from '@/types/api'

export const ticketsApi = {
  getAll: () => apiClient.get<TicketListItemDto[]>('/tickets'),
  getById: (id: string) => apiClient.get<TicketDetailDto>(`/tickets/${id}`),
  create: (data: { gameNickname: string; type: TicketType; description: string; loadout?: Loadout }) =>
    apiClient.post<TicketListItemDto>('/tickets', data),
  updateStatus: (id: string, newStatus: TicketStatus) =>
    apiClient.put<void>(`/tickets/${id}/status`, { newStatus }),
  addComment: (id: string, content: string) =>
    apiClient.post<TicketCommentDto>(`/tickets/${id}/comments`, { content }),
}
```

- [ ] **Step 5: Commit**

```
git add frontend/awake-web/src/types/api.ts frontend/awake-web/src/i18n/ru.json frontend/awake-web/src/api/items.ts frontend/awake-web/src/api/tickets.ts
git commit -m "feat(frontend): add item types, loadout i18n keys, and items API client"
```

---

### Task 6: ItemCombobox component

**Files:**
- Create: `frontend/awake-web/src/components/ItemCombobox.tsx`

**Interfaces:**
- Consumes: `itemsApi.search`, `ItemSearchResult`, `LoadoutSlot` from Task 5
- Produces: `<ItemCombobox>` component accepting `categoryPrefix`, `excludeCategory?`, `placeholder`, `value`, `onChange`, `required?`

- [ ] **Step 1: Create `ItemCombobox.tsx`**

```tsx
// frontend/awake-web/src/components/ItemCombobox.tsx
import { useState, useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { itemsApi } from '@/api/items'
import type { LoadoutSlot } from '@/types/api'
import { cn } from '@/lib/utils'
import { X, Search } from 'lucide-react'

const RANK_DOT: Record<string, string> = {
  RANK_VETERAN: 'bg-purple-500',
  RANK_MASTER: 'bg-red-500',
  RANK_LEGEND: 'bg-yellow-400',
}

interface ItemComboboxProps {
  categoryPrefix: string
  excludeCategory?: string
  placeholder: string
  value: LoadoutSlot | null
  onChange: (item: LoadoutSlot | null) => void
  required?: boolean
}

export function ItemCombobox({
  categoryPrefix,
  excludeCategory,
  placeholder,
  value,
  onChange,
  required,
}: ItemComboboxProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [] } = useQuery({
    queryKey: ['items', categoryPrefix, excludeCategory, query],
    queryFn: () => itemsApi.search(query, categoryPrefix, excludeCategory),
    enabled: query.length >= 2 || (open && query.length === 0),
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
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-secondary">
        <img src={value.itemIcon} alt="" className="w-6 h-6 object-contain" onError={(e) => (e.currentTarget.style.display = 'none')} />
        <span className="text-sm text-foreground flex-1">{value.itemName}</span>
        <button
          type="button"
          onClick={() => onChange(null)}
          className="text-muted-foreground hover:text-destructive transition-colors"
        >
          <X size={14} />
        </button>
      </div>
    )
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-background focus-within:border-accent/50 transition-colors">
        <Search size={14} className="text-muted-foreground shrink-0" />
        <input
          type="text"
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          placeholder={placeholder}
          value={query}
          required={required && !value}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
        />
      </div>

      {open && results.length > 0 && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border border-border bg-card shadow-lg overflow-hidden">
          {results.map((item) => (
            <button
              key={item.id}
              type="button"
              className="flex items-center gap-3 w-full px-3 py-2.5 text-left hover:bg-secondary transition-colors"
              onMouseDown={(e) => {
                e.preventDefault()
                onChange({ itemId: item.id, itemName: item.nameRu, itemIcon: item.icon })
                setQuery('')
                setOpen(false)
              }}
            >
              <img src={item.icon} alt="" className="w-7 h-7 object-contain shrink-0" onError={(e) => (e.currentTarget.style.display = 'none')} />
              <span className="text-sm text-foreground">{item.nameRu}</span>
              <span className={cn('ml-auto w-2 h-2 rounded-full shrink-0', RANK_DOT[item.color] ?? 'bg-muted')} />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```
git add frontend/awake-web/src/components/ItemCombobox.tsx
git commit -m "feat(frontend): ItemCombobox component with rank-color dots and debounced search"
```

---

### Task 7: Update ticket creation form

**Files:**
- Modify: `frontend/awake-web/src/routes/_auth.tickets.new.tsx`

**Interfaces:**
- Consumes: `ItemCombobox` from Task 6, `LoadoutSlot`, `Loadout`, `ticketsApi.create` from Task 5

- [ ] **Step 1: Rewrite `_auth.tickets.new.tsx`**

```tsx
// frontend/awake-web/src/routes/_auth.tickets.new.tsx
import { createFileRoute, useNavigate, Link } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ticketsApi } from '@/api/tickets'
import { TicketType } from '@/types/api'
import type { LoadoutSlot, Loadout } from '@/types/api'
import { ItemCombobox } from '@/components/ItemCombobox'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { ArrowLeft, Send } from 'lucide-react'

export const Route = createFileRoute('/_auth/tickets/new')({
  component: NewTicketPage,
})

function NewTicketPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [gameNickname, setGameNickname] = useState('')
  const [description, setDescription] = useState('')
  const [sniper, setSniper] = useState<LoadoutSlot | null>(null)
  const [weapon, setWeapon] = useState<LoadoutSlot | null>(null)
  const [armor, setArmor] = useState<LoadoutSlot | null>(null)
  const [error, setError] = useState<string | null>(null)

  const createTicket = useMutation({
    mutationFn: () => {
      const loadout: Loadout = {
        sniper,
        weapon: weapon!,
        armor: armor!,
      }
      return ticketsApi.create({
        gameNickname,
        type: TicketType.Recruitment,
        description,
        loadout,
      })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tickets'] })
      void navigate({ to: '/tickets' })
    },
    onError: () => setError(t('tickets.createError')),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!weapon || !armor) return
    setError(null)
    createTicket.mutate()
  }

  return (
    <div className="max-w-xl mx-auto">
      <Link
        to="/tickets"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground mb-6 transition-colors"
      >
        <ArrowLeft size={14} /> {t('common.cancel')}
      </Link>

      <Card>
        <CardHeader>
          <CardTitle>{t('tickets.new')}</CardTitle>
          <CardDescription>Заполни форму — офицеры рассмотрят заявку</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Nickname */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-muted-foreground">
                {t('tickets.gameNickname')}
              </label>
              <Input
                value={gameNickname}
                onChange={(e) => setGameNickname(e.target.value)}
                required
                maxLength={100}
                placeholder="Твой никнейм в игре"
              />
            </div>

            {/* Description */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium text-muted-foreground">
                  {t('tickets.description')}
                </label>
                <span className="text-xs text-muted-foreground">{description.length}/2000</span>
              </div>
              <Textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={5}
                required
                maxLength={2000}
                placeholder="Расскажи о себе — опыт, достижения, почему хочешь в клан..."
                className="resize-none"
              />
            </div>

            {/* Loadout */}
            <div className="space-y-3">
              <label className="block text-sm font-medium text-muted-foreground">
                {t('tickets.loadout.title')}
              </label>

              <div className="space-y-2">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.sniperOptional')}</p>
                <ItemCombobox
                  categoryPrefix="weapon/sniper_rifle"
                  placeholder={t('tickets.loadout.search')}
                  value={sniper}
                  onChange={setSniper}
                />
              </div>

              <div className="space-y-2">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.weapon')} *</p>
                <ItemCombobox
                  categoryPrefix="weapon"
                  excludeCategory="weapon/sniper_rifle"
                  placeholder={t('tickets.loadout.search')}
                  value={weapon}
                  onChange={setWeapon}
                  required
                />
              </div>

              <div className="space-y-2">
                <p className="text-xs text-muted-foreground">{t('tickets.loadout.armor')} *</p>
                <ItemCombobox
                  categoryPrefix="armor"
                  placeholder={t('tickets.loadout.search')}
                  value={armor}
                  onChange={setArmor}
                  required
                />
              </div>
            </div>

            {error && (
              <div className="bg-destructive/10 border border-destructive/30 text-destructive text-sm rounded-lg px-4 py-3">
                {error}
              </div>
            )}

            <Button
              type="submit"
              disabled={createTicket.isPending || !weapon || !armor}
              className="w-full"
            >
              <Send size={15} />
              {createTicket.isPending ? t('common.loading') : t('tickets.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
```

- [ ] **Step 2: Commit**

```
git add frontend/awake-web/src/routes/_auth.tickets.new.tsx
git commit -m "feat(frontend): add loadout slots to ticket creation form, remove Appeal type"
```

---

### Task 8: Show loadout on ticket detail page

**Files:**
- Modify: `frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx`

**Interfaces:**
- Consumes: `TicketDetailDto.loadout: Loadout | null` from Task 5

- [ ] **Step 1: Add loadout card to `_auth.tickets.$ticketId.tsx`**

Add the import:
```tsx
import { cn } from '@/lib/utils'
```
(already imported, no change needed)

After the existing `{/* Player data */}` card block and before `{/* Comments */}`, add:

```tsx
{/* Loadout */}
{ticket.loadout && (
  <Card>
    <CardHeader className="pb-3">
      <CardTitle className="text-sm font-medium">{t('tickets.loadout.title')}</CardTitle>
    </CardHeader>
    <CardContent className="space-y-2">
      <LoadoutRow
        label={t('tickets.loadout.sniper')}
        slot={ticket.loadout.sniper}
        emptyText={t('tickets.loadout.noSniper')}
      />
      <LoadoutRow label={t('tickets.loadout.weapon')} slot={ticket.loadout.weapon} />
      <LoadoutRow label={t('tickets.loadout.armor')} slot={ticket.loadout.armor} />
    </CardContent>
  </Card>
)}
```

Add the `LoadoutRow` helper component at the bottom of the file (after `TicketDetailPage`):

```tsx
import type { LoadoutSlot } from '@/types/api'

const RANK_DOT: Record<string, string> = {
  RANK_VETERAN: 'bg-purple-500',
  RANK_MASTER: 'bg-red-500',
  RANK_LEGEND: 'bg-yellow-400',
}

function LoadoutRow({
  label,
  slot,
  emptyText,
}: {
  label: string
  slot: LoadoutSlot | null
  emptyText?: string
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-xs text-muted-foreground w-28 shrink-0">{label}</span>
      {slot ? (
        <div className="flex items-center gap-2 flex-1">
          <img
            src={slot.itemIcon}
            alt=""
            className="w-6 h-6 object-contain"
            onError={(e) => (e.currentTarget.style.display = 'none')}
          />
          <span className="text-sm text-foreground">{slot.itemName}</span>
        </div>
      ) : (
        <span className="text-sm text-muted-foreground flex-1">{emptyText ?? '—'}</span>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Build frontend to check for TypeScript errors**

```
cd frontend/awake-web && npx tsc --noEmit
```
Expected: No errors.

- [ ] **Step 3: Commit**

```
git add frontend/awake-web/src/routes/_auth.tickets.$ticketId.tsx
git commit -m "feat(frontend): show loadout card on ticket detail page"
```

---

## Self-Review

**Spec coverage check:**
- ✅ 3 fixed loadout slots (sniper optional, weapon required, armor required)
- ✅ Items filtered by RANK_VETERAN / RANK_MASTER / RANK_LEGEND only
- ✅ Autocomplete with rank-color dots (purple/red/gold)
- ✅ snapshot stored (itemId + itemName + itemIcon) in JSONB
- ✅ Weekly Wednesday sync via IHostedService
- ✅ Backend search endpoint with category + exclude filters
- ✅ Discord embed shows loadout names as text
- ✅ Appeal type removed from UI
- ✅ Loadout shown on ticket detail page

**Type consistency:**
- `LoadoutSlotDto` (C#) ↔ `LoadoutSlot` (TS): fields match (camelCase via JsonNamingPolicy.CamelCase)
- `LoadoutDto` (C#) ↔ `Loadout` (TS): fields `sniper`, `weapon`, `armor` match
- `ItemDto` (C#) ↔ `ItemSearchResult` (TS): fields `id`, `category`, `nameRu`, `icon`, `color` match
- `IItemCacheService.Search` signature used identically in `ItemCacheService` and `ItemsController`
